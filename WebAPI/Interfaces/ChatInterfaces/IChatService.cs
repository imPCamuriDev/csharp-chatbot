using WebAPI.Models;

namespace WebAPI.Services;

public interface IChatService
{
    Task<ChatResponse> SendMessageAsync(ChatRequest request, CancellationToken ct = default);
}