namespace EquipamentosMedicosApi.Models;

public class CourseModule
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public Course? Course { get; set; }
    public ICollection<CourseLesson> Lessons { get; set; } = new List<CourseLesson>();
}
