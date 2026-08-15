namespace EquipamentosMedicosApi.DTOs
{
    public class UpdateProgressRequest
    {
        public double Percent { get; set; }
        public int? CompletedLessonId { get; set; }
    }
}
