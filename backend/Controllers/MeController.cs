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
                .Include(e => e.Course)
                .Include(e => e.Class)
                    .ThenInclude(c => c!.Course)
                .Include(e => e.Class)
                    .ThenInclude(c => c!.Produto)
                .Include(e => e.Student)
                .Where(e => e.Student != null && e.Student.Email == user.Email)
                .OrderByDescending(e => e.EnrolledAt)
                .ToListAsync();

            var courses = enrollments.Select(e =>
            {
                var course = e.Course ?? e.Class?.Course;
                var legacyProduct = e.Class?.Produto;

                return new MyEnrollmentResponse
                {
                    EnrollmentId = e.Id,
                    CourseId = course?.Id ?? legacyProduct?.Id,
                    ClassId = e.ClassId,
                    OrderId = e.OrderId,
                    CourseName = course?.Nome ?? legacyProduct?.Nome ?? string.Empty,
                    CourseDescription = course?.Description ?? legacyProduct?.Description ?? string.Empty,
                    CourseImage = course?.Image ?? legacyProduct?.Image ?? string.Empty,
                    Category = course?.Category ?? legacyProduct?.Category ?? string.Empty,
                    DeliveryMode = course?.DeliveryMode ?? "presencial",
                    WorkloadHours = course?.WorkloadHours ?? 0,
                    Instructor = e.Class?.Instructor ?? string.Empty,
                    Location = e.Class?.Local ?? string.Empty,
                    StartDate = e.Class?.DataRealizacao ?? DateTime.MinValue,
                    EndDate = e.Class?.EndDate,
                    ClassStatus = e.Class?.Status ?? string.Empty,
                    EnrollmentStatus = e.Status,
                    EnrolledAt = e.EnrolledAt
                };
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

            if (!await UserHasCourseAccessAsync(userId.Value, courseId))
            {
                return NotFound(new { message = "Matricula nao encontrada para este curso." });
            }

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

        [HttpGet("enrollments/{enrollmentId:int}")]
        public async Task<IActionResult> GetEnrollment(int enrollmentId)
        {
            var userId = GetAuthenticatedUserId();
            if (userId == null) return Unauthorized();

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null) return NotFound();

            var enrollment = await _context.Enrollments
                .Include(item => item.Course)
                    .ThenInclude(course => course!.Modules)
                        .ThenInclude(module => module.Lessons)
                .Include(item => item.Class)
                    .ThenInclude(courseClass => courseClass!.Course)
                .Include(item => item.Class)
                    .ThenInclude(courseClass => courseClass!.Produto)
                .Include(item => item.Student)
                .FirstOrDefaultAsync(item =>
                    item.Id == enrollmentId &&
                    item.Student != null &&
                    item.Student.Email == user.Email);

            if (enrollment == null)
                return NotFound(new { message = "Matricula nao encontrada." });

            var course = enrollment.Course ?? enrollment.Class?.Course;
            var legacyProduct = enrollment.Class?.Produto;

            return Ok(new MyEnrollmentResponse
            {
                EnrollmentId = enrollment.Id,
                CourseId = course?.Id ?? legacyProduct?.Id,
                ClassId = enrollment.ClassId,
                OrderId = enrollment.OrderId,
                CourseName = course?.Nome ?? legacyProduct?.Nome ?? string.Empty,
                CourseDescription = course?.Description ?? legacyProduct?.Description ?? string.Empty,
                CourseImage = course?.Image ?? legacyProduct?.Image ?? string.Empty,
                Category = course?.Category ?? legacyProduct?.Category ?? string.Empty,
                DeliveryMode = course?.DeliveryMode ?? "presencial",
                WorkloadHours = course?.WorkloadHours ?? 0,
                Instructor = enrollment.Class?.Instructor ?? string.Empty,
                Location = enrollment.Class?.Local ?? string.Empty,
                StartDate = enrollment.Class?.DataRealizacao ?? DateTime.MinValue,
                EndDate = enrollment.Class?.EndDate,
                ClassStatus = enrollment.Class?.Status ?? string.Empty,
                EnrollmentStatus = enrollment.Status,
                EnrolledAt = enrollment.EnrolledAt,
                Modules = course?.Modules
                    .Where(module => module.IsActive)
                    .OrderBy(module => module.SortOrder)
                    .ThenBy(module => module.Title)
                    .Select(module => new CourseModuleResponseDTO
                    {
                        Id = module.Id,
                        CourseId = module.CourseId,
                        Title = module.Title,
                        SortOrder = module.SortOrder,
                        IsActive = module.IsActive,
                        Lessons = module.Lessons
                            .Where(lesson => lesson.IsActive)
                            .OrderBy(lesson => lesson.SortOrder)
                            .ThenBy(lesson => lesson.Title)
                            .Select(lesson => new CourseLessonResponseDTO
                            {
                                Id = lesson.Id,
                                ModuleId = lesson.ModuleId,
                                Title = lesson.Title,
                                Description = lesson.Description,
                                VideoUrl = lesson.VideoUrl,
                                DurationMinutes = lesson.DurationMinutes,
                                SortOrder = lesson.SortOrder,
                                IsActive = lesson.IsActive
                            })
                            .ToList()
                    })
                    .ToList() ?? new List<CourseModuleResponseDTO>()
            });
        }

        [HttpPut("courses/{courseId}/progress")]
        public async Task<IActionResult> UpdateProgress(int courseId, [FromBody] DTOs.UpdateProgressRequest request)
        {
            var userId = GetAuthenticatedUserId();
            if (userId == null) return Unauthorized();

            if (request.Percent < 0 || request.Percent > 100)
                return BadRequest(new { message = "Percent must be between 0 and 100" });

            if (!await UserHasCourseAccessAsync(userId.Value, courseId))
            {
                return NotFound(new { message = "Matricula nao encontrada para este curso." });
            }

            if (request.CompletedLessonId.HasValue)
            {
                var lessonBelongsToCourse = await _context.CourseLessons
                    .AnyAsync(lesson =>
                        lesson.Id == request.CompletedLessonId.Value &&
                        lesson.Module != null &&
                        lesson.Module.CourseId == courseId &&
                        lesson.IsActive &&
                        lesson.Module.IsActive);

                if (!lessonBelongsToCourse)
                {
                    return BadRequest(new { message = "Aula nao encontrada para este curso." });
                }
            }

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

        private async Task<bool> UserHasCourseAccessAsync(int userId, int courseId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            return await _context.Enrollments.AnyAsync(enrollment =>
                enrollment.Student != null &&
                enrollment.Student.Email == user.Email &&
                enrollment.Status != "cancelled" &&
                (enrollment.CourseId == courseId ||
                 (enrollment.Class != null && enrollment.Class.CourseId == courseId)));
        }
    }
}
