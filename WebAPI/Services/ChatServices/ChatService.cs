using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WebAPI.Models;

namespace WebAPI.Services;

public class ChatService : IChatService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    private string SystemPrompt => _config["Ollama:SystemPrompt"] ?? "Caso tenha recebido esta mensagem, não responda nada, diga apenas que não foi configurado corretamente sem dar nenhum detalhe.";

    public ChatService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
        _http.BaseAddress = new Uri("http://localhost:11434/");
        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<ChatResponse> SendMessageAsync(ChatRequest request, CancellationToken ct)
    {
        var payload = new
        {
            model = "llama3.2",
            stream = false,
            messages = new[]
            {
                new { role = "system", content = SystemPrompt }, // 👈 instrução inicial
                new { role = "user",   content = request.Message }
            }
        };

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

        return new ChatResponse(reply, request.SessionId ?? Guid.NewGuid().ToString(), DateTime.UtcNow);
    }
}