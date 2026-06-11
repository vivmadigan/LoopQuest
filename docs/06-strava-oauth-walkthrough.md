# 06 — Strava OAuth, file by file (learning walkthrough)

> **What this is:** the plain-language map of the Strava connect feature — the flow, every file in
> it, how they link up, and what each property is for. Written mid-Stage 2 (2026-06-11) as a
> companion to [04-strava-integration.md](04-strava-integration.md) (the *policy*: scope, data
> boundary, secrets) and the [stage 2 plan](plans/stage-2-strava-oauth.md) (the *build order*).
>
> Status marks: ✅ built · 🔜 coming in the step shown.

---

## 1. The one-paragraph version

LoopQuest needs to read your runs from Strava, and Strava only serves requests that carry an
**access token** proving you said yes. OAuth is the procedure for getting that token *without ever
giving LoopQuest your Strava password*: you approve on Strava's own site, Strava hands your browser
a one-time **code** (a claim ticket, useless on its own), and our server trades that ticket — plus
our client secret — for the real tokens, which we store in the database. Stage 3 then spends the
stored token on every "give me activities" call.

## 2. The flow in eight steps

| # | Who | What happens | Files in play |
|---|-----|--------------|---------------|
| 1 | You | Browser opens `/auth/strava/connect` on **our** server | `AuthController` 🔜 step 6 |
| 2 | LoopQuest | Build the Strava permission URL, reply "302 — go there" | `GetStravaAuthUrlQuery` + handler ✅ · `StravaClient.BuildAuthorizationUrl` 🔜 step 3 |
| 3 | Strava | Their page asks "let LoopQuest view your activities?" — you authorize. Password goes to Strava only | none of ours |
| 4 | Strava | Redirects your browser to our callback with `?code=…&scope=…` | in transit |
| 5 | LoopQuest | Callback endpoint checks `error`/missing code; validator checks `Code` non-empty | `AuthController` 🔜 · `CompleteStravaConnectionCommand` + validator ✅ |
| 6 | LoopQuest ↔ Strava | Server-to-server: `code` + client id + client **secret** → tokens + athlete identity | handler ✅ → `StravaClient.ExchangeCodeAsync` 🔜 step 3 |
| 7 | LoopQuest | Find user by athlete id, or create them. Store the tokens | `CompleteStravaConnectionCommandHandler` ✅ · `User.ConnectStrava` ✅ |
| 8 | Database | One row in `users` now holds the keys; browser gets the safe summary | `SaveChangesAsync` → migration 🔜 step 4 · `StravaConnectionDto` ✅ |

Reconnecting runs the same eight steps and **updates the same row** (find-or-create + replace) —
that's what "idempotent" means here.

## 3. The vocabulary

| Word | What it actually is | Travels via | Lives for |
|------|--------------------|-------------|-----------|
| `client_id` | Our app's public name tag at Strava | URLs (visible is fine) | forever |
| `client_secret` | Our app's password at Strava — proves requests are really from us | server-to-server only; stored in user-secrets, never git | until rotated |
| `redirect_uri` | Where Strava may send the browser back | in the authorize URL | config |
| `scope` | What we asked to read (`activity:read_all`) | authorize URL out, callback query back | per grant |
| `code` | One-time claim ticket for tokens | browser URL (safe: useless without the secret) | minutes, single use |
| `access_token` | **The working key** — attached to every future data request | server-to-server | ~6 hours |
| `refresh_token` | **The key-making key** — only ever spent on minting fresh access tokens. Strava hands back a NEW one each time (rotation) | server-to-server | until used or revoked |
| `expires_at` | The working key's death clock (epoch seconds on the wire → `DateTimeOffset` in our code) | token response | n/a |
| athlete id | Strava's permanent number for you — our lookup key for the user row | token response | forever |

## 4. The files, layer by layer

### Domain — the rules, no frameworks ✅

**`Domain/Entities/User.cs`** — an athlete playing LoopQuest.

| Property | Why it exists |
|----------|---------------|
| `Id` (Guid) | Our own primary key — never reuse someone else's id as your PK |
| `StravaAthleteId` (long) | Strava's permanent number for you; how the callback finds your row (unique) |
| `DisplayName` | From Strava's athlete info; shown in the DTO/UI |
| `TimeZoneId` | Weeks are bucketed by *local* time later (Stage 3+) — the game needs to know whose midnight |
| `CreatedAt` | Bookkeeping; set once in `Create` |
| `Connection` (`StravaConnection?`) | The keys. **Nullable on purpose** — a user exists before they first connect |

Methods: `Create(...)` (factory with guard clauses — invalid users can't exist) and
`ConnectStrava(...)` (replaces `Connection` wholesale — one method serves first connect, reconnect,
and Stage 3's refresh).

**`Domain/ValueObjects/StravaConnection.cs`** — the stored keys themselves: `AccessToken`,
`RefreshToken`, `ExpiresAt`, `Scope` (meanings in §3). No table of its own — EF saves these as
extra columns inside `users` (an *owned* type, configured in step 4). `Create` guards against
blank tokens.

### Application — what we need, never how ✅

**`Common/Interfaces/IStravaClient.cs`** — the contract: everything LoopQuest needs Strava to do.

| Member | Job |
|--------|-----|
| `BuildAuthorizationUrl()` | Step 2's URL string |
| `ExchangeCodeAsync(code, ct)` | Step 6's ticket→keys trade; returns `StravaAuthorization` |
| `RefreshAsync(refreshToken, ct)` | Stage 3's key renewal; returns `StravaTokens` |
| `record StravaTokens(AccessToken, RefreshToken, ExpiresAt)` | The keys, translated for our app |
| `record StravaAuthorization(AthleteId, AthleteDisplayName, Tokens)` | First-connect result: who you are + the keys. Two records because a refresh has no athlete — honest shapes beat nullable holes |

The records live in this file because they're the contract's *vocabulary* — Application code
consumes them, so Application must own them (the dependency arrow points inward).

**`Common/Interfaces/IAppDbContext.cs`** — the persistence promise: `Users` and `Loops` DbSets +
`SaveChangesAsync`. Handlers depend on this interface; `AppDbContext` (Infrastructure) delivers it.

**`Auth/Queries/GetStravaAuthUrl/`** — the step-2 slice.
- `GetStravaAuthUrlQuery` — an empty request: "the permission URL, please" (`IRequest<string>`).
- `GetStravaAuthUrlQueryHandler` — injects `IStravaClient`, returns
  `Task.FromResult(stravaClient.BuildAuthorizationUrl())`. No DB, no waiting.

**`Auth/Commands/CompleteStravaConnection/`** — the steps-5-to-8 slice.
- `CompleteStravaConnectionCommand(Code, Scope?)` — what the callback URL carried. `Scope` is
  nullable because Strava may omit it; the handler stores `""` then.
- `...Validator` — one rule: `Code` non-empty. Runs automatically (`ValidationBehavior` discovers
  every validator in the assembly); a bad command becomes a 400 before the handler exists.
- `...Handler` — the recipe: ① trade code for tokens ② find user by `StravaAthleteId`
  ③ create + `Add` if missing ④ `ConnectStrava(...)` ⑤ `SaveChangesAsync` → return DTO.
  Depends only on the two interfaces; DI hands it the real implementations at run time.
- `StravaConnectionDto(DisplayName, ExpiresAt)` — the safe reply. Never tokens, never the entity.

### Infrastructure — the how 🔜 steps 3–4

- **`Strava/StravaOptions.cs`** — config holder: `ClientId` + `ClientSecret` (from user-secrets)
  and `RedirectUri` (from `appsettings.Development.json`), bound from the `"Strava"` section.
- **`Strava/StravaClient.cs`** — the only file in the whole app that knows Strava's URLs and JSON.
  Implements the three `IStravaClient` methods over an injected `HttpClient`; its private
  `TokenResponse`/`AthleteSummary` records mirror Strava's wire JSON (snake_case names, epoch
  seconds) and are translated into our records before anything leaves this file.
- **`DependencyInjection.cs`** — the two registrations that make DI work:
  `Configure<StravaOptions>(...)` and `AddHttpClient<IStravaClient, StravaClient>(...)` — the
  literal answer to "which class is registered for the interface".
- **`Persistence/Configurations/UserConfiguration.cs`** + migration — `OwnsOne` puts the
  connection columns inside `users`; the **unique index on `StravaAthleteId`** is the database
  half of the handler's `SingleOrDefaultAsync` promise (≤1 row per athlete, enforced).
- **`Persistence/AppDbContext.cs`** — delivers the `IAppDbContext` promise (✅ `Users` added).

### Api — the doors 🔜 step 6

**`Controllers/AuthController.cs`** — the only two URLs a browser can actually visit:
`GET /auth/strava/connect` (send the query → 302 to Strava) and `GET /auth/strava/callback`
(reject `error`/missing code with a 400 problem, else send the command → 200 + DTO).

## 5. How it all links up

Compile time — who is allowed to know whom (arrows always point inward):

```
Api ──▶ Infrastructure ──▶ Application ──▶ Domain
AuthController   StravaClient,         IStravaClient,        User,
                 AppDbContext,         IAppDbContext,        StravaConnection
                 StravaOptions         slices, records
```

Run time — one callback request, end to end:

```
browser ─▶ AuthController ─▶ MediatR ─▶ Logging ─▶ Validation ─▶ Handler
                                                                  │ IStravaClient ──▶ StravaClient ──▶ strava.com
                                                                  │ IAppDbContext ──▶ AppDbContext ──▶ PostgreSQL
                                                                  ▼
browser ◀─ 200 + StravaConnectionDto ◀────────────────────────── DTO
```

The handler only ever names the interfaces (left of the arrows); the DI container substitutes the
real classes (right) when it constructs the handler.

## 6. The four guards ("is everything ok?")

1. **Controller** 🔜 — `error=access_denied` or no code → 400, Strava never called.
2. **Validator** ✅ — empty `Code` → 400 before the handler runs.
3. **`EnsureSuccessStatusCode`** 🔜 — Strava refuses the trade (used/stale code, wrong secret) →
   exception → error response; nothing is saved.
4. **Unique index + `SingleOrDefaultAsync`** — the same athlete can never become two rows.

Plus the human check (plan step 7): look in the DB with `psql`, then reconnect and confirm the row
*updated* rather than duplicated.

## 7. When something breaks, look here first

| Symptom | First place to look |
|---------|--------------------|
| Strava page says invalid redirect URI | Strava app settings (callback domain) vs `StravaOptions.RedirectUri` |
| 400 from our callback | Did you click cancel? (`error=access_denied`) · empty `code` → validator |
| Exception during the trade | `StravaClient.ExchangeCodeAsync` — wrong/used code or bad `ClientSecret` in user-secrets |
| "No service for IStravaClient" | The `AddHttpClient<IStravaClient, StravaClient>` registration (step 3) |
| Token always "expired" in Stage 3 | Was `ExpiresAt` stored from Strava's `expires_at`, not the current time? |
| Refresh suddenly fails weeks in | Rotation — did we store the NEW refresh token from the last refresh? |
