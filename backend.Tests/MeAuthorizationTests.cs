using System.Data.Common;
using System.Security.Claims;
using EquipamentosMedicosApi.Controllers;
using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests;

public sealed class MeAuthorizationTests
{
    [Fact]
    public async Task UserCanReadOwnEnrollmentButNotAnotherUsersEnrollment()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var ownController = fixture.CreateController(fixture.OwnerUserId);
        var otherController = fixture.CreateController(fixture.OtherUserId);

        var ownResult = await ownController.GetEnrollment(fixture.EnrollmentId);
        var otherResult = await otherController.GetEnrollment(fixture.EnrollmentId);

        Assert.IsType<OkObjectResult>(ownResult);
        Assert.IsType<NotFoundObjectResult>(otherResult);
    }

    [Fact]
    public async Task AnotherUserCannotReadCourseProgressOrCertificateStatus()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var controller = fixture.CreateController(fixture.OtherUserId);

        var progress = await controller.GetProgress(fixture.CourseId);
        var certificate = await controller.GetCertificateStatus(fixture.CourseId);

        Assert.IsType<NotFoundObjectResult>(progress);
        Assert.IsType<NotFoundObjectResult>(certificate);
    }

    [Fact]
    public async Task CancelledEnrollmentDoesNotGrantCourseAccess()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var enrollment = await fixture.Context.Enrollments.FindAsync(fixture.EnrollmentId);
        enrollment!.Status = "cancelled";
        await fixture.Context.SaveChangesAsync();
        var controller = fixture.CreateController(fixture.OwnerUserId);

        var progress = await controller.GetProgress(fixture.CourseId);
        var certificate = await controller.GetCertificateStatus(fixture.CourseId);

        Assert.IsType<NotFoundObjectResult>(progress);
        Assert.IsType<NotFoundObjectResult>(certificate);
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly DbConnection _connection;
        public AppDbContext Context { get; }
        public int OwnerUserId { get; }
        public int OtherUserId { get; }
        public int CourseId { get; }
        public int EnrollmentId { get; }

        private TestFixture(
            DbConnection connection,
            AppDbContext context,
            int ownerUserId,
            int otherUserId,
            int courseId,
            int enrollmentId)
        {
            _connection = connection;
            Context = context;
            OwnerUserId = ownerUserId;
            OtherUserId = otherUserId;
            CourseId = courseId;
            EnrollmentId = enrollmentId;
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

            var owner = CreateUser("dono.matricula@example.com", "52998224725");
            var other = CreateUser("outro.usuario@example.com", "16899535009");
            var course = new Course
            {
                Nome = "Curso EAD protegido",
                DeliveryMode = "ead",
                Preco = 100m,
                WorkloadHours = 20
            };
            context.AddRange(owner, other, course);
            await context.SaveChangesAsync();

            var student = new Student
            {
                UserId = owner.ID,
                Name = owner.Nome,
                Email = owner.Email,
                CourseId = course.Id.ToString(),
                CourseName = course.Nome,
                EnrollmentDate = "2026-09-28"
            };
            var order = new Order
            {
                UsuarioId = owner.ID,
                Status = "completed",
                PaymentStatus = "paid",
                Total = course.Preco
            };
            context.AddRange(student, order);
            await context.SaveChangesAsync();

            var enrollment = new Enrollment
            {
                CourseId = course.Id,
                StudentId = student.Id,
                OrderId = order.Id,
                Status = "active"
            };
            context.Enrollments.Add(enrollment);
            await context.SaveChangesAsync();

            return new TestFixture(
                connection,
                context,
                owner.ID,
                other.ID,
                course.Id,
                enrollment.Id);
        }

        public MeController CreateController(int userId)
        {
            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                "TestAuthentication");
            return new MeController(Context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(identity)
                    }
                }
            };
        }

        private static User CreateUser(string email, string cpf)
        {
            return new User
            {
                Nome = email.Split('@')[0],
                Email = email,
                Cpf = cpf,
                SenhaHash = "hash",
                Role = "Cliente"
            };
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}