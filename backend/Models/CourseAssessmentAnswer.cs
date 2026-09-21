namespace EquipamentosMedicosApi.Models;

public class CourseAssessmentAnswer
{
    public int Id { get; set; }
    public int AttemptId { get; set; }
    public int QuestionId { get; set; }
    public int SelectedOptionId { get; set; }
    public bool IsCorrect { get; set; }

    public CourseAssessmentAttempt? Attempt { get; set; }
    public CourseQuestion? Question { get; set; }
    public CourseQuestionOption? SelectedOption { get; set; }
}
