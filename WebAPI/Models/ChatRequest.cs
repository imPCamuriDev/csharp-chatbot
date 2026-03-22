using System.ComponentModel.DataAnnotations;

namespace WebAPI.Models;

public record ChatRequest(
    [Required, MinLength(1)] string Message,
    Guid? ConversationId = null
);

public record ChatResponse(
    string Reply,
    Guid? ConversationId,
    DateTime Timestamp
);