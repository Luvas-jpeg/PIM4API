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

    public OrderService(
        AppDbContext context,
        InventoryService inventoryService,
        PromoCodeService promoCodeService,
        EnrollmentService enrollmentService)
    {
        _context = context;
        _inventoryService = inventoryService;
        _promoCodeService = promoCodeService;
        _enrollmentService = enrollmentService;
    }

    public async Task<ServiceResult<CreateOrderResponse>> CreateAsync(int userId, CreateOrderDTO request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.ID == userId);

        if (user == null)
        {
            return ServiceResult<CreateOrderResponse>.Fail("Usuario nao encontrado.");
        }

        var paymentMethod = request.PaymentMethod.Trim().ToLower();

        if (paymentMethod != "credit_card" && paymentMethod != "debit_card" && paymentMethod != "pix")
        {
            return ServiceResult<CreateOrderResponse>.Fail("Forma de pagamento invalida.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var inventoryResult = await _inventoryService.ValidateAndReserveAsync(request.Itens);

        if (!inventoryResult.Success || inventoryResult.Data == null)
        {
            return ServiceResult<CreateOrderResponse>.Fail(inventoryResult.Error ?? "Erro ao validar estoque.");
        }

        var products = inventoryResult.Data;

        var subtotal = request.Itens.Sum(item =>
        {
            var product = products[item.ProdutoId];
            return product.Preco * item.Quantidade;
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
            ValorFrete = request.ValorFrete,
            PaymentMethod = paymentMethod,
            Installments = paymentMethod == "credit_card" ? request.Installments : null,
            PromoCode = request.PromoCode.Trim().ToUpper(),
            Itens = request.Itens.Select(item =>
            {
                var product = products[item.ProdutoId];

                return new OrderItem
                {
                    ProdutoId = item.ProdutoId,
                    Quantidade = item.Quantidade,
                    PrecoUnitario = product.Preco
                };
            }).ToList()
        };

        order.Total = subtotal + request.ValorFrete - discount;

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        foreach (var item in request.Itens)
        {
            var product = products[item.ProdutoId];

            await _enrollmentService.CreateEnrollmentsForCourseAsync(
                user,
                product,
                order,
                item.Quantidade);
        }

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
                .ThenInclude(i => i.Produto)
            .OrderByDescending(o => o.DataPedido)
            .ToListAsync();

        var courseIds = orders
            .SelectMany(order => order.Itens)
            .Where(item => item.Produto?.TipoProduto == "course")
            .Select(item => item.ProdutoId.ToString())
            .Distinct()
            .ToList();

        var studentStatuses = await _context.Students
            .Where(student => student.Email == user.Email && courseIds.Contains(student.CourseId))
            .ToDictionaryAsync(student => student.CourseId, student => student.Status);

        var response = orders.Select(order => new OrderResponse
        {
            Id = order.Id,
            DataPedido = order.DataPedido,
            Status = order.Status,
            Total = order.Total,
            ValorFrete = order.ValorFrete,
            PaymentMethod = order.PaymentMethod,
            Installments = order.Installments,
            PromoCode = order.PromoCode,
            Itens = order.Itens.Select(item => new OrderItemResponse
            {
                ProdutoId = item.ProdutoId,
                Nome = item.Produto?.Nome ?? string.Empty,
                TipoProduto = item.Produto?.TipoProduto ?? "equipment",
                Quantidade = item.Quantidade,
                PrecoUnitario = item.PrecoUnitario,
                Status = item.Produto?.TipoProduto == "course"
                    && studentStatuses.TryGetValue(item.ProdutoId.ToString(), out var status)
                        ? status
                        : null
            }).ToList()
        }).ToList();

        return ServiceResult<List<OrderResponse>>.Ok(response);
    }

    public async Task<List<OrderResponse>> GetAllAsync()
    {
        var orders = await _context.Orders
            .Include(o => o.Usuario)
            .Include(o => o.Itens)
                .ThenInclude(i => i.Produto)
            .OrderByDescending(o => o.DataPedido)
            .ToListAsync();

        return orders.Select(order => new OrderResponse
        {
            Id = order.Id,
            DataPedido = order.DataPedido,
            Status = order.Status,
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
                ProdutoId = item.ProdutoId,
                Nome = item.Produto?.Nome ?? string.Empty,
                TipoProduto = item.Produto?.TipoProduto ?? "equipment",
                Quantidade = item.Quantidade,
                PrecoUnitario = item.PrecoUnitario
            }).ToList()
        }).ToList();
    }

    public async Task<ServiceResult<object>> UpdateStatusAsync(int id, UpdateOrderStatusDTO request)
    {
        var status = request.Status.Trim();

        if (!AllowedStatuses.Contains(status))
        {
            return ServiceResult<object>.Fail("Status invalido.");
        }

        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return ServiceResult<object>.Fail("Pedido nao encontrado.");
        }

        order.Status = status;

        await _context.SaveChangesAsync();

        return ServiceResult<object>.Ok(new
        {
            order.Id,
            order.Status
        });
    }
}
