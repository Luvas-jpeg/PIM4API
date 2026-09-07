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

    [Fact]
    public async Task CoursePurchaseReservesSeatsFromTheSelectedClass()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var product = new Product
        {
            Nome = "Curso legado",
            TipoProduto = "course",
            Preco = 300m,
            Estoque = 10
        };
        var course = new Course
        {
            Nome = product.Nome,
            Preco = product.Preco,
            LegacyProduct = product
        };
        var courseClass = new CourseClass
        {
            Course = course,
            Produto = product,
            Capacity = 5,
            AvailableSeats = 5,
            VafasDisponiveis = 5,
            DataRealizacao = new DateTime(2026, 11, 1),
            Local = "Sao Paulo",
            Instructor = "Instrutor"
        };
        fixture.Context.AddRange(product, course, courseClass);
        await fixture.Context.SaveChangesAsync();

        var service = new InventoryService(fixture.Context);
        var result = await service.ValidateAndReserveAsync(new List<CreateOrderItemDTO>
        {
            new() { ProdutoId = product.Id, TurmaId = courseClass.Id, Quantidade = 2 }
        });

        Assert.True(result.Success);
        await fixture.Context.Entry(courseClass).ReloadAsync();
        var persistedClass = await fixture.Context.CourseClasses.SingleAsync();
        Assert.Equal(3, persistedClass.AvailableSeats);
        Assert.Equal(3, persistedClass.VafasDisponiveis);
    }

    [Fact]
    public async Task CoursePurchaseMustProvideAnExplicitClass()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var product = new Product
        {
            Nome = "Curso sem turma",
            TipoProduto = "course",
            Preco = 300m,
            Estoque = 10
        };
        var course = new Course
        {
            Nome = product.Nome,
            Preco = product.Preco,
            LegacyProduct = product
        };
        fixture.Context.AddRange(product, course);
        await fixture.Context.SaveChangesAsync();

        var service = new InventoryService(fixture.Context);
        var result = await service.ValidateAndReserveAsync(new List<CreateOrderItemDTO>
        {
            new() { ProdutoId = product.Id, Quantidade = 1 }
        });

        Assert.False(result.Success);
        Assert.Contains("exige", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CoursePurchaseRejectsAClassFromAnotherCourse()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var firstProduct = new Product { Nome = "Primeiro curso", TipoProduto = "course", Preco = 100m };
        var secondProduct = new Product { Nome = "Segundo curso", TipoProduto = "course", Preco = 100m };
        var firstCourse = new Course { Nome = firstProduct.Nome, Preco = firstProduct.Preco, LegacyProduct = firstProduct };
        var secondCourse = new Course { Nome = secondProduct.Nome, Preco = secondProduct.Preco, LegacyProduct = secondProduct };
        var secondClass = new CourseClass
        {
            Course = secondCourse,
            Produto = secondProduct,
            Capacity = 5,
            AvailableSeats = 5,
            VafasDisponiveis = 5,
            DataRealizacao = new DateTime(2026, 11, 1),
            Local = "Sao Paulo",
            Instructor = "Instrutor"
        };
        fixture.Context.AddRange(firstProduct, secondProduct, firstCourse, secondCourse, secondClass);
        await fixture.Context.SaveChangesAsync();

        var service = new InventoryService(fixture.Context);
        var result = await service.ValidateAndReserveAsync(new List<CreateOrderItemDTO>
        {
            new() { ProdutoId = firstProduct.Id, TurmaId = secondClass.Id, Quantidade = 1 }
        });

        Assert.False(result.Success);
        Assert.Contains("nao pertence", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublicCatalogReturnsOnlyActiveCoursesWithAvailableFutureClasses()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var availableCourse = new Course
        {
            Nome = "Curso disponivel",
            Preco = 100m,
            Category = "Formacao",
            IsActive = true
        };
        var inactiveCourse = new Course
        {
            Nome = "Curso arquivado",
            Preco = 100m,
            Category = "Formacao",
            IsActive = false
        };

        fixture.Context.AddRange(availableCourse, inactiveCourse);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.CourseClasses.AddRange(
            new CourseClass
            {
                CourseId = availableCourse.Id,
                DataRealizacao = DateTime.UtcNow.AddDays(10),
                Status = "scheduled",
                AvailableSeats = 3,
                VafasDisponiveis = 3,
                Capacity = 3,
                Local = "Sao Paulo"
            },
            new CourseClass
            {
                CourseId = inactiveCourse.Id,
                DataRealizacao = DateTime.UtcNow.AddDays(10),
                Status = "scheduled",
                AvailableSeats = 3,
                VafasDisponiveis = 3,
                Capacity = 3,
                Local = "Sao Paulo"
            });
        await fixture.Context.SaveChangesAsync();

        var result = await new CourseService(fixture.Context).GetCatalogAsync(new CourseCatalogQueryDTO());

        Assert.Single(result.Items);
        Assert.Equal(availableCourse.Id, result.Items[0].Id);
        Assert.Equal(3, result.Items[0].Classes[0].AvailableSeats);
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
