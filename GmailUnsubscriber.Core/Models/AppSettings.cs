namespace GmailUnsubscriber.Core.Models;

public class AppSettings
{
    public string SearchQuery { get; set; } = "label:unsubscribe";
    public string SourceLabel { get; set; } = "unsubscribe";
    public string ProcessedLabel { get; set; } = "Unsubscribed";
    public string FailedLabel { get; set; } = "unsubscribe-failed";
    public int MaxMessagesPerRun { get; set; } = 500;
}
