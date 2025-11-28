using GmailUnsubscriber.Infrastructure.Services;

namespace GmailUnsubscriber.Tests;

public class HtmlExtractionServiceTests
{
    private readonly HtmlExtractionService _service;

    public HtmlExtractionServiceTests()
    {
        _service = new HtmlExtractionService();
    }

    [Fact]
    public void ExtractUnsubscribeLinks_WithUnsubscribeLink_ReturnsUrl()
    {
        var html = @"
            <html>
            <body>
                <a href=""https://example.com/unsubscribe?id=123"">Unsubscribe</a>
            </body>
            </html>";

        var links = _service.ExtractUnsubscribeLinks(html).ToList();

        Assert.Single(links);
        Assert.Equal("https://example.com/unsubscribe?id=123", links[0]);
    }

    [Fact]
    public void ExtractUnsubscribeLinks_WithUnsubscribeInText_ReturnsUrl()
    {
        var html = @"
            <html>
            <body>
                <a href=""https://example.com/manage?id=123"">Click here to unsubscribe</a>
            </body>
            </html>";

        var links = _service.ExtractUnsubscribeLinks(html).ToList();

        Assert.Single(links);
        Assert.Equal("https://example.com/manage?id=123", links[0]);
    }

    [Fact]
    public void ExtractUnsubscribeLinks_WithOptOutLink_ReturnsUrl()
    {
        var html = @"
            <html>
            <body>
                <a href=""https://example.com/optout"">Opt Out</a>
            </body>
            </html>";

        var links = _service.ExtractUnsubscribeLinks(html).ToList();

        Assert.Single(links);
        Assert.Equal("https://example.com/optout", links[0]);
    }

    [Fact]
    public void ExtractUnsubscribeLinks_WithNoUnsubscribeLinks_ReturnsEmpty()
    {
        var html = @"
            <html>
            <body>
                <a href=""https://example.com"">Visit our website</a>
            </body>
            </html>";

        var links = _service.ExtractUnsubscribeLinks(html).ToList();

        Assert.Empty(links);
    }

    [Fact]
    public void ExtractUnsubscribeLinks_WithMultipleLinks_ReturnsDistinctUrls()
    {
        var html = @"
            <html>
            <body>
                <a href=""https://example.com/unsubscribe"">Unsubscribe</a>
                <a href=""https://example.com/unsubscribe"">Unsubscribe here</a>
                <a href=""https://example.com/optout"">Opt Out</a>
            </body>
            </html>";

        var links = _service.ExtractUnsubscribeLinks(html).ToList();

        Assert.Equal(2, links.Count);
    }

    [Fact]
    public void ExtractUnsubscribeLinks_WithEmptyHtml_ReturnsEmpty()
    {
        var links = _service.ExtractUnsubscribeLinks(string.Empty).ToList();

        Assert.Empty(links);
    }

    [Fact]
    public void ExtractUnsubscribeLinks_WithNullHtml_ReturnsEmpty()
    {
        var links = _service.ExtractUnsubscribeLinks(null!).ToList();

        Assert.Empty(links);
    }

    [Fact]
    public void ParseListUnsubscribeHeader_WithHttpUrl_ReturnsUrl()
    {
        var header = "<https://example.com/unsubscribe?token=abc123>";

        var result = _service.ParseListUnsubscribeHeader(header);

        Assert.Equal("https://example.com/unsubscribe?token=abc123", result);
    }

    [Fact]
    public void ParseListUnsubscribeHeader_WithMailto_ReturnsMailto()
    {
        var header = "<mailto:unsubscribe@example.com?subject=unsubscribe>";

        var result = _service.ParseListUnsubscribeHeader(header);

        Assert.Equal("mailto:unsubscribe@example.com?subject=unsubscribe", result);
    }

    [Fact]
    public void ParseListUnsubscribeHeader_WithBothUrlAndMailto_ReturnsHttpUrl()
    {
        var header = "<mailto:unsubscribe@example.com>, <https://example.com/unsubscribe>";

        var result = _service.ParseListUnsubscribeHeader(header);

        Assert.Equal("https://example.com/unsubscribe", result);
    }

    [Fact]
    public void ParseListUnsubscribeHeader_WithEmptyHeader_ReturnsNull()
    {
        var result = _service.ParseListUnsubscribeHeader(string.Empty);

        Assert.Null(result);
    }

    [Fact]
    public void ParseListUnsubscribeHeader_WithInvalidFormat_ReturnsNull()
    {
        var header = "not a valid header";

        var result = _service.ParseListUnsubscribeHeader(header);

        Assert.Null(result);
    }
}
