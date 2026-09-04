namespace EquipamentosMedicosApi.Models;

public class CourseClass
{
    public int Id { get; set; }
    public int? CourseId { get; set; }
    public int? ProdutoId { get; set; }
    public DateTime DataRealizacao { get; set; }
    public DateTime? EndDate { get; set; }
    public string Local { get; set; } = string.Empty;
    public string Instructor { get; set; } = string.Empty;
    public int VafasDisponiveis { get; set; }
    public int Capacity { get; set; }
    public int AvailableSeats { get; set; }
    public string Status { get; set; } = "scheduled";

    public Course? Course { get; set; }
    public Product? Produto { get; set; }
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int VagasDisponiveis
    {
        get => AvailableSeats;
        set => AvailableSeats = value;
    }
}
