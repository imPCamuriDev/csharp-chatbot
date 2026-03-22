using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.Data.Entities;
using WebAPI.Models;
using ChatMessage = WebAPI.Data.Entities.ChatMessage;
using ChatResponse = WebAPI.Models.ChatResponse;

namespace WebAPI.Services;

public class ChatService : IChatService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;

    private string SystemPrompt => 
        _config["Ollama:SystemPrompt"] ?? "Você é um assistente útil.";

    public ChatService(HttpClient http, IConfiguration config, AppDbContext db)
    {
        _http = http;
        _config = config;
        _db = db;

        _http.BaseAddress = new Uri("http://localhost:11434/");
        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.Accept
             .Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<ChatResponse> SendMessageAsync(ChatRequest request, CancellationToken ct)
    {
        // Busca ou cria a conversa
        Conversation conversation;

        if (request.ConversationId.HasValue)
        {
            conversation = await _db.Conversations
                .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
                .FirstOrDefaultAsync(c => c.Id == request.ConversationId.Value, ct)
                ?? throw new KeyNotFoundException("Conversa não encontrada.");
        }
        else
        {
            conversation = new Conversation { Title = request.Message[..Math.Min(50, request.Message.Length)] };
            _db.Conversations.Add(conversation);
            await _db.SaveChangesAsync(ct);
        }

        // Salva a mensagem do usuário
        var userMessage = new ChatMessage()
        {
            ConversationId = conversation.Id,
            Role = "user",
            Content = request.Message
        };
        _db.ChatMessages.Add(userMessage);
        await _db.SaveChangesAsync(ct);

        // Monta o histórico completo para o Ollama
        var messages = new List<object>
        {
            new { role = "system", content = SystemPrompt }
        };

        foreach (var msg in conversation.Messages)
            messages.Add(new { role = msg.Role, content = msg.Content });

        // Inclui a mensagem atual
        messages.Add(new { role = "user", content = request.Message });

        var payload = new { model = "llama3.2", stream = false, messages };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync("api/chat", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Ollama error {response.StatusCode}: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken: ct);
        var reply = result?.Message?.Content ?? "Sem resposta";

        // Salva a resposta do assistente
        _db.ChatMessages.Add(new ChatMessage
        {
            ConversationId = conversation.Id,
            Role = "assistant",
            Content = reply
        });
        await _db.SaveChangesAsync(ct);

        return new ChatResponse(reply, conversation.Id, DateTime.UtcNow);
    }
}