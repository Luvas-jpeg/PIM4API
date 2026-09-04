namespace EquipamentosMedicosApi.DTOs;

public class CreateOrderDTO
{
    public List<CreateOrderItemDTO> Itens { get; set; } = new();

    public decimal ValorFrete { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;

    public int? Installments { get; set; }

    public string PromoCode { get; set; } = string.Empty;
}

public class CreateOrderItemDTO
{
    public int ProdutoId { get; set; }
    public int? TurmaId { get; set; }

    public int Quantidade { get; set; }
}

public class UpdateOrderStatusDTO
{
    public string Status { get; set; } = string.Empty;
}
