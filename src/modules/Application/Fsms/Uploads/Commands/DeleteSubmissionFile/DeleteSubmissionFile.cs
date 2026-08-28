using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Uploads.Commands.DeleteSubmissionFile;

/// <summary>
/// Removes a file. <c>PENDING</c> files are hard deleted.
/// Files linked to a submission are soft deleted.
/// </summary>
[Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
public record DeleteSubmissionFileCommand : IRequest<Result<bool>>
{
    public Guid FileId { get; init; }
}

public sealed class DeleteSubmissionFileCommandValidator : AbstractValidator<DeleteSubmissionFileCommand>
{
    public DeleteSubmissionFileCommandValidator()
    {
        RuleFor(x => x.FileId)
            .NotEmpty().WithMessage("File id is required.");
    }
}

public sealed class DeleteSubmissionFileCommandHandler(
    IApplicationDbContext context,
    IFileStorage fileStorage)
    : IRequestHandler<DeleteSubmissionFileCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteSubmissionFileCommand request, CancellationToken cancellationToken)
    {
        var file = await context.SubmissionFiles
            .FirstOrDefaultAsync(x => x.FileId == request.FileId, cancellationToken);

        if (file is null)
        {
            return Result<bool>.Fail("File not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        // A migrated file's bytes belong to the archive placed on the server in bulk, and may be
        // referenced by more than one record. Dropping our reference is the most this may ever do to
        // it — erasing the master copy because one survey no longer wants it would be unrecoverable.
        if (!file.IsPending || file.IsMigrated)
        {
            file.Deactivate();
        }
        else
        {
            await fileStorage.DeleteAsync(file.RelativePath, cancellationToken);
            context.SubmissionFiles.Remove(file);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
