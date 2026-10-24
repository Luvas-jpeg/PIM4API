namespace EquipamentosMedicosApi.DTOs
{
    public class StudentDTO
    {
        public ParticipationDTO? Participation { get; set; }
        public int? EnrollmentId { get; set; }
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string CourseId { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string EnrollmentDate { get; set; } = string.Empty;
        public string Status { get; set; } = "active";
    }

    public class StudentRequestDTO
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string CourseId { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string EnrollmentDate { get; set; } = string.Empty;
        public string Status { get; set; } = "active";
    }
}
