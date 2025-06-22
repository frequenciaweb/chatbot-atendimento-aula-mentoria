using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace src_api.Models
{
    public class ClienteChat
    {
        public int Id { get; set; }
        
        [Required]
        public int ClienteId { get; set; }
        
        [Required]
        public string Texto { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(10)]
        public string Origem { get; set; } = string.Empty; // "cliente" ou "bot"
        
        public DateTime DataCriacao { get; set; } = DateTime.Now;
        
        // Navigation property
        [ForeignKey("ClienteId")]
        public virtual Cliente Cliente { get; set; } = null!;
    }
} 