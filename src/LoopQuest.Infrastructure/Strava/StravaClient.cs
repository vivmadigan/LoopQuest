using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LoopQuest.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace LoopQuest.Infrastructure.Strava;

// A "typed HttpClient": AddHttpClient<IStravaClient, StravaClient> tells DI to construct this class
// with an HttpClient whose BaseAddress (https://www.strava.com) was set at registration — which is
// why the PostAsync calls below can use the relative path "/oauth/token".
public sealed class StravaClient(HttpClient http, IOptions<StravaOptions> options) : IStravaClient
{
    // .Value unwraps the bound "Strava" config section (user-secrets + appsettings).
    private readonly StravaOptions _options = options.Value;

    // The one method on this client that never calls Strava — no HTTP, it only manufactures
    // the URL the user's browser will visit.
    public string BuildAuthorizationUrl()
    {
        // Only the redirect URI needs escaping: it contains :// and /, which would otherwise
        // be read as URL structure instead of as a query value.
        return $"https://www.strava.com/oauth/authorize?client_id={_options.ClientId}"
            + $"&redirect_uri={Uri.EscapeDataString(_options.RedirectUri)}"
            + "&response_type=code&approval_prompt=auto&scope=activity:read_all";
    }

    // The "trade" row of the ledger: spend the one-time code, get keys + athlete identity back.
    public async Task<StravaAuthorization> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        // FormUrlEncodedContent = the POST body packaged the way an HTML form submits:
        // key=value&key=value, values escaped, Content-Type stamped as
        // application/x-www-form-urlencoded. OAuth token endpoints require this format, not JSON.
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
        });

        var response = await http.PostAsync("/oauth/token", form, cancellationToken);
        // Any non-2xx answer (used/stale code, wrong secret) throws here — nothing gets saved.
        response.EnsureSuccessStatusCode();

        // Deserialize Strava's JSON body into the wire record below. ReadFromJsonAsync returns
        // null for an empty body, hence the guard.
        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Strava returned an empty token response.");
        var athlete = payload.Athlete
            ?? throw new InvalidOperationException("Token exchange response had no athlete.");

        return new StravaAuthorization(
            athlete.Id,
            $"{athlete.FirstName} {athlete.LastName}".Trim(),
            new StravaTokens(
                payload.AccessToken,
                payload.RefreshToken,
                DateTimeOffset.FromUnixTimeSeconds(payload.ExpiresAt)));
    }

    public async Task<StravaTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        // The "refresh" row of the ledger: spend the old refresh token, get fresh keys back.
        // Same five beats as ExchangeCodeAsync — only the grant differs: we present a refresh
        // token instead of a one-time code.
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
        });

        var response = await http.PostAsync("/oauth/token", form, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Strava returned an empty token response.");

        // No athlete in a refresh response — so unlike ExchangeCodeAsync, no athlete guard, and
        // we map straight to the tokens record. payload.RefreshToken is the NEW rotated token;
        // the caller must store it or the next refresh fails.
        return new StravaTokens(
            payload.AccessToken,
            payload.RefreshToken,
            DateTimeOffset.FromUnixTimeSeconds(payload.ExpiresAt));
    }

    // Strava's wire shape, private to this file — nothing outside Infrastructure may know it.
    // [property: JsonPropertyName("access_token")] is a name tag: "in the JSON, I'm called
    // access_token" — how Strava's snake_case lands in our PascalCase properties. Athlete is
    // nullable because refresh responses don't include one.
    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_at")] long ExpiresAt,
        [property: JsonPropertyName("athlete")] AthleteSummary? Athlete);

    private sealed record AthleteSummary(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("firstname")] string FirstName,
        [property: JsonPropertyName("lastname")] string LastName);
}
