using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EquipamentosMedicosApi.Data;
using Microsoft.EntityFrameworkCore;
using EquipamentosMedicosApi.DTOs;

namespace EquipamentosMedicosApi.Controllers
{
    [Route("api/me")]
    [ApiController]
    [Authorize]
    public class MeController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MeController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetMe()
        {
            var userId = GetAuthenticatedUserId();
            if (userId == null) return Unauthorized();

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null) return NotFound();

            return Ok(new {
                id = user.ID,
                name = user.Nome,
                email = user.Email,
                role = user.Role
            });
        }

        [HttpGet("courses")]
        public async Task<IActionResult> ListMyCourses()
        {
            var userId = GetAuthenticatedUserId();
            if (userId == null) return Unauthorized();

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null) return NotFound();

            var enrollments = await _context.Enrollments
                .Include(e => e.Class)
                    .ThenInclude(c => c!.Produto)
                .Include(e => e.Student)
                .Where(e => e.Student != null && e.Student.Email == user.Email)
                .ToListAsync();

            var courses = enrollments.Select(e => new {
                courseId = e.Class?.ProdutoId,
                title = e.Class?.Produto?.Nome,
                description = e.Class?.Produto?.Description,
                imageUrl = e.Class?.Produto?.Image,
                instructor = e.Class?.Instructor,
                location = e.Class?.Local,
                startDate = e.Class?.DataRealizacao,
                enrollmentStatus = e.Status
            });

            return Ok(courses);
        }

        [HttpGet("courses/{courseId}/progress")]
        public async Task<IActionResult> GetProgress(int courseId)
        {
            var userId = GetAuthenticatedUserId();
            if (userId == null) return Unauthorized();

            var progress = await _context.CourseProgresses
                .FirstOrDefaultAsync(p => p.UserId == userId.Value && p.CourseId == courseId);

            if (progress == null)
            {
                return Ok(new { courseId = courseId, percent = 0, completedLessons = new int[] { } });
            }

            return Ok(new {
                courseId = progress.CourseId,
                percent = progress.Percent,
                completedLessons = progress.CompletedLessons,
                lastSeenAt = progress.LastSeenAt,
                completedAt = progress.CompletedAt
            });
        }

        [HttpPut("courses/{courseId}/progress")]
        public async Task<IActionResult> UpdateProgress(int courseId, [FromBody] DTOs.UpdateProgressRequest request)
        {
            var userId = GetAuthenticatedUserId();
            if (userId == null) return Unauthorized();

            if (request.Percent < 0 || request.Percent > 100)
                return BadRequest(new { message = "Percent must be between 0 and 100" });

            var progress = await _context.CourseProgresses
                .FirstOrDefaultAsync(p => p.UserId == userId.Value && p.CourseId == courseId);

            if (progress == null)
            {
                progress = new Models.CourseProgress
                {
                    UserId = userId.Value,
                    CourseId = courseId,
                    Percent = request.Percent,
                    LastSeenAt = DateTime.UtcNow
                };

                if (request.CompletedLessonId.HasValue)
                    progress.CompletedLessons.Add(request.CompletedLessonId.Value);

                _context.CourseProgresses.Add(progress);
            }
            else
            {
                progress.Percent = request.Percent;
                progress.LastSeenAt = DateTime.UtcNow;

                if (request.CompletedLessonId.HasValue && !progress.CompletedLessons.Contains(request.CompletedLessonId.Value))
                    progress.CompletedLessons.Add(request.CompletedLessonId.Value);

                if (request.Percent >= 100)
                    progress.CompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(new {
                courseId = progress.CourseId,
                percent = progress.Percent,
                completedLessons = progress.CompletedLessons,
                lastSeenAt = progress.LastSeenAt,
                completedAt = progress.CompletedAt
            });
        }

        private int? GetAuthenticatedUserId()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                           ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
                           ?? User.FindFirst("sub");

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return null;
            }

            return userId;
        }
    }
}
