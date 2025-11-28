using System.Web;
using GmailUnsubscriber.Core.Interfaces;
using GmailUnsubscriber.Core.Models;
using Microsoft.Extensions.Logging;

namespace GmailUnsubscriber.Infrastructure.Services;

public class HttpUnsubscribeService : IUnsubscribeService
{
    private readonly HttpClient _httpClient;
    private readonly IGmailClient _gmailClient;
    private readonly ILogger<HttpUnsubscribeService> _logger;

    public HttpUnsubscribeService(
        HttpClient httpClient,
        IGmailClient gmailClient,
        ILogger<HttpUnsubscribeService> logger)
    {
        _httpClient = httpClient;
        _gmailClient = gmailClient;
        _logger = logger;
    }

    public async Task<bool> ExecuteAsync(UnsubscribeInfo info)
    {
        try
        {
            _logger.LogInformation("Executing unsubscribe for message {MessageId} using method {Method}",
                info.MessageId, info.Method);

            return info.Method switch
            {
                UnsubscribeMethod.Get => await ExecuteHttpGetAsync(info.Url),
                UnsubscribeMethod.Post => await ExecuteHttpPostAsync(info.Url),
                UnsubscribeMethod.Mailto => await ExecuteMailtoAsync(info.Url),
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute unsubscribe for message {MessageId}", info.MessageId);
            return false;
        }
    }

    private async Task<bool> ExecuteHttpGetAsync(string url)
    {
        _logger.LogDebug("Sending HTTP GET to {Url}", url);

        var response = await _httpClient.GetAsync(url);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("HTTP GET unsubscribe successful: {StatusCode}", response.StatusCode);
            return true;
        }

        _logger.LogWarning("HTTP GET unsubscribe returned status {StatusCode}", response.StatusCode);
        return response.StatusCode == System.Net.HttpStatusCode.Redirect ||
               response.StatusCode == System.Net.HttpStatusCode.MovedPermanently;
    }

    private async Task<bool> ExecuteHttpPostAsync(string url)
    {
        _logger.LogDebug("Sending HTTP POST to {Url}", url);

        var response = await _httpClient.PostAsync(url, new StringContent(string.Empty));

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("HTTP POST unsubscribe successful: {StatusCode}", response.StatusCode);
            return true;
        }

        _logger.LogWarning("HTTP POST unsubscribe returned status {StatusCode}", response.StatusCode);
        return false;
    }

    private async Task<bool> ExecuteMailtoAsync(string mailtoUrl)
    {
        var parsed = ParseMailtoUrl(mailtoUrl);
        if (parsed == null)
        {
            _logger.LogWarning("Failed to parse mailto URL: {Url}", mailtoUrl);
            return false;
        }

        var (to, subject, body) = parsed.Value;

        _logger.LogDebug("Sending unsubscribe email to {To} with subject: {Subject}", to, subject);

        await _gmailClient.SendEmailAsync(to, subject, body);
        return true;
    }

    private static (string to, string subject, string body)? ParseMailtoUrl(string mailtoUrl)
    {
        if (!mailtoUrl.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rest = mailtoUrl[7..];
        var parts = rest.Split('?', 2);
        var to = parts[0];

        var subject = "Unsubscribe";
        var body = "Please unsubscribe me from this mailing list.";

        if (parts.Length > 1)
        {
            var queryParams = HttpUtility.ParseQueryString(parts[1]);

            if (!string.IsNullOrEmpty(queryParams["subject"]))
            {
                subject = queryParams["subject"]!;
            }

            if (!string.IsNullOrEmpty(queryParams["body"]))
            {
                body = queryParams["body"]!;
            }
        }

        return (to, subject, body);
    }
}
