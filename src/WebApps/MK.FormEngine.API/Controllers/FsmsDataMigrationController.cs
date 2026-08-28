using KH.Application.Fsms.DataMigration.Commands.RetryMigrationMedia;
using KH.Application.Fsms.DataMigration.Commands.StartDataMigration;
using KH.Application.Fsms.DataMigration.Models;
using KH.Application.Fsms.DataMigration.Queries.GetMigrationRunById;
using KH.Application.Fsms.DataMigration.Queries.GetMigrationRuns;
using KH.Application.Fsms.DataMigration.Queries.GetMigrationSetup;
using KH.Domain.Constants.Fsms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Core.Common;

namespace MK.FormEngine.API.Controllers;

/// <summary>
/// Imports of historical data from external systems. The operator uploads one file, picks the
/// source it came from and where its surveys should sit; the work runs out of band and is followed
/// by polling the run it produced.
/// </summary>
[Authorize]
[Route("api/v1/fsms/data-migration")]
public sealed class FsmsDataMigrationController : ApiControllerBase
{
    /// <summary>The sources this deployment can import from, and where the migration archive lives.</summary>
    [HttpGet("setup")]
    [Authorize(Policy = FsmsPolicies.ImportData)]
    [ProducesResponseType(typeof(Result<MigrationSetupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSetup(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetMigrationSetupQuery(), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Queues an import. Answers as soon as the file is stored and the run is recorded — the records
    /// themselves are written by a background job, so the response carries a run id to poll rather
    /// than a result.
    /// </summary>
    [HttpPost("runs")]
    [Authorize(Policy = FsmsPolicies.ImportData)]
    [ProducesResponseType(typeof(Result<StartedMigrationRunDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> StartRun(
        IFormFile file,
        [FromForm] string sourceCode,
        [FromForm] long templateId,
        [FromForm] string mode,
        [FromForm] string? cbuCode,
        [FromForm] string? branchCode,
        [FromForm] string? operationAreaCode,
        [FromForm] int? departmentId,
        [FromForm] long? fieldTeamId,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return ToActionResult(Result<StartedMigrationRunDto>.Fail(
                "A file is required.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 400));
        }

        await using var content = file.OpenReadStream();

        var command = new StartDataMigrationCommand
        {
            SourceCode = sourceCode ?? string.Empty,
            TemplateId = templateId,
            Mode = mode ?? MigrationModes.Validate,
            FileName = file.FileName,
            SizeBytes = file.Length,
            Content = content,
            CbuCode = cbuCode,
            BranchCode = branchCode,
            OperationAreaCode = operationAreaCode,
            DepartmentId = departmentId,
            FieldTeamId = fieldTeamId,
        };

        var result = await Mediator.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Attaches media that reached the archive after an import ran. Reuses the original run's stored
    /// workbook and options, so nothing is uploaded; answers a new run id to poll, like an import.
    /// </summary>
    [HttpPost("runs/{runId:long}/retry-media")]
    [Authorize(Policy = FsmsPolicies.ImportData)]
    [ProducesResponseType(typeof(Result<StartedMigrationRunDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RetryMedia(long runId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new RetryMigrationMediaCommand { RunId = runId }, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("runs")]
    [Authorize(Policy = FsmsPolicies.ImportData)]
    [ProducesResponseType(typeof(Result<PagedResult<MigrationRunListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRuns(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sourceCode = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetMigrationRunsQuery
        {
            Page = page,
            PageSize = pageSize,
            SourceCode = sourceCode,
            Status = status,
        };

        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>One run and a page of its per-record outcomes. Also the endpoint the page polls.</summary>
    [HttpGet("runs/{runId:long}")]
    [Authorize(Policy = FsmsPolicies.ImportData)]
    [ProducesResponseType(typeof(Result<MigrationRunDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRunById(
        long runId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? recordStatus = null,
        [FromQuery] bool? withMissingMedia = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetMigrationRunByIdQuery
        {
            RunId = runId,
            Page = page,
            PageSize = pageSize,
            RecordStatus = recordStatus,
            WithMissingMedia = withMissingMedia,
        };

        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }
}
