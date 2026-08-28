using Shared.Core.Options;

namespace KH.Application.Common.Interfaces;

public interface IExternalConsumerContext
{
    bool IsAuthenticated { get; }

    string? AppId { get; }

    string? AppName { get; }

    string? SystemName { get; }

    string? CallerId { get; }

    bool AllowTwoWaySession { get; }

    int MaxActiveSessionPerAppAndTask { get; }

    int MaxActiveSessionPerSourceUser { get; }

    int MaxActiveSessionPerTargetUser { get; }

    string DuplicateSessionPolicy { get; }

    string IvrMessageEn { get; }

    string IvrMessageAr { get; }

    ExternalConsumerAppOptions? CurrentApp { get; }
}
