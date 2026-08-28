using KH.Application.Fsms.Common.Models;
using Shared.Core.Common;

namespace KH.Application.Fsms.Common.Interfaces;

/// <summary>
/// Abstraction over the source of field-team identity, branch assignment, and schedule data. The
/// concrete source (local mirror vs. WFM API) is resolved per concern via
/// <c>TeamIntegrationOptions</c>. The WFM-backed implementation is stubbed until task-09.
/// </summary>
public interface ITeamDirectory
{
    Task<Result<TeamDto>> GetTeamAsync(long teamId, CancellationToken ct);

    Task<Result<IReadOnlyList<TeamDto>>> GetTeamsByBranchAsync(string branchCode, CancellationToken ct);

    Task<Result<IReadOnlyList<TeamScheduleDto>>> GetSchedulesAsync(long teamId, DateOnly date, CancellationToken ct);
}
