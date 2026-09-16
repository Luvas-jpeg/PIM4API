namespace EquipamentosMedicosApi.DTOs;

public class CourseResponseDTO
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public string Image { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DeliveryMode { get; set; } = "presencial";
    public int WorkloadHours { get; set; }
    public bool IsActive { get; set; }
    public int? LegacyProductId { get; set; }
    public List<CourseClassResponseDTO> Classes { get; set; } = new();
    public List<CourseModuleResponseDTO> Modules { get; set; } = new();
}

public class CourseRequestDTO
{
    public string Nome { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public string Image { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DeliveryMode { get; set; } = "presencial";
    public int WorkloadHours { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CourseClassResponseDTO
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Local { get; set; } = string.Empty;
    public string Instructor { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int AvailableSeats { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class CourseClassRequestDTO
{
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Local { get; set; } = string.Empty;
    public string Instructor { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Status { get; set; } = "scheduled";
}

public class EnrollmentStatusRequestDTO
{
    public string Status { get; set; } = "active";
}

public class CourseModuleResponseDTO
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public List<CourseLessonResponseDTO> Lessons { get; set; } = new();
}

public class CourseModuleRequestDTO
{
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CourseLessonResponseDTO
{
    public int Id { get; set; }
    public int ModuleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public class CourseLessonRequestDTO
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class TransferEnrollmentRequestDTO
{
    public int TargetClassId { get; set; }
}

public class TransferEnrollmentResponseDTO
{
    public StudentDTO Student { get; set; } = new();
    public CourseClassResponseDTO SourceClass { get; set; } = new();
    public CourseClassResponseDTO TargetClass { get; set; } = new();
}

public class CourseCatalogQueryDTO
{
    public string? Search { get; set; }
    public string? Category { get; set; }
    public string? City { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool AvailableOnly { get; set; } = true;
    public string Sort { get; set; } = "date";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 9;
}

public class PagedCourseResponseDTO
{
    public List<CourseResponseDTO> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}

public class CourseCatalogOptionsDTO
{
    public List<string> Categories { get; set; } = new();
    public List<string> Cities { get; set; } = new();
}
