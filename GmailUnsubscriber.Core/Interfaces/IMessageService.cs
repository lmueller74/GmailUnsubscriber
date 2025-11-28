using GmailUnsubscriber.Core.Models;

namespace GmailUnsubscriber.Core.Interfaces;

public interface IMessageService
{
    UnsubscribeInfo? ExtractUnsubscribeInfo(EmailMessage message);
}
