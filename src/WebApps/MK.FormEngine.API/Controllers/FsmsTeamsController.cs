using KH.Application.Fsms.Common.Interfaces;
using KH.Application.Fsms.Common.Models;
using KH.Application.Fsms.Teams.Commands.CreateTeam;
using KH.Application.Fsms.Teams.Commands.UpdateTeam;
using KH.Application.Fsms.Teams.Queries.GetTeamsPaged;
using KH.Application.Fsms.Teams.Queries.GetTeamSurveyRoute;
using KH.Domain.Constants.Fsms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Core.Common;

namespace MK.FormEngine.API.Controllers;

/// <summary>
/// Diagnostic endpoints to verify the <see cref="ITeamDirectory"/> router resolves to the configured
/// source. Team management proper is delivered in later phases.
/// </summary>
[Authorize]
[Route("api/v1/fsms/teams")]
public sealed class FsmsTeamsController(ITeamDirectory teamDirectory) : ApiControllerBase
{
    [HttpGet("{id:long}")]
    [Authorize(Policy = FsmsPolicies.ViewSurveys)]
    [ProducesResponseType(typeof(Result<TeamDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeam(long id, CancellationToken cancellationToken)
    {
        var result = await teamDirectory.GetTeamAsync(id, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("by-branch/{branchCode}")]
    [Authorize(Policy = FsmsPolicies.ViewSurveys)]
    [ProducesResponseType(typeof(Result<IReadOnlyList<TeamDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeamsByBranch(string branchCode, CancellationToken cancellationToken)
    {
        var result = await teamDirectory.GetTeamsByBranchAsync(branchCode, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{id:long}/schedules")]
    [Authorize(Policy = FsmsPolicies.ViewSurveys)]
    [ProducesResponseType(typeof(Result<IReadOnlyList<TeamScheduleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSchedules(long id, [FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        var result = await teamDirectory.GetSchedulesAsync(id, date, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// The crew's working day in order — every survey they submitted on <paramref name="date"/>,
    /// earliest first, with the asset coordinate behind each one and the straight-line gap to the
    /// previous stop. <paramref name="utcOffsetMinutes"/> decides which civil day is being asked for;
    /// left off, the day is read in Saudi time.
    /// </summary>
    [HttpGet("{id:long}/survey-route")]
    [Authorize(Policy = FsmsPolicies.ViewSurveys)]
    [ProducesResponseType(typeof(Result<TeamSurveyRouteDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSurveyRoute(
        long id,
        [FromQuery] DateOnly date,
        [FromQuery] int? utcOffsetMinutes,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new GetTeamSurveyRouteQuery
            {
                FieldTeamId = id,
                Date = date,
                UtcOffsetMinutes = utcOffsetMinutes
            },
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpGet]
    [Authorize(Policy = FsmsPolicies.ViewSurveys)]
    [ProducesResponseType(typeof(Result<PagedResult<TeamDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeamsPaged([FromQuery] GetTeamsPagedQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost]
    [Authorize(Policy = FsmsPolicies.ManageTeams)]
    [ProducesResponseType(typeof(Result<long>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTeam([FromBody] CreateTeamCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = FsmsPolicies.ManageTeams)]
    [ProducesResponseType(typeof(Result<long>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateTeam(long id, [FromBody] UpdateTeamCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id)
            return BadRequest("Id mismatch.");

        var result = await Mediator.Send(command, cancellationToken);
        return ToActionResult(result);
    }
}
