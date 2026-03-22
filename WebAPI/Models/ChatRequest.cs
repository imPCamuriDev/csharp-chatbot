using System.Text.Json.Serialization;

namespace WebAPI.Models;

public class ChatRequest
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("conversation_id")]
    public Guid? ConversationId { get; set; }
}

public class ChatResponse
{
    [JsonPropertyName("reply")]
    public string Reply { get; set; } = string.Empty;

    [JsonPropertyName("conversation_id")]
    public Guid ConversationId { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    public ChatResponse(string reply, Guid conversationId, DateTime timestamp)
    {
        Reply = reply;
        ConversationId = conversationId;
        Timestamp = timestamp;
    }
}