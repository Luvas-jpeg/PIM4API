namespace EquipamentosMedicosApi.DTOs
{
    public class RefreshTokenRequestDTO
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class PaymentWebhookDTO
    {
        public string EventId { get; set; } = string.Empty;
        public string PaymentId { get; set; } = string.Empty;
        public int? OrderId { get; set; }
        public string Status { get; set; } = string.Empty;
        public object? RawPayload { get; set; }
    }

    public class PaymentWebhookEventResponseDTO
    {
        public int Id { get; set; }
        public string EventId { get; set; } = string.Empty;
        public int OrderId { get; set; }
        public string PaymentId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime ReceivedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }
}
