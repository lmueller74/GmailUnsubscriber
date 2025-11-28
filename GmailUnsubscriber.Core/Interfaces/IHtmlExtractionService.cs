namespace GmailUnsubscriber.Core.Interfaces;

public interface IHtmlExtractionService
{
    IEnumerable<string> ExtractUnsubscribeLinks(string htmlContent);
    string? ParseListUnsubscribeHeader(string headerValue);
}
