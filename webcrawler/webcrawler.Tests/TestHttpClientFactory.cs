namespace webcrawler.Tests;

internal static class TestHttpClientFactory
{
    public static (HttpClient client, FakeHttpMessageHandler handler) CreateTestClient()
    {
        var handler = new FakeHttpMessageHandler();

        return (new HttpClient(handler), handler);
    }
}
