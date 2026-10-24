using Microsoft.EntityFrameworkCore;
using EquipamentosMedicosApi.Models;

namespace EquipamentosMedicosApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<CourseModule> CourseModules { get; set; }
        public DbSet<CourseLesson> CourseLessons { get; set; }
        public DbSet<CourseAssessment> CourseAssessments { get; set; }
        public DbSet<CourseQuestion> CourseQuestions { get; set; }
        public DbSet<CourseQuestionOption> CourseQuestionOptions { get; set; }
        public DbSet<CourseAssessmentAttempt> CourseAssessmentAttempts { get; set; }
        public DbSet<CourseAssessmentAnswer> CourseAssessmentAnswers { get; set; }
        public DbSet<CourseCertificate> CourseCertificates { get; set; }
        public DbSet<CourseInterest> CourseInterests { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<CourseClass> CourseClasses { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<PromoCode> PromoCodes { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<CourseProgress> CourseProgresses { get; set; }
        public DbSet<PaymentWebhookEvent> PaymentWebhookEvents { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Order>()
                .Property(order => order.Status)
                .HasDefaultValue("pending");

            modelBuilder.Entity<Order>()
                .Property(order => order.PaymentStatus)
                .HasDefaultValue("pending");

            modelBuilder.Entity<RefreshToken>()
                .HasIndex(refreshToken => refreshToken.Token)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(user => user.Email)
                .IsUnique();

            modelBuilder.Entity<PromoCode>()
                .HasIndex(promoCode => promoCode.Code)
                .IsUnique();

            modelBuilder.Entity<Order>()
                .HasIndex(order => new { order.UsuarioId, order.IdempotencyKey })
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL");

            modelBuilder.Entity<PaymentWebhookEvent>()
                .HasIndex(webhookEvent => webhookEvent.EventId)
                .IsUnique();

            modelBuilder.Entity<PaymentWebhookEvent>()
                .HasOne(webhookEvent => webhookEvent.Order)
                .WithMany()
                .HasForeignKey(webhookEvent => webhookEvent.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AuditLog>()
                .HasIndex(log => new { log.EntityType, log.EntityId, log.CreatedAt });

            modelBuilder.Entity<AuditLog>()
                .HasOne(log => log.User)
                .WithMany()
                .HasForeignKey(log => log.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Course>()
                .HasMany(course => course.Classes)
                .WithOne(courseClass => courseClass.Course)
                .HasForeignKey(courseClass => courseClass.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Course>()
                .Property(course => course.DeliveryMode)
                .HasDefaultValue("presencial");

            modelBuilder.Entity<Course>()
                .Property(course => course.WorkloadHours)
                .HasDefaultValue(0);

            modelBuilder.Entity<CourseModule>()
                .HasOne(module => module.Course)
                .WithMany(course => course.Modules)
                .HasForeignKey(module => module.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CourseModule>()
                .HasMany(module => module.Lessons)
                .WithOne(lesson => lesson.Module)
                .HasForeignKey(lesson => lesson.ModuleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CourseAssessment>()
                .HasOne(assessment => assessment.Course)
                .WithMany(course => course.Assessments)
                .HasForeignKey(assessment => assessment.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CourseQuestion>()
                .HasOne(question => question.Assessment)
                .WithMany(assessment => assessment.Questions)
                .HasForeignKey(question => question.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CourseQuestionOption>()
                .HasOne(option => option.Question)
                .WithMany(question => question.Options)
                .HasForeignKey(option => option.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CourseAssessmentAttempt>()
                .HasIndex(attempt => new { attempt.UserId, attempt.AssessmentId, attempt.AttemptNumber })
                .IsUnique();

            modelBuilder.Entity<CourseAssessmentAttempt>()
                .HasOne(attempt => attempt.User)
                .WithMany()
                .HasForeignKey(attempt => attempt.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CourseAssessmentAttempt>()
                .HasOne(attempt => attempt.Course)
                .WithMany()
                .HasForeignKey(attempt => attempt.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CourseAssessmentAttempt>()
                .HasOne(attempt => attempt.Assessment)
                .WithMany()
                .HasForeignKey(attempt => attempt.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CourseAssessmentAnswer>()
                .HasOne(answer => answer.Attempt)
                .WithMany(attempt => attempt.Answers)
                .HasForeignKey(answer => answer.AttemptId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CourseAssessmentAnswer>()
                .HasOne(answer => answer.Question)
                .WithMany()
                .HasForeignKey(answer => answer.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CourseAssessmentAnswer>()
                .HasOne(answer => answer.SelectedOption)
                .WithMany()
                .HasForeignKey(answer => answer.SelectedOptionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CourseCertificate>()
                .HasIndex(certificate => new { certificate.UserId, certificate.CourseId })
                .IsUnique();

            modelBuilder.Entity<CourseCertificate>()
                .HasIndex(certificate => certificate.ValidationCode)
                .IsUnique();

            modelBuilder.Entity<CourseCertificate>()
                .HasOne(certificate => certificate.User)
                .WithMany()
                .HasForeignKey(certificate => certificate.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CourseInterest>()
                .HasIndex(interest => new { interest.CourseId, interest.Email })
                .IsUnique();

            modelBuilder.Entity<CourseInterest>()
                .HasOne(interest => interest.Course)
                .WithMany()
                .HasForeignKey(interest => interest.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CourseCertificate>()
                .HasOne(certificate => certificate.Course)
                .WithMany()
                .HasForeignKey(certificate => certificate.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CourseClass>()
                .Property(courseClass => courseClass.AvailableSeats)
                .HasDefaultValue(0);

            modelBuilder.Entity<CourseClass>()
                .Property(courseClass => courseClass.Capacity)
                .HasDefaultValue(0);

            modelBuilder.Entity<CourseClass>()
                .Property(courseClass => courseClass.Status)
                .HasDefaultValue("scheduled");

            modelBuilder.Entity<Enrollment>()
                .HasOne(enrollment => enrollment.Class)
                .WithMany(courseClass => courseClass.Enrollments)
                .HasForeignKey(enrollment => enrollment.ClassId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Enrollment>()
                .HasOne(enrollment => enrollment.Course)
                .WithMany(course => course.Enrollments)
                .HasForeignKey(enrollment => enrollment.CourseId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Enrollment>()
                .HasOne(enrollment => enrollment.Student)
                .WithMany(student => student.Enrollments)
                .HasForeignKey(enrollment => enrollment.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderItem>()
                .HasOne(item => item.Course)
                .WithMany()
                .HasForeignKey(item => item.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Student>()
                .HasOne(student => student.User)
                .WithMany()
                .HasForeignKey(student => student.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Student>()
                .HasIndex(student => student.UserId);

            modelBuilder.Entity<Enrollment>()
                .HasOne(enrollment => enrollment.Order)
                .WithMany(order => order.Enrollments)
                .HasForeignKey(enrollment => enrollment.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Enrollment>()
                .Property(enrollment => enrollment.EnrolledAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<CourseProgress>()
                .HasIndex(progress => new { progress.UserId, progress.CourseId })
                .IsUnique();

            // Seed de dados
            modelBuilder.Entity<Student>().HasData(
                new Student { Id = 1, Name = "João Silva", Email = "joao.silva@email.com", Phone = "(11) 98765-4321", CourseId = "5", CourseName = "Curso de Primeiros Socorros Básico", EnrollmentDate = "10/04/2026", Status = "active" },
                new Student { Id = 2, Name = "Maria Santos", Email = "maria.santos@email.com", Phone = "(21) 99876-5432", CourseId = "6", CourseName = "Curso de Suporte Avançado de Vida (ACLS)", EnrollmentDate = "05/04/2026", Status = "active" },
                new Student { Id = 3, Name = "Pedro Oliveira", Email = "pedro.oliveira@email.com", Phone = "(31) 97654-3210", CourseId = "7", CourseName = "Workshop de Técnicas de Sutura", EnrollmentDate = "28/03/2026", Status = "completed" }
            );

            modelBuilder.Entity<PromoCode>().HasData(
                new PromoCode { Id = 1, Code = "MEDICO10", Discount = 10m, DiscountType = "percentage", StartDate = "2026-04-01", EndDate = "2026-12-31", IsActive = true, UsageLimit = 100, UsageCount = 5 },
                new PromoCode { Id = 2, Code = "PRIMEIRACOMPRA", Discount = 50m, DiscountType = "fixed", StartDate = "2026-01-01", EndDate = "2026-12-31", IsActive = true, UsageCount = 12 }
            );

            // Persist mutations to the list, not only replacement of its reference.
            modelBuilder.Entity<CourseProgress>().Property(p => p.CompletedLessons)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize<List<int>>(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<int>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<int>()
                );
            modelBuilder.Entity<CourseProgress>().Property(p => p.CompletedLessons)
                .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<int>>(
                    (a, b) => a != null && b != null && a.SequenceEqual(b),
                    v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
                    v => v.ToList()));
            modelBuilder.Entity<Order>().HasOne<PromoCode>().WithMany()
                .HasForeignKey(o => o.PromoCodeId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
