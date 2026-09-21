namespace EquipamentosMedicosApi.DTOs;

public class MyEnrollmentResponse
{
    public int EnrollmentId { get; set; }
    public int? CourseId { get; set; }
    public int? ClassId { get; set; }
    public int OrderId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string CourseDescription { get; set; } = string.Empty;
    public string CourseImage { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DeliveryMode { get; set; } = "presencial";
    public int WorkloadHours { get; set; }
    public string Instructor { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string ClassStatus { get; set; } = string.Empty;
    public string EnrollmentStatus { get; set; } = string.Empty;
    public DateTime EnrolledAt { get; set; }
    public List<CourseModuleResponseDTO> Modules { get; set; } = new();
    public List<MyCourseAssessmentDTO> Assessments { get; set; } = new();
    public List<MyAssessmentAttemptDTO> AssessmentAttempts { get; set; } = new();
    public MyCertificateDTO? Certificate { get; set; }
}

public class MyCourseAssessmentDTO
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int MinimumScore { get; set; }
    public int MaxAttempts { get; set; }
    public List<MyCourseQuestionDTO> Questions { get; set; } = new();
}

public class MyCourseQuestionDTO
{
    public int Id { get; set; }
    public int AssessmentId { get; set; }
    public string Statement { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<MyCourseQuestionOptionDTO> Options { get; set; } = new();
}

public class MyCourseQuestionOptionDTO
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public string Text { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class MyAssessmentAttemptDTO
{
    public int Id { get; set; }
    public int AssessmentId { get; set; }
    public int AttemptNumber { get; set; }
    public int Score { get; set; }
    public bool Passed { get; set; }
    public DateTime SubmittedAt { get; set; }
}

public class SubmitAssessmentRequestDTO
{
    public List<SubmitAssessmentAnswerDTO> Answers { get; set; } = new();
}

public class SubmitAssessmentAnswerDTO
{
    public int QuestionId { get; set; }
    public int SelectedOptionId { get; set; }
}

public class SubmitAssessmentResponseDTO
{
    public int AssessmentId { get; set; }
    public int AttemptNumber { get; set; }
    public int Score { get; set; }
    public int MinimumScore { get; set; }
    public bool Passed { get; set; }
    public int RemainingAttempts { get; set; }
}

public class MyCertificateDTO
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public int WorkloadHours { get; set; }
    public string ValidationCode { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
}

public class CertificateStatusDTO
{
    public bool Eligible { get; set; }
    public string Reason { get; set; } = string.Empty;
    public MyCertificateDTO? Certificate { get; set; }
}
