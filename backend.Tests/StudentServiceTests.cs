using System.Data.Common;
using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.DTOs;
using EquipamentosMedicosApi.Models;
using EquipamentosMedicosApi.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests;

public sealed class StudentServiceTests
{
    [Fact]
    public async Task CreateRejectsDuplicateStudentForSameCourseAndEmail()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var service = new StudentService(fixture.Context);
        var request = CreateRequest();

        var first = await service.CreateAsync(request);
        var second = await service.CreateAsync(new StudentRequestDTO
        {
            Name = "Outro aluno",
            Email = " ALUNO@EXAMPLE.COM ",
            Phone = "11999999999",
            CourseId = "curso-1",
            CourseName = "Curso",
            EnrollmentDate = "2026-09-15",
            Status = "active"
        });

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Equal(1, await fixture.Context.Students
            .CountAsync(student => student.Email == "aluno@example.com" && student.CourseId == "curso-1"));
    }

    [Fact]
    public async Task DeleteRejectsStudentWithEnrollments()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var service = new StudentService(fixture.Context);
        var create = await service.CreateAsync(CreateRequest());
        var course = new Course { Nome = "Curso", Preco = 100m };
        var user = new User
        {
            ID = 300,
            Nome = "Usuario",
            Email = "usuario.student@example.com",
            Cpf = "52998224725",
            SenhaHash = "hash"
        };
        var order = new Order { UsuarioId = user.ID, Status = "completed", PaymentStatus = "paid" };
        fixture.Context.AddRange(course, user, order);
        await fixture.Context.SaveChangesAsync();
        var courseClass = new CourseClass
        {
            CourseId = course.Id,
            Capacity = 1,
            AvailableSeats = 0,
            VafasDisponiveis = 0,
            Status = "scheduled",
            DataRealizacao = DateTime.UtcNow.AddDays(5)
        };
        fixture.Context.CourseClasses.Add(courseClass);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.Enrollments.Add(new Enrollment
        {
            ClassId = courseClass.Id,
            StudentId = create.Data!.Id,
            OrderId = order.Id,
            Status = "active"
        });
        await fixture.Context.SaveChangesAsync();

        var delete = await service.DeleteAsync(create.Data.Id);

        Assert.False(delete.Success);
        Assert.Equal(1, await fixture.Context.Students
            .CountAsync(student => student.Email == "aluno@example.com" && student.CourseId == "curso-1"));
    }

    private static StudentRequestDTO CreateRequest() => new()
    {
        Name = "Aluno",
        Email = "aluno@example.com",
        Phone = "11999999999",
        CourseId = "curso-1",
        CourseName = "Curso",
        EnrollmentDate = "2026-09-15",
        Status = "active"
    };

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly DbConnection _connection;
        public AppDbContext Context { get; }

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
            return new TestFixture(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
