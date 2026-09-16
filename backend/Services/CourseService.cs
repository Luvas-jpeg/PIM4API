using Microsoft.EntityFrameworkCore;
using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.DTOs;
using EquipamentosMedicosApi.Models;

namespace EquipamentosMedicosApi.Services;

public class CourseService
{
    private readonly AppDbContext _context;
    private readonly AuditService _auditService;

    public CourseService(AppDbContext context, AuditService? auditService = null)
    {
        _context = context;
        _auditService = auditService ?? new AuditService(context);
    }

    public async Task<List<CourseResponseDTO>> GetAllAsync(bool includeInactive = false)
    {
        var query = _context.Courses
            .Include(course => course.Classes)
            .Include(course => course.Modules)
                .ThenInclude(module => module.Lessons)
            .AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(course => course.IsActive);
        }

        return (await query.OrderBy(course => course.Nome).ToListAsync())
            .Select(ToResponse)
            .ToList();
    }

    public async Task<PagedCourseResponseDTO> GetCatalogAsync(CourseCatalogQueryDTO request)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var today = DateTime.UtcNow;
        var classes = _context.CourseClasses
            .Where(courseClass =>
                courseClass.Status == "scheduled" &&
                courseClass.DataRealizacao >= today &&
                (!request.AvailableOnly || courseClass.AvailableSeats > 0));

        if (!string.IsNullOrWhiteSpace(request.City))
            classes = classes.Where(courseClass => courseClass.Local.Contains(request.City));
        if (request.StartDate.HasValue)
            classes = classes.Where(courseClass => courseClass.DataRealizacao >= request.StartDate.Value);
        if (request.EndDate.HasValue)
            classes = classes.Where(courseClass => courseClass.DataRealizacao <= request.EndDate.Value);

        var query = _context.Courses
            .Where(course => course.IsActive)
            .Where(course => !string.IsNullOrWhiteSpace(request.Category)
                ? course.Category == request.Category
                : true)
            .Where(course => string.IsNullOrWhiteSpace(request.Search)
                || course.Nome.Contains(request.Search)
                || course.Description.Contains(request.Search))
            .Where(course =>
                course.DeliveryMode == "ead" ||
                course.Classes.Any(courseClass => classes.Any(item => item.Id == courseClass.Id)))
            .Include(course => course.Classes)
            .Include(course => course.Modules)
                .ThenInclude(module => module.Lessons)
            .AsNoTracking();

        var sort = request.Sort.Trim().ToLowerInvariant();
        query = sort switch
        {
            "price-asc" => query.OrderBy(course => course.Preco),
            "price-desc" => query.OrderByDescending(course => course.Preco),
            "name" => query.OrderBy(course => course.Nome),
            "relevance" when !string.IsNullOrWhiteSpace(request.Search) =>
                query.OrderByDescending(course =>
                    course.Nome.ToLower() == request.Search!.Trim().ToLower() ? 3 :
                    course.Nome.ToLower().StartsWith(request.Search.Trim().ToLower()) ? 2 : 1)
                    .ThenBy(course => course.Nome),
            _ => query.OrderBy(course => course.DeliveryMode == "ead")
                .ThenBy(course => course.Classes
                    .Where(courseClass => classes.Any(item => item.Id == courseClass.Id))
                    .Min(courseClass => (DateTime?)courseClass.DataRealizacao))
                .ThenBy(course => course.Nome)
        };

        var totalItems = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        foreach (var course in items)
            course.Classes = course.Classes
                .Where(courseClass => classes.Any(item => item.Id == courseClass.Id))
                .OrderBy(courseClass => courseClass.DataRealizacao)
                .ToList();

        return new PagedCourseResponseDTO
        {
            Items = items.Select(ToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    public async Task<CourseCatalogOptionsDTO> GetCatalogOptionsAsync()
    {
        var today = DateTime.UtcNow;
        var courses = _context.Courses
            .Where(course => course.IsActive)
            .Where(course =>
                course.DeliveryMode == "ead" ||
                course.Classes.Any(courseClass =>
                    courseClass.Status == "scheduled" &&
                    courseClass.DataRealizacao >= today &&
                    courseClass.AvailableSeats > 0));

        var categories = await courses
            .Where(course => course.Category != "")
            .Select(course => course.Category)
            .Distinct()
            .OrderBy(category => category)
            .ToListAsync();

        var cities = await courses
            .SelectMany(course => course.Classes)
            .Where(courseClass =>
                courseClass.Status == "scheduled" &&
                courseClass.DataRealizacao >= today &&
                courseClass.AvailableSeats > 0 &&
                courseClass.Local != "")
            .Select(courseClass => courseClass.Local)
            .Distinct()
            .OrderBy(local => local)
            .ToListAsync();

        return new CourseCatalogOptionsDTO
        {
            Categories = categories,
            Cities = cities
        };
    }

    public async Task<CourseResponseDTO?> GetByIdAsync(int id, bool includeInactive = false)
    {
        var course = await _context.Courses
            .Include(item => item.Classes)
            .Include(item => item.Modules)
                .ThenInclude(module => module.Lessons)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && (includeInactive || item.IsActive));

        return course == null ? null : ToResponse(course);
    }

    public async Task<ServiceResult<CourseResponseDTO>> CreateAsync(CourseRequestDTO request, int? userId = null)
    {
        var validation = Validate(request);
        if (validation != null)
        {
            return ServiceResult<CourseResponseDTO>.Fail(validation);
        }

        var course = new Course();
        Apply(course, request);
        SyncLegacyProduct(course);
        _context.Courses.Add(course);
        _auditService.Add(userId, "created", "Course", course.Id, null, new { request.Nome, request.Preco });
        await _context.SaveChangesAsync();
        return ServiceResult<CourseResponseDTO>.Ok(ToResponse(course));
    }

    public async Task<ServiceResult<CourseResponseDTO>> UpdateAsync(int id, CourseRequestDTO request, int? userId = null)
    {
        var validation = Validate(request);
        if (validation != null)
        {
            return ServiceResult<CourseResponseDTO>.Fail(validation);
        }

        var course = await _context.Courses
            .Include(item => item.LegacyProduct)
            .Include(item => item.Classes)
            .Include(item => item.Modules)
                .ThenInclude(module => module.Lessons)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (course == null)
        {
            return ServiceResult<CourseResponseDTO>.Fail("Curso nao encontrado.");
        }

        var previous = new { course.Nome, course.Preco, course.Category, course.IsActive };
        Apply(course, request);
        SyncLegacyProduct(course);
        _auditService.Add(userId, "updated", "Course", course.Id, previous,
            new { course.Nome, course.Preco, course.Category, course.IsActive });
        await _context.SaveChangesAsync();
        return ServiceResult<CourseResponseDTO>.Ok(ToResponse(course));
    }

    public async Task<ServiceResult<CourseResponseDTO>> SetActiveAsync(int id, bool isActive, int? userId = null)
    {
        var course = await _context.Courses
            .Include(item => item.Classes)
            .Include(item => item.Modules)
                .ThenInclude(module => module.Lessons)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (course == null)
        {
            return ServiceResult<CourseResponseDTO>.Fail("Curso nao encontrado.");
        }

        var previous = course.IsActive;
        course.IsActive = isActive;
        _auditService.Add(userId, isActive ? "restored" : "archived", "Course", course.Id,
            previous, isActive);
        await _context.SaveChangesAsync();
        return ServiceResult<CourseResponseDTO>.Ok(ToResponse(course));
    }

    public async Task<List<CourseClassResponseDTO>> GetClassesAsync(int courseId, bool includeInactive = false)
    {
        var query = _context.CourseClasses
            .Where(courseClass => courseClass.CourseId == courseId)
            .AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(courseClass => courseClass.Status != "cancelled");
        }

        return (await query.OrderBy(courseClass => courseClass.DataRealizacao).ToListAsync())
            .Select(ToClassResponse)
            .ToList();
    }

    public async Task<CourseClassResponseDTO?> GetClassAsync(int courseId, int classId, bool includeInactive = false)
    {
        var courseClass = await _context.CourseClasses
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.Id == classId &&
                item.CourseId == courseId &&
                (includeInactive || item.Status != "cancelled"));

        return courseClass == null ? null : ToClassResponse(courseClass);
    }

    public async Task<List<StudentDTO>?> GetClassStudentsAsync(int courseId, int classId)
    {
        var classExists = await _context.CourseClasses.AnyAsync(item =>
            item.Id == classId && item.CourseId == courseId);

        if (!classExists)
        {
            return null;
        }

        return await _context.Enrollments
            .Where(enrollment => enrollment.ClassId == classId)
            .Include(enrollment => enrollment.Student)
            .Where(enrollment => enrollment.Student != null)
            .OrderBy(enrollment => enrollment.Student!.Name)
            .Select(enrollment => new StudentDTO
            {
                Id = enrollment.Student!.Id,
                Name = enrollment.Student.Name,
                Email = enrollment.Student.Email,
                Phone = enrollment.Student.Phone,
                CourseId = enrollment.Student.CourseId,
                CourseName = enrollment.Student.CourseName,
                EnrollmentDate = enrollment.Student.EnrollmentDate,
                Status = enrollment.Status
            })
            .ToListAsync();
    }

    public async Task<ServiceResult<StudentDTO>> UpdateEnrollmentStatusAsync(
        int courseId,
        int classId,
        int studentId,
        string status,
        int? userId = null)
    {
        var normalizedStatus = status.Trim().ToLowerInvariant();
        if (normalizedStatus is not ("active" or "completed" or "cancelled"))
        {
            return ServiceResult<StudentDTO>.Fail("Status de matricula invalido.");
        }

        var enrollment = await _context.Enrollments
            .Include(item => item.Student)
            .Include(item => item.Class)
            .FirstOrDefaultAsync(item =>
                item.ClassId == classId &&
                item.StudentId == studentId &&
                item.Class!.CourseId == courseId);

        if (enrollment?.Student == null || enrollment.Class == null)
        {
            return ServiceResult<StudentDTO>.Fail("Matricula nao encontrada.");
        }

        var previousStatus = enrollment.Status.Trim().ToLowerInvariant();
        if (previousStatus == normalizedStatus)
        {
            return ToStudentResponse(enrollment);
        }

        if (normalizedStatus == "active" &&
            (enrollment.Class.Status is "cancelled" or "completed" ||
             enrollment.Class.AvailableSeats <= 0))
        {
            return ServiceResult<StudentDTO>.Fail(
                "A matricula nao pode ser ativada nesta turma.");
        }

        if (previousStatus != "cancelled" && normalizedStatus == "cancelled")
        {
            enrollment.Class.AvailableSeats = Math.Min(
                enrollment.Class.Capacity,
                enrollment.Class.AvailableSeats + 1);
            enrollment.Class.VafasDisponiveis = enrollment.Class.AvailableSeats;
        }
        else if (previousStatus == "cancelled" && normalizedStatus != "cancelled")
        {
            enrollment.Class.AvailableSeats--;
            enrollment.Class.VafasDisponiveis = enrollment.Class.AvailableSeats;
        }

        enrollment.Status = normalizedStatus;
        _auditService.Add(userId, "status_changed", "Enrollment", enrollment.Id,
            previousStatus, normalizedStatus);
        await _context.SaveChangesAsync();
        return ToStudentResponse(enrollment);
    }

    public async Task<ServiceResult<TransferEnrollmentResponseDTO>> TransferEnrollmentAsync(
        int courseId,
        int sourceClassId,
        int studentId,
        int targetClassId,
        int? userId = null)
    {
        if (sourceClassId == targetClassId)
        {
            return ServiceResult<TransferEnrollmentResponseDTO>.Fail(
                "A turma de destino deve ser diferente da turma atual.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var enrollment = await _context.Enrollments
            .Include(item => item.Student)
            .Include(item => item.Class)
            .FirstOrDefaultAsync(item =>
                item.ClassId == sourceClassId &&
                item.StudentId == studentId &&
                item.Class!.CourseId == courseId);

        if (enrollment?.Student == null || enrollment.Class == null)
        {
            return ServiceResult<TransferEnrollmentResponseDTO>.Fail("Matricula nao encontrada.");
        }

        var normalizedStatus = enrollment.Status.Trim().ToLowerInvariant();
        if (normalizedStatus != "active")
        {
            return ServiceResult<TransferEnrollmentResponseDTO>.Fail(
                "Somente matriculas ativas podem ser transferidas.");
        }

        var targetClass = await _context.CourseClasses
            .FirstOrDefaultAsync(item =>
                item.Id == targetClassId &&
                item.CourseId == courseId);

        if (targetClass == null)
        {
            return ServiceResult<TransferEnrollmentResponseDTO>.Fail(
                "Turma de destino nao encontrada para este curso.");
        }

        if (targetClass.Status is "cancelled" or "completed" || targetClass.AvailableSeats <= 0)
        {
            return ServiceResult<TransferEnrollmentResponseDTO>.Fail(
                "A turma de destino nao possui inscricoes abertas ou vagas disponiveis.");
        }

        var sourceClass = enrollment.Class;
        var previous = new
        {
            enrollment.Id,
            enrollment.StudentId,
            SourceClassId = sourceClass.Id,
            sourceClass.AvailableSeats,
            TargetClassId = targetClass.Id,
            TargetAvailableSeats = targetClass.AvailableSeats
        };

        sourceClass.AvailableSeats = Math.Min(sourceClass.Capacity, sourceClass.AvailableSeats + 1);
        sourceClass.VafasDisponiveis = sourceClass.AvailableSeats;
        targetClass.AvailableSeats--;
        targetClass.VafasDisponiveis = targetClass.AvailableSeats;
        enrollment.ClassId = targetClass.Id;

        _auditService.Add(userId, "transferred", "Enrollment", enrollment.Id, previous, new
        {
            enrollment.Id,
            enrollment.StudentId,
            SourceClassId = sourceClass.Id,
            SourceAvailableSeats = sourceClass.AvailableSeats,
            TargetClassId = targetClass.Id,
            TargetAvailableSeats = targetClass.AvailableSeats
        });

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return ServiceResult<TransferEnrollmentResponseDTO>.Ok(new TransferEnrollmentResponseDTO
        {
            Student = ToStudentResponse(enrollment).Data!,
            SourceClass = ToClassResponse(sourceClass),
            TargetClass = ToClassResponse(targetClass)
        });
    }

    public async Task<List<CourseModuleResponseDTO>?> GetModulesAsync(int courseId)
    {
        var exists = await _context.Courses.AnyAsync(course => course.Id == courseId);
        if (!exists)
        {
            return null;
        }

        var modules = await _context.CourseModules
            .Where(module => module.CourseId == courseId)
            .Include(module => module.Lessons)
            .OrderBy(module => module.SortOrder)
            .ThenBy(module => module.Title)
            .AsNoTracking()
            .ToListAsync();

        return modules.Select(ToModuleResponse).ToList();
    }

    public async Task<ServiceResult<CourseModuleResponseDTO>> CreateModuleAsync(
        int courseId,
        CourseModuleRequestDTO request,
        int? userId = null)
    {
        var validation = Validate(request);
        if (validation != null)
        {
            return ServiceResult<CourseModuleResponseDTO>.Fail(validation);
        }

        var course = await _context.Courses
            .Include(item => item.Modules)
            .FirstOrDefaultAsync(item => item.Id == courseId);

        if (course == null)
        {
            return ServiceResult<CourseModuleResponseDTO>.Fail("Curso nao encontrado.");
        }

        if (course.DeliveryMode != "ead")
        {
            return ServiceResult<CourseModuleResponseDTO>.Fail(
                "Somente cursos EAD podem possuir modulos.");
        }

        var module = new CourseModule
        {
            CourseId = courseId,
            Title = request.Title.Trim(),
            SortOrder = request.SortOrder,
            IsActive = request.IsActive
        };

        _context.CourseModules.Add(module);
        _auditService.Add(userId, "created", "CourseModule", module.Id, null,
            new { module.CourseId, module.Title, module.SortOrder });
        await _context.SaveChangesAsync();

        return ServiceResult<CourseModuleResponseDTO>.Ok(ToModuleResponse(module));
    }

    public async Task<ServiceResult<CourseModuleResponseDTO>> UpdateModuleAsync(
        int courseId,
        int moduleId,
        CourseModuleRequestDTO request,
        int? userId = null)
    {
        var validation = Validate(request);
        if (validation != null)
        {
            return ServiceResult<CourseModuleResponseDTO>.Fail(validation);
        }

        var module = await _context.CourseModules
            .Include(item => item.Lessons)
            .FirstOrDefaultAsync(item => item.Id == moduleId && item.CourseId == courseId);

        if (module == null)
        {
            return ServiceResult<CourseModuleResponseDTO>.Fail("Modulo nao encontrado.");
        }

        var previous = new { module.Title, module.SortOrder, module.IsActive };
        module.Title = request.Title.Trim();
        module.SortOrder = request.SortOrder;
        module.IsActive = request.IsActive;

        _auditService.Add(userId, "updated", "CourseModule", module.Id, previous,
            new { module.Title, module.SortOrder, module.IsActive });
        await _context.SaveChangesAsync();

        return ServiceResult<CourseModuleResponseDTO>.Ok(ToModuleResponse(module));
    }

    public async Task<ServiceResult<CourseLessonResponseDTO>> CreateLessonAsync(
        int courseId,
        int moduleId,
        CourseLessonRequestDTO request,
        int? userId = null)
    {
        var validation = Validate(request);
        if (validation != null)
        {
            return ServiceResult<CourseLessonResponseDTO>.Fail(validation);
        }

        var module = await _context.CourseModules
            .FirstOrDefaultAsync(item => item.Id == moduleId && item.CourseId == courseId);

        if (module == null)
        {
            return ServiceResult<CourseLessonResponseDTO>.Fail("Modulo nao encontrado.");
        }

        var lesson = new CourseLesson();
        Apply(lesson, request);
        lesson.ModuleId = moduleId;

        _context.CourseLessons.Add(lesson);
        _auditService.Add(userId, "created", "CourseLesson", lesson.Id, null,
            new { lesson.ModuleId, lesson.Title, lesson.SortOrder });
        await _context.SaveChangesAsync();

        return ServiceResult<CourseLessonResponseDTO>.Ok(ToLessonResponse(lesson));
    }

    public async Task<ServiceResult<CourseLessonResponseDTO>> UpdateLessonAsync(
        int courseId,
        int moduleId,
        int lessonId,
        CourseLessonRequestDTO request,
        int? userId = null)
    {
        var validation = Validate(request);
        if (validation != null)
        {
            return ServiceResult<CourseLessonResponseDTO>.Fail(validation);
        }

        var lesson = await _context.CourseLessons
            .Include(item => item.Module)
            .FirstOrDefaultAsync(item =>
                item.Id == lessonId &&
                item.ModuleId == moduleId &&
                item.Module!.CourseId == courseId);

        if (lesson == null)
        {
            return ServiceResult<CourseLessonResponseDTO>.Fail("Aula nao encontrada.");
        }

        var previous = new
        {
            lesson.Title,
            lesson.Description,
            lesson.VideoUrl,
            lesson.DurationMinutes,
            lesson.SortOrder,
            lesson.IsActive
        };
        Apply(lesson, request);

        _auditService.Add(userId, "updated", "CourseLesson", lesson.Id, previous,
            new
            {
                lesson.Title,
                lesson.Description,
                lesson.VideoUrl,
                lesson.DurationMinutes,
                lesson.SortOrder,
                lesson.IsActive
            });
        await _context.SaveChangesAsync();

        return ServiceResult<CourseLessonResponseDTO>.Ok(ToLessonResponse(lesson));
    }

    public async Task<ServiceResult<CourseClassResponseDTO>> CreateClassAsync(int courseId, CourseClassRequestDTO request, int? userId = null)
    {
        var validation = Validate(request);
        if (validation != null)
        {
            return ServiceResult<CourseClassResponseDTO>.Fail(validation);
        }

        var course = await _context.Courses.FirstOrDefaultAsync(course => course.Id == courseId);
        if (course == null)
        {
            return ServiceResult<CourseClassResponseDTO>.Fail("Curso nao encontrado.");
        }

        if (course.DeliveryMode == "ead")
        {
            return ServiceResult<CourseClassResponseDTO>.Fail(
                "Cursos EAD nao possuem turmas presenciais.");
        }

        var courseClass = new CourseClass
        {
            CourseId = courseId,
            ProdutoId = course.LegacyProductId,
            DataRealizacao = request.StartDate,
            EndDate = request.EndDate,
            Local = request.Local.Trim(),
            Instructor = request.Instructor.Trim(),
            Capacity = request.Capacity,
            AvailableSeats = request.Capacity,
            VafasDisponiveis = request.Capacity,
            Status = request.Status.Trim().ToLowerInvariant()
        };

        _context.CourseClasses.Add(courseClass);
        _auditService.Add(userId, "created", "CourseClass", courseClass.Id, null,
            new { courseClass.CourseId, courseClass.DataRealizacao, courseClass.Capacity });
        await _context.SaveChangesAsync();
        return ServiceResult<CourseClassResponseDTO>.Ok(ToClassResponse(courseClass));
    }

    public async Task<ServiceResult<CourseClassResponseDTO>> UpdateClassAsync(
        int courseId,
        int classId,
        CourseClassRequestDTO request,
        int? userId = null)
    {
        var validation = Validate(request);
        if (validation != null)
        {
            return ServiceResult<CourseClassResponseDTO>.Fail(validation);
        }

        var courseClass = await _context.CourseClasses
            .FirstOrDefaultAsync(item => item.Id == classId && item.CourseId == courseId);

        if (courseClass == null)
        {
            return ServiceResult<CourseClassResponseDTO>.Fail("Turma nao encontrada.");
        }

        var reservedSeats = Math.Max(0, courseClass.Capacity - courseClass.AvailableSeats);
        if (request.Capacity < reservedSeats)
        {
            return ServiceResult<CourseClassResponseDTO>.Fail("A capacidade nao pode ser menor que as vagas ja reservadas.");
        }

        var previous = new
        {
            courseClass.DataRealizacao,
            courseClass.EndDate,
            courseClass.Local,
            courseClass.Instructor,
            courseClass.Capacity,
            courseClass.Status
        };
        courseClass.DataRealizacao = request.StartDate;
        courseClass.EndDate = request.EndDate;
        courseClass.Local = request.Local.Trim();
        courseClass.Instructor = request.Instructor.Trim();
        courseClass.Capacity = request.Capacity;
        courseClass.AvailableSeats = request.Capacity - reservedSeats;
        courseClass.VafasDisponiveis = courseClass.AvailableSeats;
        courseClass.Status = request.Status.Trim().ToLowerInvariant();
        _auditService.Add(userId, "updated", "CourseClass", courseClass.Id, previous,
            new
            {
                courseClass.DataRealizacao,
                courseClass.EndDate,
                courseClass.Local,
                courseClass.Instructor,
                courseClass.Capacity,
                courseClass.Status
            });

        await _context.SaveChangesAsync();
        return ServiceResult<CourseClassResponseDTO>.Ok(ToClassResponse(courseClass));
    }

    private static string? Validate(CourseRequestDTO request)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
            return "Nome e obrigatorio.";
        if (request.Preco < 0)
            return "Preco nao pode ser negativo.";
        if (NormalizeDeliveryMode(request.DeliveryMode) is not ("presencial" or "ead"))
            return "Tipo do curso deve ser 'presencial' ou 'ead'.";
        if (request.WorkloadHours < 0)
            return "Carga horaria nao pode ser negativa.";
        return null;
    }

    private static string? Validate(CourseClassRequestDTO request)
    {
        if (request.StartDate == default)
            return "Data de inicio e obrigatoria.";
        if (request.EndDate.HasValue && request.EndDate.Value < request.StartDate)
            return "Data final nao pode ser anterior a data inicial.";
        if (request.Capacity <= 0)
            return "Capacidade deve ser maior que zero.";
        if (string.IsNullOrWhiteSpace(request.Local))
            return "Local e obrigatorio.";
        if (string.IsNullOrWhiteSpace(request.Instructor))
            return "Instrutor e obrigatorio.";
        return null;
    }

    private static string? Validate(CourseModuleRequestDTO request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return "Titulo do modulo e obrigatorio.";
        if (request.SortOrder < 0)
            return "Ordem do modulo nao pode ser negativa.";
        return null;
    }

    private static string? Validate(CourseLessonRequestDTO request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return "Titulo da aula e obrigatorio.";
        if (request.DurationMinutes < 0)
            return "Duracao da aula nao pode ser negativa.";
        if (request.SortOrder < 0)
            return "Ordem da aula nao pode ser negativa.";
        return null;
    }

    private static void Apply(Course course, CourseRequestDTO request)
    {
        course.Nome = request.Nome.Trim();
        course.Description = request.Description.Trim();
        course.Preco = request.Preco;
        course.Image = request.Image.Trim();
        course.Category = request.Category.Trim();
        course.DeliveryMode = NormalizeDeliveryMode(request.DeliveryMode);
        course.WorkloadHours = request.WorkloadHours;
        course.IsActive = request.IsActive;
    }

    private static void Apply(CourseLesson lesson, CourseLessonRequestDTO request)
    {
        lesson.Title = request.Title.Trim();
        lesson.Description = request.Description.Trim();
        lesson.VideoUrl = request.VideoUrl.Trim();
        lesson.DurationMinutes = request.DurationMinutes;
        lesson.SortOrder = request.SortOrder;
        lesson.IsActive = request.IsActive;
    }

    private static void SyncLegacyProduct(Course course)
    {
        course.LegacyProduct ??= new Product
        {
            TipoProduto = "course",
            Estoque = 0
        };

        course.LegacyProduct.Nome = course.Nome;
        course.LegacyProduct.Preco = course.Preco;
        course.LegacyProduct.TipoProduto = "course";
        course.LegacyProduct.Description = course.Description;
        course.LegacyProduct.Image = course.Image;
        course.LegacyProduct.Category = course.Category;
        course.LegacyProduct.Estoque ??= 0;
    }

    private static CourseResponseDTO ToResponse(Course course)
    {
        return new CourseResponseDTO
        {
            Id = course.Id,
            Nome = course.Nome,
            Description = course.Description,
            Preco = course.Preco,
            Image = course.Image,
            Category = course.Category,
            DeliveryMode = course.DeliveryMode,
            WorkloadHours = course.WorkloadHours,
            IsActive = course.IsActive,
            LegacyProductId = course.LegacyProductId,
            Classes = course.Classes
                .Where(item => item.Status != "cancelled")
                .OrderBy(item => item.DataRealizacao)
                .Select(ToClassResponse)
                .ToList(),
            Modules = course.Modules
                .Where(item => item.IsActive)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Title)
                .Select(ToModuleResponse)
                .ToList()
        };
    }

    private static CourseClassResponseDTO ToClassResponse(CourseClass courseClass)
    {
        return new CourseClassResponseDTO
        {
            Id = courseClass.Id,
            CourseId = courseClass.CourseId ?? 0,
            StartDate = courseClass.DataRealizacao,
            EndDate = courseClass.EndDate,
            Local = courseClass.Local,
            Instructor = courseClass.Instructor,
            Capacity = courseClass.Capacity,
            AvailableSeats = courseClass.AvailableSeats,
            Status = courseClass.Status
        };
    }

    private static ServiceResult<StudentDTO> ToStudentResponse(Enrollment enrollment)
    {
        return ServiceResult<StudentDTO>.Ok(new StudentDTO
        {
            Id = enrollment.Student!.Id,
            Name = enrollment.Student.Name,
            Email = enrollment.Student.Email,
            Phone = enrollment.Student.Phone,
            CourseId = enrollment.Student.CourseId,
            CourseName = enrollment.Student.CourseName,
            EnrollmentDate = enrollment.Student.EnrollmentDate,
            Status = enrollment.Status
        });
    }

    private static CourseModuleResponseDTO ToModuleResponse(CourseModule module)
    {
        return new CourseModuleResponseDTO
        {
            Id = module.Id,
            CourseId = module.CourseId,
            Title = module.Title,
            SortOrder = module.SortOrder,
            IsActive = module.IsActive,
            Lessons = module.Lessons
                .Where(item => item.IsActive)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Title)
                .Select(ToLessonResponse)
                .ToList()
        };
    }

    private static CourseLessonResponseDTO ToLessonResponse(CourseLesson lesson)
    {
        return new CourseLessonResponseDTO
        {
            Id = lesson.Id,
            ModuleId = lesson.ModuleId,
            Title = lesson.Title,
            Description = lesson.Description,
            VideoUrl = lesson.VideoUrl,
            DurationMinutes = lesson.DurationMinutes,
            SortOrder = lesson.SortOrder,
            IsActive = lesson.IsActive
        };
    }

    private static string NormalizeDeliveryMode(string value)
        => string.IsNullOrWhiteSpace(value)
            ? "presencial"
            : value.Trim().ToLowerInvariant();
}
