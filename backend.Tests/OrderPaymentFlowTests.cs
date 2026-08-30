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
        Assert.Equal(1, (await fixture.Context.Products.SingleAsync(product => product.Id == fixture.CourseId)).Estoque);
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
        Assert.Equal("paid", (await fixture.Context.Orders.SingleAsync()).PaymentStatus);
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
        Assert.Equal(2, (await fixture.Context.Products.SingleAsync(product => product.Id == fixture.CourseId)).Estoque);
    }

    private static CreateOrderDTO CreateRequest()
    {
        return new CreateOrderDTO
        {
            PaymentMethod = "pix",
            Itens = new List<CreateOrderItemDTO>
            {
                new() { ProdutoId = 99, Quantidade = 1 }
            }
        };
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly DbConnection _connection;
        public AppDbContext Context { get; }
        public int UserId { get; } = 99;
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
            context.Products.Add(new Product
            {
                Id = 99,
                Nome = "Curso de Teste",
                Preco = 100m,
                TipoProduto = "course",
                Estoque = 2,
                Date = "15/04/2026",
                Location = "Online",
                Instructor = "Instrutor Teste"
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
