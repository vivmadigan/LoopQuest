# Stage 2 Plan — Connect to Strava (OAuth)

> **How to use this doc:** work top to bottom. Every task is tagged:
> **🧩 you write it** · **📖 reference example — type it, adapt it, understand it** · **🤝 we do it together in chat**.
> Drafted by Claude (2026-06-10) for Viv's review — question anything that doesn't make sense *before* coding.
>
> **Start only after Stage 1 is merged to `dev`.** Then: `git switch dev && git switch -c feature/stage-2-strava-oauth`

---

## 1. What you're building

One athlete (you) clicks **Connect**, approves LoopQuest on Strava's website, and lands back in the
app with refreshable API tokens stored in the database. That's it — no activities yet (Stage 3).

```
 Your browser                LoopQuest API                      Strava
      │                           │                                │
      │ 1. GET /auth/strava/connect                                │
      │──────────────────────────▶│                                │
      │◀── 302 redirect ──────────│                                │
      │ 2. strava.com/oauth/authorize?client_id=…&scope=…          │
      │────────────────────────────────────────────────────────────▶
      │        (you click "Authorize" on Strava's page)            │
      │◀── 302 back to ?code=…&scope=… ─────────────────────────────
      │ 3. GET /auth/strava/callback?code=…                        │
      │──────────────────────────▶│                                │
      │                           │ 4. POST /oauth/token           │
      │                           │    (code + client secret)      │
      │                           │───────────────────────────────▶│
      │                           │◀── tokens + athlete JSON ──────│
      │                           │ 5. save User + StravaConnection│
      │◀── 200 "connected" ───────│                                │
```

**The one big idea (OAuth authorization-code flow):** the browser only ever carries a short-lived,
one-time `code`. Your *server* swaps that code for real tokens in step 4, sending the client secret
server-to-server. The secret and the tokens never touch the browser. That's the entire reason the
flow has two legs.

### New concepts in this stage

| Concept | One-liner |
|---|---|
| OAuth code flow | Diagram above. Browser gets a `code`; server swaps it for tokens. |
| Typed `HttpClient` | A class that *is* a configured HTTP client (`StravaClient`), registered with `AddHttpClient<,>`. |
| Options pattern + user-secrets | Config section → strongly-typed `StravaOptions`; secrets stay out of git. |
| EF **owned entity** | `StravaConnection` has no table of its own — its columns live inside `users`. |
| Faking HTTP in tests | Swap `HttpMessageHandler` for a fake; test the client with canned JSON, no network. |

---

## 2. Decisions already made (challenge them if they seem wrong)

| Decision | Choice | Why |
|---|---|---|
| Where tokens live | `User` entity + owned `StravaConnection` value object | Per [02-domain-model.md](../02-domain-model.md); one user row, connection columns alongside. |
| Who knows Strava's URLs/JSON | **Only Infrastructure** (`StravaClient`) | Application sees just `IStravaClient`. Swap Strava for anything tomorrow; handlers never change. |
| Auth-URL building | Also in `StravaClient` (it's Strava protocol knowledge) | Keeps `StravaOptions`/`IOptions` out of Application entirely. Simpler dependency story. |
| Client results | Two small records: `StravaTokens`, `StravaAuthorization` | Token exchange returns athlete info; refresh doesn't. Two honest shapes beat one with nullable holes. |
| Secrets | .NET user-secrets on the Api project | Already documented in [04-strava-integration.md](../04-strava-integration.md). Never committed. |
| Token refresh in this stage | Implement `RefreshAsync` + test it. **No** auto-refresh orchestration yet | Roadmap: build the bare refresh now, *lean on it* in Stage 3 when calls actually need it. |
| Where client tests live | New project `tests/LoopQuest.Infrastructure.Tests` | The fakes test Infrastructure code; the existing test project doesn't reference Infrastructure. |
| New packages | `Microsoft.Extensions.Http`, `Microsoft.Extensions.Options.ConfigurationExtensions`, both **10.0.8** | Needed for `AddHttpClient`/`Configure<TOptions>` in Infrastructure. 10.0.8 matches the EF packages already pinned (10.0.9 exists; bump everything together some other day). |

---

## 3. One-time setup (before any code)

1. **Create your Strava API app** at <https://www.strava.com/settings/api> (any name/category;
   website can be `http://localhost`). Set **Authorization Callback Domain** = `localhost` (domain
   only — no port, no path). Note the **Client ID** and **Client Secret** it gives you.

2. **Store the secrets** (from the repo root):

   ```bash
   dotnet user-secrets init   --project src/LoopQuest.Api
   dotnet user-secrets set "Strava:ClientId" "<your id>"         --project src/LoopQuest.Api
   dotnet user-secrets set "Strava:ClientSecret" "<your secret>" --project src/LoopQuest.Api
   ```

3. **Add the (non-secret) redirect URI** to `src/LoopQuest.Api/appsettings.Development.json`:

   ```json
   "Strava": {
     "RedirectUri": "http://localhost:5159/auth/strava/callback"
   }
   ```

   `5159` is the API's port from `launchSettings.json`. After your first AppHost run, confirm the
   api resource really is on 5159 in the Aspire dashboard; if not, update this value to match.

4. **Add the two package versions** to `Directory.Packages.props` (CPM = version here, *no*
   `Version=` in any csproj):

   ```xml
   <PackageVersion Include="Microsoft.Extensions.Http" Version="10.0.8" />
   <PackageVersion Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="10.0.8" />
   ```

   and the matching versionless references to `src/LoopQuest.Infrastructure/LoopQuest.Infrastructure.csproj`:

   ```xml
   <PackageReference Include="Microsoft.Extensions.Http" />
   <PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" />
   ```

---

## 4. The map — every new file

```
src/LoopQuest.Domain/
  Entities/User.cs                                      🧩  (mirror Loop.cs)
  ValueObjects/StravaConnection.cs                      🧩
src/LoopQuest.Application/
  Common/Interfaces/IStravaClient.cs                    📖  (contract given below)
  Common/Interfaces/IAppDbContext.cs                    🧩  add DbSet<User> Users { get; }
  Auth/Queries/GetStravaAuthUrl/GetStravaAuthUrlQuery.cs        🧩
  Auth/Queries/GetStravaAuthUrl/GetStravaAuthUrlQueryHandler.cs 🧩
  Auth/Commands/CompleteStravaConnection/CompleteStravaConnectionCommand.cs   🧩
  Auth/Commands/CompleteStravaConnection/CompleteStravaConnectionCommandHandler.cs 🧩
  Auth/Commands/CompleteStravaConnection/CompleteStravaConnectionCommandValidator.cs 🧩
  Auth/Commands/CompleteStravaConnection/StravaConnectionDto.cs 🧩
src/LoopQuest.Infrastructure/
  Strava/StravaOptions.cs                               📖
  Strava/StravaClient.cs                                📖 + 🧩  (one method given; two are yours)
  Persistence/Configurations/UserConfiguration.cs       📖 + 🧩
  Persistence/AppDbContext.cs                           🧩  add DbSet<User>
  Persistence/Migrations/…AddUserAndStravaConnection…   🤝  (generated; we review it together)
  DependencyInjection.cs                                📖  add options + typed client
src/LoopQuest.Api/
  Controllers/AuthController.cs                         📖 + 🧩
tests/LoopQuest.Domain.Tests/
  UserTests.cs                                          🧩
tests/LoopQuest.Infrastructure.Tests/                   (new project)
  LoopQuest.Infrastructure.Tests.csproj                 📖
  Strava/FakeHttpMessageHandler.cs                      📖
  Strava/StravaClientTests.cs                           📖 + 🧩
```

---

## 5. Build order

Work the layers inside-out, exactly like the dependency arrow: Domain → Application → Infrastructure → Api.
Each step ends with a **checkpoint** — don't move on until it passes.

### Step 1 — Domain: `User` + `StravaConnection` 🧩

Mirror `Loop.cs` precisely: private parameterless constructor for EF, private setters, static
`Create(...)` with guard clauses. Skeletons (the `🧩` bodies are yours):

```csharp
// Domain/ValueObjects/StravaConnection.cs
public class StravaConnection
{
    private StravaConnection() { }                    // EF

    public string AccessToken { get; private set; } = null!;
    public string RefreshToken { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public string Scope { get; private set; } = null!;

    public static StravaConnection Create(/* 🧩 the four values; guard against blank tokens */) { }
}
```

```csharp
// Domain/Entities/User.cs
public class User
{
    private User() { }                                // EF

    public Guid Id { get; private set; }
    public long StravaAthleteId { get; private set; }
    public string DisplayName { get; private set; } = null!;
    public string TimeZoneId { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public StravaConnection? Connection { get; private set; }   // null until first connect

    public static User Create(long stravaAthleteId, string displayName,
        string timeZoneId = "Europe/Stockholm")
    { /* 🧩 guards: athlete id > 0, display name not blank. CreatedAt = DateTimeOffset.UtcNow */ }

    public void ConnectStrava(string accessToken, string refreshToken,
        DateTimeOffset expiresAt, string scope)
    { /* 🧩 replace Connection with a new StravaConnection — same method serves refresh later */ }
}
```

**Tests first** (in `tests/LoopQuest.Domain.Tests/UserTests.cs`), suggested names:
`Create_WithValidInput_SetsIdAndDefaults` · `Create_RejectsNonPositiveAthleteId` ·
`Create_RejectsBlankDisplayName` · `ConnectStrava_StoresTheConnection` ·
`ConnectStrava_ReplacesAnExistingConnection`.

**Checkpoint:** `dotnet test tests/LoopQuest.Domain.Tests` green.

### Step 2 — Application: the contract and two slices 🧩

The contract Infrastructure will implement (📖 — take as-is, it anchors everything else):

```csharp
// Application/Common/Interfaces/IStravaClient.cs
public interface IStravaClient
{
    /// <summary>The Strava authorize URL the browser should be redirected to.</summary>
    string BuildAuthorizationUrl();

    /// <summary>Swaps the one-time callback code for tokens + the athlete's identity.</summary>
    Task<StravaAuthorization> ExchangeCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>Gets fresh tokens. Strava ROTATES refresh tokens — always store the new one.</summary>
    Task<StravaTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
}

public sealed record StravaTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

public sealed record StravaAuthorization(long AthleteId, string AthleteDisplayName, StravaTokens Tokens);
```

Then two vertical slices, copying the `GetLoops` folder shape:

- **`GetStravaAuthUrlQuery`** → `IRequest<string>`. Handler is two lines: inject `IStravaClient`,
  return `Task.FromResult(stravaClient.BuildAuthorizationUrl())`.
- **`CompleteStravaConnectionCommand(string Code, string? Scope)`** → `IRequest<StravaConnectionDto>`.
  Handler recipe:
  1. `ExchangeCodeAsync(request.Code, …)`
  2. find the user: `db.Users.SingleOrDefaultAsync(u => u.StravaAthleteId == auth.AthleteId, …)`
  3. none? `User.Create(...)` + `db.Users.Add(...)` — *this is what makes reconnecting idempotent*
  4. `user.ConnectStrava(...)` with the tokens and `request.Scope ?? ""`
  5. `SaveChangesAsync`, return a small DTO (e.g. athlete name + token expiry) — never the entity.

  Validator (your first one! the pipeline picks it up automatically):

  ```csharp
  public sealed class CompleteStravaConnectionCommandValidator
      : AbstractValidator<CompleteStravaConnectionCommand>
  {
      public CompleteStravaConnectionCommandValidator()
          => RuleFor(c => c.Code).NotEmpty();
  }
  ```

Also add `DbSet<User> Users { get; }` to `IAppDbContext`.

**Checkpoint:** `dotnet build` clean.

### Step 3 — Infrastructure: options + the typed client 📖 + 🧩

```csharp
// Infrastructure/Strava/StravaOptions.cs
public sealed class StravaOptions
{
    public const string SectionName = "Strava";

    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string RedirectUri { get; set; } = "";
}
```

Registration, added to `AddInfrastructure` (📖 — this is the typed-client pattern):

```csharp
builder.Services.Configure<StravaOptions>(
    builder.Configuration.GetSection(StravaOptions.SectionName));

builder.Services.AddHttpClient<IStravaClient, StravaClient>(client =>
{
    client.BaseAddress = new Uri("https://www.strava.com");
});
// ServiceDefaults already wraps every HttpClient with retries/timeouts (standard resilience) — free.
```

The client. `ExchangeCodeAsync` is your 📖 reference — `BuildAuthorizationUrl` and `RefreshAsync`
are 🧩 yours, built from the same parts:

```csharp
// Infrastructure/Strava/StravaClient.cs
public sealed class StravaClient(HttpClient http, IOptions<StravaOptions> options) : IStravaClient
{
    private readonly StravaOptions _options = options.Value;

    public string BuildAuthorizationUrl()
    {
        // 🧩 https://www.strava.com/oauth/authorize with query params:
        //    client_id, redirect_uri (Uri.EscapeDataString it!), response_type=code,
        //    approval_prompt=auto, scope=activity:read_all
    }

    public async Task<StravaAuthorization> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
        });

        var response = await http.PostAsync("/oauth/token", form, cancellationToken);
        response.EnsureSuccessStatusCode();

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
        // 🧩 same form pattern: client_id, client_secret, grant_type=refresh_token, refresh_token.
        //    Same endpoint. Response has tokens but NO athlete.
    }

    // Strava's wire shape, private to this file — nothing outside Infrastructure may know it.
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
```

**Checkpoint:** `dotnet build` clean.

### Step 4 — Persistence: configuration + migration 📖 + 🤝

`OwnsOne` is the new trick — the owned type's columns land in the `users` table:

```csharp
// Infrastructure/Persistence/Configurations/UserConfiguration.cs
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.HasIndex(u => u.StravaAthleteId).IsUnique();

        builder.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
        // 🧩 TimeZoneId similarly

        builder.OwnsOne(u => u.Connection, connection =>
        {
            connection.Property(c => c.AccessToken).HasMaxLength(512).IsRequired();
            // 🧩 RefreshToken (512), Scope (200) similarly; ExpiresAt needs no config
        });
    }
}
```

Add `DbSet<User> Users => Set<User>();` to `AppDbContext` (the assembly scan finds the
configuration automatically). Then generate the migration:

```bash
dotnet ef migrations add AddUserAndStravaConnection \
  --project src/LoopQuest.Infrastructure \
  --startup-project src/LoopQuest.Infrastructure \
  --output-dir Persistence/Migrations
```

🤝 **Paste the generated migration into chat before committing it** — reading your first generated
migration together (what `OwnsOne` produced, why the unique index is there) is the point of this step.

**Checkpoint:** migration file exists and `dotnet build` is clean. (It auto-applies on next run.)

### Step 5 — Tests for the client 📖 + 🧩

New project (📖 csproj — versionless references, CPM supplies versions):

```xml
<!-- tests/LoopQuest.Infrastructure.Tests/LoopQuest.Infrastructure.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\LoopQuest.Infrastructure\LoopQuest.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

Register it: `dotnet sln add tests/LoopQuest.Infrastructure.Tests`

The fake — how you test HTTP code with zero network (📖):

```csharp
/// <summary>Returns a canned response and records the request, so tests can assert on both.</summary>
public sealed class FakeHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        return response;
    }
}
```

One full example test (📖); the other three are yours (🧩):

```csharp
public class StravaClientTests
{
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
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(TokenJson, Encoding.UTF8, "application/json"),
        });

        var result = await CreateClient(handler).ExchangeCodeAsync("the-code", CancellationToken.None);

        Assert.Equal(67890, result.AthleteId);
        Assert.Equal("Viv M", result.AthleteDisplayName);
        Assert.Equal("test_access", result.Tokens.AccessToken);
        Assert.Equal("test_refresh", result.Tokens.RefreshToken);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_750_000_000), result.Tokens.ExpiresAt);
    }

    // 🧩 BuildAuthorizationUrl_ContainsClientIdRedirectUriAndScope   (no HTTP needed — just assert on the string)
    // 🧩 ExchangeCodeAsync_PostsCredentialsCodeAndGrantType          (assert on handler.LastRequestBody)
    // 🧩 RefreshAsync_MapsTokensAndUsesRefreshTokenGrant             (canned JSON *without* athlete)
}
```

**Checkpoint:** `dotnet test tests/LoopQuest.Infrastructure.Tests` green.

### Step 6 — Api: the controller 📖 + 🧩

```csharp
[ApiController]
[Route("auth/strava")]                      // note: not under /api — per the roadmap's paths
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpGet("connect")]
    public async Task<IActionResult> Connect(CancellationToken cancellationToken)
    {
        var url = await sender.Send(new GetStravaAuthUrlQuery(), cancellationToken);
        return Redirect(url);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        string? code, string? scope, string? error, CancellationToken cancellationToken)
    {
        // 🧩 1. if error is non-empty (user clicked Cancel → "access_denied") or code is missing:
        //       return a 400 problem (Problem(...) helper) — don't call Strava.
        //    2. otherwise send CompleteStravaConnectionCommand, return Ok(dto).
    }
}
```

**Checkpoint:** `dotnet build`, then `dotnet run --project src/LoopQuest.AppHost`.

### Step 7 — The moment of truth 🤝

1. AppHost running → browser → `http://localhost:5159/auth/strava/connect`.
2. Authorize on Strava → you land on the callback → JSON with your athlete name.
3. Prove it persisted (find the container name with `docker ps`):

   ```bash
   docker exec -it <postgres-container> psql -U postgres -d loopquestdb -c "select * from users"
   ```

4. Prove idempotency: hit `/auth/strava/connect` again, authorize again → **still exactly one row**, fresh tokens.

If anything misbehaves, bring me the Aspire dashboard logs and we'll debug it together.

---

## 6. Strava API cheat sheet

| Call | Details |
|---|---|
| Authorize (browser) | `GET https://www.strava.com/oauth/authorize` — `client_id`, `redirect_uri`, `response_type=code`, `approval_prompt=auto`, `scope=activity:read_all` |
| Token exchange | `POST https://www.strava.com/oauth/token` — form: `client_id`, `client_secret`, `code`, `grant_type=authorization_code` |
| Refresh | `POST https://www.strava.com/oauth/token` — form: `client_id`, `client_secret`, `refresh_token`, `grant_type=refresh_token` |

The token JSON is exactly the `TokenJson` sample in Step 5 (refresh responses omit `athlete`).
While implementing, keep <https://developers.strava.com/docs/authentication/> open and trust it over
this doc if they ever disagree.

## 7. Pitfalls (each one is a real bug you'd otherwise meet)

- **Refresh tokens rotate.** Every refresh response contains a *new* refresh token. Store it, or the
  *next* refresh fails — weeks later, mysteriously.
- **`expires_at` is epoch seconds** → `DateTimeOffset.FromUnixTimeSeconds(...)`. Don't hand-roll date math.
- **`scope` arrives on the callback query**, not in the token response. Pass it into the command from
  the controller.
- **Never log tokens.** `LoggingBehavior` only logs request *names* — keep it that way; don't add
  payload logging while debugging and forget it there.
- **CPM:** if you put `Version="…"` in a csproj you'll get error `NU1008`. Version goes in
  `Directory.Packages.props`, reference goes in the csproj.
- **User clicks Cancel on Strava** → callback gets `error=access_denied` and no code. Handle it
  before touching the mediator.
- **Owned type + `SingleOrDefaultAsync`:** no `Include` needed — owned types load with the owner
  automatically.

## 8. Definition of Done

- [ ] `dotnet build` — no new warnings/errors
- [ ] `dotnet test` — all green, including new `UserTests` + `StravaClientTests`
- [ ] Connect round-trip works end-to-end; `users` has exactly one row with tokens
- [ ] Connecting a second time updates that row — never duplicates it
- [ ] No secret anywhere in git (`git grep -i clientsecret` finds only code/config *keys*, no values)
- [ ] Dependency rule intact: Application gained **no** new package references
- [ ] Reviewed, then merged: `git switch dev && git merge --no-ff feature/stage-2-strava-oauth`

## 9. Explicitly out of scope (resist the urge)

- **CSRF `state` parameter** — real multi-user apps must send an unguessable `state` and verify it on
  callback. Single-user localhost MVP skips it; **revisit before any public deployment.**
- **Auto-refresh orchestration** ("refresh if expiring before each call") — Stage 3, where calls exist.
- **Fetching activities, current-user query, multi-user, webhooks** — Stages 3+.
