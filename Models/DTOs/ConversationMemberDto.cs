namespace MessengerClient.Models.DTOs;

public class ConversationMemberDto
{
    public string UserId { get; set; } = string.Empty;
    public UserDto User { get; set; } = new UserDto();
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public bool IsPinned { get; set; }
}
