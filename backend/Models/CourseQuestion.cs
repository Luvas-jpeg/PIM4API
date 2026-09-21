namespace EquipamentosMedicosApi.Models;

public class CourseQuestion
{
    public int Id { get; set; }
    public int AssessmentId { get; set; }
    public string Statement { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public CourseAssessment? Assessment { get; set; }
    public ICollection<CourseQuestionOption> Options { get; set; } = new List<CourseQuestionOption>();
}
