using LoopQuest.Application.Common.Interfaces;

namespace LoopQuest.Infrastructure.Tests.Sync;

public sealed class FakeStravaClient : IStravaClient
{
    public List<StravaActivitySummary> Activities { get; set; } = [];
    public StravaTokens? RefreshResult { get; set; }
    public int RefreshCallCount { get; private set; }

    public string BuildAuthorizationUrl() => "https://example.test/authorize";

    public Task<StravaAuthorization> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
        => throw new NotSupportedException("Not needed in sync tests.");

    public Task<StravaTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        RefreshCallCount++;
        return Task.FromResult(RefreshResult
            ?? throw new InvalidOperationException("Set RefreshResult first."));
    }

    public Task<IReadOnlyList<StravaActivitySummary>> GetActivitiesAsync(
        string accessToken, DateTimeOffset after, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<StravaActivitySummary>>(Activities);
}
