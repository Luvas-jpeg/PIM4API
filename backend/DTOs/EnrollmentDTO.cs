namespace EquipamentosMedicosApi.DTOs;

public class MyEnrollmentResponse
{
    public int EnrollmentId { get; set; }
    public int? CourseId { get; set; }
    public int ClassId { get; set; }
    public int OrderId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string CourseDescription { get; set; } = string.Empty;
    public string CourseImage { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Instructor { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string ClassStatus { get; set; } = string.Empty;
    public string EnrollmentStatus { get; set; } = string.Empty;
    public DateTime EnrolledAt { get; set; }
}
