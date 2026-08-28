using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Uploads.Queries.GetSubmissionFile;

/// <summary>
/// Streams an uploaded media file back by its public handle. Used both while the form is still
/// being filled (preview) and when reviewing a submitted row.
/// </summary>
[Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
public record GetSubmissionFileQuery : IRequest<Result<SubmissionFileContentDto>>
{
    public Guid FileId { get; init; }
}

/// <summary>The open stream plus what the response needs to describe it. The caller owns the stream.</summary>
public sealed record SubmissionFileContentDto(Stream Content, string ContentType, string FileName);

public sealed class GetSubmissionFileQueryValidator : AbstractValidator<GetSubmissionFileQuery>
{
    public GetSubmissionFileQueryValidator()
    {
        RuleFor(x => x.FileId)
            .NotEmpty().WithMessage("File id is required.");
    }
}

public sealed class GetSubmissionFileQueryHandler(
    IApplicationDbContext context,
    IFileStorage fileStorage)
    : IRequestHandler<GetSubmissionFileQuery, Result<SubmissionFileContentDto>>
{
    public async Task<Result<SubmissionFileContentDto>> Handle(GetSubmissionFileQuery request, CancellationToken cancellationToken)
    {
        var file = await context.SubmissionFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.FileId == request.FileId && x.IsActive, cancellationToken);

        if (file is null)
        {
            return Result<SubmissionFileContentDto>.Fail("File not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        try
        {
            var content = await fileStorage.OpenReadAsync(file.RelativePath, cancellationToken);
            return Result<SubmissionFileContentDto>.Success(
                new SubmissionFileContentDto(content, file.ContentType, file.FileName));
        }
        catch (FileNotFoundException)
        {
            // The row outlived the bytes — report it as missing rather than a 500.
            return Result<SubmissionFileContentDto>.Fail(
                "The stored file is no longer available.",
                ApiErrorCodes.NotFound,
                httpStatusCode: 404);
        }
    }
}
