using System.Diagnostics.CodeAnalysis;
using Shared.Core.Enums;

namespace Shared.Core.Options;

public sealed class ExternalConsumerAppOptions
{
    public string AppID { get; set; } = default!;

    public string AppName { get; set; } = default!;

    public string ApiKey { get; set; } = default!;

    public string CallerID { get; set; } = default!;

    public bool AllowTwoWaySession { get; set; }

    /// <summary>When false, this app cannot call create session.</summary>
    public bool AllowCreateSession { get; set; } = true;

    /// <summary>When false, this app cannot call close session.</summary>
    public bool AllowCloseSession { get; set; } = true;

    /// <summary>When true, session inquire returns sessions from all apps instead of only this app.</summary>
    public bool InquireAllAppsSessions { get; set; }

    public int MaxActiveSessionPerAppAndTask { get; set; } = 1;

    public int MaxActiveSessionPerSourceUser { get; set; } = 1;

    public int MaxActiveSessionPerTargetUser { get; set; } = 1;

    public string DuplicateSessionPolicy { get; set; } = "CloseAndReplace";

    public int DefaultSessionTimeoutMinutes { get; set; } = 480;

    public bool EnableConsumptionLimit { get; set; }

    public int MaxConsumptionCount { get; set; } = 1;

    public string ConsumingPolicy { get; set; } = "OnSessionUsed";

    public Dictionary<string, IvrMessageOptions> IvrMessages { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Deprecated — use <see cref="IvrMessages"/> SessionSelection scenario.</summary>
    public string IvrMessageEn { get; set; } = IvrMessageDefaults.SessionSelectionEn;

    /// <summary>Deprecated — use <see cref="IvrMessages"/> SessionSelection scenario.</summary>
    public string IvrMessageAr { get; set; } = IvrMessageDefaults.SessionSelectionAr;

    public (string En, string Ar) ResolveIvrMessage(IvrMessageScenarioEnum scenario)
    {
        var code = scenario.Code ?? scenario.NameEn;
        if (IvrMessages.TryGetValue(code, out var configured) &&
            (!string.IsNullOrWhiteSpace(configured.En) || !string.IsNullOrWhiteSpace(configured.Ar)))
        {
            return (
                string.IsNullOrWhiteSpace(configured.En) ? GetDefaultEn(scenario) : configured.En,
                string.IsNullOrWhiteSpace(configured.Ar) ? GetDefaultAr(scenario) : configured.Ar);
        }

        if (scenario == IvrMessageScenarioEnum.SessionSelection)
        {
            return (IvrMessageEn, IvrMessageAr);
        }

        return (GetDefaultEn(scenario), GetDefaultAr(scenario));
    }

    private static string GetDefaultEn(IvrMessageScenarioEnum scenario) => scenario.Code switch
    {
        "SessionSelection" => IvrMessageDefaults.SessionSelectionEn,
        "NoActiveSession" => IvrMessageDefaults.NoActiveSessionEn,
        "SessionExpired" => IvrMessageDefaults.SessionExpiredEn,
        "SessionConsumed" => IvrMessageDefaults.SessionConsumedEn,
        "MaxSessionsReached" => IvrMessageDefaults.MaxSessionsReachedEn,
        "ActiveSessionExists" => IvrMessageDefaults.ActiveSessionExistsEn,
        "TwoWayNotAllowed" => IvrMessageDefaults.TwoWayNotAllowedEn,
        "InvalidReferenceType" => IvrMessageDefaults.InvalidReferenceTypeEn,
        "RecordingMessage" => IvrMessageDefaults.RecordingMessageEn,
        _ => IvrMessageDefaults.SessionSelectionEn
    };

    private static string GetDefaultAr(IvrMessageScenarioEnum scenario) => scenario.Code switch
    {
        "SessionSelection" => IvrMessageDefaults.SessionSelectionAr,
        "NoActiveSession" => IvrMessageDefaults.NoActiveSessionAr,
        "SessionExpired" => IvrMessageDefaults.SessionExpiredAr,
        "SessionConsumed" => IvrMessageDefaults.SessionConsumedAr,
        "MaxSessionsReached" => IvrMessageDefaults.MaxSessionsReachedAr,
        "ActiveSessionExists" => IvrMessageDefaults.ActiveSessionExistsAr,
        "TwoWayNotAllowed" => IvrMessageDefaults.TwoWayNotAllowedAr,
        "InvalidReferenceType" => IvrMessageDefaults.InvalidReferenceTypeAr,
        "RecordingMessage" => IvrMessageDefaults.RecordingMessageAr,
        _ => IvrMessageDefaults.SessionSelectionAr
    };
}

public sealed class ExternalConsumersOptions
{
    public const string SectionName = "ExternalConsumers";

    /// <summary>Internal tools (Swagger UI, Postman, etc.) that call API-key endpoints.</summary>
    public List<ExternalConsumerAppOptions> Internal { get; set; } = [];

    private Dictionary<string, (string SystemName, ExternalConsumerAppOptions App)> _byApiKey =
        new(StringComparer.Ordinal);

    private Dictionary<string, ExternalConsumerAppOptions> _byAppId =
        new(StringComparer.OrdinalIgnoreCase);

    public void RebuildLookup()
    {
        var lookup = new Dictionary<string, (string, ExternalConsumerAppOptions)>(StringComparer.Ordinal);
        var appIdLookup = new Dictionary<string, ExternalConsumerAppOptions>(StringComparer.OrdinalIgnoreCase);
        IndexApps(lookup, ExternalConsumerSystemNames.Internal, Internal);
        IndexAppsByAppId(appIdLookup, Internal);
        _byApiKey = lookup;
        _byAppId = appIdLookup;
    }

    public bool TryResolve(
        string? apiKey,
        [NotNullWhen(true)] out string? systemName,
        [NotNullWhen(true)] out ExternalConsumerAppOptions? app)
    {
        systemName = null;
        app = null;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return false;
        }

        if (_byApiKey.TryGetValue(apiKey.Trim(), out var entry))
        {
            systemName = entry.SystemName;
            app = entry.App;
            return true;
        }

        return false;
    }

    public bool TryResolveByAppId(
        string? appId,
        [NotNullWhen(true)] out ExternalConsumerAppOptions? app)
    {
        app = null;

        if (string.IsNullOrWhiteSpace(appId))
        {
            return false;
        }

        if (_byAppId.TryGetValue(appId.Trim(), out var resolved))
        {
            app = resolved;
            return true;
        }

        return false;
    }

    private static void IndexApps(
        Dictionary<string, (string, ExternalConsumerAppOptions)> lookup,
        string systemName,
        IEnumerable<ExternalConsumerAppOptions> apps)
    {
        foreach (var consumer in apps)
        {
            if (string.IsNullOrWhiteSpace(consumer.ApiKey))
            {
                continue;
            }

            lookup[consumer.ApiKey.Trim()] = (systemName, consumer);
        }
    }

    private static void IndexAppsByAppId(
        Dictionary<string, ExternalConsumerAppOptions> lookup,
        IEnumerable<ExternalConsumerAppOptions> apps)
    {
        foreach (var consumer in apps)
        {
            if (string.IsNullOrWhiteSpace(consumer.AppID))
            {
                continue;
            }

            lookup[consumer.AppID.Trim()] = consumer;
        }
    }
}
