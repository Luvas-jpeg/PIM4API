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
                .Include(item => item.Course)
                    .ThenInclude(course => course!.Assessments)
                        .ThenInclude(assessment => assessment.Questions)
                            .ThenInclude(question => question.Options)
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
            var certificate = course == null
                ? null
                : await _context.CourseCertificates
                    .Include(item => item.Course)
                    .Include(item => item.User)
                    .FirstOrDefaultAsync(item => item.UserId == userId.Value && item.CourseId == course.Id);

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
                    .ToList() ?? new List<CourseModuleResponseDTO>(),
                Assessments = course?.Assessments
                    .Where(assessment => assessment.IsActive)
                    .OrderBy(assessment => assessment.Title)
                    .Select(ToStudentAssessment)
                    .ToList() ?? new List<MyCourseAssessmentDTO>(),
                AssessmentAttempts = course == null
                    ? new List<MyAssessmentAttemptDTO>()
                    : await _context.CourseAssessmentAttempts
                        .Where(attempt => attempt.UserId == userId.Value && attempt.CourseId == course.Id)
                        .OrderByDescending(attempt => attempt.SubmittedAt)
                        .Select(attempt => new MyAssessmentAttemptDTO
                        {
                            Id = attempt.Id,
                            AssessmentId = attempt.AssessmentId,
                            AttemptNumber = attempt.AttemptNumber,
                            Score = attempt.Score,
                            Passed = attempt.Passed,
                            SubmittedAt = attempt.SubmittedAt
                        })
                        .ToListAsync(),
                Certificate = certificate == null ? null : ToCertificateResponse(certificate)
            });
        }

        [HttpPost("courses/{courseId:int}/assessments/{assessmentId:int}/submit")]
        public async Task<IActionResult> SubmitAssessment(
            int courseId,
            int assessmentId,
            [FromBody] SubmitAssessmentRequestDTO request)
        {
            var userId = GetAuthenticatedUserId();
            if (userId == null) return Unauthorized();

            if (!await UserHasCourseAccessAsync(userId.Value, courseId))
            {
                return NotFound(new { message = "Matricula nao encontrada para este curso." });
            }

            var assessment = await _context.CourseAssessments
                .Include(item => item.Questions.Where(question => question.IsActive))
                    .ThenInclude(question => question.Options)
                .FirstOrDefaultAsync(item =>
                    item.Id == assessmentId &&
                    item.CourseId == courseId &&
                    item.IsActive);

            if (assessment == null)
            {
                return NotFound(new { message = "Avaliacao nao encontrada." });
            }

            var questions = assessment.Questions
                .Where(question => question.IsActive)
                .OrderBy(question => question.SortOrder)
                .ToList();

            if (questions.Count == 0)
            {
                return BadRequest(new { message = "A avaliacao ainda nao possui questoes." });
            }

            var attemptsCount = await _context.CourseAssessmentAttempts.CountAsync(attempt =>
                attempt.UserId == userId.Value &&
                attempt.AssessmentId == assessmentId);

            if (attemptsCount >= assessment.MaxAttempts)
            {
                return BadRequest(new { message = "Limite de tentativas atingido." });
            }

            var answersByQuestion = request.Answers
                .GroupBy(answer => answer.QuestionId)
                .ToDictionary(group => group.Key, group => group.Last().SelectedOptionId);

            if (questions.Any(question => !answersByQuestion.ContainsKey(question.Id)))
            {
                return BadRequest(new { message = "Responda todas as questoes antes de enviar." });
            }

            var answers = new List<Models.CourseAssessmentAnswer>();
            var correctAnswers = 0;

            foreach (var question in questions)
            {
                var selectedOptionId = answersByQuestion[question.Id];
                var selectedOption = question.Options.FirstOrDefault(option => option.Id == selectedOptionId);

                if (selectedOption == null)
                {
                    return BadRequest(new { message = "Alternativa invalida para uma das questoes." });
                }

                if (selectedOption.IsCorrect)
                {
                    correctAnswers++;
                }

                answers.Add(new Models.CourseAssessmentAnswer
                {
                    QuestionId = question.Id,
                    SelectedOptionId = selectedOption.Id,
                    IsCorrect = selectedOption.IsCorrect
                });
            }

            var score = (int)Math.Round((correctAnswers / (double)questions.Count) * 100);
            var passed = score >= assessment.MinimumScore;
            var attempt = new Models.CourseAssessmentAttempt
            {
                UserId = userId.Value,
                CourseId = courseId,
                AssessmentId = assessmentId,
                AttemptNumber = attemptsCount + 1,
                Score = score,
                Passed = passed,
                SubmittedAt = DateTime.UtcNow,
                Answers = answers
            };

            _context.CourseAssessmentAttempts.Add(attempt);
            await _context.SaveChangesAsync();

            return Ok(new SubmitAssessmentResponseDTO
            {
                AssessmentId = assessmentId,
                AttemptNumber = attempt.AttemptNumber,
                Score = score,
                MinimumScore = assessment.MinimumScore,
                Passed = passed,
                RemainingAttempts = Math.Max(0, assessment.MaxAttempts - attempt.AttemptNumber)
            });
        }

        [HttpGet("courses/{courseId:int}/certificate")]
        public async Task<IActionResult> GetCertificateStatus(int courseId)
        {
            var userId = GetAuthenticatedUserId();
            if (userId == null) return Unauthorized();

            var status = await GetCertificateStatusAsync(userId.Value, courseId);
            return status == null
                ? NotFound(new { message = "Matricula nao encontrada para este curso." })
                : Ok(status);
        }

        [HttpPost("courses/{courseId:int}/certificate/issue")]
        public async Task<IActionResult> IssueCertificate(int courseId)
        {
            var userId = GetAuthenticatedUserId();
            if (userId == null) return Unauthorized();

            var status = await GetCertificateStatusAsync(userId.Value, courseId);
            if (status == null)
            {
                return NotFound(new { message = "Matricula nao encontrada para este curso." });
            }

            if (status.Certificate != null)
            {
                return Ok(status);
            }

            if (!status.Eligible)
            {
                return BadRequest(new { message = status.Reason });
            }

            var certificate = new Models.CourseCertificate
            {
                UserId = userId.Value,
                CourseId = courseId,
                ValidationCode = $"CERT-{courseId}-{userId.Value}-{Guid.NewGuid():N}"[..28].ToUpperInvariant(),
                IssuedAt = DateTime.UtcNow
            };

            _context.CourseCertificates.Add(certificate);
            await _context.SaveChangesAsync();

            certificate = await _context.CourseCertificates
                .Include(item => item.Course)
                .Include(item => item.User)
                .FirstAsync(item => item.Id == certificate.Id);

            return Ok(new CertificateStatusDTO
            {
                Eligible = true,
                Reason = "Certificado emitido.",
                Certificate = ToCertificateResponse(certificate)
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

        private async Task<CertificateStatusDTO?> GetCertificateStatusAsync(int userId, int courseId)
        {
            if (!await UserHasCourseAccessAsync(userId, courseId))
            {
                return null;
            }

            var certificate = await _context.CourseCertificates
                .Include(item => item.Course)
                .Include(item => item.User)
                .FirstOrDefaultAsync(item => item.UserId == userId && item.CourseId == courseId);

            if (certificate != null)
            {
                return new CertificateStatusDTO
                {
                    Eligible = true,
                    Reason = "Certificado ja emitido.",
                    Certificate = ToCertificateResponse(certificate)
                };
            }

            var course = await _context.Courses
                .Include(item => item.Assessments)
                .FirstOrDefaultAsync(item => item.Id == courseId);

            if (course == null)
            {
                return null;
            }

            if (course.DeliveryMode != "ead")
            {
                return new CertificateStatusDTO
                {
                    Eligible = false,
                    Reason = "Certificado automatico esta disponivel apenas para cursos EAD nesta etapa."
                };
            }

            var progress = await _context.CourseProgresses
                .FirstOrDefaultAsync(item => item.UserId == userId && item.CourseId == courseId);

            if ((progress?.Percent ?? 0) < 100)
            {
                return new CertificateStatusDTO
                {
                    Eligible = false,
                    Reason = "Conclua 100% das aulas para liberar o certificado."
                };
            }

            var activeAssessmentIds = course.Assessments
                .Where(item => item.IsActive)
                .Select(item => item.Id)
                .ToList();

            if (activeAssessmentIds.Count > 0)
            {
                var passedAssessmentIds = await _context.CourseAssessmentAttempts
                    .Where(item =>
                        item.UserId == userId &&
                        item.CourseId == courseId &&
                        item.Passed &&
                        activeAssessmentIds.Contains(item.AssessmentId))
                    .Select(item => item.AssessmentId)
                    .Distinct()
                    .ToListAsync();

                if (passedAssessmentIds.Count < activeAssessmentIds.Count)
                {
                    return new CertificateStatusDTO
                    {
                        Eligible = false,
                        Reason = "Seja aprovado nas avaliacoes ativas para liberar o certificado."
                    };
                }
            }

            return new CertificateStatusDTO
            {
                Eligible = true,
                Reason = "Certificado disponivel para emissao."
            };
        }

        private static MyCertificateDTO ToCertificateResponse(Models.CourseCertificate certificate)
        {
            return new MyCertificateDTO
            {
                Id = certificate.Id,
                CourseId = certificate.CourseId,
                CourseName = certificate.Course?.Nome ?? string.Empty,
                StudentName = certificate.User?.Nome ?? string.Empty,
                WorkloadHours = certificate.Course?.WorkloadHours ?? 0,
                ValidationCode = certificate.ValidationCode,
                IssuedAt = certificate.IssuedAt
            };
        }

        private static MyCourseAssessmentDTO ToStudentAssessment(Models.CourseAssessment assessment)
        {
            return new MyCourseAssessmentDTO
            {
                Id = assessment.Id,
                CourseId = assessment.CourseId,
                Title = assessment.Title,
                MinimumScore = assessment.MinimumScore,
                MaxAttempts = assessment.MaxAttempts,
                Questions = assessment.Questions
                    .Where(question => question.IsActive)
                    .OrderBy(question => question.SortOrder)
                    .ThenBy(question => question.Id)
                    .Select(question => new MyCourseQuestionDTO
                    {
                        Id = question.Id,
                        AssessmentId = question.AssessmentId,
                        Statement = question.Statement,
                        SortOrder = question.SortOrder,
                        Options = question.Options
                            .OrderBy(option => option.SortOrder)
                            .ThenBy(option => option.Id)
                            .Select(option => new MyCourseQuestionOptionDTO
                            {
                                Id = option.Id,
                                QuestionId = option.QuestionId,
                                Text = option.Text,
                                SortOrder = option.SortOrder
                            })
                            .ToList()
                    })
                    .ToList()
            };
        }
    }
}
