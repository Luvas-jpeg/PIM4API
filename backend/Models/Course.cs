namespace EquipamentosMedicosApi.Models;

/// <summary>
/// Course catalog entity. Product remains as a legacy catalog item during migration.
/// </summary>
public class Course
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public string Image { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int? LegacyProductId { get; set; }

    public Product? LegacyProduct { get; set; }
    public ICollection<CourseClass> Classes { get; set; } = new List<CourseClass>();
}
