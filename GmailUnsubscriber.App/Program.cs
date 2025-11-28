using GmailUnsubscriber.Core.Interfaces;
using GmailUnsubscriber.Core.Models;
using GmailUnsubscriber.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GmailUnsubscriber.App;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Gmail Unsubscriber");
        Console.WriteLine("==================\n");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var settings = configuration.Get<AppSettings>() ?? new AppSettings();

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddConsole()
                .SetMinimumLevel(LogLevel.Debug);
        });

        var logger = loggerFactory.CreateLogger<Program>();

        logger.LogInformation("Starting Gmail Unsubscriber");
        logger.LogInformation("Search Query: {Query}", settings.SearchQuery);
        logger.LogInformation("Processed Label: {Label}", settings.ProcessedLabel);
        logger.LogInformation("Max Messages Per Run: {Max}", settings.MaxMessagesPerRun);

        var credentialsPath = Path.Combine(Directory.GetCurrentDirectory(), "credentials.json");
        if (!File.Exists(credentialsPath))
        {
            logger.LogError("credentials.json not found at {Path}", credentialsPath);
            logger.LogError("Please download OAuth credentials from Google Cloud Console");
            return;
        }

        var authService = new GmailAuthService();
        var gmailService = await authService.AuthenticateAsync(credentialsPath);

        logger.LogInformation("Successfully authenticated with Gmail API");

        var gmailClient = new GmailClient(
            gmailService,
            loggerFactory.CreateLogger<GmailClient>());

        var htmlExtractionService = new HtmlExtractionService();

        IMessageService messageService = new MessageService(
            htmlExtractionService,
            loggerFactory.CreateLogger<MessageService>());

        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        IUnsubscribeService unsubscribeService = new HttpUnsubscribeService(
            httpClient,
            gmailClient,
            loggerFactory.CreateLogger<HttpUnsubscribeService>());

        Console.WriteLine("\nSearching for messages...\n");

        var messages = await gmailClient.SearchAsync(settings.SearchQuery, settings.MaxMessagesPerRun);

        foreach (var msg in messages)
        {
            Console.WriteLine($"Processing: {msg.Id}");
            Console.WriteLine($"  From: {msg.From}");
            Console.WriteLine($"  Subject: {msg.Subject}");

            var info = messageService.ExtractUnsubscribeInfo(msg);

            if (info == null)
            {
                Console.WriteLine($"  No unsubscribe info found - marking as failed");
                await gmailClient.ApplyLabelAsync(msg.Id, settings.FailedLabel);
                await gmailClient.RemoveLabelAsync(msg.Id, settings.SourceLabel);
                Console.WriteLine();
                continue;
            }

            Console.WriteLine($"  Unsubscribe URL: {info.Url}");
            Console.WriteLine($"  Method: {info.Method}");

            bool success = await unsubscribeService.ExecuteAsync(info);

            Console.WriteLine($"  Result: {(success ? "SUCCESS" : "FAILED")}");

            if (success)
            {
                await gmailClient.ApplyLabelAsync(msg.Id, settings.ProcessedLabel);
                await gmailClient.RemoveFromInboxAsync(msg.Id);
                await gmailClient.RemoveLabelAsync(msg.Id, settings.SourceLabel);
                Console.WriteLine($"  Labeled as '{settings.ProcessedLabel}' and archived");
            }
            else
            {
                await gmailClient.ApplyLabelAsync(msg.Id, settings.FailedLabel);
                await gmailClient.RemoveLabelAsync(msg.Id, settings.SourceLabel);
                Console.WriteLine($"  Labeled as '{settings.FailedLabel}'");
            }

            Console.WriteLine();
        }

        logger.LogInformation("Gmail Unsubscriber completed");
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
