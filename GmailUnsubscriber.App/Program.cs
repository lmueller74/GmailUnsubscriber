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

        // Command-line override: --nuke or --marked
        if (args.Contains("--nuke", StringComparer.OrdinalIgnoreCase))
            settings.Mode = "nuke";
        else if (args.Contains("--marked", StringComparer.OrdinalIgnoreCase))
            settings.Mode = "marked";

        var isNukeMode = settings.Mode.Equals("nuke", StringComparison.OrdinalIgnoreCase);
        var activeQuery = settings.GetActiveQuery();

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddConsole()
                .SetMinimumLevel(LogLevel.Debug);
        });

        var logger = loggerFactory.CreateLogger<Program>();

        // Display mode
        if (isNukeMode)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("*** NUKE MODE ***");
            Console.WriteLine("This will unsubscribe from ALL emails containing 'unsubscribe' in your inbox!");
            Console.ResetColor();
            Console.Write("\nAre you sure? (yes/no): ");
            var confirm = Console.ReadLine();
            if (!confirm?.Equals("yes", StringComparison.OrdinalIgnoreCase) ?? true)
            {
                Console.WriteLine("Aborted.");
                return;
            }
            Console.WriteLine();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("MARKED MODE - Processing only emails labeled 'unsubscribe'");
            Console.ResetColor();
            Console.WriteLine();
        }

        logger.LogInformation("Starting Gmail Unsubscriber");
        logger.LogInformation("Mode: {Mode}", settings.Mode.ToUpper());
        logger.LogInformation("Search Query: {Query}", activeQuery);
        logger.LogInformation("Label Name: {Label}", settings.LabelName);
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

        var messages = await gmailClient.SearchAsync(activeQuery, settings.MaxMessagesPerRun);

        foreach (var msg in messages)
        {
            Console.WriteLine($"Processing: {msg.Id}");
            Console.WriteLine($"  From: {msg.From}");
            Console.WriteLine($"  Subject: {msg.Subject}");

            var info = messageService.ExtractUnsubscribeInfo(msg);

            if (info == null)
            {
                Console.WriteLine($"  No unsubscribe info found - marking as failed");

                // Mark as failed and remove source label
                if (!isNukeMode && !string.IsNullOrEmpty(settings.SourceLabel))
                {
                    await gmailClient.ApplyLabelAsync(msg.Id, settings.FailedLabel);
                    await gmailClient.RemoveLabelAsync(msg.Id, settings.SourceLabel);
                }

                Console.WriteLine();
                continue;
            }

            Console.WriteLine($"  Unsubscribe URL: {info.Url}");
            Console.WriteLine($"  Method: {info.Method}");

            bool success = await unsubscribeService.ExecuteAsync(info);

            Console.WriteLine($"  Result: {(success ? "SUCCESS" : "FAILED")}");

            if (success)
            {
                await gmailClient.ApplyLabelAsync(msg.Id, settings.LabelName);
                await gmailClient.RemoveFromInboxAsync(msg.Id);

                // In marked mode, remove the source label so it won't be picked up again
                if (!isNukeMode && !string.IsNullOrEmpty(settings.SourceLabel))
                {
                    await gmailClient.RemoveLabelAsync(msg.Id, settings.SourceLabel);
                }

                Console.WriteLine($"  Labeled as '{settings.LabelName}' and archived");
            }
            else
            {
                // Mark as failed and remove source label
                if (!isNukeMode && !string.IsNullOrEmpty(settings.SourceLabel))
                {
                    await gmailClient.ApplyLabelAsync(msg.Id, settings.FailedLabel);
                    await gmailClient.RemoveLabelAsync(msg.Id, settings.SourceLabel);
                    Console.WriteLine($"  Labeled as '{settings.FailedLabel}'");
                }
            }

            Console.WriteLine();
        }

        logger.LogInformation("Gmail Unsubscriber completed");
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
