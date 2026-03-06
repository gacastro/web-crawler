﻿using System.Collections.Concurrent;
using System.Net;
using System.Text;
using AngleSharp.Html.Parser;

namespace webcrawler;

public class WebCrawler
{
    private const int MaxContentLength = 2 * 1024 * 1024; // 2MB limit
    private const int MaxConcurrency = 5; // amount of concurrent requests

    private static readonly HashSet<string> AllowedHtmlExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".html", ".htm", ".php", ".asp", ".aspx", ".jsp", ".jspx", ".cfm", ".cgi", ".shtml"
    };

    private readonly ConcurrentDictionary<string, byte> _visitedUrls = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<string> _urlsToVisit = new();
    private readonly HttpClient _httpClient;
    private readonly string _domainToCrawl;
    private readonly TextWriter _writer;

    public WebCrawler(string startUrl, HttpClient httpClient, TextWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        if (!IsHtmlPage(startUrl))
        {
            throw new ArgumentException($"Starting URL must be an HTML page. Got: {startUrl}", nameof(startUrl));
        }

        var uri = new Uri(startUrl);
        _domainToCrawl = uri.Host;
        _visitedUrls.TryAdd(startUrl, 0); // at this point, starting url will be visited for sure
    }

    public async Task CrawlAsync()
    {
        var activeTasks = new List<Task> { ProcessUrlAsync(_visitedUrls.Keys.First()) };

        while (activeTasks.Count > 0 || !_urlsToVisit.IsEmpty)
        {
            // Before picking up a new url, ensure the amount of active tasks
            // is around our expectations
            while (activeTasks.Count >= MaxConcurrency)
            {
                await Task.WhenAny(activeTasks);
                activeTasks.RemoveAll(t => t.IsCompleted);
            }

            _urlsToVisit.TryDequeue(out var currentUrl);

            if (currentUrl != null)
            {
                activeTasks.Add(ProcessUrlAsync(currentUrl));
            }

            activeTasks.RemoveAll(t => t.IsCompleted);
        }

        await _writer.WriteLineAsync($"\n\nCrawling complete! Visited {_visitedUrls.Count} pages.");
    }

    private async Task ProcessUrlAsync(string currentUrl)
    {
        try
        {
            await _writer.WriteLineAsync($"\nVisiting: {currentUrl}");

            var html = await GetPageContentAsync(currentUrl);
            if (html == null)
            {
                await _writer.WriteLineAsync("Skipped: too large or download failed");
                return;
            }

            var links = ExtractLinks(html, currentUrl);

            await _writer.WriteLineAsync($"Found {links.Count} links:");

            foreach (var link in links)
            {
                await _writer.WriteLineAsync($"  - {link}");

                if (
                    !IsSameDomain(link) ||
                    !IsHtmlPage(link) ||
                    !_visitedUrls.TryAdd(link, 0) //already visited
                )
                {
                    continue;
                }

                await _writer.WriteLineAsync("    -> to visit");
                _urlsToVisit.Enqueue(link);
            }
        }
        catch (Exception ex)
        {
            await _writer.WriteLineAsync($"Error crawling {currentUrl}: {ex.Message}");
        }
    }

    private async Task<string?> GetPageContentAsync(string url)
    {
        // First, do a HEAD request to check Content-Length before downloading
        using var headResponse = await _httpClient.SendAsync(
            new HttpRequestMessage(HttpMethod.Head, url));

        if (!headResponse.IsSuccessStatusCode)
        {
            // If server doesn't support HEAD method, fall back to GET
            if (headResponse.StatusCode is HttpStatusCode.MethodNotAllowed
                or HttpStatusCode.NotImplemented)
            {
                return await GetPageWithGetAsync(url);
            }

            // All other failures: reject
            return null;
        }

        // Check content length before downloading
        if (headResponse.Content.Headers.ContentLength is > MaxContentLength)
        {
            return null; // Skip pages that are too large
        }

        return await GetPageWithGetAsync(url);
    }

    private async Task<string?> GetPageWithGetAsync(string url)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        // sometimes server report the content length as the compressed size
        // better to stream and enforce size as we read bytes
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var memoryStream = new MemoryStream();

        var buffer = new byte[64 * 1024];
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
        {
            if (memoryStream.Length + bytesRead > MaxContentLength)
            {
                return null;
            }

            await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead));
        }

        var encoding = GetEncodingFromContentType(response.Content.Headers.ContentType?.CharSet) ?? Encoding.UTF8;
        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream, encoding, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync();
    }

    private static Encoding? GetEncodingFromContentType(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset)) return null;
        try
        {
            return Encoding.GetEncoding(charset.Trim().Trim('"'));
        }
        catch
        {
            return null;
        }
    }

    internal static List<string> ExtractLinks(string html, string baseUrl)
    {
        var links = new List<string>();
        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);

        var anchorElements = document.QuerySelectorAll("a[href]");

        foreach (var anchor in anchorElements)
        {
            var href = anchor.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            try
            {
                // mostly to complete relative urls but will work just as fine for full urls
                // even with a different base
                var uri = new Uri(new Uri(baseUrl), href);

                if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                {
                    continue; // Skip non-http(s) links like mailto:, javascript:, etc.
                }

                // Build clean URL without section and port
                var uriBuilder = new UriBuilder(uri)
                {
                    Fragment = string.Empty,
                    Port = -1
                };

                var absoluteUrl = uriBuilder.ToString();
                links.Add(absoluteUrl);
            }
            catch
            {
                // Skip invalid URLs
            }
        }

        return links;
    }

    internal bool IsSameDomain(string url)
    {
        return
            Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Host.Equals(_domainToCrawl, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsHtmlPage(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var extension = Path.GetExtension(uri.AbsolutePath);

        // Whitelist approach: only allow known HTML extensions or no extension (assume HTML)
        return string.IsNullOrEmpty(extension) || AllowedHtmlExtensions.Contains(extension);
    }
}