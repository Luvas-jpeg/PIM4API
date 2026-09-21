namespace EquipamentosMedicosApi.Models;

public class CourseAssessmentAttempt
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CourseId { get; set; }
    public int AssessmentId { get; set; }
    public int AttemptNumber { get; set; }
    public int Score { get; set; }
    public bool Passed { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public Course? Course { get; set; }
    public CourseAssessment? Assessment { get; set; }
    public ICollection<CourseAssessmentAnswer> Answers { get; set; } = new List<CourseAssessmentAnswer>();
}
