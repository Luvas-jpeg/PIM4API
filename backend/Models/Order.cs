
namespace EquipamentosMedicosApi.Models
{
    public class Order
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public DateTime DataPedido { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "pending";
        public string PaymentStatus { get; set; } = "pending"; // pending, paid, refunded, cancelled
        public decimal Total { get; set; }
        public decimal ValorFrete { get; set; }
        public string PaymentMethod { get; set; } = "credit_card";
        public int? Installments { get; set; }
        public string PromoCode { get; set; } = string.Empty;
        public string? GatewayPaymentId { get; set; }
        public DateTime? PaidAt { get; set; }
        public string? IdempotencyKey { get; set; }

        // Relacionamentos
        public User? Usuario {get; set;}
        public ICollection<OrderItem> Itens {get; set;} = new List<OrderItem>();
        public ICollection<Enrollment> Enrollments {get; set;} = new List<Enrollment>();
    }
}
