using GmailUnsubscriber.Core.Models;

namespace GmailUnsubscriber.Core.Interfaces;

public interface IGmailClient
{
    Task<IEnumerable<EmailMessage>> SearchAsync(string query, int maxResults);
    Task<EmailMessage?> GetMessageAsync(string messageId);
    Task ApplyLabelAsync(string messageId, string labelName);
    Task RemoveLabelAsync(string messageId, string labelName);
    Task RemoveFromInboxAsync(string messageId);
    Task SendEmailAsync(string to, string subject, string body);
}
