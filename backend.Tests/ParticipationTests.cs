using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.Models;
using EquipamentosMedicosApi.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests;

public sealed class ParticipationTests
{
    private static readonly DateTime Start = new(2030, 6, 10, 15, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(-8, "waiting", false)]
    [InlineData(-7, "pending", true)]
    [InlineData(-1, "pending", true)]
    [InlineData(0, "closed", false)]
    [InlineData(1, "closed", false)]
    public void ConfirmationWindowHasExactBoundaries(int days, string status, bool allowed)
    {
        var state = ParticipationService.Describe(NewEnrollment(), Start.AddDays(days));
        Assert.Equal(status, state.Status);
        Assert.Equal(allowed, state.CanConfirm);
        Assert.Equal(Start.AddDays(-7), state.OpensAt);
    }

    [Theory]
    [InlineData("cancelled", "scheduled")]
    [InlineData("completed", "scheduled")]
    [InlineData("active", "cancelled")]
    [InlineData("active", "completed")]
    public void InactiveEnrollmentOrClassCannotConfirm(string enrollmentStatus, string classStatus)
    {
        var enrollment = NewEnrollment();
        enrollment.Status = enrollmentStatus;
        enrollment.Class!.Status = classStatus;
        Assert.False(ParticipationService.Describe(enrollment, Start.AddDays(-2)).CanConfirm);
    }

    [Fact]
    public void EadHasNoAttendanceConfirmation()
    {
        var enrollment = NewEnrollment();
        enrollment.Class = null;
        Assert.Equal("not_applicable", ParticipationService.Describe(enrollment, Start.AddDays(-1)).Status);
    }

    [Fact]
    public async Task ConfirmationIsOwnedPersistentAndIdempotent()
    {
        await using var f = await Fixture.CreateAsync();
        var service = new ParticipationService(f.Db, f.Clock);
        Assert.Null(await service.ConfirmAsync(f.Enrollment.Id, 9999));
        var first = await service.ConfirmAsync(f.Enrollment.Id, f.User.ID);
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var second = await service.ConfirmAsync(f.Enrollment.Id, f.User.ID);
        Assert.Equal("confirmed", first!.Status);
        Assert.Equal(first.ConfirmedAt, second!.ConfirmedAt);
        f.Db.ChangeTracker.Clear();
        var saved = await f.Db.Enrollments.SingleAsync();
        Assert.Equal(first.ConfirmedAt, saved.ParticipationConfirmedAt);
        Assert.Equal(Start, saved.ParticipationStartDate);
        Assert.Equal("active", saved.Status);
        Assert.Equal(9, (await f.Db.CourseClasses.SingleAsync()).AvailableSeats);
    }

    [Fact]
    public async Task DateChangeRequiresNewConfirmation()
    {
        await using var f = await Fixture.CreateAsync();
        var service = new ParticipationService(f.Db, f.Clock);
        await service.ConfirmAsync(f.Enrollment.Id, f.User.ID);
        f.Enrollment.Class!.DataRealizacao = Start.AddHours(2);
        await f.Db.SaveChangesAsync();
        Assert.Equal("pending", ParticipationService.Describe(f.Enrollment, f.Clock.Now.UtcDateTime).Status);
        var result = await service.ConfirmAsync(f.Enrollment.Id, f.User.ID);
        Assert.Equal("confirmed", result!.Status);
        Assert.Equal(Start.AddHours(2), f.Enrollment.ParticipationStartDate);
    }

    [Fact]
    public async Task TooEarlyOrStartedCannotWriteConfirmation()
    {
        await using var f = await Fixture.CreateAsync();
        var service = new ParticipationService(f.Db, f.Clock);
        foreach (var now in new[] { Start.AddDays(-8), Start })
        {
            f.Clock.Now = new DateTimeOffset(now);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmAsync(f.Enrollment.Id, f.User.ID));
            Assert.Null(f.Enrollment.ParticipationConfirmedAt);
        }
    }

    [Fact]
    public async Task StudentCancellationAndReactivationDoNotRestoreOldConfirmation()
    {
        await using var f = await Fixture.CreateAsync();
        await new ParticipationService(f.Db, f.Clock).ConfirmAsync(f.Enrollment.Id, f.User.ID);
        var request = new EquipamentosMedicosApi.DTOs.StudentRequestDTO {
            Name = f.User.Nome, Email = f.User.Email, Phone = "11999999999",
            CourseId = f.Enrollment.CourseId!.Value.ToString(), CourseName = "Curso teste",
            EnrollmentDate = "2030-06-01", Status = "cancelled"
        };
        var service = new StudentService(f.Db);
        Assert.True((await service.UpdateAsync(f.Enrollment.StudentId, request)).Success);
        Assert.Null(f.Enrollment.ParticipationConfirmedAt);
        request.Status = "active";
        Assert.True((await service.UpdateAsync(f.Enrollment.StudentId, request)).Success);
        Assert.Equal("pending", ParticipationService.Describe(f.Enrollment, f.Clock.Now.UtcDateTime).Status);
    }

    private static Enrollment NewEnrollment() => new() {
        Class = new CourseClass { DataRealizacao = Start, Status = "scheduled", Capacity = 10, AvailableSeats = 9 }
    };
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(Start.AddDays(-3));
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public required SqliteConnection Connection { get; init; }
        public required AppDbContext Db { get; init; }
        public TestClock Clock { get; } = new();
        public required Enrollment Enrollment { get; init; }
        public required User User { get; init; }
        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var user = new User { Nome = "Aluno teste", Email = "reminder@example.com" };
            var course = new Course { Nome = "Curso teste" };
            var enrollment = NewEnrollment();
            enrollment.Course = course;
            enrollment.Class!.Course = course;
            enrollment.Student = new Student { User = user, Name = user.Nome, Email = user.Email };
            enrollment.Order = new Order { Usuario = user, PaymentStatus = "paid" };
            db.Add(enrollment);
            await db.SaveChangesAsync();
            return new Fixture { Db = db, Connection = connection, Enrollment = enrollment, User = user };
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }
}
