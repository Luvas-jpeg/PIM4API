namespace EquipamentosMedicosApi.Services;

public class CreateOrderResponse
{
    public string Message {get; set;} = string.Empty;
    public int OrderId {get; set;}
    public decimal Total {get; set;}
}

public class OrderResponse
{
    public int Id {get; set;}
    public DateTime DataPedido {get; set;}
    public string Status {get; set;} = string.Empty;
    public decimal Total {get; set;}
    public decimal ValorFrete {get; set;}
    public string PaymentMethod {get; set;} = string.Empty;
    public int? Installments {get; set;}
    public string PromoCode {get; set;} = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string? GatewayPaymentId { get; set; }
    public DateTime? PaidAt { get; set; }
    public OrderUserResponse? Usuario {get; set;}
    public List<OrderItemResponse> Itens {get; set;} = new();

    

   
}
public class OrderUserResponse
{
        public int Id {get; set;}
        public string Nome {get; set;} = string.Empty;
        public string Email {get; set;} = string.Empty;
}
public class OrderItemResponse
{
    public int ProdutoId { get; set; }
    public int? TurmaId { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string TipoProduto { get; set; } = string.Empty;

    public int Quantidade { get; set; }

    public decimal PrecoUnitario { get; set; }

    public string? Status { get; set; }
}