using KH.Application.Fsms.Uploads.Commands.DeleteSubmissionFile;
using KH.Application.Fsms.Uploads.Commands.UploadSubmissionFile;
using KH.Application.Fsms.Uploads.Queries.GetSubmissionFile;
using KH.Domain.Constants.Fsms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Core.Common;

namespace MK.FormEngine.API.Controllers;

/// <summary>
/// Real-time media uploads for survey forms. A file is stored the moment the user picks it and
/// stays <c>PENDING</c> until the form is submitted, so the submit payload only carries references.
/// Also reachable from the field-team controller; kept here too since a survey is filled — and its
/// media picked — from both sides, the field team on site and the back office afterwards.
/// </summary>
[Authorize]
[Route("api/v1/fsms/uploads")]
public sealed class FsmsUploadsController : ApiControllerBase
{
    [HttpPost]
    [Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
    [ProducesResponseType(typeof(Result<UploadedFileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Upload(
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

    [HttpDelete("{fileId:guid}")]
    [Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid fileId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteSubmissionFileCommand { FileId = fileId }, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{fileId:guid}")]
    [Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Download(Guid fileId, CancellationToken cancellationToken)
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
