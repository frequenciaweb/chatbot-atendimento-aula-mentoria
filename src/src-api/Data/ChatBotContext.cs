using Microsoft.EntityFrameworkCore;
using src_api.Models;

namespace src_api.Data
{
    public class ChatBotContext : DbContext
    {
        public ChatBotContext(DbContextOptions<ChatBotContext> options) : base(options)
        {
        }

        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<ClienteChat> ClienteChats { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configuração da tabela Cliente
            modelBuilder.Entity<Cliente>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Telefone).IsUnique();
                entity.Property(e => e.Nome).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Telefone).IsRequired().HasMaxLength(20);
                entity.Property(e => e.DataCriacao).HasDefaultValueSql("datetime('now')");
            });

            // Configuração da tabela ClienteChat
            modelBuilder.Entity<ClienteChat>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Texto).IsRequired();
                entity.Property(e => e.Origem).IsRequired().HasMaxLength(10);
                entity.Property(e => e.DataCriacao).HasDefaultValueSql("datetime('now')");
                
                // Relacionamento
                entity.HasOne(d => d.Cliente)
                    .WithMany(p => p.ClienteChats)
                    .HasForeignKey(d => d.ClienteId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
} 