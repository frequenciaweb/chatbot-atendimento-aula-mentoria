using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using src_api.Data;
using src_api.DTOs;
using src_api.Models;
using src_api.Services;
using System;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace src_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly ChatBotContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly KnowledgeBaseService _knowledgeBaseService;

        // Regex para telefone brasileiro: DDD (2 dígitos) + 9 + 8 dígitos
        private readonly Regex _telefoneRegex = new Regex(@"\b(\d{2}9\d{8})\b", RegexOptions.Compiled);

        // Regex para extrair nome (pelo menos duas palavras)
        private readonly Regex _nomeRegex = new Regex(@"\b([A-ZÁÀÂÃÉÈÊÍÌÎÓÒÔÕÚÙÛÇ][a-záàâãéèêíìîóòôõúùûç]+(?:\s+[A-ZÁÀÂÃÉÈÊÍÌÎÓÒÔÕÚÙÛÇ][a-záàâãéèêíìîóòôõúùûç]+)+)\b", RegexOptions.Compiled);

        public ChatController(ChatBotContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, KnowledgeBaseService knowledgeBaseService)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _knowledgeBaseService = knowledgeBaseService;
        }

        [HttpPost("enviar")]
        public async Task<IActionResult> EnviarMensagem([FromBody] MensagemDto mensagem)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest("Digite uma mensagem");
                }

                // Verificando se já passamos do fluxo de identificação
                // DadosConfirmados = true indica que o cliente confirmou os dados e foi cadastrado
                if (mensagem.DadosConfirmados == true)
                {
                    return await ExecutarFluxoAutenticadoAsync(mensagem);
                }
                else
                {
                    return await ExecutarFluxoParaIdentificarAsync(mensagem);
                }
            }
            catch (Exception)
            {
                // Adicionado de volta para garantir que qualquer erro inesperado seja tratado
                return StatusCode(500, new RespostaDto
                {
                    Sucesso = false,
                    Erro = "Desculpe, ocorreu um erro inesperado em nosso sistema. Tente novamente."
                });
            }
        }

        private async Task<IActionResult> ExecutarFluxoParaIdentificarAsync(MensagemDto mensagem)
        {
            //var dadosExtraidos = ExtrairDadosIdentificacao(mensagem.Texto);

            (string? Nome, string? Telefone) dadosExtraidos = await ExtrairDadosIdentificacaoPorIA(mensagem);

            // Cenário 1: Usuário enviou dados para identificação. Pedir confirmação.
            if (!string.IsNullOrEmpty(dadosExtraidos.Telefone))
            {
                return ExecutarCenario01(mensagem, dadosExtraidos);
            }

            // Cenário 2: Usuário confirmou ("sim") e o frontend enviou os dados temporários. Criar o cliente.
            //if (IsConfirmacaoPositiva(mensagem.Texto) && !string.IsNullOrEmpty(mensagem.Nome) && !string.IsNullOrEmpty(mensagem.Telefone))
            //{
            //    return await ExecutarCenario02(mensagem, dadosExtraidos);
            //}

            if (!string.IsNullOrEmpty(mensagem.Nome) && !string.IsNullOrEmpty(mensagem.Telefone) && await ProcessarConfirmacaoUsuarioIA(mensagem.Texto, "phi-3-mini-4k-instruct"))
            {
                // Cliente confirmou os dados, definir DadosConfirmados como true antes do cadastro
                mensagem.DadosConfirmados = true;
                return await ExecutarCenario02(mensagem, dadosExtraidos);
            }

            // Cenário 3: Usuário negou ("não").
            if (IsConfirmacao(mensagem.Texto) && !IsConfirmacaoPositiva(mensagem.Texto))
            {
                var mensagensAlternadas = new[]
                {
                    "Tudo bem. Para continuar, por favor, me informe seu nome completo e telefone celular corretos.",
                    "Sem problemas. Pode me informar seu nome e telefone para prosseguirmos?",
                };

                var random = new Random();
                var mensagemAleatoria = mensagensAlternadas[random.Next(mensagensAlternadas.Length)];

                return Ok(new RespostaDto
                {
                    Mensagem = mensagemAleatoria,
                    Sucesso = true
                });
            }

            // Cenário 4 (Fallback): Mensagem inicial do usuário sem dados (ex: "oi").
            return await ExecutarCenario04(mensagem, dadosExtraidos);

        }

        private async Task<bool> ProcessarConfirmacaoUsuarioIA(string texto, string modelo)
        {
            string prompt = @"Você é um assistente virtual da Omni Inovações, especializado em entender confirmações de clientes.
Sua missão é identificar de forma precisa qualquer manifestação de acordo, consentimento ou aprovação, mesmo que expressem isso sem usar literalmente ""sim"". Exemplos: concordo, aceito, perfeito, está bom, fechado, combinado, vamos em frente, estou de acordo, etc.";

            string tarefa = @"Dada a Mensagem do Usuário abaixo, analise se há uma confirmação positiva de operação. 
Considere como confirmação qualquer termo ou expressão que indique entendimento e anuência (não apenas 'sim'). 
Se encontrar confirmação, retorne exatamente 'SIM'. 
Caso contrário, retorne 'NÃO'.
Mensagem do Usuário: " + texto;

            var mensagemSolicitacao = await ProcessarMensagemIA(tarefa, modelo, new List<ChatMensagemDto>(), prompt);
            return mensagemSolicitacao.ToUpper().Contains("SIM");
        }

        private async Task<(string? Nome, string? Telefone)> ExtrairDadosIdentificacaoPorIA(MensagemDto mensagem)
        {
            string prompt = @"Você é um assistente da empresa Omni Inovações. 
IMPORTANTE: Sua tarefa é extrair APENAS o nome e telefone que estão explicitamente mencionados na mensagem do cliente.
NUNCA invente ou adicione dados que não estão na mensagem original.
Se não encontrar nome ou telefone na mensagem, retorne strings vazias.
Responda APENAS com o JSON solicitado, sem explicações ou pensamentos.";

            string tarefa = @"Analise a mensagem do cliente e extraia APENAS os dados que estão explicitamente mencionados:
Mensagem do cliente: """ + mensagem.Texto + @"""

Regras:
1. Use APENAS os dados que estão na mensagem
2. NÃO invente nomes ou telefones
3. Se não encontrar um dado, deixe vazio
4. Retorne APENAS o JSON no formato: {""nome"": ""nome_extraido"", ""telefone"": ""telefone_extraido""}

Exemplo de resposta quando encontrar dados: {""nome"": ""JOSE DA SILVA"", ""telefone"": ""61999988887""}
Exemplo de resposta quando NÃO encontrar dados: {""nome"": """", ""telefone"": """"}";

            var mensagemSolicitacao = await ProcessarMensagemIA(tarefa, mensagem.ModeloIA, new List<ChatMensagemDto>(), prompt);

            // Limpar a resposta da IA removendo markdown e espaços extras
            mensagemSolicitacao = mensagemSolicitacao.Replace("```json", "").Replace("```", "").Trim();
            
            // Tentar extrair JSON válido da resposta
            var jsonMatch = Regex.Match(mensagemSolicitacao, @"\{[^{}]*""nome""[^{}]*""telefone""[^{}]*\}", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (jsonMatch.Success)
            {
                mensagemSolicitacao = jsonMatch.Value;
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var dadosJson = JsonSerializer.Deserialize<DTODadosCadastrais>(mensagemSolicitacao, options);
                if (dadosJson == null)
                {
                    return (string.Empty, string.Empty);
                }

                if (string.IsNullOrEmpty(dadosJson.Nome) || (string.IsNullOrEmpty(dadosJson.Nome)))
                {
                    return (string.Empty, string.Empty);
                }

                return (dadosJson.Nome, dadosJson.Telefone);
            }
            catch (JsonException)
            {
                // Se a deserialização falhar, tentar extrair os dados manualmente
                var nomeMatch = Regex.Match(mensagemSolicitacao, @"""nome""\s*:\s*""([^""]*)""", RegexOptions.IgnoreCase);
                var telefoneMatch = Regex.Match(mensagemSolicitacao, @"""telefone""\s*:\s*""([^""]*)""", RegexOptions.IgnoreCase);
                
                var nome = nomeMatch.Success ? nomeMatch.Groups[1].Value : string.Empty;
                var telefone = telefoneMatch.Success ? telefoneMatch.Groups[1].Value : string.Empty;
                
                return (nome, telefone);
            }
        }

        private async Task<IActionResult> ExecutarCenario04(MensagemDto mensagem, (string? Nome, string? Telefone) dadosExtraidos)
        {
            var mensagensIdentificacao = new[]
            {
                "Olá! Para começar nosso atendimento, preciso de seu nome completo e número de celular com DDD.",
                "Para continuar, por favor, me informe seu nome completo e telefone celular com DDD.",
            };

            var randomDefault = new Random();
            var mensagemSolicitacao = mensagensIdentificacao[randomDefault.Next(mensagensIdentificacao.Length)];

            string prompt = @"Você é um atendente da empresa Omni Inovações.
Sua tarefa é atender o cliente de forma educada, informal e objetiva, como se fosse em uma conversa de WhatsApp.
Responda **APENAS** com a mensagem de usuário pedida, sem saudação longa, sem assinatura extra, sem explicações.
Use sempre o português do Brasil. Evite formalidades.
Escreva mensagens curtas, simpáticas e diretas, com emojis se fizer sentido.";

            string tarefa = @"Crie uma mensagem curta e simpatica de resposta para o WhatsApp simples pedindo ao cliente que informe:
- Nome completo
- Telefone celular (com DDD).

#Exemplo de mensagem curta e aceitavel
- Olá! 😊
Sou da Omni Inovações e estou aqui para te ajudar!
Para darmos início ao seu atendimento, preciso de algumas informações básicas:
📝 Nome completo:
📱 Telefone celular (com DDD):
Aguardo seu retorno!
";

            mensagemSolicitacao = await ProcessarMensagemIA(tarefa, mensagem.ModeloIA, new List<ChatMensagemDto>(), prompt);

            return Ok(new RespostaDto
            {
                Mensagem = mensagemSolicitacao,
                Sucesso = true
            });

        }

        private async Task<IActionResult> ExecutarCenario02(MensagemDto mensagem, (string? Nome, string? Telefone) dadosExtraidos)
        {
            var cliente = new Cliente
            {
                Nome = mensagem.Nome ?? string.Empty,
                Telefone = mensagem.Telefone ?? string.Empty,
            };

            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            var templateCadastro = ObterTemplateCadastroSucesso();
            var mensagemCadastro = SubstituirPlaceholders(templateCadastro, cliente.Nome, cliente.Telefone);

            return Ok(new RespostaDto
            {
                Mensagem = mensagemCadastro,
                Sucesso = true,
                ClienteIdentificado = true,
                DadosConfirmados = true, // Indica que o cliente foi cadastrado e os dados estão confirmados
                Nome = cliente.Nome,
                Telefone = cliente.Telefone,
            });
        }

        /// <summary>
        ///  Cenário 1: Usuário enviou dados para identificação. Pedir confirmação.
        /// </summary>
        /// <param name="mensagem"></param>
        /// <param name="dadosExtraidos"></param>
        /// <returns></returns>
        private IActionResult ExecutarCenario01(MensagemDto mensagem, (string? Nome, string? Telefone) dadosExtraidos)
        {
            var templateConfirmacao = ObterTemplateConfirmacao();
            var respostaConfirmacao = SubstituirPlaceholders(templateConfirmacao, dadosExtraidos.Nome, dadosExtraidos.Telefone);

            return Ok(new RespostaDto
            {
                Mensagem = respostaConfirmacao,
                Sucesso = true,
                DadosTemporarios = new { Nome = dadosExtraidos.Nome, Telefone = dadosExtraidos.Telefone }
            });
        }

        private async Task<IActionResult> ExecutarFluxoAutenticadoAsync(MensagemDto mensagem)
        {
            /* Tentar buscar o cliente pelo número do telefone caso não encontre vamos direcionar para o fluxo de autenticação novamente */
            var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Telefone == mensagem.Telefone);
            if (cliente != null)
            {
                /* Identiifcar se o cliente solicitou exclusão da conta */
                if (cliente.AguardandoConfirmacaoExclusao)
                {
                    return await ExecutarFluxoExclusao(mensagem, cliente);
                }

                if (QueroExcluirMinhaConta(mensagem.Texto))
                {
                    return await PrepararParaExclusaoConta(mensagem, cliente);
                }

                return await ProcessarMensagensBaseConhecimento(mensagem, cliente);
            }

            return await ExecutarFluxoParaIdentificarAsync(mensagem);
        }

        private async Task<IActionResult> ProcessarMensagensBaseConhecimento(MensagemDto mensagem, Cliente cliente)
        {
            // Buscar histórico das últimas 24 horas
            var historico = await _context.ClienteChats
                .Where(c => c.ClienteId == cliente.Id && c.DataCriacao >= DateTime.Now.AddHours(-24))
                .OrderBy(c => c.DataCriacao)
                .Select(c => new ChatMensagemDto
                {
                    Texto = c.Texto,
                    Origem = c.Origem,
                    DataCriacao = c.DataCriacao
                })
                .ToListAsync();

            // Salvar mensagem do cliente
            var chatClienteExistente = new ClienteChat
            {
                ClienteId = cliente.Id,
                Texto = mensagem.Texto,
                Origem = "cliente",
                DataCriacao = DateTime.Now
            };
            _context.ClienteChats.Add(chatClienteExistente);


            // Processar resposta da IA com contexto focado na base de conhecimento
            var systemPrompt = _knowledgeBaseService.GetSystemPrompt();
            var respostaIA = await ProcessarMensagemIA(mensagem.Texto, mensagem.ModeloIA, historico, systemPrompt);

            // Salvar resposta da IA
            var chatBotExistente = new ClienteChat
            {
                ClienteId = cliente.Id,
                Texto = respostaIA,
                Origem = "bot",
                DataCriacao = DateTime.Now
            };
            _context.ClienteChats.Add(chatBotExistente);

            await _context.SaveChangesAsync();

            // Atualizar histórico
            historico.Add(new ChatMensagemDto { Texto = mensagem.Texto, Origem = "cliente", DataCriacao = DateTime.Now });
            historico.Add(new ChatMensagemDto { Texto = respostaIA, Origem = "bot", DataCriacao = DateTime.Now });

            return Ok(new RespostaDto
            {
                Mensagem = respostaIA,
                Sucesso = true,
                Historico = historico,
                ClienteIdentificado = true
            });
        }

        private async Task<IActionResult> PrepararParaExclusaoConta(MensagemDto mensagem, Cliente cliente)
        {
            cliente.AguardandoConfirmacaoExclusao = true;
            await _context.SaveChangesAsync();

            return Ok(new RespostaDto
            {
                Mensagem = "Você tem certeza que deseja excluir sua conta e todo o histórico de conversas? Esta ação não pode ser desfeita. responda (sim/não)",
                Sucesso = true
            });
        }

        private bool QueroExcluirMinhaConta(string texto)
        {
            var textoLower = texto.ToLowerInvariant();
            var palavrasChave = new[] { "excluir minha conta", "delete minha conta", "apagar meus dados", "remover meu cadastro" };
            return palavrasChave.Any(p => textoLower.Contains(p));
        }

        private async Task<IActionResult> ExecutarFluxoExclusao(MensagemDto mensagem, Cliente cliente)
        {
            // Se confirmou, apagar tudo
            if (IsConfirmacaoPositiva(mensagem.Texto))
            {
                var chatsParaExcluir = _context.ClienteChats.Where(c => c.ClienteId == cliente.Id);
                _context.ClienteChats.RemoveRange(chatsParaExcluir);
                _context.Clientes.Remove(cliente);
                await _context.SaveChangesAsync();

                return Ok(new RespostaDto
                {
                    Mensagem = "Sua conta e todo o histórico de conversas foram excluídos com sucesso.",
                    ContaExcluida = true,
                    Sucesso = true
                });
            }
            else // Se negou ou mudou de assunto, resetar o estado
            {
                cliente.AguardandoConfirmacaoExclusao = false;
                await _context.SaveChangesAsync();

                return Ok(new RespostaDto
                {
                    Mensagem = "Ok, sua conta não foi excluída. Como posso te ajudar?",
                    Sucesso = true
                });
            }
        }



        private (string? Nome, string? Telefone) ExtrairDadosIdentificacao(string texto)
        {
            // Extrair telefone
            var matchTelefone = _telefoneRegex.Match(texto);
            string? telefone = matchTelefone.Success ? matchTelefone.Groups[1].Value : null;

            // Extrair nome (pelo menos duas palavras)
            var matchNome = _nomeRegex.Match(texto);
            string? nome = matchNome.Success ? matchNome.Groups[1].Value : null;

            // Se não encontrou nome com regex, tentar extração simples (pelo menos duas palavras)
            if (nome == null)
            {
                var palavras = texto.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(p => !_telefoneRegex.IsMatch(p) && p.Length > 1 && !IsCommonWord(p))
                    .ToArray();

                if (palavras.Length >= 2)
                {
                    nome = string.Join(" ", palavras.Take(palavras.Length >= 3 ? 3 : 2));
                }
            }

            return (nome, telefone);
        }

        private bool IsCommonWord(string word)
        {
            var commonWords = new[] { "meu", "me", "chamo", "nome", "sou", "telefone", "celular", "número" };
            return commonWords.Contains(word.ToLower());
        }

        private bool IsConfirmacao(string texto)
        {
            var textoLower = texto.ToLower().Trim();
            return textoLower == "sim" || textoLower == "não" || textoLower == "nao" ||
                   textoLower == "yes" || textoLower == "no" ||
                   textoLower == "s" || textoLower == "n";
        }

        private bool IsConfirmacaoPositiva(string texto)
        {
            var textoLower = texto.ToLower().Trim();
            return textoLower == "sim" || textoLower == "yes" || textoLower == "s" || textoLower == "ok" || textoLower == "de acordo" || textoLower == "confirmo" || textoLower == "aceito";
        }

        private string ObterPromptSolicitacaoIdentificacao()
        {
            return @"Responda em português brasileiro com UMA ÚNICA frase solicitando o nome completo e telefone celular do cliente para iniciar o atendimento. Seja cordial e direto.";
        }

        private string ObterPromptIdentificacao()
        {
            return @"Você é um atendente da empresa Omni Inovações.
Sua tarefa é atender o cliente de forma educada, informal e objetiva, como se fosse uma conversa de WhatsApp.
Use sempre o português do Brasil. Evite formalidades.
Escreva mensagens curtas, simpáticas e diretas, com emojis se fizer sentido.";
        }

        private string ObterTemplateConfirmacao()
        {
            return "Você confirma que seu nome é [nome completo] e seu telefone é [número de telefone]? (sim/não)";
        }

        private string ObterTemplateCadastroSucesso()
        {
            return "Perfeito, [nome completo]! Número [número de telefone] registrado. Como posso te ajudar?";
        }

        private string SubstituirPlaceholders(string template, string? nome, string? telefone)
        {
            if (string.IsNullOrEmpty(template))
                return template;

            var resultado = template;

            // RF02: Controle condicional da substituição
            // RF01: Substituição de placeholders com validação de segurança
            if (!string.IsNullOrEmpty(nome))
            {
                // RN01: Validação de segurança - sanitizar dados
                var nomeLimpo = SanitizarTexto(nome);
                resultado = resultado.Replace("[nome completo]", nomeLimpo);
            }
            else
            {
                // RF02: Se dados não disponíveis, remover placeholders para evitar exibição literal
                resultado = resultado.Replace("[nome completo]", "[NOME_NAO_DISPONIVEL]");
            }

            if (!string.IsNullOrEmpty(telefone))
            {
                // RN01: Validação de segurança - sanitizar dados
                var telefoneLimpo = SanitizarTelefone(telefone);
                resultado = resultado.Replace("[número de telefone]", telefoneLimpo);
            }
            else
            {
                // RF02: Se dados não disponíveis, remover placeholders para evitar exibição literal
                resultado = resultado.Replace("[número de telefone]", "[TELEFONE_NAO_DISPONIVEL]");
            }

            // RN03: Limpar placeholders não substituídos para mensagens claras
            resultado = LimparPlaceholdersNaoDisponiveis(resultado);

            return resultado;
        }

        private string LimparPlaceholdersNaoDisponiveis(string texto)
        {
            if (string.IsNullOrEmpty(texto))
                return texto;

            // RN03: Remover marcadores de dados não disponíveis para manter mensagens limpas
            return texto.Replace("[NOME_NAO_DISPONIVEL]", "")
                       .Replace("[TELEFONE_NAO_DISPONIVEL]", "")
                       .Replace("  ", " ") // Remover espaços duplos
                       .Trim();
        }

        private string SanitizarTexto(string texto)
        {
            if (string.IsNullOrEmpty(texto))
                return texto;

            // Remover caracteres perigosos para evitar injection
            return texto.Replace("\"", "").Replace("'", "").Replace("<", "").Replace(">", "")
                       .Replace("&", "").Replace("{", "").Replace("}", "").Trim();
        }

        private string SanitizarTelefone(string telefone)
        {
            if (string.IsNullOrEmpty(telefone))
                return telefone;

            // Manter apenas dígitos para telefone
            return new string(telefone.Where(char.IsDigit).ToArray());
        }

        [HttpGet("modelos")]
        public async Task<IActionResult> ListarModelos()
        {
            try
            {
                var modelos = new List<ModeloIADto>();

                // Modelos ChatGPT
                modelos.AddRange(new[]
                {
                    new ModeloIADto { Id = "gpt-3.5-turbo",    Nome = "GPT-3.5 Turbo",    Tipo = "chatgpt" },
                    new ModeloIADto { Id = "gpt-3.5-turbo-16k",Nome = "GPT-3.5 Turbo 16k",Tipo = "chatgpt" },
                    new ModeloIADto { Id = "gpt-4",            Nome = "GPT-4",            Tipo = "chatgpt" },
                    new ModeloIADto { Id = "gpt-4-turbo",      Nome = "GPT-4 Turbo",      Tipo = "chatgpt" },
                    new ModeloIADto { Id = "gpt-4o",           Nome = "GPT-4o",           Tipo = "chatgpt" },
                    new ModeloIADto { Id = "gpt-4o-mini",      Nome = "GPT-4o Mini",      Tipo = "chatgpt" },
                });

                // Modelos Claude
                modelos.AddRange(new[]
                {
                    new ModeloIADto { Id = "claude-3-opus-latest",   Nome = "Claude 3 Opus",   Tipo = "claude" },
                    new ModeloIADto { Id = "claude-3-sonnet-latest", Nome = "Claude 3 Sonnet", Tipo = "claude" },
                    new ModeloIADto { Id = "claude-3-haiku-latest",  Nome = "Claude 3 Haiku",  Tipo = "claude" },
                    new ModeloIADto { Id = "claude-sonnet-4-0",      Nome = "Claude Sonnet 4", Tipo = "claude" },
                    new ModeloIADto { Id = "claude-opus-4-0",        Nome = "Claude Opus 4",   Tipo = "claude" }
                });

                // Modelos Gemini
                modelos.Add(new ModeloIADto
                {
                    Id = "gemini-2.0-flash",
                    Nome = "Gemini 2.0 Flash",
                    Tipo = "gemini"
                });

                // Modelos Grok
                modelos.Add(new ModeloIADto
                {
                    Id = "grok-3-beta",
                    Nome = "Grok 3 Beta",
                    Tipo = "grok"
                });

                // Modelos Locais (LM Studio)
                try
                {
                    var httpClient = _httpClientFactory.CreateClient("LMStudio");
                    var response = await httpClient.GetAsync("v1/models");

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var modelosLocais = JsonSerializer.Deserialize<JsonElement>(json);

                        if (modelosLocais.TryGetProperty("data", out var data))
                        {
                            foreach (var modelo in data.EnumerateArray())
                            {
                                if (modelo.TryGetProperty("id", out var id))
                                {
                                    modelos.Add(new ModeloIADto
                                    {
                                        Id = id.GetString() ?? "",
                                        Nome = id.GetString() ?? "",
                                        Tipo = "local"
                                    });
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Se não conseguir conectar ao LM Studio, ignora
                }

                return Ok(new ModelosDisponiveis { Modelos = modelos });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { erro = "Erro ao buscar modelos disponíveis." });
            }
        }

        private async Task<string> ProcessarMensagemIA(string mensagem, string modeloIA, List<ChatMensagemDto> historico, string? systemPrompt = null)
        {
            try
            {
                // Constrói a lista de mensagens no formato esperado pelas APIs
                var mensagensApi = new List<Dictionary<string, string>>();

                // Adiciona a mensagem de sistema se ela existir
                if (!string.IsNullOrEmpty(systemPrompt))
                {
                    mensagensApi.Add(new Dictionary<string, string> { { "role", "system" }, { "content", systemPrompt } });
                }
                else // Adiciona o prompt de identificação padrão se não houver um de sistema
                {
                    mensagensApi.Add(new Dictionary<string, string> { { "role", "system" }, { "content", ObterPromptIdentificacao() } });
                }

                // Adiciona o histórico da conversa
                foreach (var msg in historico)
                {
                    mensagensApi.Add(new Dictionary<string, string> { { "role", msg.Origem == "bot" ? "assistant" : "user" }, { "content", msg.Texto } });
                }

                // Adiciona a mensagem atual do usuário
                mensagensApi.Add(new Dictionary<string, string> { { "role", "user" }, { "content", mensagem } });


                // RN02: Determinar tipo de IA e processar (compatibilidade com todos os modelos)
                if (modeloIA.StartsWith("gpt-") || modeloIA.StartsWith("o4-"))
                {
                    return await ProcessarChatGPT(mensagensApi, modeloIA);
                }
                else if (modeloIA.StartsWith("claude-"))
                {
                    return await ProcessarClaude(mensagensApi, modeloIA);
                }
                else if (modeloIA.StartsWith("gemini-"))
                {
                    // Implementar lógica para Gemini se necessário
                    return "Modelo Gemini ainda não implementado.";
                }
                else if (modeloIA.StartsWith("grok-"))
                {
                    // Implementar lógica para Grok se necessário
                    return "Modelo Grok ainda não implementado.";
                }
                else // Assumindo que é um modelo local
                {
                    return await ProcessarModeloLocal(mensagensApi, modeloIA);
                }
            }
            catch
            {
                return "Desculpe, ocorreu um erro ao processar sua mensagem. Tente novamente.";
            }
        }

        private async Task<string> ProcessarChatGPT(List<Dictionary<string, string>> mensagens, string modelo)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("ChatGPT");

                var requestBody = new
                {
                    model = modelo,
                    messages = mensagens,
                    max_tokens = 500
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync("v1/chat/completions", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
                    var respostaCompleta = result.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "Erro na resposta.";

                    // Extrair apenas a resposta, removendo o pensamento interno
                    return ExtrairRespostaIA(respostaCompleta);
                }

                return "Erro ao conectar com ChatGPT.";
            }
            catch
            {
                return "Erro ao processar com ChatGPT.";
            }
        }

        private async Task<string> ProcessarClaude(List<Dictionary<string, string>> mensagens, string modelo)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("Claude");

                // A API do Claude espera que a mensagem de sistema seja tratada de forma diferente
                var systemPrompt = mensagens.FirstOrDefault(m => m["role"] == "system")?["content"] ?? "";
                var userMessages = mensagens.Where(m => m["role"] != "system").ToList();

                var requestBody = new
                {
                    model = modelo,
                    system = systemPrompt,
                    max_tokens = 500,
                    messages = userMessages
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync("v1/messages", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
                    var respostaCompleta = result.GetProperty("content")[0].GetProperty("text").GetString() ?? "Erro na resposta.";

                    // Extrair apenas a resposta, removendo o pensamento interno
                    return ExtrairRespostaIA(respostaCompleta);
                }

                return "Erro ao conectar com Claude.";
            }
            catch
            {
                return "Erro ao processar com Claude.";
            }
        }

        private async Task<string> ProcessarModeloLocal(List<Dictionary<string, string>> mensagens, string modelo)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("LMStudio");

                var requestBody = new
                {
                    model = modelo,
                    messages = mensagens,
                    temperature = 0.2,
                    max_tokens = 500
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync("v1/chat/completions", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
                    var respostaCompleta = result.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "Erro na resposta.";

                    // Extrair apenas a resposta, removendo o pensamento interno
                    return ExtrairRespostaIA(respostaCompleta);
                }

                return "Erro ao conectar com modelo local.";
            }
            catch
            {
                return "Erro ao processar com modelo local.";
            }
        }

        /// <summary>
        /// Extrai apenas a resposta da IA, removendo o pensamento interno que pode vir em tags <think>
        /// </summary>
        /// <param name="respostaCompleta">Resposta completa da IA que pode conter pensamento interno</param>
        /// <returns>Apenas a resposta limpa da IA</returns>
        private string ExtrairRespostaIA(string respostaCompleta)
        {
            if (string.IsNullOrEmpty(respostaCompleta))
                return respostaCompleta;

            // Padrão para remover tags <think> e seu conteúdo
            var thinkPattern = @"<think>.*?</think>";
            var respostaLimpa = Regex.Replace(respostaCompleta, thinkPattern, "", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // Remover também outras possíveis tags de pensamento
            var outrasTagsPensamento = new[]
            {
                @"<thinking>.*?</thinking>",
                @"<reasoning>.*?</reasoning>",
                @"<thought>.*?</thought>",
                @"<internal>.*?</internal>",
                @"<process>.*?</process>"
            };

            foreach (var pattern in outrasTagsPensamento)
            {
                respostaLimpa = Regex.Replace(respostaLimpa, pattern, "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            }

            // Limpar espaços em branco extras e quebras de linha desnecessárias
            respostaLimpa = Regex.Replace(respostaLimpa, @"\n\s*\n", "\n", RegexOptions.Multiline);
            respostaLimpa = respostaLimpa.Trim();

            // Se após a limpeza a resposta ficou vazia, retornar uma mensagem padrão
            if (string.IsNullOrWhiteSpace(respostaLimpa))
            {
                return "Desculpe, não consegui processar sua solicitação. Pode tentar novamente?";
            }

            return respostaLimpa;
        }
    }
}