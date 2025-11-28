using GmailUnsubscriber.Core.Models;

namespace GmailUnsubscriber.Core.Interfaces;

public interface IUnsubscribeService
{
    Task<bool> ExecuteAsync(UnsubscribeInfo info);
}
