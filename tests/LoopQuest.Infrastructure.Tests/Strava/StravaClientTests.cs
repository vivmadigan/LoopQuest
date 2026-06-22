using System.Net;
using System.Text;
using System.Text.Json;
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

    // One activity, hand-written with a DISTINCT value per field so a swapped mapping (e.g. distance
    // landing in elevation) fails loudly. It's a JSON array ([ ... ]) because the endpoint returns a list.
    private const string OneActivityJson = """
    [
      {
        "id": 1234567890,
        "name": "Lunch Run",
        "sport_type": "TrailRun",
        "start_date_local": "2026-06-08T07:30:00Z",
        "distance": 8012.3,
        "total_elevation_gain": 142.0
      }
    ]
    """;

    // Builds a JSON page of `count` activities. The field values are filler — the paging tests only
    // care how MANY items a page has, not what's in them (the mapping test uses OneActivityJson instead).
    private static string ActivitiesJson(int count)
    {
        var activities = Enumerable.Range(1, count).Select(i => new
        {
            id = i,
            name = $"Run {i}",
            sport_type = "Run",                       // snake_case on purpose — matches the wire shape
            start_date_local = "2026-06-08T07:30:00Z",
            distance = 8000.0,
            total_elevation_gain = 100.0,
        });

        return JsonSerializer.Serialize(activities);
    }

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
    [Fact]
    public async Task GetActivities_StopsAfterAShortPage()
    {
        // Arrange: queue TWO pages — a full one (exactly 200) then a short one (1).
        // The full page must be exactly PageSize, or the loop would stop after page 1 and prove nothing.
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ActivitiesJson(200), Encoding.UTF8, "application/json"),
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ActivitiesJson(1), Encoding.UTF8, "application/json"),
            });

        // Act
        var result = await CreateClient(handler).GetActivitiesAsync(
            "test_access", DateTimeOffset.UtcNow.AddDays(-30), CancellationToken.None);

        // Assert: page 1 was full → it asked again; page 2 was short → it stopped. Exactly two requests.
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(201, result.Count);   // 200 + 1 — both pages' activities came back combined
    }
    [Fact]
    public async Task GetActivities_SendsBearerTokenAndAfterEpoch()
    {
        // Arrange: one short page is enough — we only care about the request that goes OUT.
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ActivitiesJson(1), Encoding.UTF8, "application/json"),
        });
        // A FIXED time so its epoch is predictable: ToUnixTimeSeconds() on this is exactly 1700000000.
        // DateTimeOffset.UtcNow would change every run, leaving nothing stable to assert on.
        var after = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

        // Act
        await CreateClient(handler).GetActivitiesAsync("test_access", after, CancellationToken.None);

        // Assert — the token: scheme is "Bearer", the value is the access token we passed.
        // The ! is the null-forgiving operator: LastRequest is typed nullable, but a request definitely
        // happened, so we tell the compiler "trust me, not null here" instead of guarding it.
        var auth = handler.LastRequest!.Headers.Authorization!;
        Assert.Equal("Bearer", auth.Scheme);
        Assert.Equal("test_access", auth.Parameter);

        // Assert — the URL: the after epoch made it into the query string.
        Assert.Contains("after=1700000000", handler.LastRequest!.RequestUri!.ToString());
    }
    [Fact]
    public async Task GetActivities_MapsAllFields()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(OneActivityJson, Encoding.UTF8, "application/json"),
        });

        // Act
        var result = await CreateClient(handler).GetActivitiesAsync(
            "test_access", DateTimeOffset.UtcNow.AddDays(-30), CancellationToken.None);

        // Assert — one activity came back, and every field is in the right place.
        // Assert.Single does two jobs: it fails unless there's exactly one item, and returns that item.
        var activity = Assert.Single(result);
        Assert.Equal(1234567890, activity.Id);
        Assert.Equal("Lunch Run", activity.Name);
        Assert.Equal("TrailRun", activity.SportType);
        // Build the expected time directly (year, month, day, hour, minute, second, zero offset) rather
        // than Parse(...): no locale dependence (silences CA1305), and the "Z" in the JSON means offset 0.
        Assert.Equal(new DateTimeOffset(2026, 6, 8, 7, 30, 0, TimeSpan.Zero), activity.StartDateLocal);
        Assert.Equal(8012.3, activity.DistanceMeters);
        Assert.Equal(142.0, activity.ElevationGainMeters);
    }
}
