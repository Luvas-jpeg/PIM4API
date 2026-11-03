using EquipamentosMedicosApi.Controllers;
using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.Models;
using EquipamentosMedicosApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests;

public sealed class CourseRecommendationEndpointTests
{
    [Fact]
    public async Task RecommendationsExcludeArchivedEadUnavailableAndCurrentCourse()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        for (var id = 10; id <= 15; id++)
        {
            db.Courses.Add(new Course
            {
                Id = id, Nome = id == 13 ? "Gestão financeira" : $"Primeiros socorros {id}", Category = id == 13 ? "Gestão" : "Emergência", Description = id == 13 ? "finanças orçamento investimento contabilidade" : "primeiros socorros atendimento emergência",
                Preco = 100, IsActive = id != 14, DeliveryMode = id == 15 ? "ead" : "presencial",
                Classes = [new CourseClass { Capacity = 10, AvailableSeats = id == 12 ? 0 : 10, DataRealizacao = DateTime.UtcNow.AddDays(10) }]
            });
        }
        await db.SaveChangesAsync();
        var controller = new CourseRecommendationsController(db, new CourseRecommendationEngine(), NullLogger<CourseRecommendationsController>.Instance);
        var response = Assert.IsType<OkObjectResult>(await controller.Get(10, CancellationToken.None));
        var result = Assert.IsType<CourseRecommendationResult>(response.Value);
        Assert.Equal(4, result.TrainingCourseCount);
        Assert.Equal("ready", result.Status);
        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item => Assert.Contains(item.Id, new[] { 11, 13 }));
        Assert.IsType<NotFoundObjectResult>(await controller.Get(14, CancellationToken.None));
    }
}
