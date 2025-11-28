using System.Text.RegularExpressions;
using GmailUnsubscriber.Core.Interfaces;
using HtmlAgilityPack;

namespace GmailUnsubscriber.Infrastructure.Services;

public partial class HtmlExtractionService : IHtmlExtractionService
{
    public IEnumerable<string> ExtractUnsubscribeLinks(string htmlContent)
    {
        var links = new List<string>();

        if (string.IsNullOrWhiteSpace(htmlContent))
        {
            return links;
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(htmlContent);

        var anchorNodes = doc.DocumentNode.SelectNodes("//a[@href]");
        if (anchorNodes == null)
        {
            return links;
        }

        foreach (var anchor in anchorNodes)
        {
            var href = anchor.GetAttributeValue("href", string.Empty);
            var text = anchor.InnerText?.ToLowerInvariant() ?? string.Empty;

            if (IsUnsubscribeLink(href, text))
            {
                links.Add(href);
            }
        }

        return links.Distinct();
    }

    private static bool IsUnsubscribeLink(string href, string text)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        var hrefLower = href.ToLowerInvariant();
        var textLower = text.ToLowerInvariant();

        return hrefLower.Contains("unsubscribe") ||
               hrefLower.Contains("optout") ||
               hrefLower.Contains("opt-out") ||
               hrefLower.Contains("remove") ||
               textLower.Contains("unsubscribe") ||
               textLower.Contains("opt out") ||
               textLower.Contains("opt-out");
    }

    public string? ParseListUnsubscribeHeader(string headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return null;
        }

        var httpMatch = HttpUrlRegex().Match(headerValue);
        if (httpMatch.Success)
        {
            return httpMatch.Groups[1].Value;
        }

        var mailtoMatch = MailtoRegex().Match(headerValue);
        if (mailtoMatch.Success)
        {
            return mailtoMatch.Groups[1].Value;
        }

        return null;
    }

    [GeneratedRegex(@"<(https?://[^>]+)>", RegexOptions.IgnoreCase)]
    private static partial Regex HttpUrlRegex();

    [GeneratedRegex(@"<(mailto:[^>]+)>", RegexOptions.IgnoreCase)]
    private static partial Regex MailtoRegex();
}
