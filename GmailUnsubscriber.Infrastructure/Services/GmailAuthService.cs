using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace GmailUnsubscriber.Infrastructure.Services;

public class GmailAuthService
{
    private static readonly string[] Scopes =
    {
        GmailService.Scope.GmailModify,
        GmailService.Scope.GmailSend,
        GmailService.Scope.GmailLabels
    };

    private const string ApplicationName = "GmailUnsubscriber";

    public async Task<GmailService> AuthenticateAsync(string credentialsPath)
    {
        UserCredential credential;

        using (var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read))
        {
            var tokenStorePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GmailUnsubscriber.Auth");

            credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                Scopes,
                "user",
                CancellationToken.None,
                new FileDataStore(tokenStorePath, true));
        }

        return new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName
        });
    }
}
