namespace MessengerClient.Models.DTOs;

public class MessageDto
{
    public string Id { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public UserDto Sender { get; set; } = new UserDto();
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool IsDeleted { get; set; }
    public string? ReplyToMessageId { get; set; }
}
