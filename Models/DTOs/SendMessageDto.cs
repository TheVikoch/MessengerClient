namespace MessengerClient.Models.DTOs;

public class SendMessageDto
{
    public string ConversationId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ReplyToMessageId { get; set; }
}
