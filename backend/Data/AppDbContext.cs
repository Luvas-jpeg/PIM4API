using Microsoft.EntityFrameworkCore;
using EquipamentosMedicosApi.Models;

namespace EquipamentosMedicosApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<CourseClass> CourseClasses { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<PromoCode> PromoCodes { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<CourseProgress> CourseProgresses { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Order>()
                .Property(order => order.Status)
                .HasDefaultValue("pending");

            modelBuilder.Entity<Order>()
                .Property(order => order.PaymentStatus)
                .HasDefaultValue("pending");

            modelBuilder.Entity<RefreshToken>()
                .HasIndex(refreshToken => refreshToken.Token)
                .IsUnique();

            modelBuilder.Entity<CourseProgress>()
                .HasIndex(progress => new { progress.UserId, progress.CourseId })
                .IsUnique();

            // Seed de dados
            modelBuilder.Entity<Product>().HasData(
                new Product { Id = 1, Nome = "Estetoscópio Profissional", Preco = 289.90m, TipoProduto = "equipment", Estoque = 15, Description = "Estetoscópio de alta precisão com membrana dupla, ideal para ausculta cardíaca e pulmonar.", Image = "https://images.unsplash.com/photo-1655313719612-8248b2c4d1e7?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxtZWRpY2FsJTIwZXF1aXBtZW50JTIwc3RldGhvc2NvcGV8ZW58MXx8fHwxNzczNTE3MTExfDA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral", Category = "Diagnóstico" },
                new Product { Id = 2, Nome = "Monitor de Sinais Vitais", Preco = 3599.00m, TipoProduto = "equipment", Estoque = 8, Description = "Monitor multiparamétrico com tela colorida para monitoramento de pressão arterial, frequência cardíaca e oximetria.", Image = "https://images.unsplash.com/photo-1770836037704-44bd8c7b6978?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxob3NwaXRhbCUyMGVxdWlwbWVudCUyMG1vbml0b3J8ZW58MXx8fHwxNzczNTE3MTEyfDA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral", Category = "Monitoramento" },
                new Product { Id = 3, Nome = "Luvas Cirúrgicas Estéreis - Caixa com 50 pares", Preco = 159.90m, TipoProduto = "equipment", Estoque = 50, Description = "Luvas cirúrgicas em látex, estéreis e de alta qualidade. Tamanhos variados disponíveis.", Image = "https://images.unsplash.com/photo-1758206523660-3ef5a51f1113?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxtZWRpY2FsJTIwZ2xvdmVzJTIwc3VyZ2ljYWx8ZW58MXx8fHwxNzczNTE3MTEyfDA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral", Category = "EPI" },
                new Product { Id = 4, Nome = "Desfibrilador Automático Externo (DEA)", Preco = 8999.00m, TipoProduto = "equipment", Estoque = 5, Description = "DEA portátil com instruções de voz e análise automática do ritmo cardíaco. Ideal para ambientes públicos.", Image = "https://images.unsplash.com/photo-1762161916712-09592fa05b20?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxkZWZpYnJpbGxhdG9yJTIwZW1lcmdlbmN5JTIwbWVkaWNhbHxlbnwxfHx8fDE3NzM1MTcxMTN8MA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral", Category = "Emergência" },
                new Product { Id = 5, Nome = "Curso de Primeiros Socorros Básico", Preco = 450.00m, TipoProduto = "course", Estoque = 20, Description = "Aprenda técnicas essenciais de primeiros socorros, RCP e uso de DEA. Certificado reconhecido nacionalmente.", Image = "https://images.unsplash.com/photo-1622115585848-1d5b6e8af4e4?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxmaXJzdCUyMGFpZCUyMHRyYWluaW5nfGVufDF8fHx8MTc3MzUxNzExM3ww&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral", Category = "Primeiros Socorros", Date = "15/04/2026 - 16/04/2026", Location = "São Paulo - SP", Instructor = "Dr. Carlos Silva" },
                new Product { Id = 6, Nome = "Curso de Suporte Avançado de Vida (ACLS)", Preco = 1200.00m, TipoProduto = "course", Estoque = 15, Description = "Treinamento avançado para profissionais de saúde em emergências cardiovasculares. Carga horária de 16h.", Image = "https://images.unsplash.com/photo-1659353887019-b142198f2668?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxtZWRpY2FsJTIwdHJhaW5pbmclMjBjb3Vyc2V8ZW58MXx8fHwxNzczNTE3MTEyfDA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral", Category = "Treinamento Avançado", Date = "22/04/2026 - 23/04/2026", Location = "Rio de Janeiro - RJ", Instructor = "Dra. Ana Paula Costa" },
                new Product { Id = 7, Nome = "Workshop de Técnicas de Sutura", Preco = 890.00m, TipoProduto = "course", Estoque = 12, Description = "Práticas intensivas de diferentes técnicas de sutura em modelos sintéticos. Aula prática com supervisão.", Image = "https://images.unsplash.com/photo-1659353887019-b142198f2668?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxtZWRpY2FsJTIwdHJhaW5pbmclMjBjb3Vyc2V8ZW58MXx8fHwxNzczNTE3MTEyfDA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral", Category = "Workshop", Date = "10/05/2026", Location = "Belo Horizonte - MG", Instructor = "Dr. Roberto Mendes" },
                new Product { Id = 8, Nome = "Curso de Biossegurança Hospitalar", Preco = 350.00m, TipoProduto = "course", Estoque = 25, Description = "Normas e práticas de biossegurança em ambientes hospitalares. Essencial para toda equipe de saúde.", Image = "https://images.unsplash.com/photo-1622115585848-1d5b6e8af4e4?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxmaXJzdCUyMGFpZCUyMHRyYWluaW5nfGVufDF8fHx8MTc3MzUxNzExM3ww&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral", Category = "Biossegurança", Date = "18/04/2026", Location = "Curitiba - PR", Instructor = "Enf. Marina Oliveira" }
            );

            modelBuilder.Entity<Student>().HasData(
                new Student { Id = 1, Name = "João Silva", Email = "joao.silva@email.com", Phone = "(11) 98765-4321", CourseId = "5", CourseName = "Curso de Primeiros Socorros Básico", EnrollmentDate = "10/04/2026", Status = "active" },
                new Student { Id = 2, Name = "Maria Santos", Email = "maria.santos@email.com", Phone = "(21) 99876-5432", CourseId = "6", CourseName = "Curso de Suporte Avançado de Vida (ACLS)", EnrollmentDate = "05/04/2026", Status = "active" },
                new Student { Id = 3, Name = "Pedro Oliveira", Email = "pedro.oliveira@email.com", Phone = "(31) 97654-3210", CourseId = "7", CourseName = "Workshop de Técnicas de Sutura", EnrollmentDate = "28/03/2026", Status = "completed" }
            );

            modelBuilder.Entity<PromoCode>().HasData(
                new PromoCode { Id = 1, Code = "MEDICO10", Discount = 10m, DiscountType = "percentage", StartDate = "2026-04-01", EndDate = "2026-12-31", IsActive = true, UsageLimit = 100, UsageCount = 5 },
                new PromoCode { Id = 2, Code = "PRIMEIRACOMPRA", Discount = 50m, DiscountType = "fixed", StartDate = "2026-01-01", EndDate = "2026-12-31", IsActive = true, UsageCount = 12 }
            );

            // Optional: set up JSON mapping for CourseProgress.CompletedLessons
            modelBuilder.Entity<CourseProgress>().Property(p => p.CompletedLessons)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize<List<int>>(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<int>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<int>()
                );
        }
    }
}
