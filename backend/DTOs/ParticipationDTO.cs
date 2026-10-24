namespace EquipamentosMedicosApi.DTOs;

public sealed class ParticipationDTO
{
    public string Status { get; set; } = "not_applicable";
    public DateTime? OpensAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public bool CanConfirm { get; set; }
}
