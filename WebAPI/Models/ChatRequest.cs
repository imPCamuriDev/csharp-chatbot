using System.ComponentModel.DataAnnotations;

namespace WebAPI.Models;

public record ChatRequest(
    [Required, MinLength(1)] string Message,
    string? SessionId = null
);

public record ChatResponse(
    string Reply,
    string SessionId,
    DateTime Timestamp
);