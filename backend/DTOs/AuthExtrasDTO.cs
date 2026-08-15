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
}