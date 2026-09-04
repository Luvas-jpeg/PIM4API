using System.Data.Common;
using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.DTOs;
using EquipamentosMedicosApi.Models;
using EquipamentosMedicosApi.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests;

public sealed class CourseDomainTests
{
    [Fact]
    public async Task CourseClassesCanBeCreatedWithoutLegacyProducts()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var service = new CourseService(fixture.Context);

        var courseResult = await service.CreateAsync(new CourseRequestDTO
        {
            Nome = "Curso independente",
            Description = "Curso sem produto legado",
            Preco = 250m,
            Category = "Formacao"
        });

        Assert.True(courseResult.Success);
        var courseId = courseResult.Data!.Id;

        var classResult = await service.CreateClassAsync(
            courseId,
            new CourseClassRequestDTO
            {
                StartDate = new DateTime(2026, 10, 1),
                Local = "Sao Paulo",
                Instructor = "Instrutor",
                Capacity = 20
            });

        Assert.True(classResult.Success);
        Assert.Equal(courseId, classResult.Data!.CourseId);
        Assert.Null(await fixture.Context.CourseClasses
            .Where(courseClass => courseClass.Id == classResult.Data.Id)
            .Select(courseClass => courseClass.ProdutoId)
            .SingleAsync());
    }

    [Fact]
    public async Task EnrollmentUsesTheExplicitCourseClassRelation()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var course = new Course
        {
            Nome = "Curso relacional",
            Description = "Curso",
            Category = "Formacao",
            Image = string.Empty,
            Preco = 100m
        };
        var student = new Student
        {
            Name = "Aluno",
            Email = "aluno.relacional@example.com",
            Phone = string.Empty,
            CourseId = "course",
            CourseName = course.Nome,
            EnrollmentDate = "2026-09-04"
        };
        var user = new User
        {
            ID = 100,
            Nome = "Usuario",
            Email = "usuario.relacional@example.com",
            Cpf = "52998224725",
            SenhaHash = "hash",
            Role = "Cliente"
        };

        fixture.Context.AddRange(course, student, user);
        await fixture.Context.SaveChangesAsync();

        var courseClass = new CourseClass
        {
            CourseId = course.Id,
            DataRealizacao = new DateTime(2026, 10, 1),
            Capacity = 10,
            AvailableSeats = 10,
            VafasDisponiveis = 10,
            Local = "Sao Paulo",
            Instructor = "Instrutor"
        };
        fixture.Context.CourseClasses.Add(courseClass);
        await fixture.Context.SaveChangesAsync();

        var order = new Order
        {
            UsuarioId = user.ID,
            Total = 100m,
            PaymentMethod = "pix",
            PromoCode = string.Empty
        };
        fixture.Context.Orders.Add(order);
        await fixture.Context.SaveChangesAsync();

        fixture.Context.Enrollments.Add(new Enrollment
        {
            ClassId = courseClass.Id,
            StudentId = student.Id,
            OrderId = order.Id
        });
        await fixture.Context.SaveChangesAsync();

        var enrollment = await fixture.Context.Enrollments
            .Include(item => item.Class)
            .SingleAsync();

        Assert.NotNull(enrollment.Class);
        Assert.Equal(course.Id, enrollment.Class!.CourseId);
    }

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
