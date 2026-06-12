using System.Net;
using System.Text;
using LoopQuest.Infrastructure.Strava;
using Microsoft.Extensions.Options;

namespace LoopQuest.Infrastructure.Tests.Strava;
// Arrange (set the stage) → Act (do the one thing) → Assert (check the outcome)
public class StravaClientTests
{
    // A forged Strava token reply ("""…""" is a raw string literal — multi-line, no escaping).
    // Deliberately realistic: snake_case names, epoch-seconds expires_at, and fields our code
    // ignores (token_type, expires_in) — the deserializer picks out what TokenResponse names
    // and skips the rest, exactly as it will against the real API.
    private const string TokenJson = """
        {
          "token_type": "Bearer",
          "expires_at": 1750000000,
          "expires_in": 21600,
          "refresh_token": "test_refresh",
          "access_token": "test_access",
          "athlete": { "id": 67890, "firstname": "Viv", "lastname": "M" }
        }
        """;

    private const string RefreshJson = """
        {
            "token_type": "Bearer",
            "expires_at": 1750000000,
            "expires_in": 21600,
            "refresh_token": "test_refresh",
             "access_token": "test_access"
        }
        """;

    // The DI container's job, done by hand: a real HttpClient around the fake engine (BaseAddress
    // set manually, as AddHttpClient does in production), and Options.Create(...) wrapping a
    // hand-built StravaOptions with no config system involved. The values are nonsense on
    // purpose — tests only assert they end up in the right places, not that they're real.
    private static StravaClient CreateClient(FakeHttpMessageHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://www.strava.com") },
        Options.Create(new StravaOptions
        {
            ClientId = "123",
            ClientSecret = "shh",
            RedirectUri = "http://localhost:5159/auth/strava/callback",
        }));

    [Fact]
    public async Task ExchangeCodeAsync_MapsTheTokenResponse()
    {
        // Arrange: load the forged reply into the fake courier.
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(TokenJson, Encoding.UTF8, "application/json"),
        });

        // Act: the "HTTP call" runs entirely in memory.
        var result = await CreateClient(handler).ExchangeCodeAsync("the-code", CancellationToken.None);

        // Assert: every line checks one translation — Strava's dialect → our records.
        Assert.Equal(67890, result.AthleteId);
        Assert.Equal("Viv M", result.AthleteDisplayName);                  // firstname + lastname glued
        Assert.Equal("test_access", result.Tokens.AccessToken);
        Assert.Equal("test_refresh", result.Tokens.RefreshToken);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_750_000_000), result.Tokens.ExpiresAt);
    }

    // 🧩 BuildAuthorizationUrl_ContainsClientIdRedirectUriAndScope  (no HTTP at all — assert on the string)

    [Fact]
    public void BuildAuthorizationUrl_ContainsClientIdRedirectUriAndScope()
    {
        // The handler is just constructor fodder — this method never sends anything.
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage());

        var url = CreateClient(handler).BuildAuthorizationUrl();
        Assert.Contains("client_id=123", url);
        Assert.Contains($"redirect_uri={Uri.EscapeDataString("http://localhost:5159/auth/strava/callback")}", url);
        Assert.Contains("scope=activity:read_all", url);
    }

    [Fact]
    public async Task ExchangeCodeAsync_PostsCredentialsCodeAndGrantType()
    {
        //Arrange (set the stage) → Act (do the one thing) → Assert (check the outcome)

        // Arrange: load the forged reply into the fake courier.
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(TokenJson, Encoding.UTF8, "application/json"),
        });
        // Act — result deliberately discarded: this test only cares what was SENT.
        await CreateClient(handler).ExchangeCodeAsync("the-code", CancellationToken.None);

        // Assert
        Assert.Contains("client_id=123", handler.LastRequestBody);
        Assert.Contains("client_secret=shh", handler.LastRequestBody);
        Assert.Contains("grant_type=authorization_code", handler.LastRequestBody);
        Assert.Contains("code=the-code", handler.LastRequestBody);
    }
    [Fact]
    public async Task RefreshAsync_MapsTokensAndUsesRefreshTokenGrant()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(RefreshJson, Encoding.UTF8, "application/json"),
        });
        // Act
        var result = await CreateClient(handler).RefreshAsync("old_refresh", CancellationToken.None);

        // Assert — receive direction: tokens mapped from the JSON. Note the returned refresh
        // token is the NEW one from the reply, not the "old_refresh" we spent — rotation, visible.
        Assert.Equal("test_access", result.AccessToken);
        Assert.Equal("test_refresh", result.RefreshToken);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_750_000_000), result.ExpiresAt);

        // Assert — send direction: we asked with the refresh grant, spending the old token.
        Assert.Contains("grant_type=refresh_token", handler.LastRequestBody);
        Assert.Contains("refresh_token=old_refresh", handler.LastRequestBody);
    }
}
