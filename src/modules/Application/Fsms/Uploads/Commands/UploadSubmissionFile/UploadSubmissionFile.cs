using KH.Application.Common.Interfaces;
using KH.Application.Common.Options;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Definition;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Submissions;
using Microsoft.Extensions.Options;
using Shared.Core.Common;

namespace KH.Application.Fsms.Uploads.Commands.UploadSubmissionFile;

/// <summary>
/// Stores one picked media file straight away, before the form is submitted. The row starts
/// <c>PENDING</c> with no <c>SubmissionId</c>; submitting the form links it. Open to whoever may
/// fill a form — the back office fills its own part of a survey and picks media while doing so.
/// </summary>
[Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
public record UploadSubmissionFileCommand : IRequest<Result<UploadedFileDto>>
{
    public long TemplateId { get; init; }

    public long? SurveyId { get; init; }

    /// <summary>The field (<c>data_name</c>) the file was picked for.</summary>
    public string DataName { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;

    /// <summary>Size reported by the request; the stored size is re-measured after writing.</summary>
    public long SizeBytes { get; init; }

    /// <summary>Request body stream — read once by the handler, never buffered into memory.</summary>
    public Stream Content { get; init; } = Stream.Null;
}

/// <summary>What the client keeps in the field value: enough to render and to reference on submit.</summary>
public sealed record UploadedFileDto(Guid FileId, string Path, string Name, string Type, long Size);

public sealed class UploadSubmissionFileCommandValidator : AbstractValidator<UploadSubmissionFileCommand>
{
    public UploadSubmissionFileCommandValidator()
    {
        RuleFor(x => x.TemplateId)
            .GreaterThan(0).WithMessage("Template id is required.");

        RuleFor(x => x.DataName)
            .NotEmpty().WithMessage("The field data name is required.")
            .MaximumLength(200).WithMessage("The field data name must not exceed 200 characters.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("A file name is required.")
            .MaximumLength(400).WithMessage("The file name must not exceed 400 characters.");

        RuleFor(x => x.SizeBytes)
            .GreaterThan(0).WithMessage("An empty file cannot be uploaded.");
    }
}

public sealed class UploadSubmissionFileCommandHandler(
    IApplicationDbContext context,
    IFileStorage fileStorage,
    IOptions<FileStorageOptions> options)
    : IRequestHandler<UploadSubmissionFileCommand, Result<UploadedFileDto>>
{
    private const int BytesPerMb = 1024 * 1024;

    public async Task<Result<UploadedFileDto>> Handle(UploadSubmissionFileCommand request, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        var maxBytes = (long)settings.MaxFileSizeMb * BytesPerMb;
        if (request.SizeBytes > maxBytes)
        {
            return Result<UploadedFileDto>.Fail(
                $"The file exceeds the {settings.MaxFileSizeMb} MB upload limit.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 400);
        }

        if (!IsAllowedContentType(request.ContentType, settings.AllowedContentTypes))
        {
            return Result<UploadedFileDto>.Fail(
                $"Content type '{request.ContentType}' is not accepted.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 400);
        }

        var definitionJson = await context.SurveyTemplates
            .Where(x => x.Id == request.TemplateId)
            .Select(x => x.DefinitionJson)
            .FirstOrDefaultAsync(cancellationToken);

        if (definitionJson is null)
        {
            return Result<UploadedFileDto>.Fail("Template not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        var extension = SafeExtension(request.FileName);

        // Rejected before anything is written, so a disallowed file never touches disk at all.
        if (!IsAllowedExtension(definitionJson, request.DataName, extension, out var allowed))
        {
            return Result<UploadedFileDto>.Fail(
                $"'{request.FileName}' is not an accepted file for '{request.DataName}'. Allowed: {string.Join(", ", allowed)}.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 400);
        }

        // The stored name is the file id, so a hostile client-supplied name can never reach disk.
        var fileId = Guid.NewGuid();
        var storedName = fileId.ToString("N") + extension;

        var stored = await fileStorage.SaveAsync(
            request.Content,
            storedName,
            request.ContentType,
            settings.PendingFolder,
            cancellationToken);

        if (stored.SizeBytes > maxBytes)
        {
            // The declared size lied; drop what was written rather than keep an over-limit file.
            await fileStorage.DeleteAsync(stored.RelativePath, cancellationToken);
            return Result<UploadedFileDto>.Fail(
                $"The file exceeds the {settings.MaxFileSizeMb} MB upload limit.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 400);
        }

        var entry = SubmissionFile.CreatePending(
            fileId,
            request.TemplateId,
            request.DataName,
            request.FileName,
            request.ContentType,
            stored.SizeBytes,
            stored.RelativePath,
            request.SurveyId);

        context.SubmissionFiles.Add(entry);
        await context.SaveChangesAsync(cancellationToken);

        return Result<UploadedFileDto>.Success(new UploadedFileDto(
            entry.FileId,
            entry.RelativePath,
            entry.FileName,
            entry.ContentType,
            entry.SizeBytes));
    }

    /// <summary>
    /// Checks the file against the extensions its own field declares. The picker's <c>accept</c> and
    /// the client's own check are only hints — drag-drop, "All files" and a hand-rolled request all
    /// bypass them — so the field's list is enforced here, where it cannot be skipped.
    /// </summary>
    /// <remarks>
    /// A <c>data_name</c> the definition does not mention stays permitted: drafts and older published
    /// templates legitimately upload against names the parser drops, and rejecting those would break
    /// a fill that used to work.
    /// </remarks>
    private static bool IsAllowedExtension(
        string definitionJson,
        string dataName,
        string extension,
        out IReadOnlyList<string> allowed)
    {
        allowed = [];

        if (!SurveyDefinitionParser.IsValidJson(definitionJson))
        {
            return true;
        }

        var field = SurveyDefinitionParser.Parse(definitionJson).Fields
            .FirstOrDefault(x => string.Equals(x.DataName, dataName, StringComparison.OrdinalIgnoreCase));

        if (field is null || field.AllowedExtensions.Count == 0)
        {
            return true;
        }

        allowed = field.AllowedExtensions;

        // `SafeExtension` returns '.pdf' or '' — the field's list holds 'pdf'.
        return extension.Length > 1
            && allowed.Contains(extension[1..], StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>An empty allow-list means "anything"; entries may be exact or <c>image/*</c>-style.</summary>
    private static bool IsAllowedContentType(string contentType, IReadOnlyList<string> allowed)
    {
        if (allowed.Count == 0)
        {
            return true;
        }

        return allowed.Any(pattern => pattern.EndsWith("/*", StringComparison.Ordinal)
            ? contentType.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase)
            : string.Equals(pattern, contentType, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Keeps a short, dot-prefixed alphanumeric extension; drops anything else.</summary>
    private static string SafeExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension) || extension.Length > 16)
        {
            return string.Empty;
        }

        return extension.Skip(1).All(char.IsLetterOrDigit) ? extension.ToLowerInvariant() : string.Empty;
    }
}
