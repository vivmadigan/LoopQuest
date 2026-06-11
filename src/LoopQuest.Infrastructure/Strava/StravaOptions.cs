namespace LoopQuest.Infrastructure.Strava;

/// <summary>
/// The "Strava" section of configuration, bound onto this class at startup by
/// Configure&lt;StravaOptions&gt; (see DependencyInjection): ClientId and ClientSecret come from
/// user-secrets (never git), RedirectUri from appsettings.Development.json. Consumers receive it
/// as IOptions&lt;StravaOptions&gt; — see StravaClient's constructor.
/// </summary>
public sealed class StravaOptions
{
    public const string SectionName = "Strava";

    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string RedirectUri { get; set; } = "";
}
