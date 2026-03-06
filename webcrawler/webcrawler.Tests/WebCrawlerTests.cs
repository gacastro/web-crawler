using System.Net;
using FluentAssertions;

namespace webcrawler.Tests;

public class WebCrawlerTests
{
    [Theory]
    [InlineData("https://crawlme.monzo.com/page1", true)]
    [InlineData("https://crawlme.monzo.com", true)]
    [InlineData("https://crawlme.monzo.com/path/to/page", true)]
    [InlineData("https://monzo.com/", false)]
    [InlineData("https://community.monzo.com/", false)]
    [InlineData("https://facebook.com/", false)]
    [InlineData("http://crawlme.monzo.com/", true)] // Same host, different scheme - allow it
    public void IsSameDomain_ShouldReturnCorrectResult(string url, bool expected)
    {
        // Arrange
        var crawler = new WebCrawler("https://crawlme.monzo.com/", new HttpClient(), TextWriter.Null);

        // Act
        var result = crawler.IsSameDomain(url);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsSameDomain_WithInvalidUrl_ShouldReturnFalse()
    {
        // Arrange
        var crawler = new WebCrawler("https://crawlme.monzo.com/", new HttpClient(), TextWriter.Null);

        // Act
        var result = crawler.IsSameDomain("not-a-valid-url");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ExtractLinks_ShouldExtractCompleteLinksFromHtml()
    {
        // Arrange
        const string html =
            """
                <html>
                    <body>
                        <a href="https://example.com/page1">Page 1</a>
                        <a href="https://example.com/page2">Page 2</a>
                        <a href="https://external.com/">External</a>
                    </body>
                </html>
            """;

        // Act
        var links = WebCrawler.ExtractLinks(html, "https://example.com/");

        // Assert
        links.Should().HaveCount(3);
        links.Should().ContainSingle(link => link == "https://example.com/page1");
        links.Should().ContainSingle(link => link == "https://example.com/page2");
        links.Should().ContainSingle(link => link == "https://external.com/");
    }

    [Fact]
    public void ExtractLinks_ShouldConvertRelativeUrlsToAbsolute()
    {
        // Arrange
        const string html =
            """
                <html>
                    <body>
                        <a href="/page1">Page 1</a>
                        <a href="page2">Page 2</a>
                        <a href="./page3">Page 3</a>
                        <a href="../page4">Page 4</a>
                    </body>
                </html>
            """;

        // Act
        var links = WebCrawler.ExtractLinks(html, "https://example.com/path/");

        // Assert
        links.Should().ContainSingle(link => link == "https://example.com/page1");
        links.Should().ContainSingle(link => link == "https://example.com/path/page2");
        links.Should().ContainSingle(link => link == "https://example.com/path/page3");
        links.Should().ContainSingle(link => link == "https://example.com/page4");
    }

    [Fact]
    public void ExtractLinks_ShouldRemoveFragments()
    {
        // Arrange
        const string html =
            """
                <html>
                    <body>
                        <a href="https://example.com/page1#section1">Page 1</a>
                        <a href="https://example.com/page2#section2">Page 2</a>
                    </body>
                </html>
            """;

        // Act
        var links = WebCrawler.ExtractLinks(html, "https://example.com/");

        // Assert
        links.Should().ContainSingle(link => link == "https://example.com/page1");
        links.Should().ContainSingle(link => link == "https://example.com/page2");
        links.Should().NotContain(link => link.Contains('#'));
    }

    [Fact]
    public void ExtractLinks_ShouldIgnoreLinksWithoutHref()
    {
        // Arrange
        const string html =
            """
                <html>
                    <body>
                        <a>No href</a>
                        <a href="">Empty href</a>
                        <a href="  ">Whitespace href</a>
                        <a href="https://example.com/valid">Valid</a>
                    </body>
                </html>
            """;

        // Act
        var links = WebCrawler.ExtractLinks(html, "https://example.com/");

        // Assert
        links.Should().HaveCount(1);
        links.Should().ContainSingle(link => link == "https://example.com/valid");
    }

    [Fact]
    public void ExtractLinks_WithNoLinks_ShouldReturnEmptyList()
    {
        // Arrange
        const string html =
            """
                <html>
                    <body>
                        <p>No links here</p>
                    </body>
                </html>
            """;

        // Act
        var links = WebCrawler.ExtractLinks(html, "https://example.com/");

        // Assert
        links.Should().BeEmpty();
    }

    [Fact]
    public void ExtractLinks_ShouldHandleComplexHtml()
    {
        // Arrange
        const string html =
            """
                <!DOCTYPE html>
                <html>
                    <head>
                        <title>Test Page</title>
                    </head>
                    <body>
                        <nav>
                            <a href="/">Home</a>
                            <a href="/about">About</a>
                        </nav>
                        <main>
                            <article>
                                <a href="/blog/post1">Post 1</a>
                                <a href="/blog/post2">Post 2</a>
                            </article>
                        </main>
                        <footer>
                            <a href="https://twitter.com/example">Twitter</a>
                            <a href="https://facebook.com/example">Facebook</a>
                        </footer>
                    </body>
                </html>
            """;

        // Act
        var links = WebCrawler.ExtractLinks(html, "https://example.com/");

        // Assert
        links.Should().HaveCount(6);
        links.Should().ContainSingle(link => link == "https://example.com/");
        links.Should().ContainSingle(link => link == "https://example.com/about");
        links.Should().ContainSingle(link => link == "https://example.com/blog/post1");
        links.Should().ContainSingle(link => link == "https://example.com/blog/post2");
        links.Should().ContainSingle(link => link == "https://twitter.com/example");
        links.Should().ContainSingle(link => link == "https://facebook.com/example");
    }

    [Fact]
    public void ExtractLinks_WithQueryParameters_ShouldPreserveThem()
    {
        // Arrange
        const string html =
            """
                <html>
                    <body>
                        <a href="/search?q=test&page=1">Search</a>
                        <a href="https://example.com/page?id=123">Page</a>
                    </body>
                </html>
            """;

        // Act
        var links = WebCrawler.ExtractLinks(html, "https://example.com/");

        // Assert
        links.Should().ContainSingle(link => link == "https://example.com/search?q=test&page=1");
        links.Should().ContainSingle(link => link == "https://example.com/page?id=123");
    }

    [Fact]
    public void ExtractLinks_ShouldIgnoreNonHttpSchemes()
    {
        // Arrange
        const string html =
            """
                <html>
                    <body>
                        <a href="javascript:void(0)">JavaScript</a>
                        <a href="mailto:test@example.com">Email</a>
                        <a href="https://example.com/valid">Valid</a>
                    </body>
                </html>
            """;

        // Act
        var links = WebCrawler.ExtractLinks(html, "https://example.com/");

        // Assert
        links.Should().HaveCount(1);
        links.Should().ContainSingle(link => link == "https://example.com/valid");
    }

    [Theory]
    [InlineData("https://example.com/page", true)]
    [InlineData("https://example.com", true)]
    [InlineData("https://example.com/index.html", true)]
    [InlineData("https://example.com/index.php", true)]
    [InlineData("https://example.com/index.asp", true)]
    [InlineData("https://example.com/assets/file.pdf", false)]
    [InlineData("https://example.com/assets/image.png", false)]
    [InlineData("https://example.com/assets/archive.zip", false)]
    [InlineData("mailto:test@example.com", false)]
    [InlineData("javascript:void(0)", false)]
    [InlineData("not-a-url", false)]
    public void IsHtmlPage_ShouldWorkAsExpected(string url, bool expected)
    {
        // Act
        var result = WebCrawler.IsHtmlPage(url);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task CrawlAsync_ShouldCrawlMultiplePagesOnSameDomain()
    {
        // Arrange
        var (httpClient, handler) = TestHttpClientFactory.CreateTestClient();
        handler.AddResponse("https://example.com/", """
            <html>
                <body>
                    <a href="/page1">Page 1</a>
                    <a href="/page2">Page 2</a>
                </body>
            </html>
            """);
        handler.AddResponse("https://example.com/page1", """
            <html>
                <body>
                    <a href="/page3">Page 3</a>
                </body>
            </html>
            """);
        handler.AddResponse("https://example.com/page2", """
            <html>
                <body>
                    <p>No links here</p>
                </body>
            </html>
            """);
        handler.AddResponse("https://example.com/page3", """
            <html>
                <body>
                    <p>Final page</p>
                </body>
            </html>
            """);

        var output = new StringWriter();
        var crawler = new WebCrawler("https://example.com/", httpClient, output);

        // Act
        await crawler.CrawlAsync();
        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines.Should().ContainSingle(line => line == "Visiting: https://example.com/");
        lines.Should().ContainSingle(line => line.Contains("Visiting: https://example.com/page1"));
        lines.Should().ContainSingle(line => line.Contains("Visiting: https://example.com/page2"));
        lines.Should().ContainSingle(line => line.Contains("Visiting: https://example.com/page3"));
        lines.Should().ContainSingle(line => line.Contains("Visited 4 pages"));

        httpClient.Dispose();
    }

    [Fact]
    public async Task CrawlAsync_ShouldNotFollowExternalLinks()
    {
        // Arrange
        var (httpClient, handler) = TestHttpClientFactory.CreateTestClient();
        handler.AddResponse("https://example.com/", """
            <html>
                <body>
                    <a href="https://external.com/page">External</a>
                    <a href="/page1">Internal</a>
                </body>
            </html>
            """);
        handler.AddResponse("https://example.com/page1", """
            <html>
                <body>
                    <p>Internal page</p>
                </body>
            </html>
            """);

        var output = new StringWriter();
        var crawler = new WebCrawler("https://example.com/", httpClient, output);

        // Act
        await crawler.CrawlAsync();
        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines.Should().ContainSingle(line => line == "Visiting: https://example.com/");
        lines.Should().ContainSingle(line => line.Contains("Visiting: https://example.com/page1"));
        lines.Should().NotContain(line => line.Contains("Visiting: https://external.com/page"));
        lines.Should().ContainSingle(line => line.Contains("Visited 2 pages"));

        httpClient.Dispose();
    }

    [Fact]
    public async Task CrawlAsync_ShouldNotFollowNonHtmlLinks()
    {
        // Arrange
        var (httpClient, handler) = TestHttpClientFactory.CreateTestClient();
        handler.AddResponse("https://example.com/", """
            <html>
                <body>
                    <a href="/file.pdf">PDF File</a>
                    <a href="/image.jpg">Image</a>
                    <a href="/page">HTML Page</a>
                </body>
            </html>
            """);
        handler.AddResponse("https://example.com/page", """
            <html>
                <body>
                    <p>HTML page</p>
                </body>
            </html>
            """);

        var output = new StringWriter();
        var crawler = new WebCrawler("https://example.com/", httpClient, output);

        // Act
        await crawler.CrawlAsync();
        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines.Should().ContainSingle(line => line == "Visiting: https://example.com/");
        lines.Should().ContainSingle(line => line.Contains("Visiting: https://example.com/page"));
        lines.Should().NotContain(line => line.Contains("Visiting: https://example.com/file.pdf"));
        lines.Should().NotContain(line => line.Contains("Visiting: https://example.com/image.jpg"));
        lines.Should().ContainSingle(line => line.Contains("Visited 2 pages"));

        httpClient.Dispose();
    }

    [Fact]
    public async Task CrawlAsync_ShouldNotVisitSamePageTwice()
    {
        // Arrange
        var (httpClient, handler) = TestHttpClientFactory.CreateTestClient();
        handler.AddResponse("https://example.com/", """
            <html>
                <body>
                    <a href="/page">Link 1</a>
                    <a href="/page">Link 2</a>
                    <a href="https://example.com/">Link 3</a>
                </body>
            </html>
            """);
        handler.AddResponse("https://example.com/page", """
            <html>
                <body>
                    <p>Page</p>
                </body>
            </html>
            """);

        var output = new StringWriter();
        var crawler = new WebCrawler("https://example.com/", httpClient, output);

        // Act
        await crawler.CrawlAsync();
        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines.Should().ContainSingle(line => line == "Visiting: https://example.com/");
        lines.Should().ContainSingle(line => line.Contains("Visiting: https://example.com/page"));
        lines.Should().ContainSingle(line => line.Contains("Visited 2 pages"));

        httpClient.Dispose();
    }

    [Fact]
    public async Task CrawlAsync_ShouldSkipPagesThatAreTooLarge()
    {
        const long maxContentLengthBytes = 2L * 1024 * 1024; // 2MB limit (matches crawler)
        var (httpClient, handler) = TestHttpClientFactory.CreateTestClient();

        handler.AddResponse(
            "https://example.com/",
            "<html><body><p>Large</p></body></html>",
            HttpStatusCode.OK,
            "text/html; charset=utf-8",
            maxContentLengthBytes + 1);

        var output = new StringWriter();
        var crawler = new WebCrawler("https://example.com/", httpClient, output);

        await crawler.CrawlAsync();
        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        lines.Should().ContainSingle(line => line.Contains("Visiting: https://example.com/"));
        lines.Should().ContainSingle(line => line.Contains("Skipped: too large or download failed"));
        lines.Should().ContainSingle(line => line.Contains("Visited 1 pages"));

        httpClient.Dispose();
    }

    [Fact]
    public async Task CrawlAsync_ShouldStopStreamingWhenContentExceedsMaxSize()
    {
        const long maxContentLengthBytes = 2L * 1024 * 1024; // 2MB limit
        var (httpClient, handler) = TestHttpClientFactory.CreateTestClient();

        // Create content larger than max (simulating a case where Content-Length header is absent or incorrect)
        var largeContent = new string('x', (int)(maxContentLengthBytes + 1024));
        var htmlContent = $"<html><body><p>{largeContent}</p></body></html>";

        // Don't set Content-Length so it falls through to streaming
        handler.AddResponse(
            HttpMethod.Head,
            "https://example.com/",
            string.Empty,
            HttpStatusCode.MethodNotAllowed);
        
        handler.AddResponse(
            HttpMethod.Get,
            "https://example.com/",
            htmlContent); // No Content-Length header

        var output = new StringWriter();
        var crawler = new WebCrawler("https://example.com/", httpClient, output);

        await crawler.CrawlAsync();
        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        lines.Should().ContainSingle(line => line.Contains("Visiting: https://example.com/"));
        lines.Should().ContainSingle(line => line.Contains("Skipped: too large or download failed"));
        lines.Should().ContainSingle(line => line.Contains("Visited 1 pages"));

        httpClient.Dispose();
    }

    [Fact]
    public async Task CrawlAsync_ShouldSkipWhenResponseIsNotFound()
    {
        var (httpClient, handler) = TestHttpClientFactory.CreateTestClient();

        handler.AddResponse(
            "https://example.com/",
            "<html><body><p>Missing</p></body></html>",
            HttpStatusCode.NotFound);

        var output = new StringWriter();
        var crawler = new WebCrawler("https://example.com/", httpClient, output);

        await crawler.CrawlAsync();
        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        lines.Should().ContainSingle(line => line.Contains("Visiting: https://example.com/"));
        lines.Should().ContainSingle(line => line.Contains("Skipped: too large or download failed"));
        lines.Should().ContainSingle(line => line.Contains("Visited 1 pages"));

        httpClient.Dispose();
    }

    [Fact]
    public async Task CrawlAsync_ShouldReturnContentWhenGetIsSuccessful()
    {
        var (httpClient, handler) = TestHttpClientFactory.CreateTestClient();

        handler.AddResponse(HttpMethod.Head, "https://example.com/", string.Empty, HttpStatusCode.MethodNotAllowed);
        handler.AddResponse(HttpMethod.Get, "https://example.com/", "<html><body><p>Ok</p></body></html>");

        var output = new StringWriter();
        var crawler = new WebCrawler("https://example.com/", httpClient, output);

        await crawler.CrawlAsync();
        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        lines.Should().ContainSingle(line => line.Contains("Visiting: https://example.com/"));
        lines.Should().NotContain(line => line.Contains("Skipped: too large or download failed"));
        lines.Should().ContainSingle(line => line.Contains("Visited 1 pages"));

        httpClient.Dispose();
    }

    [Fact]
    public async Task CrawlAsync_ShouldSkipWhenGetIsNotSuccessful()
    {
        var (httpClient, handler) = TestHttpClientFactory.CreateTestClient();

        handler.AddResponse(HttpMethod.Head, "https://example.com/", string.Empty, HttpStatusCode.MethodNotAllowed);
        handler.AddResponse(HttpMethod.Get, "https://example.com/", "<html><body><p>Missing</p></body></html>", HttpStatusCode.NotFound);

        var output = new StringWriter();
        var crawler = new WebCrawler("https://example.com/", httpClient, output);

        await crawler.CrawlAsync();
        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        lines.Should().ContainSingle(line => line.Contains("Visiting: https://example.com/"));
        lines.Should().ContainSingle(line => line.Contains("Skipped: too large or download failed"));
        lines.Should().ContainSingle(line => line.Contains("Visited 1 pages"));

        httpClient.Dispose();
    }

    [Fact]
    public async Task CrawlAsync_ShouldSkipWhenHeadRequestReturnsNotFound()
    {
        var (httpClient, handler) = TestHttpClientFactory.CreateTestClient();

        handler.AddResponse(
            HttpMethod.Head,
            "https://example.com/",
            string.Empty,
            HttpStatusCode.NotFound);

        var output = new StringWriter();
        var crawler = new WebCrawler("https://example.com/", httpClient, output);

        await crawler.CrawlAsync();
        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        lines.Should().ContainSingle(line => line.Contains("Visiting: https://example.com/"));
        lines.Should().ContainSingle(line => line.Contains("Skipped: too large or download failed"));
        lines.Should().ContainSingle(line => line.Contains("Visited 1 pages"));

        httpClient.Dispose();
    }
}
