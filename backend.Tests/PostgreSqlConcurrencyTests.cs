using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.DTOs;
using EquipamentosMedicosApi.Models;
using EquipamentosMedicosApi.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace backend.Tests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("PIM4_POSTGRES_TESTS") != "1")
            Skip = "Ative PIM4_POSTGRES_TESTS=1 para criar bancos isolados de concorrencia no PostgreSQL local.";
    }
}

public sealed class PostgreSqlConcurrencyTests
{
    [PostgreSqlFact]
    public async Task SameIdempotencyKeyCreatesOneOrderAndReservesOneSeat()
    {
        await using var f=await Fixture.CreateAsync();
        var results=await Task.WhenAll(f.Run(s=>s.CreateAsync(99,Request(),"same")),f.Run(s=>s.CreateAsync(99,Request(),"same")));
        Assert.All(results,r=>Assert.True(r.Success,r.Error));
        Assert.Equal(results[0].Data!.OrderId,results[1].Data!.OrderId);
        await using var db=f.Open();Assert.Equal(1,await db.Orders.CountAsync());Assert.Equal(0,(await db.CourseClasses.SingleAsync()).AvailableSeats);
    }

    [PostgreSqlFact]
    public async Task DifferentStudentsCannotBuyTheSameLastSeat()
    {
        await using var f=await Fixture.CreateAsync();
        var results=await Task.WhenAll(f.Run(s=>s.CreateAsync(99,Request(),"a")),f.Run(s=>s.CreateAsync(100,Request(),"b")));
        Assert.Single(results,r=>r.Success);
        await using var db=f.Open();Assert.Equal(1,await db.Orders.CountAsync());Assert.Equal(0,(await db.CourseClasses.SingleAsync()).AvailableSeats);
    }

    [PostgreSqlFact]
    public async Task DifferentPaidEventsDoNotDuplicateEnrollment()
    {
        await using var f=await Fixture.CreateAsync();
        var created=await f.Run(s=>s.CreateAsync(99,Request()));var id=created.Data!.OrderId;
        var results=await Task.WhenAll(f.Run(s=>s.ProcessPaymentWebhookAsync("event-a",id,"p","paid")),f.Run(s=>s.ProcessPaymentWebhookAsync("event-b",id,"p","paid")));
        Assert.All(results,r=>Assert.True(r.Success,r.Error));
        await using var db=f.Open();Assert.Equal(1,await db.Enrollments.CountAsync());Assert.Equal(2,await db.PaymentWebhookEvents.CountAsync());
    }

    [PostgreSqlFact]
    public async Task CancellationRacingApprovalKeepsOneConsistentOutcome()
    {
        await using var f=await Fixture.CreateAsync();
        var created=await f.Run(s=>s.CreateAsync(99,Request()));var id=created.Data!.OrderId;
        await Task.WhenAll(f.Run(s=>s.CancelAsync(99,id)),f.Run(s=>s.ProcessPaymentWebhookAsync("payment",id,"p","paid")));
        await using var db=f.Open();var paid=(await db.Orders.SingleAsync()).PaymentStatus=="paid";
        Assert.Equal(paid?1:0,await db.Enrollments.CountAsync());Assert.Equal(paid?0:1,(await db.CourseClasses.SingleAsync()).AvailableSeats);
    }

    [PostgreSqlFact]
    public async Task CouponLastUseCannotBeConsumedByTwoOrders()
    {
        await using var f=await Fixture.CreateAsync(2);
        await using(var setup=f.Open()) { setup.Add(new PromoCode { Code="LAST", Discount=10, DiscountType="percentage", StartDate="2026-01-01",EndDate="2099-12-31",UsageLimit=1,IsActive=true });await setup.SaveChangesAsync(); }
        var a=Request();a.PromoCode="LAST";var b=Request();b.PromoCode="LAST";
        var results=await Task.WhenAll(f.Run(s=>s.CreateAsync(99,a)),f.Run(s=>s.CreateAsync(100,b)));
        Assert.Single(results,r=>r.Success);
        await using var db=f.Open();Assert.Equal(1,(await db.PromoCodes.SingleAsync(p=>p.Code=="LAST")).UsageCount);
        Assert.Equal(1,(await db.CourseClasses.SingleAsync()).AvailableSeats);
    }

    [PostgreSqlFact]
    public async Task TransfersCannotShareTheLastSeat()
    {
        await using var f=await Fixture.CreateAsync(2);
        foreach(var user in new[]{99,100}) {var order=await f.Run(s=>s.CreateAsync(user,Request()));await f.Run(s=>s.ProcessPaymentWebhookAsync("pay-"+user,order.Data!.OrderId,"p"+user,"paid"));}
        int[] students;
        await using(var setup=f.Open()) {students=await setup.Enrollments.OrderBy(e=>e.Id).Select(e=>e.StudentId).ToArrayAsync();setup.Add(new CourseClass {Id=100,CourseId=99,Capacity=1,AvailableSeats=1,DataRealizacao=DateTime.UtcNow.AddDays(15)});await setup.SaveChangesAsync();}
        async Task<bool> Transfer(int student) {await using var db=f.Open();return (await new CourseService(db).TransferEnrollmentAsync(99,99,student,100)).Success;}
        var results=await Task.WhenAll(Transfer(students[0]),Transfer(students[1]));Assert.Single(results,r=>r);
        await using var final=f.Open();Assert.Equal(0,(await final.CourseClasses.SingleAsync(c=>c.Id==100)).AvailableSeats);Assert.Equal(1,(await final.CourseClasses.SingleAsync(c=>c.Id==99)).AvailableSeats);
    }

    private static CreateOrderDTO Request()=>new(){PaymentMethod="pix",Itens=new(){new(){CourseId=99,TurmaId=99,Quantidade=1}}};

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string connection;
        private Fixture(string value)=>connection=value;
        public AppDbContext Open()=>new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        public async Task<T> Run<T>(Func<OrderService,Task<T>> action) { await using var db=Open();return await action(new OrderService(db,new(db),new(db),new(db))); }
        public static async Task<Fixture> CreateAsync(int capacity=1)
        {
            using var source=new AppDbContextFactory().CreateDbContext(Array.Empty<string>());
            var config=new NpgsqlConnectionStringBuilder(source.Database.GetConnectionString()) {Database="pim4_audit_test_"+Guid.NewGuid().ToString("N")};
            var f=new Fixture(config.ConnectionString);await using var db=f.Open();await db.Database.EnsureCreatedAsync();
            db.AddRange(new User {ID=99,Nome="Teste",Email="concurrency99@example.com",Cpf="52998224725",SenhaHash="hash"},new User {ID=100,Nome="Teste",Email="concurrency100@example.com",Cpf="16899535009",SenhaHash="hash"},new Course {Id=99,Nome="Concorrencia",Preco=100},new CourseClass {Id=99,CourseId=99,Capacity=capacity,AvailableSeats=capacity,DataRealizacao=DateTime.UtcNow.AddDays(10)});
            await db.SaveChangesAsync();return f;
        }
        public async ValueTask DisposeAsync()
        {
            // Only the unique disposable database created by this fixture is removed.
            if(!new NpgsqlConnectionStringBuilder(connection).Database!.StartsWith("pim4_audit_test_"))throw new InvalidOperationException();
            await using var db=Open();await db.Database.EnsureDeletedAsync();
        }
    }
}