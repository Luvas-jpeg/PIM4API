namespace EquipamentosMedicosApi.Models;

public class CourseLesson
{
    public int Id { get; set; }
    public int ModuleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public CourseModule? Module { get; set; }
}
