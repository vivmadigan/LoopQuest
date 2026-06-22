namespace LoopQuest.Infrastructure.Tests.Strava;

/// <summary>
/// A fake HttpMessageHandler for tests. HttpClient never touches the network itself — it hands every
/// request to its HttpMessageHandler, and that part is swappable: new HttpClient(handler). This fake
/// returns responses you pre-load, one per request in the order given, and records every request it
/// receives so tests can assert on what was sent. No network is ever touched.
/// </summary>
public sealed class FakeHttpMessageHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
{
    // The responses to hand back, in order: the first one passed answers the first request, and so on.
    private readonly Queue<HttpResponseMessage> _responses = new(responses);

    // Every request (and its body) that came through, kept so tests can check what was sent —
    // for example, that a paging loop made exactly two requests.
    public List<HttpRequestMessage> Requests { get; } = [];
    public List<string?> RequestBodies { get; } = [];

    // Shortcuts to the most recent request, so the older single-request tests stay simple.
    public HttpRequestMessage? LastRequest => Requests.Count > 0 ? Requests[^1] : null;
    public string? LastRequestBody => RequestBodies.Count > 0 ? RequestBodies[^1] : null;

    // Protected because only the HttpClient machinery calls it — every request lands here, not online.
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Record the request, reading its body NOW: the content is a stream that can't be re-read later.
        Requests.Add(request);
        RequestBodies.Add(request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken));

        // Hand back the next queued response (throws if a test queued fewer responses than requests made).
        return _responses.Dequeue();
    }
}
