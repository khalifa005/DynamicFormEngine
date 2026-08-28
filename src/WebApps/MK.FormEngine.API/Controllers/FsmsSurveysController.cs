using KH.Application.Fsms.Surveys.Commands.AllocateSurvey;
using KH.Application.Fsms.Surveys.Commands.BulkAllocateSurveys;
using KH.Application.Fsms.Surveys.Commands.CompleteSurvey;
using KH.Application.Fsms.Surveys.Commands.CreateSurvey;
using KH.Application.Fsms.Surveys.Commands.CreateSurveyFromApi;
using KH.Application.Fsms.Surveys.Commands.ExpireSurvey;
using KH.Application.Fsms.Surveys.Commands.MigrateSurveyVersion;
using KH.Application.Fsms.Submissions.Queries.GetSurveyLatestFill;
using KH.Application.Fsms.Surveys.Commands.ReturnSurvey;
using KH.Application.Fsms.Surveys.Commands.UpdateSurvey;
using KH.Application.Fsms.Surveys.Models;
using KH.Application.Fsms.Surveys.Queries.GetAllocationSuggestions;
using KH.Application.Fsms.Surveys.Queries.GetSurveyById;
using KH.Application.Fsms.Surveys.Queries.GetSurveyFiles;
using KH.Application.Fsms.Surveys.Queries.ExportSurveyPdf;
using KH.Application.Fsms.Surveys.Queries.GetSurveyTimeline;
using KH.Application.Fsms.Surveys.Queries.GetSurveys;
using KH.Application.Fsms.Teams.Queries.GetEligibleTeamsForSurvey;
using KH.Domain.Constants;
using KH.Domain.Constants.Fsms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MK.FormEngine.API.Filters;
using Shared.Core.Common;
using Shared.Logs.Audit;
using AuditEventNames = Shared.Core.Constants.AuditEventNames;

namespace MK.FormEngine.API.Controllers;

[Authorize]
[Route("api/v1/fsms/surveys")]
public sealed class FsmsSurveysController : ApiControllerBase
{
    /// <summary>
    /// The received-survey feed from the originating system. Machine-to-machine, so it is guarded
    /// by an API key rather than a user token — <c>[AllowAnonymous]</c> lifts the controller's JWT
    /// requirement and <c>[RequireApiKey]</c> puts the key check in its place.
    /// </summary>
    [HttpPost("inbound")]
    [AllowAnonymous]
    [RequireApiKey]
    [ProducesResponseType(typeof(Result<SurveyDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateFromApi([FromBody] CreateSurveyFromApiCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet]
    [Authorize(Policy = FsmsPolicies.ViewSurveys)]
    [ProducesResponseType(typeof(Result<PagedResult<SurveyListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSurveys([FromQuery] GetSurveysQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = FsmsPolicies.ViewSurveys)]
    [ProducesResponseType(typeof(Result<SurveyDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSurveyById(long id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetSurveyByIdQuery { SurveyId = id }, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// The most recent fill recorded against the survey, so a repeat fill opens on the answers
    /// already given. Returns <c>hasFill: false</c> for a survey nobody has filled yet.
    /// </summary>
    [HttpGet("{id:long}/latest-fill")]
    [Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
    [ProducesResponseType(typeof(Result<SurveyLatestFillDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLatestFill(long id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetSurveyLatestFillQuery { SurveyId = id }, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{id:long}/timeline")]
    [Authorize(Policy = FsmsPolicies.ViewSurveys)]
    [ProducesResponseType(typeof(Result<IReadOnlyList<SurveyTimelineEntryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSurveyTimeline(long id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetSurveyTimelineQuery { SurveyId = id }, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{id:long}/files")]
    [Authorize(Policy = FsmsPolicies.ViewSurveys)]
    [ProducesResponseType(typeof(Result<IReadOnlyList<SurveyFileDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSurveyFiles(long id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetSurveyFilesQuery { SurveyId = id }, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{id:long}/export-pdf")]
    [Authorize(Policy = FsmsPolicies.ViewOrSubmitOrReviewSurveys)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportPdf(long id, [FromQuery] string language = "en", CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(new ExportSurveyPdfQuery { SurveyId = id, Language = language }, cancellationToken);
        if (!result.IsSuccess || result.Data is null)
        {
            return ToActionResult(result);
        }
        return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }

    [HttpPost]
    [AuditLog(EventName = AuditEventNames.FsmsUserCreateSurvey)]
    [Authorize(Policy = FsmsPolicies.CreateOrManageAssignments)]
    [ProducesResponseType(typeof(Result<SurveyDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateSurvey([FromBody] CreateSurveyCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Corrects a survey's location (CBU / branch / operation area), department or due date.</summary>
    [HttpPut("{id:long}")]
    [Authorize(Policy = FsmsPolicies.ManageAssignments)]
    [ProducesResponseType(typeof(Result<SurveyDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSurvey(long id, [FromBody] UpdateSurveyCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command with { SurveyId = id }, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Allocates a pending survey to a field team.</summary>
    [HttpPost("{id:long}/allocate")]
    [Authorize(Policy = FsmsPolicies.ManageAssignments)]
    [ProducesResponseType(typeof(Result<SurveyDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Allocate(long id, [FromBody] AllocateSurveyCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command with { SurveyId = id }, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// The crews that may take this survey — active, inside the caller's territory, and scoped to
    /// the survey's own location and department. What the allocate picker is filled from.
    /// </summary>
    [HttpGet("{id:long}/eligible-teams")]
    [Authorize(Policy = FsmsPolicies.ViewSurveys)]
    [ProducesResponseType(typeof(Result<IReadOnlyList<EligibleTeamDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEligibleTeams(long id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetEligibleTeamsForSurveyQuery { SurveyId = id }, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Groups the selected surveys by the location that decides their coverage and names the crews
    /// scoped to each group. A POST because the selection is a list of ids, not a filter.
    /// </summary>
    [HttpPost("allocation-suggestions")]
    [Authorize(Policy = FsmsPolicies.ManageAssignments)]
    [ProducesResponseType(typeof(Result<AllocationSuggestionsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllocationSuggestions(
        [FromBody] GetAllocationSuggestionsQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Allocates many surveys in one call. Reports per survey rather than failing the run, so one
    /// survey that moved on in the meantime does not cost the rest of the batch.
    /// </summary>
    [HttpPost("bulk-allocate")]
    [AuditLog(EventName = AuditEventNames.FsmsUserBulkAllocateSurvey)]
    [Authorize(Policy = FsmsPolicies.ManageAssignments)]
    [ProducesResponseType(typeof(Result<BulkAllocationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BulkAllocate(
        [FromBody] BulkAllocateSurveysCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Moves an in-flight survey onto its template's newest published version. The survey stays
    /// pinned otherwise, so this is the only way it picks a republish up.
    /// </summary>
    [HttpPost("{id:long}/migrate-version")]
    [Authorize(Policy = FsmsPolicies.ManageAssignments)]
    [ProducesResponseType(typeof(Result<SurveyDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MigrateVersion(long id, [FromBody] MigrateSurveyVersionCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command with { SurveyId = id }, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Closes a filled survey. Acts straight on <c>SUBMITTED</c> — there is no receive step.</summary>
    [HttpPost("{id:long}/complete")]
    [Authorize(Policy = FsmsPolicies.ReviewSurveys)]
    [ProducesResponseType(typeof(Result<SurveyDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Complete(long id, [FromBody] CompleteSurveyCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command with { SurveyId = id }, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{id:long}/return")]
    [Authorize(Policy = FsmsPolicies.ReviewSurveys)]
    [ProducesResponseType(typeof(Result<SurveyDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Return(long id, [FromBody] ReturnSurveyCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command with { SurveyId = id }, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{id:long}/expire")]
    [Authorize(Roles = Roles.Administrator)]
    [ProducesResponseType(typeof(Result<SurveyDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Expire(long id, [FromBody] ExpireSurveyCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command with { SurveyId = id }, cancellationToken);
        return ToActionResult(result);
    }
}
