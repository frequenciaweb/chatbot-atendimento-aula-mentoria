using System.Text;

namespace src_api.Services
{
    public class KnowledgeBaseService
    {
        private readonly string _knowledgeBasePath;
        private string? _cachedKnowledgeBase;
        private DateTime _lastReadTime;
        private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

        public KnowledgeBaseService(IWebHostEnvironment env)
        {
            // O caminho para a base de conhecimento é relativo à raiz do conteúdo do projeto da API
            _knowledgeBasePath = Path.Combine(env.ContentRootPath, "BaseConhecimento");
            _cachedKnowledgeBase = null;
        }

        private string LoadKnowledgeBase()
        {
            var filePaths = new[]
            {
                Path.Combine(_knowledgeBasePath, "empresa.txt"),
                Path.Combine(_knowledgeBasePath, "produto.txt"),
                Path.Combine(_knowledgeBasePath, "faq.txt")
            };

            var sb = new StringBuilder();

            foreach (var filePath in filePaths)
            {
                if (File.Exists(filePath))
                {
                    sb.AppendLine(File.ReadAllText(filePath, Encoding.UTF8));
                    sb.AppendLine(); // Adiciona uma linha em branco entre os arquivos
                }
            }

            return sb.ToString();
        }

        public string GetSystemPrompt()
        {
            // Recarrega o cache se estiver expirado
            if (_cachedKnowledgeBase == null || DateTime.UtcNow - _lastReadTime > _cacheDuration)
            {
                _cachedKnowledgeBase = LoadKnowledgeBase();
                _lastReadTime = DateTime.UtcNow;
            }

            var promptTemplate = """
            Você é um assistente virtual da empresa Omni Inovações.
            Sua única função é responder a perguntas sobre a empresa e sobre nosso produto, o OmniChatBot, com base estritamente na base de conhecimento fornecida abaixo.
            Não responda a perguntas que não possam ser respondidas com as informações a seguir.

            Se a pergunta do usuário for sobre qualquer outro tópico, responda exatamente com a seguinte frase:
            "Desculpe, só posso responder perguntas relacionadas à nossa empresa ou ao nosso produto. Como posso te ajudar dentro desse escopo?"

            Se a pergunta for ambígua, peça para o usuário reformular a pergunta em relação à empresa ou ao produto, com a seguinte frase:
            "Poderia reformular sua pergunta relacionada ao OmniChatBot ou à nossa empresa?"

            Base de Conhecimento:
            ---
            {0}
            ---
            """;
            
            return string.Format(promptTemplate, _cachedKnowledgeBase);
        }
    }
} 