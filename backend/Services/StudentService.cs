using Microsoft.EntityFrameworkCore;
using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.DTOs;
using EquipamentosMedicosApi.Models;

namespace EquipamentosMedicosApi.Services;

public class StudentService
{
    private static readonly HashSet<string> AllowedStatuses = new()
    {
        "active",
        "completed",
        "cancelled"
    };

    private readonly AppDbContext _context;
    private readonly AuditService _auditService;

    public StudentService(AppDbContext context, AuditService? auditService = null)
    {
        _context = context;
        _auditService = auditService ?? new AuditService(context);
    }

    public async Task<List<StudentDTO>> GetAllAsync()
    {
        return await _context.Students
            .OrderBy(student => student.Name)
            .Select(student => ToResponse(student))
            .ToListAsync();
    }

    public async Task<StudentDTO?> GetByIdAsync(int id)
    {
        var student = await _context.Students
            .Include(student => student.Enrollments)
            .FirstOrDefaultAsync(student => student.Id == id);

        return student == null ? null : ToResponse(student);
    }

    public async Task<ServiceResult<StudentDTO>> CreateAsync(StudentRequestDTO request, int? userId = null)
    {
        var validation = ValidateRequest(request);
        if (validation != null)
        {
            return ServiceResult<StudentDTO>.Fail(validation);
        }

        var duplicate = await HasDuplicateAsync(
            request.Email,
            request.CourseId,
            excludedStudentId: null);

        if (duplicate)
        {
            return ServiceResult<StudentDTO>.Fail(
                "Ja existe um aluno cadastrado para este e-mail neste curso.");
        }

        var student = new Student();
        ApplyRequest(student, request);

        _context.Students.Add(student);
        _auditService.Add(userId, "created", "Student", student.Id, null,
            new { student.Name, student.Email, student.CourseId, student.Status });
        await _context.SaveChangesAsync();

        return ServiceResult<StudentDTO>.Ok(ToResponse(student));
    }

    public async Task<ServiceResult<StudentDTO>> UpdateAsync(int id, StudentRequestDTO request, int? userId = null)
    {
        var validation = ValidateRequest(request);
        if (validation != null)
        {
            return ServiceResult<StudentDTO>.Fail(validation);
        }

        var student = await _context.Students
            .Include(student => student.Enrollments)
            .FirstOrDefaultAsync(student => student.Id == id);

        if (student == null)
        {
            return ServiceResult<StudentDTO>.Fail("Aluno nao encontrado.");
        }

        var duplicate = await HasDuplicateAsync(request.Email, request.CourseId, id);
        if (duplicate)
        {
            return ServiceResult<StudentDTO>.Fail(
                "Ja existe outro aluno cadastrado para este e-mail neste curso.");
        }

        var previous = new
        {
            student.Name,
            student.Email,
            student.Phone,
            student.CourseId,
            student.CourseName,
            student.EnrollmentDate,
            student.Status
        };

        ApplyRequest(student, request);

        foreach (var enrollment in student.Enrollments)
        {
            enrollment.Status = student.Status;
        }

        _auditService.Add(userId, "updated", "Student", student.Id, previous,
            new
            {
                student.Name,
                student.Email,
                student.Phone,
                student.CourseId,
                student.CourseName,
                student.EnrollmentDate,
                student.Status
            });
        await _context.SaveChangesAsync();

        return ServiceResult<StudentDTO>.Ok(ToResponse(student));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id, int? userId = null)
    {
        var student = await _context.Students
            .Include(item => item.Enrollments)
            .FirstOrDefaultAsync(student => student.Id == id);

        if (student == null)
        {
            return ServiceResult<bool>.Fail("Aluno nao encontrado.");
        }

        if (student.Enrollments.Count != 0)
        {
            return ServiceResult<bool>.Fail(
                "Aluno com matriculas vinculadas nao pode ser removido manualmente.");
        }

        _auditService.Add(userId, "deleted", "Student", student.Id,
            new { student.Name, student.Email, student.CourseId, student.Status }, null);
        _context.Students.Remove(student);
        await _context.SaveChangesAsync();

        return ServiceResult<bool>.Ok(true);
    }

    private static StudentDTO ToResponse(Student student)
    {
        return new StudentDTO
        {
            Id = student.Id,
            Name = student.Name,
            Email = student.Email,
            Phone = student.Phone,
            CourseId = student.CourseId,
            CourseName = student.CourseName,
            EnrollmentDate = student.EnrollmentDate,
            Status = student.Status
        };
    }

    private static void ApplyRequest(Student student, StudentRequestDTO request)
    {
        student.Name = request.Name.Trim();
        student.Email = NormalizeEmail(request.Email);
        student.Phone = request.Phone.Trim();
        student.CourseId = request.CourseId.Trim();
        student.CourseName = request.CourseName.Trim();
        student.EnrollmentDate = request.EnrollmentDate.Trim();
        student.Status = request.Status.Trim().ToLowerInvariant();
    }

    private static string? ValidateRequest(StudentRequestDTO request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Nome e obrigatorio.";
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return "E-mail e obrigatorio.";
        }

        if (string.IsNullOrWhiteSpace(request.CourseId))
        {
            return "Curso e obrigatorio.";
        }

        if (!AllowedStatuses.Contains(request.Status.Trim().ToLowerInvariant()))
        {
            return "Status deve ser 'active', 'completed' ou 'cancelled'.";
        }

        return null;
    }

    private async Task<bool> HasDuplicateAsync(
        string email,
        string courseId,
        int? excludedStudentId)
    {
        var normalizedEmail = NormalizeEmail(email);
        var normalizedCourseId = courseId.Trim();

        return await _context.Students.AnyAsync(student =>
            student.Email.ToLower() == normalizedEmail &&
            student.CourseId == normalizedCourseId &&
            (!excludedStudentId.HasValue || student.Id != excludedStudentId.Value));
    }

    private static string NormalizeEmail(string email)
        => email.Trim().ToLowerInvariant();
}
