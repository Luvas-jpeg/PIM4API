namespace EquipamentosMedicosApi.Models
{
    public class User
    {
        public int ID {get; set;}
        public string Nome {get; set;} = string.Empty;
        public string Email {get; set;} = string.Empty;
        public string Cpf {get; set;} = string.Empty;
        public string Phone {get; set;} = string.Empty;
        public string Street {get; set;} = string.Empty;
        public string Number {get; set;} = string.Empty;
        public string Complement {get; set;} = string.Empty;
        public string Neighborhood {get; set;} = string.Empty;
        public string City {get; set;} = string.Empty;
        public string State {get; set;} = string.Empty;
        public string ZipCode {get; set;} = string.Empty;
        public string SenhaHash {get; set;} = string.Empty;
        public string Role {get; set;} = "Cliente";
        public int FailedLoginAttempts { get; set; }
        public DateTime? LockoutEnd { get; set; }

        public ICollection<Order> Pedidos {get; set;} = new List<Order>();
    }
}
