using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EquipamentosMedicosApi.Controllers;

[ApiController]
[Route("api/courses/{courseId:int}/recommendations")]
public sealed class CourseRecommendationsController(AppDbContext db, CourseRecommendationEngine engine,
    ILogger<CourseRecommendationsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(int courseId, CancellationToken cancellationToken)
    {
        if (!await db.Courses.AnyAsync(c => c.Id == courseId && c.IsActive && c.DeliveryMode == "presencial", cancellationToken))
            return NotFound(new { message = "Curso não encontrado." });
        var now = DateTime.UtcNow;
        var courses = await db.Courses.AsNoTracking()
            .Where(c => c.IsActive && c.DeliveryMode == "presencial")
            .OrderBy(c => c.Id).Take(CourseRecommendationEngine.MaximumCourses)
            .Select(c => new RecommendationCourse(c.Id, c.Nome,
                c.Description.Length > 2000 ? c.Description.Substring(0, 2000) : c.Description,
                c.Category, c.Preco, c.Image,
                c.Classes.Any(t => t.Status == "scheduled" && t.AvailableSeats > 0 && t.DataRealizacao > now)))
            .ToListAsync(cancellationToken);
        try
        {
            return Ok(engine.Recommend(courses, courseId));
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            logger.LogWarning(error, "Unable to train course recommendation model for {Count} courses.", courses.Count);
            return Ok(new CourseRecommendationResult("model-unavailable", CourseRecommendationEngine.Algorithm,
                null, null, courses.Count, 0, [], "model-unavailable"));
        }
    }
}
