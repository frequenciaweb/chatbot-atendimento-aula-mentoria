namespace src_api.DTOs
{
    public class RespostaDto
    {
        public bool Sucesso { get; set; } = true;
        public string Mensagem { get; set; } = string.Empty;
        public string? Erro { get; set; }
        public bool ClienteIdentificado { get; set; } = false;
        public string? Nome { get; set; }
        public string? Telefone { get; set; }
        public bool AguardandoConfirmacaoExclusao { get; set; } = false;
        public bool ContaExcluida { get; set; } = false;
        public object? DadosTemporarios { get; set; }
        public List<ChatMensagemDto>? Historico { get; set; }
    }

    public class ChatMensagemDto
    {
        public string Texto { get; set; } = string.Empty;
        public string Origem { get; set; } = string.Empty; // "cliente" ou "bot"
        public DateTime DataCriacao { get; set; }
    }
} 