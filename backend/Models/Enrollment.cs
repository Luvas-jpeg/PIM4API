namespace EquipamentosMedicosApi.Models;

public class Enrollment
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public int StudentId { get; set; }
    public int OrderId { get; set; }
    public string Status { get; set; } = "active";
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;

    public CourseClass? Class { get; set; }
    public Student? Student { get; set; }
    public Order? Order { get; set; }
}
