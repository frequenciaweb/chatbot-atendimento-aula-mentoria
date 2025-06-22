using System.ComponentModel.DataAnnotations;

namespace src_api.DTOs
{
    public class MensagemDto
    {
        [Required]
        public string Texto { get; set; } = string.Empty;
        
        public string? Telefone { get; set; }
        
        public string? Nome { get; set; }
        
        [Required]
        public string ModeloIA { get; set; } = string.Empty;
        
        public bool ConfirmacaoExclusao { get; set; } = false;
        public bool DadosConfirmados { get; set; } = false;
    }
} 