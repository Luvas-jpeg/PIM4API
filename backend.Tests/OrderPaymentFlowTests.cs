using System.Data.Common;
using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.DTOs;
using EquipamentosMedicosApi.Models;
using EquipamentosMedicosApi.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests;

public sealed class OrderPaymentFlowTests
{
    [Fact]
    public async Task CreatingOrderDoesNotCreateEnrollmentBeforePayment()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var service = fixture.CreateOrderService();

        var result = await service.CreateAsync(fixture.UserId, CreateRequest());

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(0, await fixture.Context.Enrollments.CountAsync());
        Assert.Equal("pending", (await fixture.Context.Orders.SingleAsync()).PaymentStatus);
        Assert.Equal(1, (await fixture.Context.CourseClasses.AsNoTracking().SingleAsync(courseClass => courseClass.Id == fixture.CourseId)).AvailableSeats);
    }

    [Fact]
    public async Task ApprovedPaymentCreatesEnrollmentOnlyOnce()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var service = fixture.CreateOrderService();
        var orderResult = await service.CreateAsync(fixture.UserId, CreateRequest());

        var firstWebhook = await service.ProcessPaymentWebhookAsync(
            orderResult.Data!.OrderId, "gateway-payment-1", "paid");
        var secondWebhook = await service.ProcessPaymentWebhookAsync(
            orderResult.Data.OrderId, "gateway-payment-1", "paid");

        Assert.True(firstWebhook.Success);
        Assert.True(secondWebhook.Success);
        Assert.Equal(1, await fixture.Context.Enrollments.CountAsync());
        Assert.Equal(1, await fixture.Context.PaymentWebhookEvents.CountAsync());
        Assert.Equal("paid", (await fixture.Context.Orders.SingleAsync()).PaymentStatus);
    }

    [Fact]
    public async Task ApprovedEadPaymentCreatesEnrollmentWithoutClass()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var course = await fixture.Context.Courses.SingleAsync(item => item.Id == fixture.CourseId);
        course.DeliveryMode = "ead";
        await fixture.Context.SaveChangesAsync();
        var service = fixture.CreateOrderService();
        var request = CreateRequest();
        request.Itens[0].TurmaId = null;
        var orderResult = await service.CreateAsync(fixture.UserId, request);
        var webhook = await service.ProcessPaymentWebhookAsync(
            orderResult.Data!.OrderId, "gateway-payment-ead", "paid");

        Assert.True(webhook.Success);
        var enrollment = await fixture.Context.Enrollments.SingleAsync();
        Assert.Equal(course.Id, enrollment.CourseId);
        Assert.Null(enrollment.ClassId);
        Assert.Equal(2, await fixture.Context.CourseClasses
            .Where(item => item.Id == fixture.CourseId)
            .Select(item => item.AvailableSeats)
            .SingleAsync());
    }

    [Fact]
    public async Task CancellingPendingOrderReleasesReservedStock()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var service = fixture.CreateOrderService();
        var orderResult = await service.CreateAsync(fixture.UserId, CreateRequest());

        var result = await service.CancelAsync(fixture.UserId, orderResult.Data!.OrderId);

        Assert.True(result.Success);
        Assert.Equal(0, await fixture.Context.Enrollments.CountAsync());
        Assert.Equal("cancelled", (await fixture.Context.Orders.SingleAsync()).Status);
        Assert.Equal(2, (await fixture.Context.CourseClasses.AsNoTracking().SingleAsync(courseClass => courseClass.Id == fixture.CourseId)).AvailableSeats);
    }

    [Fact]
    public async Task RefundingPaidOrderCancelsEnrollmentsAndReleasesStockOnlyOnce()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var service = fixture.CreateOrderService();
        var orderResult = await service.CreateAsync(fixture.UserId, CreateRequest());

        await service.ProcessPaymentWebhookAsync(orderResult.Data!.OrderId, "gateway-payment-2", "paid");
        var refund = await service.ProcessPaymentWebhookAsync(orderResult.Data.OrderId, "gateway-payment-2", "refunded");
        var repeatedRefund = await service.ProcessPaymentWebhookAsync(orderResult.Data.OrderId, "gateway-payment-2", "refunded");

        Assert.True(refund.Success);
        Assert.True(repeatedRefund.Success);
        Assert.Equal(1, await fixture.Context.Enrollments.CountAsync());
        Assert.All(await fixture.Context.Enrollments.ToListAsync(), enrollment =>
            Assert.Equal("cancelled", enrollment.Status));
        Assert.Equal(2, (await fixture.Context.CourseClasses.AsNoTracking().SingleAsync(courseClass => courseClass.Id == fixture.CourseId)).AvailableSeats);
    }

    [Fact]
    public async Task AdministrativeRefundIsRestrictedToPaidOrdersAndIsIdempotent()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var service = fixture.CreateOrderService();
        var orderResult = await service.CreateAsync(fixture.UserId, CreateRequest());

        var pendingRefund = await service.RefundAsync(orderResult.Data!.OrderId);
        Assert.False(pendingRefund.Success);

        await service.ProcessPaymentWebhookAsync(
            "payment-paid-event",
            orderResult.Data.OrderId,
            "gateway-payment-admin",
            "paid");

        var refund = await service.RefundAsync(orderResult.Data.OrderId);
        var repeatedRefund = await service.RefundAsync(orderResult.Data.OrderId);

        Assert.True(refund.Success);
        Assert.True(repeatedRefund.Success);
        Assert.Equal("refunded", (await fixture.Context.Orders.SingleAsync()).PaymentStatus);
        Assert.Equal(2, (await fixture.Context.CourseClasses
            .SingleAsync(courseClass => courseClass.Id == fixture.CourseId)).AvailableSeats);
        Assert.All(await fixture.Context.Enrollments.ToListAsync(), enrollment =>
            Assert.Equal("cancelled", enrollment.Status));
    }

    [Fact]
    public async Task RefusedPaymentReleasesReservedStock()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var service = fixture.CreateOrderService();
        var orderResult = await service.CreateAsync(fixture.UserId, CreateRequest());

        var result = await service.ProcessPaymentWebhookAsync(
            orderResult.Data!.OrderId, "gateway-payment-3", "refused");

        Assert.True(result.Success);
        Assert.Equal(0, await fixture.Context.Enrollments.CountAsync());
        Assert.Equal(2, (await fixture.Context.CourseClasses.AsNoTracking().SingleAsync(courseClass => courseClass.Id == fixture.CourseId)).AvailableSeats);
    }

    [Fact]
    public async Task CreatingCourseOrderIgnoresClientProvidedShipping()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var service = fixture.CreateOrderService();
        var request = CreateRequest();
        request.ValorFrete = 999m;

        var result = await service.CreateAsync(fixture.UserId, request);

        Assert.True(result.Success);
        Assert.Equal(0m, (await fixture.Context.Orders.SingleAsync()).ValorFrete);
        Assert.Equal(100m, result.Data!.Total);
    }

    [Fact]
    public async Task ExpiringPendingOrderReleasesReservationOnlyOnce()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var service = fixture.CreateOrderService();
        var orderResult = await service.CreateAsync(fixture.UserId, CreateRequest());
        var order = await fixture.Context.Orders.SingleAsync();
        order.DataPedido = DateTime.UtcNow.AddMinutes(-31);
        await fixture.Context.SaveChangesAsync();

        var expired = await service.ExpirePendingOrdersAsync(TimeSpan.FromMinutes(30));
        var repeated = await service.ExpirePendingOrdersAsync(TimeSpan.FromMinutes(30));

        Assert.Equal(1, expired);
        Assert.Equal(0, repeated);
        Assert.Equal("cancelled", order.Status);
        Assert.Equal("cancelled", order.PaymentStatus);
        Assert.Equal(2, (await fixture.Context.CourseClasses
            .SingleAsync(courseClass => courseClass.Id == fixture.CourseId)).AvailableSeats);
    }

    [Fact]
    public async Task UserCannotReadAnotherUsersOrder()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var service = fixture.CreateOrderService();
        var orderResult = await service.CreateAsync(fixture.UserId, CreateRequest());

        var result = await service.GetByIdAsync(
            fixture.AnotherUserId,
            orderResult.Data!.OrderId);

        Assert.False(result.Success);
        Assert.Equal("Pedido nao encontrado.", result.Error);
    }

    [Fact]
    public async Task UserCannotCancelAnotherUsersOrder()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var service = fixture.CreateOrderService();
        var orderResult = await service.CreateAsync(fixture.UserId, CreateRequest());

        var result = await service.CancelAsync(
            fixture.AnotherUserId,
            orderResult.Data!.OrderId);

        Assert.False(result.Success);
        Assert.Equal("Pedido nao encontrado.", result.Error);
        var order = await fixture.Context.Orders.SingleAsync();
        Assert.Equal("pending", order.Status);
        Assert.Equal("pending", order.PaymentStatus);
    }

    [Fact]
    public async Task OrderPreservesPurchasedCourseNameAndPriceAfterCatalogChanges()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var service = fixture.CreateOrderService();
        var created = await service.CreateAsync(fixture.UserId, CreateRequest());
        Assert.True(created.Success);

        var course = await fixture.Context.Courses.FindAsync(fixture.CourseId);
        course!.Nome = "Nome alterado";
        course.Preco = 999m;
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        var result = await service.GetByIdAsync(fixture.UserId, created.Data!.OrderId);
        var item = Assert.Single(result.Data!.Itens);
        Assert.Equal(fixture.CourseId, item.CourseId);
        Assert.Equal("Curso de Teste", item.Nome);
        Assert.Equal(100m, item.PrecoUnitario);
        Assert.Equal(100m, result.Data.Total);
    }

    [Fact]
    public async Task SplitItemsCannotBypassCourseQuantityLimit()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var request = CreateRequest();
        request.Itens = new List<CreateOrderItemDTO>
        {
            new() { CourseId = fixture.CourseId, TurmaId = 99, Quantidade = 3 },
            new() { CourseId = fixture.CourseId, TurmaId = 99, Quantidade = 3 }
        };
        var result = await fixture.CreateOrderService().CreateAsync(fixture.UserId, request);
        Assert.False(result.Success);
        Assert.Equal(0, await fixture.Context.Orders.CountAsync());
        Assert.Equal(2, await fixture.Context.CourseClasses.Select(item => item.AvailableSeats).SingleAsync());
    }

    [Theory]
    [InlineData("paid")]
    [InlineData("refused")]
    [InlineData("cancelled")]
    public async Task LaterEventDoesNotRegressApprovedPayment(string status)
    {
        await using var fixture = await TestFixture.CreateAsync();
        var service = fixture.CreateOrderService();
        var created = await service.CreateAsync(fixture.UserId, CreateRequest());
        await service.ProcessPaymentWebhookAsync("approved", created.Data!.OrderId, "payment", "paid");
        var result = await service.ProcessPaymentWebhookAsync("later", created.Data.OrderId, "payment", status);
        Assert.True(result.Success);
        var order = await fixture.Context.Orders.SingleAsync();
        Assert.Equal("paid", order.PaymentStatus);
        Assert.Equal("processing", order.Status);
        Assert.Single(await fixture.Context.Enrollments.ToListAsync());
        Assert.Equal(1, await fixture.Context.CourseClasses.AsNoTracking().Select(c => c.AvailableSeats).SingleAsync());
        Assert.Equal(2, await fixture.Context.PaymentWebhookEvents.CountAsync());
    }

    [Fact]
    public async Task DifferentFailureEventsReleaseSeatsOnlyOnce()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var service = fixture.CreateOrderService();
        var created = await service.CreateAsync(fixture.UserId, CreateRequest());
        await service.ProcessPaymentWebhookAsync("refusal", created.Data!.OrderId, "payment", "refused");
        var result = await service.ProcessPaymentWebhookAsync("cancellation", created.Data.OrderId, "payment", "cancelled");
        Assert.True(result.Success);
        Assert.Equal("refused", (await fixture.Context.Orders.SingleAsync()).PaymentStatus);
        Assert.Equal(2, await fixture.Context.CourseClasses.AsNoTracking().Select(c => c.AvailableSeats).SingleAsync());
    }

    [Fact]
    public async Task ApprovalAfterExpirationDoesNotEnrollWithoutReservedSeats()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var service = fixture.CreateOrderService();
        var created = await service.CreateAsync(fixture.UserId, CreateRequest());
        var order = await fixture.Context.Orders.SingleAsync();
        order.DataPedido = DateTime.UtcNow.AddMinutes(-31);
        await fixture.Context.SaveChangesAsync();
        await service.ExpirePendingOrdersAsync(TimeSpan.FromMinutes(30));
        var result = await service.ProcessPaymentWebhookAsync("late", created.Data!.OrderId, "payment", "paid");
        Assert.False(result.Success);
        Assert.Equal("cancelled", order.PaymentStatus);
        Assert.Empty(await fixture.Context.Enrollments.ToListAsync());
        Assert.Equal(2, await fixture.Context.CourseClasses.AsNoTracking().Select(c => c.AvailableSeats).SingleAsync());
    }

    [Fact]
    public async Task RefundBeforeApprovalIsRejected()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var service = fixture.CreateOrderService();
        var created = await service.CreateAsync(fixture.UserId, CreateRequest());
        var result = await service.ProcessPaymentWebhookAsync("refund", created.Data!.OrderId, "payment", "refunded");
        Assert.False(result.Success);
        Assert.Equal("pending", (await fixture.Context.Orders.SingleAsync()).PaymentStatus);
        Assert.Equal(1, await fixture.Context.CourseClasses.AsNoTracking().Select(c => c.AvailableSeats).SingleAsync());
    }

    [Fact]
    public async Task EventCannotBeReusedForAnotherOrder()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var service = fixture.CreateOrderService();
        var first = await service.CreateAsync(fixture.UserId, CreateRequest());
        var second = await service.CreateAsync(fixture.AnotherUserId, CreateRequest());
        await service.ProcessPaymentWebhookAsync("shared", first.Data!.OrderId, "payment", "paid");
        var result = await service.ProcessPaymentWebhookAsync("shared", second.Data!.OrderId, "payment", "paid");
        Assert.False(result.Success);
        Assert.Equal("pending", (await fixture.Context.Orders.SingleAsync(o => o.Id == second.Data.OrderId)).PaymentStatus);
    }

    private static CreateOrderDTO CreateRequest()
    {
        return new CreateOrderDTO
        {
            PaymentMethod = "pix",
            Itens = new List<CreateOrderItemDTO>
            {
                new() { CourseId = 99, TurmaId = 99, Quantidade = 1 }
            }
        };
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly DbConnection _connection;
        public AppDbContext Context { get; }
        public int UserId { get; } = 99;
        public int AnotherUserId { get; } = 100;
        public int CourseId { get; } = 99;

        private TestFixture(DbConnection connection, AppDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public static async Task<TestFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();

            context.Users.Add(new User
            {
                ID = 99,
                Nome = "Aluno Teste",
                Email = "aluno.teste@example.com",
                Cpf = "52998224725",
                SenhaHash = "hash",
                Role = "Cliente"
            });
            context.Users.Add(new User
            {
                ID = 100,
                Nome = "Outro Aluno",
                Email = "outro.aluno@example.com",
                Cpf = "16899535009",
                SenhaHash = "hash",
                Role = "Cliente"
            });
            context.Courses.Add(new Course
            {
                Id = 99, Nome = "Curso de Teste", Preco = 100m, DeliveryMode = "presencial"
            });
            context.CourseClasses.Add(new CourseClass
            {
                Id = 99, CourseId = 99, Capacity = 2, AvailableSeats = 2,
                VafasDisponiveis = 2, DataRealizacao = DateTime.UtcNow.AddDays(10),
                Local = "Sao Paulo", Instructor = "Instrutor Teste", Status = "scheduled"
            });
            await context.SaveChangesAsync();

            return new TestFixture(connection, context);
        }

        public OrderService CreateOrderService()
        {
            return new OrderService(
                Context,
                new InventoryService(Context),
                new PromoCodeService(Context),
                new EnrollmentService(Context));
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
