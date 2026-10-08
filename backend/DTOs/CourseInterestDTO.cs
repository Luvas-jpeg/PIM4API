namespace EquipamentosMedicosApi.DTOs;

public class CourseInterestRequestDTO
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
}

public class CourseInterestResponseDTO
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
