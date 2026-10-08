namespace EquipamentosMedicosApi.Models
{
    public class OrderItem
    {
        public int Id { get; set; }
        public int PedidoId { get; set; }
        public int? CourseId { get; set; }
        public string NameSnapshot { get; set; } = string.Empty;
        public string ItemType { get; set; } = "course";
        public int? TurmaId { get; set; }
        public int Quantidade { get; set; }
        public decimal PrecoUnitario { get; set; }

        //Relacionamentos
        public Order? Pedido { get; set; }
        public Course? Course { get; set; }
        public CourseClass? Turma { get; set; }
    }
}
