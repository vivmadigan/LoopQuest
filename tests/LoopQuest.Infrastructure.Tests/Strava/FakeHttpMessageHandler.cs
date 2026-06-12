namespace LoopQuest.Infrastructure.Tests.Strava;

/// <summary>
/// A stand-in for HttpClient's real engine. HttpClient sends nothing itself — every request goes
/// through its HttpMessageHandler, and that engine is swappable: new HttpClient(handler). This fake
/// answers every request with one canned response and records what was asked, so tests can assert
/// on both sides of the conversation. No network is ever touched.
/// </summary>
public sealed class FakeHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
{
    // The "spy" half: what the code under test asked for, kept for the test's asserts.
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }

    // Protected because only the HttpClient machinery calls it — every request the client
    // makes lands here instead of the internet.
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        // Read the body NOW: request content is a stream that can't reliably be read later.
        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        return response;
    }
}
