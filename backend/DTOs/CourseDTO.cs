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
    public List<CourseAssessmentResponseDTO> Assessments { get; set; } = new();
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

public class CourseAssessmentResponseDTO
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int MinimumScore { get; set; }
    public int MaxAttempts { get; set; }
    public bool IsActive { get; set; }
    public List<CourseQuestionResponseDTO> Questions { get; set; } = new();
}

public class CourseAssessmentRequestDTO
{
    public string Title { get; set; } = string.Empty;
    public int MinimumScore { get; set; } = 70;
    public int MaxAttempts { get; set; } = 3;
    public bool IsActive { get; set; } = true;
}

public class CourseQuestionResponseDTO
{
    public int Id { get; set; }
    public int AssessmentId { get; set; }
    public string Statement { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public List<CourseQuestionOptionResponseDTO> Options { get; set; } = new();
}

public class CourseQuestionRequestDTO
{
    public string Statement { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public List<CourseQuestionOptionRequestDTO> Options { get; set; } = new();
}

public class CourseQuestionOptionResponseDTO
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int SortOrder { get; set; }
}

public class CourseQuestionOptionRequestDTO
{
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int SortOrder { get; set; }
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
    public string? DeliveryMode { get; set; }
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
