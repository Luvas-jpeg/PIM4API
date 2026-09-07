using Microsoft.EntityFrameworkCore;
using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.DTOs;
using EquipamentosMedicosApi.Models;

namespace EquipamentosMedicosApi.Services;

public class CourseService
{
    private readonly AppDbContext _context;

    public CourseService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<CourseResponseDTO>> GetAllAsync(bool includeInactive = false)
    {
        var query = _context.Courses
            .Include(course => course.Classes)
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
            .Where(course => course.Classes.Any(courseClass => classes.Any(item => item.Id == courseClass.Id)))
            .Include(course => course.Classes)
            .AsNoTracking();

        var sort = request.Sort.Trim().ToLowerInvariant();
        query = sort switch
        {
            "price-asc" => query.OrderBy(course => course.Preco),
            "price-desc" => query.OrderByDescending(course => course.Preco),
            "name" => query.OrderBy(course => course.Nome),
            _ => query.OrderBy(course => course.Classes
                .Where(courseClass => classes.Any(item => item.Id == courseClass.Id))
                .Min(courseClass => courseClass.DataRealizacao))
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

    public async Task<CourseResponseDTO?> GetByIdAsync(int id, bool includeInactive = false)
    {
        var course = await _context.Courses
            .Include(item => item.Classes)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && (includeInactive || item.IsActive));

        return course == null ? null : ToResponse(course);
    }

    public async Task<ServiceResult<CourseResponseDTO>> CreateAsync(CourseRequestDTO request)
    {
        var validation = Validate(request);
        if (validation != null)
        {
            return ServiceResult<CourseResponseDTO>.Fail(validation);
        }

        var course = new Course();
        Apply(course, request);
        _context.Courses.Add(course);
        await _context.SaveChangesAsync();
        return ServiceResult<CourseResponseDTO>.Ok(ToResponse(course));
    }

    public async Task<ServiceResult<CourseResponseDTO>> UpdateAsync(int id, CourseRequestDTO request)
    {
        var validation = Validate(request);
        if (validation != null)
        {
            return ServiceResult<CourseResponseDTO>.Fail(validation);
        }

        var course = await _context.Courses
            .Include(item => item.Classes)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (course == null)
        {
            return ServiceResult<CourseResponseDTO>.Fail("Curso nao encontrado.");
        }

        Apply(course, request);
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

    public async Task<ServiceResult<CourseClassResponseDTO>> CreateClassAsync(int courseId, CourseClassRequestDTO request)
    {
        var validation = Validate(request);
        if (validation != null)
        {
            return ServiceResult<CourseClassResponseDTO>.Fail(validation);
        }

        if (!await _context.Courses.AnyAsync(course => course.Id == courseId))
        {
            return ServiceResult<CourseClassResponseDTO>.Fail("Curso nao encontrado.");
        }

        var courseClass = new CourseClass
        {
            CourseId = courseId,
            ProdutoId = await _context.Courses
                .Where(course => course.Id == courseId)
                .Select(course => course.LegacyProductId)
                .FirstOrDefaultAsync(),
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
        await _context.SaveChangesAsync();
        return ServiceResult<CourseClassResponseDTO>.Ok(ToClassResponse(courseClass));
    }

    public async Task<ServiceResult<CourseClassResponseDTO>> UpdateClassAsync(
        int courseId,
        int classId,
        CourseClassRequestDTO request)
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

        courseClass.DataRealizacao = request.StartDate;
        courseClass.EndDate = request.EndDate;
        courseClass.Local = request.Local.Trim();
        courseClass.Instructor = request.Instructor.Trim();
        courseClass.Capacity = request.Capacity;
        courseClass.AvailableSeats = request.Capacity - reservedSeats;
        courseClass.VafasDisponiveis = courseClass.AvailableSeats;
        courseClass.Status = request.Status.Trim().ToLowerInvariant();

        await _context.SaveChangesAsync();
        return ServiceResult<CourseClassResponseDTO>.Ok(ToClassResponse(courseClass));
    }

    private static string? Validate(CourseRequestDTO request)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
            return "Nome e obrigatorio.";
        if (request.Preco < 0)
            return "Preco nao pode ser negativo.";
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

    private static void Apply(Course course, CourseRequestDTO request)
    {
        course.Nome = request.Nome.Trim();
        course.Description = request.Description.Trim();
        course.Preco = request.Preco;
        course.Image = request.Image.Trim();
        course.Category = request.Category.Trim();
        course.IsActive = request.IsActive;
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
            IsActive = course.IsActive,
            LegacyProductId = course.LegacyProductId,
            Classes = course.Classes
                .Where(item => item.Status != "cancelled")
                .OrderBy(item => item.DataRealizacao)
                .Select(ToClassResponse)
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
}
