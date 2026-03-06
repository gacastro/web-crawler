# WebCrawler

A simple web crawler built with .NET 8 that crawls websites within a single subdomain.

## Features

- Crawls all pages within a specified subdomain
- Prints each URL visited
- Lists all links found on each page
- Respects domain boundaries (won't follow external links)

## Requirements

- .NET 8.0 SDK

## Usage

### Run with a URL argument:

```bash
dotnet run https://books.toscrape.com/
```

### Run interactively:

```bash
dotnet run
```

Then enter the URL when prompted. You can omit the scheme (e.g., `books.toscrape.com`) and the crawler will automatically use `https://`.

### Run the tests:

```bash
dotnet test
```

This will run the full test suite, including unit tests for link extraction, domain checking, and integration tests that verify the crawler's behavior with simulated HTTP responses.

> **Note:** You don't need to run `dotnet build` explicitly - both `dotnet run` and `dotnet test` will automatically build the project if needed.

## How It Works

When you start the crawler with a URL like `https://books.toscrape.com/`:

1. **Visit the starting page** - The crawler fetches the HTML content from your starting URL
2. **Extract all links** - It parses the HTML and finds every link on the page
3. **Print what it found** - The crawler outputs the current URL and all discovered links
4. **Filter and queue** - Links are checked to ensure they're on the same subdomain and point to HTML pages, then added to the queue
5. **Repeat** - The crawler picks the next URL from the queue and starts over
6. **Track progress** - Already-visited URLs are remembered to avoid duplicates
7. **Finish** - Once all reachable pages on the subdomain are visited, the crawler prints a summary

**The crawler stays within boundaries:**
- ✅ `books.toscrape.com/about` - Same subdomain, will visit
- ✅ `books.toscrape.com/blog/post-1` - Same subdomain, will visit
- ❌ `toscrape.com` - Different subdomain, will skip
- ❌ `community.toscrape.com` - Different subdomain, will skip
- ❌ `facebook.com` - External domain, will skip

**Safety features:**
- Skips non-HTML files (PDFs, images, etc.)
- Enforces a 2MB size limit per page to avoid memory issues
- Runs up to 5 concurrent requests for faster crawling

## Future Optimizations

- Politeness: add request throttling, configurable delays, and sensible concurrency caps.
- Robots compliance: fetch and honor `robots.txt` before crawling.
- Resilience: retries with exponential backoff for transient failures, plus circuit breaker behavior during outages.
- URL handling: better normalization (query parameters, trailing slashes, canonicalization, and fragments).
- Concurrency testing: use the fake HTTP handler to assert and measure concurrent request counts.
- Error logging: structured logs with request context and response codes.
- Graceful shutdown: handle signals and allow in-flight work to finish cleanly.
- Checkpointing: persist the crawl queue and visited set to resume after crashes.
