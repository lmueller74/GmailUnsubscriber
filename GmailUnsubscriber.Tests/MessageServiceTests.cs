using GmailUnsubscriber.Core.Models;
using GmailUnsubscriber.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace GmailUnsubscriber.Tests;

public class MessageServiceTests
{
    private readonly MessageService _service;

    public MessageServiceTests()
    {
        var htmlExtractionService = new HtmlExtractionService();
        _service = new MessageService(
            htmlExtractionService,
            NullLogger<MessageService>.Instance);
    }

    [Fact]
    public void ExtractUnsubscribeInfo_WithListUnsubscribeHeader_ReturnsInfo()
    {
        var message = new EmailMessage
        {
            Id = "msg123",
            Subject = "Test Email",
            From = "test@example.com",
            ListUnsubscribeHeader = "<https://example.com/unsubscribe?token=abc>"
        };

        var result = _service.ExtractUnsubscribeInfo(message);

        Assert.NotNull(result);
        Assert.Equal("https://example.com/unsubscribe?token=abc", result.Url);
        Assert.Equal(UnsubscribeMethod.Get, result.Method);
        Assert.Equal("msg123", result.MessageId);
    }

    [Fact]
    public void ExtractUnsubscribeInfo_WithMailtoHeader_ReturnsMailtoMethod()
    {
        var message = new EmailMessage
        {
            Id = "msg123",
            Subject = "Test Email",
            From = "test@example.com",
            ListUnsubscribeHeader = "<mailto:unsub@example.com?subject=unsubscribe>"
        };

        var result = _service.ExtractUnsubscribeInfo(message);

        Assert.NotNull(result);
        Assert.Equal("mailto:unsub@example.com?subject=unsubscribe", result.Url);
        Assert.Equal(UnsubscribeMethod.Mailto, result.Method);
    }

    [Fact]
    public void ExtractUnsubscribeInfo_WithHtmlBodyUnsubscribeLink_ReturnsInfo()
    {
        var message = new EmailMessage
        {
            Id = "msg123",
            Subject = "Test Email",
            From = "test@example.com",
            HtmlBody = @"<html><body><a href=""https://example.com/unsubscribe"">Unsubscribe</a></body></html>"
        };

        var result = _service.ExtractUnsubscribeInfo(message);

        Assert.NotNull(result);
        Assert.Equal("https://example.com/unsubscribe", result.Url);
        Assert.Equal(UnsubscribeMethod.Get, result.Method);
    }

    [Fact]
    public void ExtractUnsubscribeInfo_PrefersHeaderOverHtmlBody()
    {
        var message = new EmailMessage
        {
            Id = "msg123",
            Subject = "Test Email",
            From = "test@example.com",
            ListUnsubscribeHeader = "<https://header.example.com/unsub>",
            HtmlBody = @"<html><body><a href=""https://body.example.com/unsubscribe"">Unsubscribe</a></body></html>"
        };

        var result = _service.ExtractUnsubscribeInfo(message);

        Assert.NotNull(result);
        Assert.Equal("https://header.example.com/unsub", result.Url);
    }

    [Fact]
    public void ExtractUnsubscribeInfo_WithNoUnsubscribeInfo_ReturnsNull()
    {
        var message = new EmailMessage
        {
            Id = "msg123",
            Subject = "Test Email",
            From = "test@example.com",
            HtmlBody = @"<html><body><a href=""https://example.com"">Visit us</a></body></html>"
        };

        var result = _service.ExtractUnsubscribeInfo(message);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractUnsubscribeInfo_PreservesMessageMetadata()
    {
        var message = new EmailMessage
        {
            Id = "msg123",
            Subject = "Weekly Newsletter",
            From = "newsletter@example.com",
            ListUnsubscribeHeader = "<https://example.com/unsub>"
        };

        var result = _service.ExtractUnsubscribeInfo(message);

        Assert.NotNull(result);
        Assert.Equal("Weekly Newsletter", result.Subject);
        Assert.Equal("newsletter@example.com", result.From);
    }
}
