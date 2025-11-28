namespace GmailUnsubscriber.Core.Models;

public class AppSettings
{
    public string Mode { get; set; } = "marked";
    public string MarkedQuery { get; set; } = "label:unsubscribe";
    public string SourceLabel { get; set; } = "unsubscribe";
    public string NukeQuery { get; set; } = "label:inbox \"unsubscribe\"";
    public string LabelName { get; set; } = "Unsubscribed";
    public string FailedLabel { get; set; } = "unsubscribe-failed";
    public int MaxMessagesPerRun { get; set; } = 5;

    public string GetActiveQuery() => Mode.ToLowerInvariant() == "nuke" ? NukeQuery : MarkedQuery;
}
