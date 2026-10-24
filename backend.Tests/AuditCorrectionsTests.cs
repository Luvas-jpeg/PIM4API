using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.DTOs;
using EquipamentosMedicosApi.Models;
using EquipamentosMedicosApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using static backend.Tests.OrderPaymentFlowTests;

namespace backend.Tests;

public sealed class AuditCorrectionsTests
{
    [Theory]
    [InlineData("172.31.245.10", "198.51.100.23")]
    [InlineData("203.0.113.10", "203.0.113.10")]
    public async Task ForwardedClientAddressIsAcceptedOnlyFromTrustedProxy(string proxy, string expected)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["ReverseProxy:KnownNetworks:0"]="172.31.245.0/24" }).Build();
        var options=new ForwardedHeadersOptions();ReverseProxyConfiguration.Configure(options,config);
        var context=new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Connection.RemoteIpAddress=System.Net.IPAddress.Parse(proxy);
        context.Request.Headers["X-Forwarded-For"]="198.51.100.23";
        var middleware=new Microsoft.AspNetCore.HttpOverrides.ForwardedHeadersMiddleware(_=>Task.CompletedTask,
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance,Microsoft.Extensions.Options.Options.Create(options));
        await middleware.Invoke(context);Assert.Equal(expected,context.Connection.RemoteIpAddress!.ToString());
    }

    [Fact]
    public async Task LegacyDuplicateEnrollmentsRequireAnExplicitIdentifier()
    {
        await using var f=await TestFixture.CreateAsync();
        var created=await f.CreateOrderService().CreateAsync(f.UserId,CreateRequest());
        await f.CreateOrderService().ProcessPaymentWebhookAsync("paid",created.Data!.OrderId,"p","paid");
        var first=await f.Context.Enrollments.SingleAsync();
        var second=new Enrollment {ClassId=99,CourseId=99,StudentId=first.StudentId,OrderId=first.OrderId};
        f.Context.Add(second);(await f.Context.CourseClasses.SingleAsync()).AvailableSeats=0;
        await f.Context.SaveChangesAsync();var service=new CourseService(f.Context);
        Assert.False((await service.UpdateEnrollmentStatusAsync(99,99,first.StudentId,"cancelled")).Success);
        Assert.True((await service.UpdateEnrollmentStatusAsync(99,99,first.StudentId,"cancelled",enrollmentId:second.Id)).Success);
        Assert.Equal("active",first.Status);Assert.Equal("cancelled",second.Status);
    }

    [Fact]
    public async Task MultipleCoursesAllowOneSeatEachAndRejectDuplicatePurchase()
    {
        await using var f = await TestFixture.CreateAsync();
        var course = new Course { Id = 200, Nome = "Outro curso", Preco = 70, DeliveryMode = "presencial" };
        f.Context.AddRange(course, new CourseClass { Id = 200, Course = course, Capacity = 2, AvailableSeats = 2,
            DataRealizacao = DateTime.UtcNow.AddDays(10) });
        await f.Context.SaveChangesAsync();
        var request = CreateRequest();
        request.Itens.Add(new() { CourseId = 200, TurmaId = 200, Quantidade = 1 });
        var order = await f.CreateOrderService().CreateAsync(f.UserId, request, "two-courses");
        Assert.True(order.Success, order.Error);
        Assert.Equal(170m, order.Data!.Total);
        Assert.Equal(2, await f.Context.OrderItems.CountAsync());
        var duplicate = await f.CreateOrderService().CreateAsync(f.UserId, CreateRequest());
        Assert.False(duplicate.Success);
        Assert.Contains("pendente", duplicate.Error);
        await f.CreateOrderService().ProcessPaymentWebhookAsync("two-courses-paid", order.Data.OrderId, "payment", "paid");
        var activeDuplicate = await f.CreateOrderService().CreateAsync(f.UserId, CreateRequest());
        Assert.False(activeDuplicate.Success);
        Assert.Contains("matricula", activeDuplicate.Error);
        Assert.Equal(2, await f.Context.Enrollments.CountAsync());
    }

    [Fact]
    public async Task TransferThenRefundReleasesCurrentClassOnce()
    {
        await using var f = await TestFixture.CreateAsync();
        var service = f.CreateOrderService();
        var order = await service.CreateAsync(f.UserId, CreateRequest());
        await service.ProcessPaymentWebhookAsync("paid", order.Data!.OrderId, "p", "paid");
        var enrollment = await f.Context.Enrollments.SingleAsync();
        var target = new CourseClass { CourseId = 99, Capacity = 1, AvailableSeats = 1,
            DataRealizacao = DateTime.UtcNow.AddDays(11), Status = "scheduled" };
        f.Context.Add(target); await f.Context.SaveChangesAsync();
        var transfer = await new CourseService(f.Context).TransferEnrollmentAsync(99, 99, enrollment.StudentId, target.Id);
        Assert.True(transfer.Success, transfer.Error);
        var refund = await service.RefundAsync(order.Data.OrderId);
        Assert.True(refund.Success);
        Assert.True((await service.RefundAsync(order.Data.OrderId)).Success);
        Assert.Equal(2, await f.Context.CourseClasses.Where(c => c.Id == 99).Select(c => c.AvailableSeats).SingleAsync());
        Assert.Equal(1, await f.Context.CourseClasses.Where(c => c.Id == target.Id).Select(c => c.AvailableSeats).SingleAsync());
        Assert.Equal("cancelled", enrollment.Status);
    }

    [Fact]
    public async Task GeneralStudentStatusAndRefundDoNotDoubleRelease()
    {
        await using var f = await TestFixture.CreateAsync();
        var order = await f.CreateOrderService().CreateAsync(f.UserId, CreateRequest());
        await f.CreateOrderService().ProcessPaymentWebhookAsync("paid", order.Data!.OrderId, "p", "paid");
        var student = await f.Context.Students.SingleAsync(s => s.UserId == f.UserId);
        var request = new StudentRequestDTO { Name=student.Name, Email=student.Email, CourseId=student.CourseId,
            CourseName=student.CourseName, EnrollmentDate=student.EnrollmentDate, Phone=student.Phone, Status="cancelled" };
        var cancelled = await new StudentService(f.Context).UpdateAsync(student.Id, request);
        Assert.True(cancelled.Success, cancelled.Error);
        Assert.Equal(2, await f.Context.CourseClasses.Where(c=>c.Id==99).Select(c=>c.AvailableSeats).SingleAsync());
        request.Status="active";
        Assert.True((await new StudentService(f.Context).UpdateAsync(student.Id, request)).Success);
        Assert.Equal(1, await f.Context.CourseClasses.Where(c=>c.Id==99).Select(c=>c.AvailableSeats).SingleAsync());
        request.Status="cancelled";
        Assert.True((await new StudentService(f.Context).UpdateAsync(student.Id, request)).Success);
        Assert.True((await f.CreateOrderService().RefundAsync(order.Data.OrderId)).Success);
        Assert.Equal(2, await f.Context.CourseClasses.Where(c=>c.Id==99).Select(c=>c.AvailableSeats).SingleAsync());
        request.Status="active";
        Assert.False((await new StudentService(f.Context).UpdateAsync(student.Id, request)).Success);
    }

    [Fact]
    public async Task CancelledOrderReturnsCouponUsageAndDoesNotReturnItTwice()
    {
        await using var f = await TestFixture.CreateAsync();
        var promo = new PromoCode { Code="ONE", Discount=10, DiscountType="percentage", IsActive=true, UsageLimit=1,
            StartDate="2026-01-01", EndDate="2099-12-31" };
        f.Context.Add(promo);await f.Context.SaveChangesAsync();
        var request=CreateRequest();request.PromoCode="ONE";
        var order=await f.CreateOrderService().CreateAsync(f.UserId,request);
        Assert.True(order.Success,order.Error);
        Assert.Equal(1,promo.UsageCount);
        Assert.True((await f.CreateOrderService().CancelAsync(f.UserId,order.Data!.OrderId)).Success);
        Assert.Equal(0,promo.UsageCount);
        Assert.False((await f.CreateOrderService().CancelAsync(f.UserId,order.Data.OrderId)).Success);
        Assert.Equal(0,promo.UsageCount);
    }

    [Fact]
    public async Task AuditStoresFinalIdentifierAndProgressDetectsListMutation()
    {
        await using var f=await TestFixture.CreateAsync();
        var course=await new CourseService(f.Context).CreateAsync(new CourseRequestDTO { Nome="Identificador real", Preco=10 });
        Assert.True(course.Success);
        Assert.Equal(course.Data!.Id.ToString(),(await f.Context.AuditLogs.SingleAsync()).EntityId);
        var progress=new CourseProgress { UserId=f.UserId,CourseId=99,CompletedLessons=new(){1} };
        f.Context.Add(progress);await f.Context.SaveChangesAsync();
        progress.CompletedLessons.Add(2);await f.Context.SaveChangesAsync();
        await f.Context.Entry(progress).ReloadAsync();
        Assert.Equal(new[]{1,2},progress.CompletedLessons);
        Assert.Null(f.Context.Model.FindEntityType(typeof(CourseClass))!.FindProperty("VafasDisponiveis"));
        Assert.DoesNotContain(f.Context.Model.GetEntityTypes(),e=>e.GetTableName()=="Products");
    }

    [Fact]
    public void ProxyConfigurationNeverTrustsEveryNetwork()
    {
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["ReverseProxy:KnownNetworks:0"]="0.0.0.0/0" }).Build();
        Assert.Throws<InvalidOperationException>(()=>ReverseProxyConfiguration.Configure(new ForwardedHeadersOptions(),configuration));
    }
}
