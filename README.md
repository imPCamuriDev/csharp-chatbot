# WebAPI + Ollama + PostgreSQL — Guia de Setup

API intermediária em C# (.NET 8) que conecta uma aplicação web a um chatbot local via Ollama (Llama), com histórico de conversas salvo no PostgreSQL. Tudo rodando em Docker.

---

## Pré-requisitos

- [Docker](https://docs.docker.com/get-docker/) e [Docker Compose](https://docs.docker.com/compose/install/) instalados
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8) (para rodar fora do Docker / gerar migrations)
- Git

---

## Estrutura do Projeto

```
RiderProjects/
└── WebAPI/
    ├── WebAPI/
    │   ├── Controllers/
    │   ├── Services/
    │   ├── Models/
    │   ├── Data/
    │   │   ├── Entities/
    │   │   │   ├── Conversation.cs
    │   │   │   └── ChatMessage.cs
    │   │   └── AppDbContext.cs
    │   ├── Migrations/
    │   ├── appsettings.json
    │   ├── appsettings.Development.json
    │   ├── Program.cs
    │   └── Dockerfile
    └── docker-compose.yml
```

---

## Configuração

### 1. `appsettings.json` (produção / Docker)

```json
{
  "ConnectionStrings": {
    "Default": ""
  },
  "AllowedOrigins": "",
  "Ollama": {
    "BaseUrl": "",
    "SystemPrompt": ""
  }
}
```

> Deixe os valores vazios aqui. Em produção, use variáveis de ambiente via `docker-compose.yml`.

### 2. `appsettings.Development.json` (desenvolvimento local)

> ⚠️ **Nunca suba esse arquivo pro Git!** Adicione ao `.gitignore`.

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Database=chatdb;Username=chatuser;Password=suasenha"
  },
  "AllowedOrigins": "http://localhost:3000",
  "Ollama": {
    "BaseUrl": "http://localhost:11434/",
    "SystemPrompt": "Você é um assistente útil. Responda sempre em português."
  }
}
```

### 3. `.gitignore`

```
appsettings.Development.json
```

---

## Rodando com Docker

### 1. Suba todos os containers

```bash
docker compose up -d --build
```

Isso irá subir:
- `postgres` — banco de dados PostgreSQL na porta `5432`
- `ollama` — servidor do modelo Llama na porta `11434`
- `ollama-setup` — container temporário que baixa o modelo `llama3.2` (~2GB)
- `webapi` — a API .NET na porta `8080`
- `pgadmin` — painel visual do banco na porta `5050`

### 2. Acompanhe o download do modelo

O modelo Llama3.2 tem ~2GB e precisa ser baixado na primeira vez:

```bash
docker logs -f ollama-setup
```

Aguarde até ver uma mensagem de conclusão. Para confirmar:

```bash
docker exec ollama ollama list
```

Deve aparecer `llama3.2:latest` na lista.

### 3. Verifique se a API está rodando

Acesse o Swagger em: [http://localhost:8080/swagger](http://localhost:8080/swagger)

---

## Usando a API

### Nova conversa

Não envie `conversation_id`:

```json
POST http://localhost:8080/api/chat/message

{
  "message": "Olá! Qual é a capital do Brasil?"
}
```

Resposta:

```json
{
  "reply": "A capital do Brasil é Brasília.",
  "conversation_id": "3f2a1b4c-...",
  "timestamp": "2026-03-22T..."
}
```

### Continuar uma conversa

Use o `conversation_id` retornado:

```json
POST http://localhost:8080/api/chat/message

{
  "message": "E qual é a população dela?",
  "conversation_id": "3f2a1b4c-..."
}
```

---

## Visualizando o Banco (pgAdmin)

1. Acesse [http://localhost:5050](http://localhost:5050)
2. Login:
    - **Email:** `admin@admin.com`
    - **Senha:** `admin`
3. Clique em **"Add New Server"** e preencha:

| Campo    | Valor      |
|----------|------------|
| Host     | `postgres` |
| Port     | `5432`     |
| Database | `chatdb`   |
| Username | `chatuser` |
| Password | `suasenha` |

> ⚠️ O host é `postgres` (nome do container), não `localhost`.

---

## Migrations

As migrations são aplicadas **automaticamente** ao subir o container via `db.Database.Migrate()` no `Program.cs`.

Para rodar manualmente fora do Docker:

### Instalar o dotnet-ef

```bash
dotnet tool install --global dotnet-ef --version 8.0.0
```

### Adicionar o PATH (Linux/macOS)

```bash
echo 'export PATH="$PATH:$HOME/.dotnet/tools"' >> ~/.zshrc
source ~/.zshrc
```

Se usar bash:

```bash
echo 'export PATH="$PATH:$HOME/.dotnet/tools"' >> ~/.bashrc
source ~/.bashrc
```

### Criar e aplicar migration

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

---

## Pacotes NuGet necessários

Compatíveis com **.NET 8**:

```bash
dotnet add package Microsoft.EntityFrameworkCore --version 8.0.0
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.0
dotnet tool install --global dotnet-ef --version 8.0.0
```

---

## Comandos Docker úteis

```bash
# Subir tudo
docker compose up -d --build

# Ver logs da API
docker logs -f webapi

# Ver logs do Ollama
docker logs -f ollama

# Verificar variáveis de ambiente do container
docker exec webapi printenv | grep Ollama

# Parar tudo
docker compose down

# Parar e apagar volumes (banco zerado)
docker compose down -v

# Reinstalar o modelo manualmente
docker exec -it ollama ollama pull llama3.2
```

---

## Solução de Problemas

### ❌ `Host can't be null` ao iniciar

**Causa:** A `ConnectionString` está nula — a chave `Default` não foi encontrada no `appsettings.json`.

**Solução:** Verifique se o `appsettings.json` contém a seção `ConnectionStrings` com a chave `Default`, ou se as variáveis de ambiente estão corretamente definidas no `docker-compose.yml`:

```yaml
environment:
  - ConnectionStrings__Default=Host=postgres;Database=chatdb;Username=chatuser;Password=suasenha
```

---

### ❌ `Value cannot be null (Parameter 'origin')` — CORS

**Causa:** A chave `AllowedOrigins` está nula no `appsettings.json`.

**Solução:** No `Program.cs`, sempre verifique antes de chamar `WithOrigins()`:

```csharp
var origins = builder.Configuration["AllowedOrigins"];
if (!string.IsNullOrEmpty(origins))
    policy.WithOrigins(origins.Split(",")).AllowAnyMethod().AllowAnyHeader();
else
    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
```

---

### ❌ `relation "Conversations" does not exist`

**Causa:** O banco foi criado antes das migrations existirem, ou as migrations estão vazias.

**Solução:**

```bash
# 1. Apague as migrations antigas
rm -rf WebAPI/Migrations/

# 2. Recrie o banco do zero
docker compose down -v
docker compose up -d postgres

# 3. Regenere a migration (certifique-se que as entidades existem)
dotnet ef migrations add InitialCreate
dotnet ef database update

# 4. Ou suba tudo via Docker (migrations rodam automaticamente)
docker compose up -d --build
```

---

### ❌ `Connection refused (localhost:11434)`

**Causa:** O `ChatService` está com a URL do Ollama hardcoded como `localhost`, mas dentro do Docker o Ollama é acessado pelo nome do serviço `ollama`.

**Solução:** No `ChatService.cs`, leia a URL da configuração:

```csharp
var ollamaUrl = _config["Ollama:BaseUrl"] ?? "http://localhost:11434/";
_http.BaseAddress = new Uri(ollamaUrl);
```

E no `docker-compose.yml`:

```yaml
environment:
  - Ollama__BaseUrl=http://ollama:11434/
```

> ⚠️ Dentro do Docker, use `__` (dois underscores) para separar seções, equivalente ao `:` do JSON.

---

### ❌ `model 'llama3.2' not found`

**Causa:** O modelo ainda não foi baixado no container do Ollama.

**Solução:**

```bash
# Baixar manualmente (aguarde, ~2GB)
docker exec -it ollama ollama pull llama3.2

# Confirmar que foi baixado
docker exec ollama ollama list
```

---

### ❌ `The request field is required` / erro de desserialização do JSON

**Causa:** O `JsonNamingPolicy.SnakeCaseLower` configurado globalmente conflita com a desserialização dos `records` C#.

**Solução:** Remova a política global do `Program.cs` e use `[JsonPropertyName]` explicitamente nos models:

```csharp
// ❌ Remova isso do Program.cs:
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

// ✅ Deixe só isso:
builder.Services.AddControllers();
```

E no `ChatRequest.cs`:

```csharp
public class ChatRequest
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("conversation_id")]
    public Guid? ConversationId { get; set; }
}
```

---

### ❌ `dotnet-ef: command not found`

**Causa:** O `dotnet-ef` foi instalado mas não está no PATH do sistema.

**Solução:**

```bash
echo 'export PATH="$PATH:$HOME/.dotnet/tools"' >> ~/.zshrc
source ~/.zshrc
dotnet ef --version
```

---

### ❌ `Conversa não encontrada` (404)

**Causa:** Você está enviando um `conversation_id` que não existe no banco — geralmente após recriar o banco do zero.

**Solução:** Na primeira mensagem, não envie o campo `conversation_id`. A API criará uma nova conversa e retornará o ID para uso nas próximas mensagens.

```json
{
  "message": "Olá!"
}
```

---

## Variáveis de Ambiente (docker-compose.yml)

| Variável | Descrição | Exemplo |
|----------|-----------|---------|
| `ConnectionStrings__Default` | String de conexão do PostgreSQL | `Host=postgres;Database=chatdb;...` |
| `Ollama__BaseUrl` | URL do servidor Ollama | `http://ollama:11434/` |
| `Ollama__SystemPrompt` | Instrução inicial para o modelo | `Você é um assistente útil.` |
| `AllowedOrigins` | Origens permitidas no CORS | `http://localhost:3000` |

> Use `__` (duplo underscore) para separar seções no Docker, equivalente ao `:` no JSON.