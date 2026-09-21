namespace EquipamentosMedicosApi.Models;

public class CourseAssessment
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int MinimumScore { get; set; } = 70;
    public int MaxAttempts { get; set; } = 3;
    public bool IsActive { get; set; } = true;

    public Course? Course { get; set; }
    public ICollection<CourseQuestion> Questions { get; set; } = new List<CourseQuestion>();
}
