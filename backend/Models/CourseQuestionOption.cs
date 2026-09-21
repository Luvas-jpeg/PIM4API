namespace EquipamentosMedicosApi.Models;

public class CourseQuestionOption
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int SortOrder { get; set; }

    public CourseQuestion? Question { get; set; }
}
