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
    public async Task CatalogDoesNotAdvertiseSeatsAfterAClassHasStarted()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var pastCourse = new Course { Nome = "Turma iniciada", Preco = 100m, IsActive = true };
        var futureCourse = new Course { Nome = "Turma futura", Preco = 100m, IsActive = true };
        fixture.Context.Courses.AddRange(pastCourse, futureCourse);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.CourseClasses.AddRange(
            new CourseClass {
                CourseId = pastCourse.Id, DataRealizacao = DateTime.UtcNow.AddSeconds(-1),
                Status = "scheduled", Capacity = 30, AvailableSeats = 30, VafasDisponiveis = 30,
                Local = "Sao Paulo"
            },
            new CourseClass {
                CourseId = futureCourse.Id, DataRealizacao = DateTime.UtcNow.AddHours(2),
                Status = "scheduled", Capacity = 30, AvailableSeats = 30, VafasDisponiveis = 30,
                Local = "Sao Paulo"
            });
        await fixture.Context.SaveChangesAsync();
        var service = new CourseService(fixture.Context);
        var catalog = await service.GetCatalogAsync(new CourseCatalogQueryDTO());
        Assert.Empty(catalog.Items.Single(c => c.Id == pastCourse.Id).Classes);
        Assert.Single(catalog.Items.Single(c => c.Id == futureCourse.Id).Classes);
        var available = await service.GetCatalogAsync(new CourseCatalogQueryDTO { AvailableOnly = true });
        Assert.DoesNotContain(available.Items, c => c.Id == pastCourse.Id);
        Assert.Contains(available.Items, c => c.Id == futureCourse.Id);
    }

    [Fact]
    public async Task CreatedClassesReferenceCoursesWithoutALegacyProductRelation()
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
        Assert.Equal(courseId, await fixture.Context.CourseClasses
            .Where(courseClass => courseClass.Id == classResult.Data.Id)
            .Select(courseClass => courseClass.CourseId)
            .SingleAsync());
        var classModel = fixture.Context.Model.FindEntityType(typeof(CourseClass))!;
        Assert.Null(classModel.FindProperty("ProdutoId"));
        Assert.DoesNotContain(classModel.GetForeignKeys(),
            relation => relation.PrincipalEntityType.ClrType.Name == "Product");
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
        var course = new Course
        {
            Nome = "Curso legado",
            Preco = 300m
        };
        var courseClass = new CourseClass
        {
            Course = course,
            Capacity = 5,
            AvailableSeats = 5,
            VafasDisponiveis = 5,
            DataRealizacao = new DateTime(2026, 11, 1),
            Local = "Sao Paulo",
            Instructor = "Instrutor"
        };
        fixture.Context.AddRange(course, courseClass);
        await fixture.Context.SaveChangesAsync();

        var service = new InventoryService(fixture.Context);
        var result = await service.ValidateAndReserveAsync(new List<CreateOrderItemDTO>
        {
            new() { CourseId = course.Id, TurmaId = courseClass.Id, Quantidade = 1 }
        });

        Assert.True(result.Success);
        await fixture.Context.Entry(courseClass).ReloadAsync();
        var persistedClass = await fixture.Context.CourseClasses.SingleAsync();
        Assert.Equal(4, persistedClass.AvailableSeats);
        Assert.Equal(4, persistedClass.VafasDisponiveis);
    }

    [Fact]
    public async Task CoursePurchaseMustProvideAnExplicitClass()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var course = new Course
        {
            Nome = "Curso sem turma",
            Preco = 300m
        };
        fixture.Context.AddRange(course);
        await fixture.Context.SaveChangesAsync();

        var service = new InventoryService(fixture.Context);
        var result = await service.ValidateAndReserveAsync(new List<CreateOrderItemDTO>
        {
            new() { CourseId = course.Id, Quantidade = 1 }
        });

        Assert.False(result.Success);
        Assert.Contains("exige", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EadCoursePurchaseDoesNotRequireAClass()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var course = new Course
        {
            Nome = "Curso EAD",
            Preco = 300m,
            DeliveryMode = "ead"
        };
        fixture.Context.AddRange(course);
        await fixture.Context.SaveChangesAsync();

        var result = await new InventoryService(fixture.Context).ValidateAndReserveAsync(new List<CreateOrderItemDTO>
        {
            new() { CourseId = course.Id, Quantidade = 1 }
        });

        Assert.True(result.Success);
        Assert.Empty(await fixture.Context.CourseClasses.ToListAsync());
    }

    [Fact]
    public async Task EadCoursePurchaseRejectsAProvidedClass()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var course = new Course
        {
            Nome = "Curso EAD com turma indevida",
            Preco = 300m,
            DeliveryMode = "ead"
        };
        var courseClass = new CourseClass
        {
            Course = course,
            Capacity = 5,
            AvailableSeats = 5,
            VafasDisponiveis = 5,
            DataRealizacao = DateTime.UtcNow.AddDays(5),
            Status = "scheduled"
        };
        fixture.Context.AddRange(course, courseClass);
        await fixture.Context.SaveChangesAsync();

        var result = await new InventoryService(fixture.Context).ValidateAndReserveAsync(new List<CreateOrderItemDTO>
        {
            new() { CourseId = course.Id, TurmaId = courseClass.Id, Quantidade = 1 }
        });

        Assert.False(result.Success);
        Assert.Contains("nao deve possuir turma", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CoursePurchaseRejectsAClassFromAnotherCourse()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var firstCourse = new Course { Nome = "Primeiro curso", Preco = 100m };
        var secondCourse = new Course { Nome = "Segundo curso", Preco = 100m };
        var secondClass = new CourseClass
        {
            Course = secondCourse,
            Capacity = 5,
            AvailableSeats = 5,
            VafasDisponiveis = 5,
            DataRealizacao = new DateTime(2026, 11, 1),
            Local = "Sao Paulo",
            Instructor = "Instrutor"
        };
        fixture.Context.AddRange(firstCourse, secondCourse, secondClass);
        await fixture.Context.SaveChangesAsync();

        var service = new InventoryService(fixture.Context);
        var result = await service.ValidateAndReserveAsync(new List<CreateOrderItemDTO>
        {
            new() { CourseId = firstCourse.Id, TurmaId = secondClass.Id, Quantidade = 1 }
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

    [Fact]
    public async Task EadCoursesArePreservedButHiddenWhileTheFeatureIsDisabled()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var course = new Course
        {
            Nome = "Curso EAD protegido",
            Preco = 100m,
            DeliveryMode = "ead",
            IsActive = true,
            Modules = new List<CourseModule>
            {
                new()
                {
                    Title = "Modulo protegido",
                    Lessons = new List<CourseLesson>
                    {
                        new() { Title = "Aula protegida", VideoUrl = "https://video.example/private" }
                    }
                }
            },
            Assessments = new List<CourseAssessment>
            {
                new() { Title = "Avaliacao protegida" }
            }
        };
        fixture.Context.Courses.Add(course);
        await fixture.Context.SaveChangesAsync();
        var service = new CourseService(fixture.Context);

        var catalog = await service.GetCatalogAsync(new CourseCatalogQueryDTO());
        var detail = await service.GetByIdAsync(course.Id);
        var administrative = await service.GetAllAsync(includeInactive: true);

        Assert.Empty(catalog.Items);
        Assert.Null(detail);
        Assert.Empty(administrative);
        Assert.True(await fixture.Context.Courses.AnyAsync(item => item.Id == course.Id));
    }
    [Fact]
    public async Task CourseCanBeArchivedAndRestoredWithoutDeletingItsHistory()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var course = new Course
        {
            Nome = "Curso arquivavel",
            Preco = 100m,
            Category = "Formacao",
            IsActive = true
        };

        fixture.Context.Courses.Add(course);
        await fixture.Context.SaveChangesAsync();

        var service = new CourseService(fixture.Context);
        var archived = await service.SetActiveAsync(course.Id, false);

        Assert.True(archived.Success);
        Assert.False(archived.Data!.IsActive);
        Assert.Equal(course.Id, await fixture.Context.Courses
            .Where(item => item.Id == course.Id)
            .Select(item => item.Id)
            .SingleAsync());

        var restored = await service.SetActiveAsync(course.Id, true);

        Assert.True(restored.Success);
        Assert.True(restored.Data!.IsActive);
    }

    [Fact]
    public async Task EnrollmentStatusChangesUpdateClassAvailability()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var course = new Course { Nome = "Curso", Preco = 100m, IsActive = true };
        var student = new Student { Name = "Aluno", Email = "aluno@example.com" };
        var user = new User
        {
            ID = 200,
            Nome = "Usuario",
            Email = "usuario.enrollment@example.com",
            Cpf = "52998224725",
            SenhaHash = "hash"
        };
        var order = new Order { UsuarioId = user.ID, Status = "completed", PaymentStatus = "paid" };
        fixture.Context.AddRange(course, student, user, order);
        await fixture.Context.SaveChangesAsync();

        var courseClass = new CourseClass
        {
            CourseId = course.Id,
            Capacity = 2,
            AvailableSeats = 1,
            VafasDisponiveis = 1,
            Status = "scheduled",
            DataRealizacao = DateTime.UtcNow.AddDays(5)
        };
        fixture.Context.CourseClasses.Add(courseClass);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.Enrollments.Add(new Enrollment
        {
            ClassId = courseClass.Id,
            StudentId = student.Id,
            OrderId = order.Id,
            Status = "active"
        });
        await fixture.Context.SaveChangesAsync();

        var service = new CourseService(fixture.Context);
        var completed = await service.UpdateEnrollmentStatusAsync(
            course.Id, courseClass.Id, student.Id, "completed");

        Assert.True(completed.Success);
        Assert.Equal("completed", completed.Data!.Status);
        Assert.Equal(1, await fixture.Context.CourseClasses
            .Where(item => item.Id == courseClass.Id)
            .Select(item => item.AvailableSeats)
            .SingleAsync());

        var cancelled = await service.UpdateEnrollmentStatusAsync(
            course.Id, courseClass.Id, student.Id, "cancelled");

        Assert.True(cancelled.Success);
        Assert.Equal(2, await fixture.Context.CourseClasses
            .Where(item => item.Id == courseClass.Id)
            .Select(item => item.AvailableSeats)
            .SingleAsync());
    }

    [Fact]
    public async Task ActiveEnrollmentCanBeTransferredBetweenClasses()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var course = new Course { Nome = "Curso com transferencia", Preco = 100m, IsActive = true };
        var student = new Student { Name = "Aluno", Email = "transferencia@example.com" };
        var user = new User
        {
            ID = 201,
            Nome = "Usuario",
            Email = "usuario.transferencia@example.com",
            Cpf = "52998224725",
            SenhaHash = "hash"
        };
        var order = new Order { UsuarioId = user.ID, Status = "completed", PaymentStatus = "paid" };
        fixture.Context.AddRange(course, student, user, order);
        await fixture.Context.SaveChangesAsync();

        var sourceClass = new CourseClass
        {
            CourseId = course.Id,
            Capacity = 3,
            AvailableSeats = 2,
            VafasDisponiveis = 2,
            Status = "scheduled",
            DataRealizacao = DateTime.UtcNow.AddDays(5),
            Local = "Sao Paulo",
            Instructor = "Instrutor A"
        };
        var targetClass = new CourseClass
        {
            CourseId = course.Id,
            Capacity = 2,
            AvailableSeats = 1,
            VafasDisponiveis = 1,
            Status = "scheduled",
            DataRealizacao = DateTime.UtcNow.AddDays(10),
            Local = "Campinas",
            Instructor = "Instrutor B"
        };
        fixture.Context.CourseClasses.AddRange(sourceClass, targetClass);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.Enrollments.Add(new Enrollment
        {
            ClassId = sourceClass.Id,
            StudentId = student.Id,
            OrderId = order.Id,
            Status = "active"
        });
        await fixture.Context.SaveChangesAsync();

        var result = await new CourseService(fixture.Context).TransferEnrollmentAsync(
            course.Id,
            sourceClass.Id,
            student.Id,
            targetClass.Id);

        Assert.True(result.Success);
        Assert.Equal(targetClass.Id, result.Data!.TargetClass.Id);
        Assert.Equal(3, await fixture.Context.CourseClasses
            .Where(item => item.Id == sourceClass.Id)
            .Select(item => item.AvailableSeats)
            .SingleAsync());
        Assert.Equal(0, await fixture.Context.CourseClasses
            .Where(item => item.Id == targetClass.Id)
            .Select(item => item.AvailableSeats)
            .SingleAsync());
        Assert.Equal(targetClass.Id, await fixture.Context.Enrollments
            .Where(item => item.StudentId == student.Id)
            .Select(item => item.ClassId)
            .SingleAsync());
        Assert.Equal(1, await fixture.Context.AuditLogs
            .CountAsync(log => log.Action == "transferred" && log.EntityType == "Enrollment"));
    }

    [Fact]
    public async Task CoursePurchaseRejectsCompletedClasses()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var course = new Course
        {
            Nome = "Curso concluido",
            Preco = 100m
        };
        var courseClass = new CourseClass
        {
            Course = course,
            Status = "completed",
            DataRealizacao = DateTime.UtcNow.AddDays(2),
            Capacity = 5,
            AvailableSeats = 5,
            VafasDisponiveis = 5
        };
        fixture.Context.AddRange(course, courseClass);
        await fixture.Context.SaveChangesAsync();

        var result = await new InventoryService(fixture.Context).ValidateAndReserveAsync(new List<CreateOrderItemDTO>
        {
            new() { CourseId = course.Id, TurmaId = courseClass.Id, Quantidade = 1 }
        });

        Assert.False(result.Success);
        Assert.Contains("inscricoes abertas", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CoursePurchaseRejectsMoreThanMaximumQuantity()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var course = new Course
        {
            Nome = "Curso com limite",
            Preco = 100m
        };
        var courseClass = new CourseClass
        {
            Course = course,
            Status = "scheduled",
            DataRealizacao = DateTime.UtcNow.AddDays(2),
            Capacity = 10,
            AvailableSeats = 10,
            VafasDisponiveis = 10
        };
        fixture.Context.AddRange(course, courseClass);
        await fixture.Context.SaveChangesAsync();

        var result = await new InventoryService(fixture.Context).ValidateAndReserveAsync(new List<CreateOrderItemDTO>
        {
            new() { CourseId = course.Id, TurmaId = courseClass.Id, Quantidade = 6 }
        });

        Assert.False(result.Success);
        Assert.Contains("uma vaga", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NewOrdersRejectArchivedCourses()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var course = new Course { Nome = "Curso arquivado", Preco = 100m, IsActive = false };
        fixture.Context.Courses.Add(course);
        await fixture.Context.SaveChangesAsync();
        var result = await new InventoryService(fixture.Context).ValidateAndReserveAsync(
            new List<CreateOrderItemDTO> { new() { CourseId = course.Id, Quantidade = 1 } });
        Assert.False(result.Success);
        Assert.Contains("arquivado", result.Error, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task CatalogOptionsIncludeOnlyAvailableActiveCourseData()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var course = new Course
        {
            Nome = "Curso com opcoes",
            Preco = 100m,
            Category = "Urgencia",
            IsActive = true
        };
        fixture.Context.Courses.Add(course);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.CourseClasses.Add(new CourseClass
        {
            CourseId = course.Id,
            DataRealizacao = DateTime.UtcNow.AddDays(5),
            Status = "scheduled",
            AvailableSeats = 4,
            Capacity = 4,
            VafasDisponiveis = 4,
            Local = "Campinas"
        });
        await fixture.Context.SaveChangesAsync();

        var options = await new CourseService(fixture.Context).GetCatalogOptionsAsync();

        Assert.Contains("Urgencia", options.Categories);
        Assert.Contains("Campinas", options.Cities);
    }

    [Fact]
    public async Task CourseWithClassesCannotBeDeleted()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var course = new Course { Nome = "Curso", Preco = 100m };
        var courseClass = new CourseClass
        {
            Course = course, Capacity = 3, AvailableSeats = 3,
            DataRealizacao = DateTime.UtcNow.AddDays(10)
        };
        fixture.Context.AddRange(course, courseClass);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        fixture.Context.Courses.Remove(new Course { Id = course.Id });
        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Context.SaveChangesAsync());
        Assert.Equal(1, await fixture.Context.CourseClasses.CountAsync());
    }
    [Fact]
    public async Task ClassCannotBeSavedWithoutAValidCourse()
    {
        await using var fixture = await TestFixture.CreateAsync();
        fixture.Context.CourseClasses.Add(new CourseClass
        {
            CourseId = 987654,
            Capacity = 3,
            AvailableSeats = 3,
            DataRealizacao = DateTime.UtcNow.AddDays(10)
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Context.SaveChangesAsync());
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
