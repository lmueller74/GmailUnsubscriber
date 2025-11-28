namespace GmailUnsubscriber.Core.Models;

public class UnsubscribeInfo
{
    public required string Url { get; set; }
    public UnsubscribeMethod Method { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public required string MessageId { get; set; }
    public string? Subject { get; set; }
    public string? From { get; set; }
}
