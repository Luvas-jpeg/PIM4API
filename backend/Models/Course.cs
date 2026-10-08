namespace EquipamentosMedicosApi.Models;

/// <summary>
/// Course catalog entity shared by all clients.
/// </summary>
public class Course
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public string Image { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DeliveryMode { get; set; } = "presencial";
    public int WorkloadHours { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<CourseClass> Classes { get; set; } = new List<CourseClass>();
    public ICollection<CourseModule> Modules { get; set; } = new List<CourseModule>();
    public ICollection<CourseAssessment> Assessments { get; set; } = new List<CourseAssessment>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}
