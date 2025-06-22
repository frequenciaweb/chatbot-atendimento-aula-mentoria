using System.ComponentModel.DataAnnotations;

namespace src_api.Models
{
    public class Cliente
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string Nome { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(20)]
        public string Telefone { get; set; } = string.Empty;
        
        public DateTime DataCriacao { get; set; } = DateTime.Now;
        
        public bool AguardandoConfirmacaoExclusao { get; set; } = false;
        
        // Navigation property
        public virtual ICollection<ClienteChat> ClienteChats { get; set; } = new List<ClienteChat>();
    }
} 