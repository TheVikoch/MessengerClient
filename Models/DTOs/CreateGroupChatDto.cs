namespace MessengerClient.Models.DTOs;

public class CreateGroupChatDto
{
    public string Name { get; set; } = string.Empty;
    public List<string> MemberEmails { get; set; } = new List<string>();
}
