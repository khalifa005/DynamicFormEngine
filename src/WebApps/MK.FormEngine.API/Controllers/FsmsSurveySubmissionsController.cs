using KH.Application.Fsms.Submissions.Commands.SubmitSurvey;
using KH.Application.Fsms.Submissions.Queries.GetSubmissionById;
using KH.Application.Fsms.Submissions.Queries.GetSubmissions;
using KH.Domain.Constants.Fsms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Core.Common;
using Shared.Core.Constants;
using Shared.Logs.Audit;

namespace MK.FormEngine.API.Controllers;

[Authorize]
[Route("api/v1/fsms/templates/{templateId:long}/submissions")]
public sealed class FsmsSurveySubmissionsController : ApiControllerBase
{
    /// <summary>
    /// Records a fill — by the field team on site or by the back office afterwards. Also reachable
    /// from the field-team controller; kept here too since a survey is filled from both sides.
    /// </summary>
    [HttpPost]
    [AuditLog(EventName = AuditEventNames.FsmsUserSubmitSurvey)]
    [Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
    [ProducesResponseType(typeof(Result<long>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Submit(long templateId, [FromBody] SubmitSurveyCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command with { TemplateId = templateId }, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet]
    [Authorize(Policy = FsmsPolicies.ViewSurveys)]
    [ProducesResponseType(typeof(Result<PagedResult<IReadOnlyDictionary<string, object?>>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubmissions(long templateId, [FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken)
    {
        var query = new GetSubmissionsQuery
        {
            TemplateId = templateId,
            Page = page <= 0 ? 1 : page,
            PageSize = pageSize <= 0 ? 20 : pageSize,
        };

        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{submissionId:long}")]
    [Authorize(Policy = FsmsPolicies.ViewSurveys)]
    [ProducesResponseType(typeof(Result<IReadOnlyDictionary<string, object?>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubmissionById(long templateId, long submissionId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetSubmissionByIdQuery { TemplateId = templateId, SubmissionId = submissionId }, cancellationToken);
        return ToActionResult(result);
    }
}
