# Chatbot de Atendimento com Múltiplas IAs

> 📚 **Projeto desenvolvido durante a mentoria do [Curso de Desenvolvimento Focado em IA](https://hotmart.com/pt-br/marketplace/produtos/curso-de-desenvolvimento-focado-em-ia/F99940338H)**
>
> 🚀 **Adquira o curso e ganhe mentoria para seus projetos particulares!**
>
> O curso ensina como turbinar sua carreira de desenvolvedor utilizando ferramentas de IA como GitHub Copilot, ChatGPT, Claude, Cursor e muito mais. São 20 horas de conteúdo prático ao vivo para automatizar tarefas, programar com mais eficiência e aumentar significativamente sua produtividade.
>
> 🎯 [**Clique aqui para saber mais sobre o curso**](https://hotmart.com/pt-br/marketplace/produtos/curso-de-desenvolvimento-focado-em-ia/F99940338H)

Sistema de chatbot inteligente com integração a múltiplos agentes de IA (ChatGPT, Claude, Modelos Locais via LM Studio).

## Tecnologias

- **Frontend:** Angular 19
- **Backend:** .NET 8.0 Web API
- **Banco de Dados:** SQLite
- **IAs Integradas:** ChatGPT, Claude, Modelos Locais (LM Studio)

## Estrutura do Projeto

```
src/
├── README.md                    # Este arquivo
├── src-api/                     # Projeto Backend (.NET Core 8 + SQLite)  
├── src-web/                     # Projeto Frontend (Angular 19)
└── docs/                        # Documentação técnica
```

## Como Executar

### Backend (.NET 8.0)
```bash
cd src-api
dotnet run
```

### Frontend (Angular 19)
```bash
cd src-web
npm install
ng serve
```

## Funcionalidades

- ✅ Chat conversacional sem cadastro
- ✅ Identificação automática via conversa (nome + telefone)
- ✅ Múltiplas IAs disponíveis
- ✅ Histórico de conversa (24h)
- ✅ Sessões separadas por cliente
- ✅ API documentada com Swagger

## Como Executar

### Pré-requisitos
- .NET 8.0 SDK
- Node.js 18+ e npm
- Angular CLI 19

### 1. Backend (.NET 8.0)
```bash
cd src-api
dotnet run
```
- API: https://localhost:7030
- Swagger: https://localhost:7030/swagger

### 2. Frontend (Angular 19)
```bash
cd src-web
npm install
ng serve
```
- App: http://localhost:4200

### 3. Configuração das IAs (Opcional)
Edite `src-api/appsettings.json` com suas chaves:
```json
{
  "OpenAI": { "ApiKey": "sua_chave_openai" },
  "Claude": { "ApiKey": "sua_chave_claude" }
}
```

## Status do Projeto

✅ **CONCLUÍDO COM SUCESSO**  
**Desenvolvido em:** 21/06/2025  
**Tempo Total:** 67 minutos  
**Todos os requisitos:** 100% implementados

### Recursos Funcionais
- Chat conversacional sem cadastro
- Identificação automática (nome + telefone)  
- Múltiplas IAs (ChatGPT, Claude, LM Studio)
- Histórico 24h por cliente
- Interface moderna e responsiva
- API REST documentada com Swagger 