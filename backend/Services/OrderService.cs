using Microsoft.EntityFrameworkCore;
using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.DTOs;
using EquipamentosMedicosApi.Models;

namespace EquipamentosMedicosApi.Services;

public class OrderService
{
    private static readonly HashSet<string> AllowedStatuses = new()
    {
        "pending",
        "processing",
        "completed",
        "cancelled"
    };

    private readonly AppDbContext _context;
    private readonly InventoryService _inventoryService;
    private readonly PromoCodeService _promoCodeService;
    private readonly EnrollmentService _enrollmentService;
    private readonly AuditService _auditService;

    public OrderService(
        AppDbContext context,
        InventoryService inventoryService,
        PromoCodeService promoCodeService,
        EnrollmentService enrollmentService,
        AuditService? auditService = null)
    {
        _context = context;
        _inventoryService = inventoryService;
        _promoCodeService = promoCodeService;
        _enrollmentService = enrollmentService;
        _auditService = auditService ?? new AuditService(context);
    }

    public async Task<ServiceResult<CreateOrderResponse>> CreateAsync(
        int userId,
        CreateOrderDTO request,
        string? idempotencyKey = null)
    {
        await using var transaction = await MutationTransaction.BeginAsync(_context);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.ID == userId);

        if (user == null)
        {
            return ServiceResult<CreateOrderResponse>.Fail("Usuario nao encontrado.");
        }

        idempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey)
            ? null
            : idempotencyKey.Trim();

        if (idempotencyKey != null)
        {
            var existingOrder = await _context.Orders
                .FirstOrDefaultAsync(order =>
                    order.UsuarioId == userId &&
                    order.IdempotencyKey == idempotencyKey);

            if (existingOrder != null)
            {
                return ServiceResult<CreateOrderResponse>.Ok(new CreateOrderResponse
                {
                    Message = "Pedido ja criado anteriormente.",
                    OrderId = existingOrder.Id,
                    Total = existingOrder.Total
                });
            }
        }

        var paymentMethod = request.PaymentMethod.Trim().ToLower();

        if (paymentMethod != "credit_card" && paymentMethod != "debit_card" && paymentMethod != "pix")
        {
            return ServiceResult<CreateOrderResponse>.Fail("Forma de pagamento invalida.");
        }


        foreach (var item in request.Itens)
        {
            if (await _context.Enrollments.AnyAsync(e => e.Student != null && e.Student.UserId == userId &&
                e.CourseId == item.CourseId && e.ClassId == item.TurmaId && e.Status != "cancelled"))
                return ServiceResult<CreateOrderResponse>.Fail("Voce ja possui matricula nesta turma/curso.");
            if (await _context.Orders.AnyAsync(o => o.UsuarioId == userId && o.PaymentStatus == "pending" &&
                o.Itens.Any(i => i.CourseId == item.CourseId && i.TurmaId == item.TurmaId)))
                return ServiceResult<CreateOrderResponse>.Fail("Voce ja possui pedido pendente para esta turma/curso. Consulte seus pedidos.");
        }
        var inventoryResult = await _inventoryService.ValidateAndReserveAsync(request.Itens);

        if (!inventoryResult.Success || inventoryResult.Data == null)
        {
            return ServiceResult<CreateOrderResponse>.Fail(inventoryResult.Error ?? "Erro ao validar estoque.");
        }

        var courses = inventoryResult.Data;

        var subtotal = request.Itens.Sum(item =>
        {
            var course = courses[item.CourseId];
            return course.Preco * item.Quantidade;
        });

        var discountResult = await _promoCodeService.ApplyDiscountAsync(request.PromoCode, subtotal);

        if (!discountResult.Success)
        {
            return ServiceResult<CreateOrderResponse>.Fail(discountResult.Error ?? "Erro ao aplicar cupom.");
        }

        var discount = discountResult.Data;

        var order = new Order
        {
            UsuarioId = userId,
            DataPedido = DateTime.UtcNow,
            Status = "pending",
            PaymentStatus = "pending",
            IdempotencyKey = idempotencyKey,
            ValorFrete = request.ValorFrete,
            PaymentMethod = paymentMethod,
            Installments = paymentMethod == "credit_card" ? request.Installments : null,
            PromoCode = (request.PromoCode ?? "").Trim().ToUpperInvariant(),
            PromoCodeId = string.IsNullOrWhiteSpace(request.PromoCode) ? null :
                await _context.PromoCodes.Where(p => p.Code == request.PromoCode.Trim().ToUpper()).Select(p => (int?)p.Id).SingleAsync(),
            Itens = request.Itens.Select(item =>
            {
                var course = courses[item.CourseId];

                return new OrderItem
                {
                    CourseId = item.CourseId,
                    NameSnapshot = course.Nome,
                    ItemType = "course",
                    TurmaId = item.TurmaId,
                    Quantidade = item.Quantidade,
                    PrecoUnitario = course.Preco
                };
            }).ToList()
        };

        order.ValorFrete = 0;
        order.Total = subtotal - discount;

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return ServiceResult<CreateOrderResponse>.Ok(new CreateOrderResponse
        {
            Message = "Pedido criado com sucesso!",
            OrderId = order.Id,
            Total = order.Total
        });
    }


    public async Task<ServiceResult<List<OrderResponse>>> GetMyOrdersAsync(int userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.ID == userId);

        if (user == null)
        {
            return ServiceResult<List<OrderResponse>>.Fail("Usuario nao encontrado.");
        }

        var orders = await _context.Orders
            .Where(o => o.UsuarioId == userId)
            .Include(o => o.Itens)
                .ThenInclude(i => i.Course)
            .OrderByDescending(o => o.DataPedido)
            .ToListAsync();

        var courseIds = orders
            .SelectMany(order => order.Itens)
            .Where(item => item.CourseId.HasValue)
            .Select(item => item.CourseId!.Value.ToString())
            .Distinct()
            .ToList();

        var studentStatuses = await _context.Students
            .Where(student => student.UserId == user.ID && courseIds.Contains(student.CourseId))
            .ToDictionaryAsync(student => student.CourseId, student => student.Status);

        var response = orders.Select(order => new OrderResponse
        {
            Id = order.Id,
            DataPedido = order.DataPedido,
            Status = order.Status,
            PaymentStatus = order.PaymentStatus,
            GatewayPaymentId = order.GatewayPaymentId,
            PaidAt = order.PaidAt,
            Total = order.Total,
            ValorFrete = order.ValorFrete,
            PaymentMethod = order.PaymentMethod,
            Installments = order.Installments,
            PromoCode = order.PromoCode,
            Itens = order.Itens.Select(item => new OrderItemResponse
            {
                CourseId = item.CourseId,
                TurmaId = item.TurmaId,
                Nome = item.NameSnapshot,
                ItemType = item.ItemType,
                Quantidade = item.Quantidade,
                PrecoUnitario = item.PrecoUnitario,
                Status = item.CourseId.HasValue
                    && studentStatuses.TryGetValue(item.CourseId!.Value.ToString(), out var status)
                        ? status
                        : null
            }).ToList()
        }).ToList();

        return ServiceResult<List<OrderResponse>>.Ok(response);
    }

    public async Task<ServiceResult<OrderResponse>> GetByIdAsync(int userId, int orderId, bool isAdmin = false)
    {
        var query = _context.Orders
            .Include(order => order.Usuario)
            .Include(order => order.Itens)
                .ThenInclude(item => item.Course)
            .Where(order => order.Id == orderId);

        if (!isAdmin)
        {
            query = query.Where(order => order.UsuarioId == userId);
        }

        var order = await query.FirstOrDefaultAsync();
        return order == null
            ? ServiceResult<OrderResponse>.Fail("Pedido nao encontrado.")
            : ServiceResult<OrderResponse>.Ok(ToResponse(order));
    }

    public async Task<ServiceResult<OrderResponse>> CancelAsync(int userId, int orderId)
    {
        await using var transaction = await MutationTransaction.BeginAsync(_context);

        var order = await _context.Orders
            .Include(item => item.Itens)
            .FirstOrDefaultAsync(item => item.Id == orderId && item.UsuarioId == userId);

        if (order == null)
            return ServiceResult<OrderResponse>.Fail("Pedido nao encontrado.");

        if (order.PaymentStatus != "pending")
            return ServiceResult<OrderResponse>.Fail("Pedido nao pode ser cancelado.");

        await ReleaseStockAsync(order);
        order.Status = "cancelled";
        order.PaymentStatus = "cancelled";

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return ServiceResult<OrderResponse>.Ok(ToResponse(order));
    }

    public async Task<int> ExpirePendingOrdersAsync(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow.Subtract(maxAge);

        await using var transaction = await MutationTransaction.BeginAsync(_context);

        var orders = await _context.Orders
            .Include(order => order.Itens)
            .Where(order =>
                order.PaymentStatus == "pending" &&
                order.Status == "pending" &&
                order.DataPedido < cutoff)
            .ToListAsync();

        foreach (var order in orders)
        {
            await ReleaseStockAsync(order);
            order.Status = "cancelled";
            order.PaymentStatus = "cancelled";
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return orders.Count;
    }

    public async Task<ServiceResult<OrderResponse>> RefundAsync(int orderId, int? userId = null)
    {
        await using var transaction = await MutationTransaction.BeginAsync(_context);

        var order = await _context.Orders
            .Include(item => item.Itens)
                .ThenInclude(item => item.Course)
            .Include(item => item.Itens)
                .ThenInclude(item => item.Turma)
            .Include(item => item.Usuario)
            .FirstOrDefaultAsync(item => item.Id == orderId);

        if (order == null)
            return ServiceResult<OrderResponse>.Fail("Pedido nao encontrado.");

        if (order.PaymentStatus == "refunded")
            return ServiceResult<OrderResponse>.Ok(ToResponse(order));

        if (order.PaymentStatus != "paid")
            return ServiceResult<OrderResponse>.Fail(
                "Somente pedidos pagos podem ser reembolsados.");

        await ReleaseStockAsync(order);

        var enrollments = await _context.Enrollments
            .Where(enrollment => enrollment.OrderId == order.Id)
            .ToListAsync();

        foreach (var enrollment in enrollments)
        {
            enrollment.Status = "cancelled";
            enrollment.ParticipationConfirmedAt = null;
            enrollment.ParticipationStartDate = null;
        }

        order.PaymentStatus = "refunded";
        order.Status = "cancelled";
        _auditService.Add(userId, "refunded", "Order", order.Id,
            new { PaymentStatus = "paid", Status = "completed" },
            new { order.PaymentStatus, order.Status });

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return ServiceResult<OrderResponse>.Ok(ToResponse(order));
    }

    public async Task<ServiceResult<OrderResponse>> ProcessPaymentWebhookAsync(int orderId, string paymentId, string paymentStatus)
    {
        return await ProcessPaymentWebhookAsync(
            $"legacy-{orderId}-{paymentId}-{paymentStatus}",
            orderId,
            paymentId,
            paymentStatus);
    }

    public async Task<ServiceResult<OrderResponse>> ProcessPaymentWebhookAsync(
        string eventId,
        int orderId,
        string paymentId,
        string paymentStatus)
    {
        eventId = eventId.Trim();
        if (string.IsNullOrWhiteSpace(eventId))
            return ServiceResult<OrderResponse>.Fail("EventId e obrigatorio.");

        await using var transaction = await MutationTransaction.BeginAsync(_context);

        var existingEvent = await _context.PaymentWebhookEvents
            .Include(webhookEvent => webhookEvent.Order)
                .ThenInclude(order => order!.Usuario)
            .Include(webhookEvent => webhookEvent.Order)
                .ThenInclude(order => order!.Itens)
                    .ThenInclude(item => item.Course)
            .FirstOrDefaultAsync(webhookEvent => webhookEvent.EventId == eventId);

        if (existingEvent?.Order != null)
        {
            if (existingEvent.OrderId != orderId)
                return ServiceResult<OrderResponse>.Fail("Evento pertence a outro pedido.");
            return ServiceResult<OrderResponse>.Ok(ToResponse(existingEvent.Order));
        }

        var order = await _context.Orders
            .Include(item => item.Itens)
                .ThenInclude(item => item.Course)
            .Include(item => item.Itens)
                .ThenInclude(item => item.Turma)
            .Include(item => item.Usuario)
            .FirstOrDefaultAsync(item => item.Id == orderId);

        if (order == null)
            return ServiceResult<OrderResponse>.Fail("Pedido nao encontrado.");

        var normalizedStatus = paymentStatus.Trim().ToLowerInvariant();
        if (normalizedStatus is not ("paid" or "refused" or "cancelled" or "refunded"))
            return ServiceResult<OrderResponse>.Fail("Status de pagamento invalido.");

        // Aprovacao tardia exige conciliacao; a reserva ja foi liberada.
        if (normalizedStatus == "paid" && order.PaymentStatus is "cancelled" or "refused" or "refunded")
            return ServiceResult<OrderResponse>.Fail(
                "Pagamento recebido apos encerramento do pedido. Necessaria conciliacao com o provedor.");
        if (normalizedStatus == "refunded" && order.PaymentStatus is not ("paid" or "refunded"))
            return ServiceResult<OrderResponse>.Fail("Somente pedidos pagos podem ser reembolsados.");

        var webhookEvent = new PaymentWebhookEvent
        {
            EventId = eventId,
            OrderId = order.Id,
            PaymentId = paymentId?.Trim() ?? string.Empty,
            Status = normalizedStatus,
            ReceivedAt = DateTime.UtcNow
        };
        _context.PaymentWebhookEvents.Add(webhookEvent);

        if (order.PaymentStatus == "pending")
        {
            if (!string.IsNullOrWhiteSpace(paymentId))
                order.GatewayPaymentId = paymentId;
            order.PaymentStatus = normalizedStatus;
            if (normalizedStatus == "paid")
            {
                order.Status = "processing";
                order.PaidAt = DateTime.UtcNow;
                foreach (var item in order.Itens)
                {
                    if (item.Course != null)
                        await _enrollmentService.CreateEnrollmentsForCourseAsync(
                            order.Usuario!, item.Course, order, item.Quantidade, item.TurmaId);
                }
            }
            else
            {
                await ReleaseStockAsync(order);
                order.Status = "cancelled";
            }
        }
        else if (order.PaymentStatus == "paid" && normalizedStatus == "refunded")
        {
            await ReleaseStockAsync(order);
            var enrollments = await _context.Enrollments
                .Where(enrollment => enrollment.OrderId == order.Id).ToListAsync();
            foreach (var enrollment in enrollments)
            {
                enrollment.Status = "cancelled";
                enrollment.ParticipationConfirmedAt = null;
                enrollment.ParticipationStartDate = null;
            }
            order.PaymentStatus = "refunded";
            order.Status = "cancelled";
        }
        // Eventos repetidos ou atrasados sao registrados sem regredir o
        // pagamento nem liberar a mesma reserva novamente.

        webhookEvent.ProcessedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return ServiceResult<OrderResponse>.Ok(ToResponse(order));
    }

    private async Task ReleaseStockAsync(Order order)
    {
        if (order.PaymentStatus == "paid")
        {
            var enrollments = await _context.Enrollments.Include(e => e.Class)
                .Where(e => e.OrderId == order.Id).ToListAsync();
            var states = new EnrollmentStateService(_context);
            foreach (var enrollment in enrollments)
                await states.ChangeAsync(enrollment, "cancelled");
        }
        else
        {
            foreach (var item in order.Itens.Where(i => i.TurmaId.HasValue))
            {
                await _context.CourseClasses.Where(c => c.Id == item.TurmaId!.Value)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.AvailableSeats,
                        c => c.AvailableSeats + item.Quantidade > c.Capacity ? c.Capacity : c.AvailableSeats + item.Quantidade));
                var tracked = _context.ChangeTracker.Entries<CourseClass>().FirstOrDefault(e => e.Entity.Id == item.TurmaId!.Value);
                if (tracked != null) await tracked.ReloadAsync();
            }
        }
        if (order.PromoCodeId.HasValue)
        {
            var promo = await _context.PromoCodes.FindAsync(order.PromoCodeId.Value);
            if (promo != null) promo.UsageCount = Math.Max(0, promo.UsageCount - 1);
        }
    }
    public async Task<List<OrderResponse>> GetAllAsync()
    {
        var orders = await _context.Orders
            .Include(o => o.Usuario)
            .Include(o => o.Itens)
                .ThenInclude(i => i.Course)
            .OrderByDescending(o => o.DataPedido)
            .ToListAsync();

        return orders.Select(order => new OrderResponse
        {
            Id = order.Id,
            DataPedido = order.DataPedido,
            Status = order.Status,
            PaymentStatus = order.PaymentStatus,
            GatewayPaymentId = order.GatewayPaymentId,
            PaidAt = order.PaidAt,
            Total = order.Total,
            ValorFrete = order.ValorFrete,
            PaymentMethod = order.PaymentMethod,
            Installments = order.Installments,
            PromoCode = order.PromoCode,
            Usuario = order.Usuario == null ? null : new OrderUserResponse
            {
                Id = order.Usuario.ID,
                Nome = order.Usuario.Nome,
                Email = order.Usuario.Email
            },
            Itens = order.Itens.Select(item => new OrderItemResponse
            {
                CourseId = item.CourseId,
                TurmaId = item.TurmaId,
                Nome = item.NameSnapshot,
                ItemType = item.ItemType,
                Quantidade = item.Quantidade,
                PrecoUnitario = item.PrecoUnitario
            }).ToList()
        }).ToList();
    }

    public async Task<ServiceResult<object>> UpdateStatusAsync(int id, UpdateOrderStatusDTO request, int? userId = null)
    {
        var status = request.Status.Trim();

        if (!AllowedStatuses.Contains(status))
        {
            return ServiceResult<object>.Fail("Status invalido.");
        }

        await using var transaction = await MutationTransaction.BeginAsync(_context);
        var order = await _context.Orders.Include(o => o.Itens).FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return ServiceResult<object>.Fail("Pedido nao encontrado.");
        }

        if (status == "cancelled" && order.PaymentStatus == "paid")
            return ServiceResult<object>.Fail("Use o reembolso para encerrar um pedido pago.");
        if (status is "processing" or "completed" && order.PaymentStatus != "paid")
            return ServiceResult<object>.Fail("Somente pedidos pagos podem avancar de situacao.");
        if (order.PaymentStatus is "cancelled" or "refunded" or "refused" && status != "cancelled")
            return ServiceResult<object>.Fail("Pedido financeiro encerrado nao pode ser reaberto.");
        var previousStatus = order.Status;
        if (status == "cancelled" && order.PaymentStatus == "pending")
        {
            await ReleaseStockAsync(order);
            order.PaymentStatus = "cancelled";
        }
        order.Status = status;
        _auditService.Add(userId, "status_changed", "Order", order.Id, previousStatus, status);

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();
        return ServiceResult<object>.Ok(new
        {
            order.Id,
            order.Status
        });
    }

    private static OrderResponse ToResponse(Order order)
    {
        return new OrderResponse
        {
            Id = order.Id,
            DataPedido = order.DataPedido,
            Status = order.Status,
            PaymentStatus = order.PaymentStatus,
            GatewayPaymentId = order.GatewayPaymentId,
            PaidAt = order.PaidAt,
            Total = order.Total,
            ValorFrete = order.ValorFrete,
            PaymentMethod = order.PaymentMethod,
            Installments = order.Installments,
            PromoCode = order.PromoCode,
            Usuario = order.Usuario == null ? null : new OrderUserResponse
            {
                Id = order.Usuario.ID,
                Nome = order.Usuario.Nome,
                Email = order.Usuario.Email
            },
            Itens = order.Itens.Select(item => new OrderItemResponse
            {
                CourseId = item.CourseId,
                TurmaId = item.TurmaId,
                Nome = item.NameSnapshot,
                ItemType = item.ItemType,
                Quantidade = item.Quantidade,
                PrecoUnitario = item.PrecoUnitario
            }).ToList()
        };
    }
}
