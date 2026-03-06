using System.Net;

namespace webcrawler.Tests;

internal class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly 
        Dictionary<
            (string url, HttpMethod? method),
            (HttpStatusCode statusCode, string content, string? contentType, long? contentLength)
        > 
        _responses = new(new ResponseKeyComparer());

    public void AddResponse(string url,
        string content,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string? contentType = "text/html; charset=utf-8",
        long? contentLength = null)
    {
        _responses[(url, null)] = (statusCode, content, contentType, contentLength);
    }

    public void AddResponse(
        HttpMethod method,
        string url,
        string content,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string? contentType = "text/html; charset=utf-8",
        long? contentLength = null)
    {
        _responses[(url, method)] = (statusCode, content, contentType, contentLength);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.ToString() ?? string.Empty;

        // Try method-specific response first, then fall back to generic (null method)
        var response = 
            _responses.TryGetValue((url, request.Method), out var methodSpecific)
                ? methodSpecific
                : _responses.TryGetValue((url, null), out var generic)
                    ? generic
                    : (HttpStatusCode.NotFound, "", null, null);

        return await Task.FromResult(CreateResponse(response, request.Method));
    }

    private class ResponseKeyComparer : IEqualityComparer<(string url, HttpMethod? method)>
    {
        //  case‑insensitive on URL and supports a method fallback
        public bool Equals((string url, HttpMethod? method) x, (string url, HttpMethod? method) y)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(x.url, y.url) &&
                   (x.method == y.method || x.method == null || y.method == null);
        }

        public int GetHashCode((string url, HttpMethod? method) obj)
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.url) ^ (obj.method?.GetHashCode() ?? 0);
        }
    }

    private static HttpResponseMessage CreateResponse(
        (HttpStatusCode statusCode, string content, string? contentType, long? contentLength) response,
        HttpMethod method)
    {
        var (statusCode, content, contentType, contentLength) = response;
        var httpResponse = new HttpResponseMessage(statusCode);

        var contentBytes = System.Text.Encoding.UTF8.GetBytes(content);
        var length = contentLength ?? contentBytes.Length;
        httpResponse.Content.Headers.ContentLength = length;

        httpResponse.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(
                contentType!.Split(";")[0].Trim()
            )
            {
                CharSet = contentType.Split("charset=")[1].Split(";")[0].Trim()
            };


        if (method == HttpMethod.Get)
        {
            httpResponse.Content = new ByteArrayContent(contentBytes);
        }

        return httpResponse;
    }
}