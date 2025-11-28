using System.Text;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using GmailUnsubscriber.Core.Interfaces;
using GmailUnsubscriber.Core.Models;
using Microsoft.Extensions.Logging;

namespace GmailUnsubscriber.Infrastructure.Services;

public class GmailClient : IGmailClient
{
    private readonly GmailService _gmailService;
    private readonly ILogger<GmailClient> _logger;
    private const string UserId = "me";
    private string? _unsubscribedLabelId;

    public GmailClient(GmailService gmailService, ILogger<GmailClient> logger)
    {
        _gmailService = gmailService;
        _logger = logger;
    }

    public async Task<IEnumerable<EmailMessage>> SearchAsync(string query, int maxResults)
    {
        var messages = new List<EmailMessage>();

        var request = _gmailService.Users.Messages.List(UserId);
        request.Q = query;
        request.MaxResults = maxResults;

        var response = await request.ExecuteAsync();

        if (response.Messages == null)
        {
            _logger.LogInformation("No messages found matching query: {Query}", query);
            return messages;
        }

        foreach (var messageRef in response.Messages)
        {
            var message = await GetMessageAsync(messageRef.Id);
            if (message != null)
            {
                messages.Add(message);
            }
        }

        _logger.LogInformation("Found {Count} messages matching query", messages.Count);
        return messages;
    }

    public async Task<EmailMessage?> GetMessageAsync(string messageId)
    {
        var request = _gmailService.Users.Messages.Get(UserId, messageId);
        request.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Full;

        var gmailMessage = await request.ExecuteAsync();

        var emailMessage = new EmailMessage
        {
            Id = gmailMessage.Id
        };

        if (gmailMessage.Payload?.Headers != null)
        {
            foreach (var header in gmailMessage.Payload.Headers)
            {
                switch (header.Name.ToLowerInvariant())
                {
                    case "subject":
                        emailMessage.Subject = header.Value;
                        break;
                    case "from":
                        emailMessage.From = header.Value;
                        break;
                    case "list-unsubscribe":
                        emailMessage.ListUnsubscribeHeader = header.Value;
                        break;
                }
            }
        }

        ExtractBody(gmailMessage.Payload, emailMessage);

        return emailMessage;
    }

    private void ExtractBody(MessagePart? payload, EmailMessage emailMessage)
    {
        if (payload == null) return;

        if (payload.MimeType == "text/html" && !string.IsNullOrEmpty(payload.Body?.Data))
        {
            emailMessage.HtmlBody = DecodeBase64Url(payload.Body.Data);
        }
        else if (payload.MimeType == "text/plain" && !string.IsNullOrEmpty(payload.Body?.Data))
        {
            emailMessage.TextBody = DecodeBase64Url(payload.Body.Data);
        }

        if (payload.Parts != null)
        {
            foreach (var part in payload.Parts)
            {
                ExtractBody(part, emailMessage);
            }
        }
    }

    private static string DecodeBase64Url(string base64Url)
    {
        var base64 = base64Url.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        var bytes = Convert.FromBase64String(base64);
        return Encoding.UTF8.GetString(bytes);
    }

    public async Task ApplyLabelAsync(string messageId, string labelName)
    {
        var labelId = await GetOrCreateLabelIdAsync(labelName);

        var modifyRequest = new ModifyMessageRequest
        {
            AddLabelIds = new List<string> { labelId }
        };

        await _gmailService.Users.Messages.Modify(modifyRequest, UserId, messageId).ExecuteAsync();
        _logger.LogInformation("Applied label '{LabelName}' to message {MessageId}", labelName, messageId);
    }

    public async Task RemoveLabelAsync(string messageId, string labelName)
    {
        var labelId = await GetLabelIdAsync(labelName);
        if (labelId == null)
        {
            _logger.LogWarning("Label '{LabelName}' not found, cannot remove", labelName);
            return;
        }

        var modifyRequest = new ModifyMessageRequest
        {
            RemoveLabelIds = new List<string> { labelId }
        };

        await _gmailService.Users.Messages.Modify(modifyRequest, UserId, messageId).ExecuteAsync();
        _logger.LogInformation("Removed label '{LabelName}' from message {MessageId}", labelName, messageId);
    }

    public async Task RemoveFromInboxAsync(string messageId)
    {
        var modifyRequest = new ModifyMessageRequest
        {
            RemoveLabelIds = new List<string> { "INBOX" }
        };

        await _gmailService.Users.Messages.Modify(modifyRequest, UserId, messageId).ExecuteAsync();
        _logger.LogInformation("Removed message {MessageId} from inbox", messageId);
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        var message = new Message
        {
            Raw = Base64UrlEncode(CreateEmailRaw(to, subject, body))
        };

        await _gmailService.Users.Messages.Send(message, UserId).ExecuteAsync();
        _logger.LogInformation("Sent unsubscribe email to {To}", to);
    }

    private async Task<string?> GetLabelIdAsync(string labelName)
    {
        var labelsResponse = await _gmailService.Users.Labels.List(UserId).ExecuteAsync();
        var existingLabel = labelsResponse.Labels?.FirstOrDefault(l =>
            l.Name.Equals(labelName, StringComparison.OrdinalIgnoreCase));

        return existingLabel?.Id;
    }

    private async Task<string> GetOrCreateLabelIdAsync(string labelName)
    {
        if (!string.IsNullOrEmpty(_unsubscribedLabelId))
        {
            return _unsubscribedLabelId;
        }

        var labelId = await GetLabelIdAsync(labelName);
        if (labelId != null)
        {
            _unsubscribedLabelId = labelId;
            return _unsubscribedLabelId;
        }

        var newLabel = new Label
        {
            Name = labelName,
            LabelListVisibility = "labelShow",
            MessageListVisibility = "show"
        };

        var createdLabel = await _gmailService.Users.Labels.Create(newLabel, UserId).ExecuteAsync();
        _unsubscribedLabelId = createdLabel.Id;
        _logger.LogInformation("Created label '{LabelName}'", labelName);

        return _unsubscribedLabelId;
    }

    private static string CreateEmailRaw(string to, string subject, string body)
    {
        return $"To: {to}\r\n" +
               $"Subject: {subject}\r\n" +
               "Content-Type: text/plain; charset=utf-8\r\n\r\n" +
               body;
    }

    private static string Base64UrlEncode(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", "");
    }
}
