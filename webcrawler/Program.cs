namespace webcrawler;

public static class Program
{
    public static async Task Main(string[] args)
    {
        string startUrl;

        if (args.Length > 0)
        {
            startUrl = args[0];
        }
        else
        {
            Console.Write("Enter the starting URL to crawl: ");
            startUrl = Console.ReadLine() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(startUrl))
        {
            Console.WriteLine("Error: No URL provided.");
            return;
        }

        // to improve the UX we allow to type in url without a scheme
        if (!startUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
            !startUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            startUrl = "https://" + startUrl;
        }

        try
        {
            Console.WriteLine($"Starting crawler at: {startUrl}");
            Console.WriteLine("==========================================\n");

            using var httpClient = CreateHttpClient();
            var crawler = new WebCrawler(startUrl, httpClient, Console.Out);
            await crawler.CrawlAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            AllowAutoRedirect = false //don't want to chance visiting unintended urls 
        })
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        
        client.DefaultRequestHeaders.Add("User-Agent", "WebWalker/1.0");
        client.DefaultRequestHeaders.Add("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
        client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");

        return client;
    }
}
