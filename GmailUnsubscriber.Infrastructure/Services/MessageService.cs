using GmailUnsubscriber.Core.Interfaces;
using GmailUnsubscriber.Core.Models;
using Microsoft.Extensions.Logging;

namespace GmailUnsubscriber.Infrastructure.Services;

public class MessageService : IMessageService
{
    private readonly IHtmlExtractionService _htmlExtractionService;
    private readonly ILogger<MessageService> _logger;

    public MessageService(IHtmlExtractionService htmlExtractionService, ILogger<MessageService> logger)
    {
        _htmlExtractionService = htmlExtractionService;
        _logger = logger;
    }

    public UnsubscribeInfo? ExtractUnsubscribeInfo(EmailMessage message)
    {
        if (!string.IsNullOrEmpty(message.ListUnsubscribeHeader))
        {
            var headerUrl = _htmlExtractionService.ParseListUnsubscribeHeader(message.ListUnsubscribeHeader);
            if (!string.IsNullOrEmpty(headerUrl))
            {
                var method = DetermineMethod(headerUrl);
                _logger.LogDebug("Found unsubscribe URL in List-Unsubscribe header: {Url}", headerUrl);

                return new UnsubscribeInfo
                {
                    Url = headerUrl,
                    Method = method,
                    MessageId = message.Id,
                    Subject = message.Subject,
                    From = message.From
                };
            }
        }

        if (!string.IsNullOrEmpty(message.HtmlBody))
        {
            var links = _htmlExtractionService.ExtractUnsubscribeLinks(message.HtmlBody);
            var firstLink = links.FirstOrDefault();

            if (!string.IsNullOrEmpty(firstLink))
            {
                _logger.LogDebug("Found unsubscribe URL in HTML body: {Url}", firstLink);

                return new UnsubscribeInfo
                {
                    Url = firstLink,
                    Method = DetermineMethod(firstLink),
                    MessageId = message.Id,
                    Subject = message.Subject,
                    From = message.From
                };
            }
        }

        _logger.LogDebug("No unsubscribe info found for message {MessageId}", message.Id);
        return null;
    }

    private static UnsubscribeMethod DetermineMethod(string url)
    {
        if (url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return UnsubscribeMethod.Mailto;
        }

        return UnsubscribeMethod.Get;
    }
}
