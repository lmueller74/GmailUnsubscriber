namespace GmailUnsubscriber.Core.Models;

public class EmailMessage
{
    public required string Id { get; set; }
    public string? Subject { get; set; }
    public string? From { get; set; }
    public string? ListUnsubscribeHeader { get; set; }
    public string? HtmlBody { get; set; }
    public string? TextBody { get; set; }
}
