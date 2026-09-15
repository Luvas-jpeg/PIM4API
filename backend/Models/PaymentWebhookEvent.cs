namespace EquipamentosMedicosApi.Models;

public class PaymentWebhookEvent
{
    public int Id { get; set; }
    public string EventId { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public string PaymentId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }

    public Order? Order { get; set; }
}
