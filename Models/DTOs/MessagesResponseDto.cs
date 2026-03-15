namespace MessengerClient.Models.DTOs;

public class MessagesResponseDto
{
    public List<MessageDto> Messages { get; set; } = new List<MessageDto>();
    public bool HasMore { get; set; }
    public string? NextCursor { get; set; }
}
