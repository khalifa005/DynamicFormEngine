using KH.Application.Auth.Commands.LoginFieldTeam;
using KH.Application.Fsms.Teams.Commands.UpdateFieldTeamLastActive;
using KH.Application.Auth.Commands.Logout;
using KH.Application.Auth.Models;
using KH.Application.Auth.Queries.GetCurrentUserProfile;
using KH.Application.Fsms.Lookups.Cbus;
using KH.Application.Fsms.Lookups.Clusters;
using KH.Application.Fsms.Lookups.Departments;
using KH.Application.Fsms.Lookups.CustomerTypes;
using KH.Application.Fsms.Lookups.FaTypes;
using KH.Application.Fsms.Lookups.OperationAreas;
using KH.Application.Fsms.Lookups.TaskTypes;
using KH.Application.Fsms.Lookups.Statuses;
using KH.Application.Fsms.Lookups.Branches;
using KH.Application.Fsms.Submissions.Commands.BulkSubmitSurveys;
using KH.Application.Fsms.Submissions.Commands.SubmitSurvey;
using KH.Application.Fsms.Surveys.Commands.CreateFieldTeamSurvey;
using KH.Application.Fsms.Surveys.Models;
using KH.Application.Fsms.Surveys.Queries.GetFieldTeamCreatedSurveyStats;
using KH.Application.Fsms.Surveys.Queries.GetFieldTeamSurveyById;
using KH.Application.Fsms.Surveys.Queries.GetFieldTeamSurveys;
using KH.Application.Fsms.Surveys.Queries.GetFieldTeamSurveyStatuses;
using KH.Application.Fsms.Surveys.Queries.GetFieldTeamSyncBundle;
using KH.Application.Fsms.Surveys.Queries.ExportSurveyPdf;
using KH.Application.Fsms.Templates.Queries.GetAvailableTemplatesForFieldTeam;
using KH.Application.Fsms.Uploads.Commands.DeleteSubmissionFile;
using KH.Application.Fsms.Uploads.Commands.UploadSubmissionFile;
using KH.Application.Fsms.Uploads.Queries.GetSubmissionFile;
using KH.Domain.Constants.Fsms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shared.Core.Common;
using Shared.Core.Constants;
using Shared.Logs.Audit;

namespace MK.FormEngine.API.Controllers;

/// <summary>
/// The complete field-team (mobile survey crew) API surface — sign-in, own profile, the crew's own
/// allocated worklist, and the fill/submit/media workflow. Kept as one controller so it can be
/// lifted out into its own deployment later without untangling it from back-office endpoints.
///
/// Deliberately narrow. Every survey read here is limited to work allocated to the team on the
/// caller's token, and no review-side action (complete, return) is exposed: those belong to the
/// back office and live on <see cref="FsmsSurveysController"/>.
/// </summary>
[Authorize]
[Route("api/v1/fsms/field-team")]
public sealed class FsmsFieldTeamController : ApiControllerBase
{
    /// <summary>
    /// Mobile sign-in for a field crew. The crew signs in with its team user code; the token comes
    /// back carrying the team it belongs to. Optional device and last-active location fields on the
    /// body are stored on the team and overwritten on every successful sign-in.
    /// </summary>
    [HttpPost("team-login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Result<AuthTokenDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> TeamLogin([FromBody] LoginFieldTeamCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Signs the crew out. Post the refresh token to close just this device's session, or an empty
    /// body to close every session the crew account holds — which is what a shared device should do
    /// when it is handed back. The access token already issued is not recalled, so the app must
    /// clear its own copy too.
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] LogoutCommand? command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command ?? new LogoutCommand(), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Periodic device and last-active location ping for the crew on the token. Same optional body
    /// as team-login (<c>deviceName</c>, <c>uuid</c>, <c>version</c>, <c>os</c>,
    /// <c>lastActiveLatitude</c>, <c>lastActiveLongitude</c>). The mobile app can call this on a
    /// timer (for example every hour) without signing in again.
    /// </summary>
    [HttpPost("me/last-active")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMyLastActive(
        [FromBody] UpdateFieldTeamLastActiveCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>The caller's own identity, roles/permissions and resolved territory.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(Result<CurrentUserProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetCurrentUserProfileQuery(), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Daily created vs currently-approved counts for field-raised surveys this crew raised itself.
    /// Approvals stay on the creation day even when the reviewer acted later, which is the figure
    /// the profile chart and a pay run both need. The range is inclusive
    /// (<c>?fromDate=2026-08-01&amp;toDate=2026-08-16</c>); days with no work come back as zeros.
    /// </summary>
    [HttpGet("me/created-survey-stats")]
    [Authorize(Policy = FsmsPolicies.CreateOrManageAssignments)]
    [ProducesResponseType(typeof(Result<FieldTeamCreatedSurveyStatsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyCreatedSurveyStats(
        [FromQuery] GetFieldTeamCreatedSurveyStatsQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    // ---------------------------------------------------------------------------------------
    // The crew's own worklist
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Every survey allocated to the caller's own team. The team is read from the token — there is
    /// no way to ask for another crew's list.
    ///
    /// Narrowed by lifecycle state (<c>?statuses=ASSIGNED&amp;statuses=RETURNED</c>, or one
    /// comma-separated value), by one of the team's own working scopes (<c>?scopeId=</c>, as
    /// <c>GET me</c> reports them), and by free text. Finished work — completed or expired — is left
    /// out unless <c>?includeClosed=true</c> asks for it. The codes the filter accepts are listed by
    /// <c>GET lookups/survey-statuses</c>.
    /// </summary>
    [HttpGet("surveys")]
    [Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
    [ProducesResponseType(typeof(Result<PagedResult<FieldTeamSurveyListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMySurveys(
        [FromQuery] GetFieldTeamSurveysQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// One allocated survey with everything needed to open its form: the form definition frozen at
    /// the survey's pinned version, and the answers already recorded against it. A survey not
    /// allocated to the caller's team answers as "not found".
    /// </summary>
    [HttpGet("surveys/{id:long}")]
    [Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
    [ProducesResponseType(typeof(Result<FieldTeamSurveyDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMySurveyById(long id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetFieldTeamSurveyByIdQuery { SurveyId = id }, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("surveys/{id:long}/export-pdf")]
    [Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportSurveyPdf(long id, [FromQuery] string language = "en", CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(new ExportSurveyPdfQuery { SurveyId = id, Language = language }, cancellationToken);
        if (!result.IsSuccess || result.Data is null)
        {
            return ToActionResult(result);
        }
        return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }

    /// <summary>
    /// Where a batch of surveys the app already holds currently stand. Built for the reconciliation
    /// the app runs after draining its offline queue, or after a submit whose response was lost:
    /// status alone for as many surveys as it asks about, in one call, rather than a detail read per
    /// survey or the whole sync bundle.
    ///
    /// Surveys are named by server id, by the code the device generated, or both — a create whose
    /// response never came back leaves the app holding only the code. Keys that answer nothing,
    /// including work allocated away from the caller's team, come back in the not-found lists.
    ///
    /// POST rather than GET because the selection is a list of keys, as on
    /// <c>POST surveys/allocation-suggestions</c>.
    /// </summary>
    [HttpPost("surveys/statuses")]
    [Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
    [ProducesResponseType(typeof(Result<FieldTeamSurveyStatusBatchDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMySurveyStatuses(
        [FromBody] GetFieldTeamSurveyStatusesQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Records a fill against an allocated survey. The allocation is resolved from the caller's
    /// token, so the crew posts answers alone — it cannot fill work handed to another team.
    /// </summary>
    [HttpPost("surveys/{id:long}/submit")]
    [AuditLog(EventName = AuditEventNames.FsmsFieldTeamSubmitSurvey)]
    [Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
    [ProducesResponseType(typeof(Result<long>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitSurvey(
        long id, [FromBody] SubmitSurveyCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command with { SurveyId = id }, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Drains the app's offline queue: every survey the crew filled out of coverage, sent in one
    /// call when the connection returns. Each item reports on its own, and an item already accepted
    /// under the same client key comes back marked as a duplicate rather than being filed twice, so
    /// the whole queue can safely be re-sent after a dropped response.
    /// </summary>
    //[HttpPost("surveys/bulk-submit")]
    //[AuditLog(EventName = AuditEventNames.FsmsFieldTeamBulkSubmitSurveys)]
    //[Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
    //[ProducesResponseType(typeof(Result<BulkSubmissionResultDto>), StatusCodes.Status200OK)]
    //public async Task<IActionResult> BulkSubmitSurveys(
    //    [FromBody] BulkSubmitSurveysCommand command, CancellationToken cancellationToken)
    //{
    //    var result = await Mediator.Send(command, cancellationToken);
    //    return ToActionResult(result);
    //}

    /// <summary>
    /// The offline cache in one read: the crew's open work plus the form definitions to render it,
    /// each definition sent once however many surveys share it. Pass the previous bundle's
    /// <c>serverTime</c> as <c>since</c> to fetch only what changed.
    /// </summary>
    //[HttpGet("sync")]
    //[Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
    //[ProducesResponseType(typeof(Result<FieldTeamSyncBundleDto>), StatusCodes.Status200OK)]
    //public async Task<IActionResult> Sync(
    //    [FromQuery] GetFieldTeamSyncBundleQuery query, CancellationToken cancellationToken)
    //{
    //    var result = await Mediator.Send(query, cancellationToken);
    //    return ToActionResult(result);
    //}

    /// <summary>
    /// Records a whole piece of field-raised work in one call: the crew names the working scope, the
    /// survey code it generated on the device, the template and the answers, and the API creates the
    /// survey (stamped <c>TEAM</c>), self-allocates it to the caller's own team and writes the
    /// submission in one transaction. Filling work the back office allocated to the crew still goes
    /// through <c>POST surveys/{id}/submit</c>.
    ///
    /// The generated survey code is the natural key that makes the call safe to re-send from an
    /// offline queue: a code already on record is rejected 409 (already accepted — drop it from the
    /// queue), never replayed.
    /// </summary>
    [HttpPost("surveys")]
    [AuditLog(EventName = AuditEventNames.FsmsFieldTeamCreateSurvey)]
    [Authorize(Policy = FsmsPolicies.CreateOrManageAssignments)]
    [ProducesResponseType(typeof(Result<FieldTeamSurveySubmissionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateSurvey(
        [FromBody] CreateFieldTeamSurveyCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Published templates the caller may raise a new survey against, within its own territory.
    /// Filling allocated work does not need this: that survey carries its own definition.
    /// </summary>
    [HttpGet("templates")]
    [Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
    [ProducesResponseType(typeof(Result<PagedResult<FieldTeamTemplateListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableTemplates(
        [FromQuery] GetAvailableTemplatesForFieldTeamQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    // ---------------------------------------------------------------------------------------
    // Reference data for the raise-a-survey form
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Every status code the system supports, in English and Arabic — the survey lifecycle states the
    /// worklist filter accepts, and the allocation states a worklist row can carry. Static reference
    /// data: fetch once and cache on the device.
    /// </summary>
    [HttpGet("lookups/survey-statuses")]
    [ProducesResponseType(typeof(Result<SurveyStatusCatalogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSurveyStatuses(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetSurveyStatusCatalogQuery(), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Departments, to classify the kind of work a raised survey covers.</summary>
    [HttpGet("lookups/departments")]
    [Authorize(Policy = FsmsPolicies.ViewTemplates)]
    [ProducesResponseType(typeof(Result<PagedResult<FsmsDepartmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDepartments(
        [FromQuery] GetDepartmentsQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Clusters — the top of the org hierarchy.</summary>
    [HttpGet("lookups/clusters")]
    [Authorize(Policy = FsmsPolicies.ViewTemplates)]
    [ProducesResponseType(typeof(Result<PagedResult<FsmsClusterDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClusters(
        [FromQuery] GetClustersQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>CBUs — the cascading step under a cluster.</summary>
    [HttpGet("lookups/cbus")]
    [Authorize(Policy = FsmsPolicies.ViewTemplates)]
    [ProducesResponseType(typeof(Result<PagedResult<FsmsCbuDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCbus(
        [FromQuery] GetCbusQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Branches / CBU branches — the cascading step under a CBU.</summary>
    [HttpGet("lookups/branches")]
    [Authorize(Policy = FsmsPolicies.ViewTemplates)]
    [ProducesResponseType(typeof(Result<PagedResult<FsmsBranchDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBranches(
        [FromQuery] GetBranchesQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Operation areas — a leaf of the org hierarchy, under a CBU and beside a branch.</summary>
    [HttpGet("lookups/operation-areas")]
    [Authorize(Policy = FsmsPolicies.ViewTemplates)]
    [ProducesResponseType(typeof(Result<PagedResult<FsmsOperationAreaDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOperationAreas(
        [FromQuery] GetOperationAreasQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>FA types — selects the template a raised survey is answered against.</summary>
    [HttpGet("lookups/fa-types")]
    [Authorize(Policy = FsmsPolicies.ViewTemplates)]
    [ProducesResponseType(typeof(Result<PagedResult<FsmsFaTypeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFaTypes(
        [FromQuery] GetFaTypesQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Task types — classifies the kind of work a raised survey covers.</summary>
    [HttpGet("lookups/task-types")]
    [Authorize(Policy = FsmsPolicies.ViewTemplates)]
    [ProducesResponseType(typeof(Result<PagedResult<FsmsTaskTypeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTaskTypes(
        [FromQuery] GetTaskTypesQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Customer types — residential, commercial, and similar classes.</summary>
    [HttpGet("lookups/customer-types")]
    [Authorize(Policy = FsmsPolicies.ViewTemplates)]
    [ProducesResponseType(typeof(Result<PagedResult<FsmsCustomerTypeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomerTypes(
        [FromQuery] GetCustomerTypesQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    // ---------------------------------------------------------------------------------------
    // Media
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Stores one picked media file straight away, before the form is submitted. The row starts
    /// <c>PENDING</c> with no <c>SubmissionId</c>; submitting the form links it.
    /// </summary>
    [HttpPost("uploads")]
    [Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
    [ProducesResponseType(typeof(Result<UploadedFileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadFile(
        IFormFile file,
        [FromForm] long templateId,
        [FromForm] string dataName,
        [FromForm] long? surveyId,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return ToActionResult(Result<UploadedFileDto>.Fail(
                "A file is required.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 400));
        }

        await using var content = file.OpenReadStream();

        var command = new UploadSubmissionFileCommand
        {
            TemplateId = templateId,
            SurveyId = surveyId,
            DataName = dataName,
            FileName = file.FileName,
            ContentType = file.ContentType ?? string.Empty,
            SizeBytes = file.Length,
            Content = content,
        };

        var result = await Mediator.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("uploads/{fileId:guid}")]
    [Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteFile(Guid fileId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteSubmissionFileCommand { FileId = fileId }, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("uploads/{fileId:guid}")]
    [Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFile(Guid fileId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetSubmissionFileQuery { FileId = fileId }, cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            return ToActionResult(result);
        }

        // FileStreamResult disposes the stream once the response has been written.
        return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }
}
