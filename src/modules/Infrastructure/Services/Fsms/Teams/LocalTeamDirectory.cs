using KH.Application.Common.Interfaces;
using KH.Application.Fsms.Common.Interfaces;
using KH.Application.Fsms.Common.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Core.Common;

namespace KH.Infrastructure.Services.Fsms.Teams;

/// <summary>
/// Reads field-team identity/branch/schedule data from the local FSMS mirror tables.
/// </summary>
public sealed class LocalTeamDirectory(IApplicationDbContext context) : ITeamDirectory
{
    public async Task<Result<TeamDto>> GetTeamAsync(long teamId, CancellationToken ct)
    {
        var team = await context.FieldTeams
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == teamId, ct);

        if (team is null)
        {
            return Result<TeamDto>.Fail("Team not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        var contractors = await LoadContractorsAsync(ct);
        return Result<TeamDto>.Success(Map(team, contractors));
    }

    public async Task<Result<IReadOnlyList<TeamDto>>> GetTeamsByBranchAsync(string branchCode, CancellationToken ct)
    {
        var normalizedBranch = branchCode?.Trim();

        // Branch assignment for the local source is derived from schedules covering the branch.
        var teamIds = await context.TeamSchedules
            .AsNoTracking()
            .Where(x => x.IsActive && x.WorkBranch == normalizedBranch)
            .Select(x => x.TeamId)
            .Distinct()
            .ToListAsync(ct);

        var teams = await context.FieldTeams
            .AsNoTracking()
            .Where(x => teamIds.Contains(x.Id))
            .ToListAsync(ct);

        var contractors = await LoadContractorsAsync(ct);
        IReadOnlyList<TeamDto> result = teams.Select(team => Map(team, contractors)).ToList();
        return Result<IReadOnlyList<TeamDto>>.Success(result);
    }

    public async Task<Result<IReadOnlyList<TeamScheduleDto>>> GetSchedulesAsync(long teamId, DateOnly date, CancellationToken ct)
    {
        var schedules = await context.TeamSchedules
            .AsNoTracking()
            .Where(x => x.TeamId == teamId && x.ScheduleDate == date)
            .ToListAsync(ct);

        IReadOnlyList<TeamScheduleDto> result = schedules
            .Select(x => new TeamScheduleDto
            {
                TeamId = x.TeamId,
                ScheduleDate = x.ScheduleDate,
                WorkStart = x.WorkStart,
                WorkEnd = x.WorkEnd,
                WorkBranch = x.WorkBranch,
                IsActive = x.IsActive
            })
            .ToList();

        return Result<IReadOnlyList<TeamScheduleDto>>.Success(result);
    }

    private async Task<IReadOnlyDictionary<int, (string PoNumber, string NameEn, string NameAr)>> LoadContractorsAsync(
        CancellationToken ct)
    {
        return await context.FsmsContractors
            .AsNoTracking()
            .Select(x => new { x.Id, x.PoNumber, x.NameEn, x.NameAr })
            .ToDictionaryAsync(x => x.Id, x => (x.PoNumber, x.NameEn, x.NameAr), ct);
    }

    private static TeamDto Map(
        Domain.Entities.Fsms.Teams.FieldTeam team,
        IReadOnlyDictionary<int, (string PoNumber, string NameEn, string NameAr)> contractors)
    {
        (string PoNumber, string NameEn, string NameAr)? contractor = team.ContractorId is int id
            && contractors.TryGetValue(id, out var row)
            ? row
            : null;

        return new TeamDto
        {
            TeamId = team.Id,
            UserCode = team.UserCode,
            Name = team.Name,
            Mobile = team.Mobile,
            TeamType = team.TeamType,
            ContractorId = team.ContractorId,
            ContractorPoNumber = contractor?.PoNumber,
            ContractorNameEn = contractor?.NameEn,
            ContractorNameAr = contractor?.NameAr,
            Departments = string.IsNullOrWhiteSpace(team.Departments)
                ? []
                : team.Departments.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            IsActive = team.IsActive,
            DeviceName = team.DeviceName,
            DeviceUuid = team.DeviceUuid,
            AppVersion = team.AppVersion,
            DeviceOs = team.DeviceOs,
            LastActiveLatitude = team.LastActiveLatitude,
            LastActiveLongitude = team.LastActiveLongitude,
            LastActiveAt = team.LastActiveAt
        };
    }
}
