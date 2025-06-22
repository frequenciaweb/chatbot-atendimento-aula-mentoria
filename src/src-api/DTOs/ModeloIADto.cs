namespace src_api.DTOs
{
    public class ModeloIADto
    {
        public string Id { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty; // "chatgpt", "claude", "local"
    }

    public class ModelosDisponiveis
    {
        public List<ModeloIADto> Modelos { get; set; } = new List<ModeloIADto>();
    }
} 