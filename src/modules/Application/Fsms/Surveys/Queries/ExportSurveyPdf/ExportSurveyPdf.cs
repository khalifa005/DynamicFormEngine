using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Definition;
using KH.Application.Fsms.Submissions.Interfaces;
using KH.Application.Fsms.Surveys.Common;
using KH.Application.Fsms.Surveys.Models;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Submissions;
using KH.Domain.Entities.Fsms.Surveys;
using Microsoft.Extensions.Logging;
using Shared.Core.Common;
using QuestPDF.Fluent;
using System.Text.Json;

namespace KH.Application.Fsms.Surveys.Queries.ExportSurveyPdf;

[Authorize(Policy = FsmsPolicies.ViewOrSubmitOrReviewSurveys)]
public record ExportSurveyPdfQuery : IRequest<Result<SurveyPdfResultDto>>
{
    public long SurveyId { get; init; }
    public string Language { get; init; } = "en";
}

public sealed record SurveyPdfResultDto(Stream Content, string ContentType, string FileName);

public sealed class ExportSurveyPdfQueryValidator : AbstractValidator<ExportSurveyPdfQuery>
{
    public ExportSurveyPdfQueryValidator()
    {
        RuleFor(x => x.SurveyId).GreaterThan(0).WithMessage("Survey id is required.");
    }
}

public sealed class ExportSurveyPdfQueryHandler(
    IApplicationDbContext context,
    ISurveySubmissionStore submissionStore,
    IFileStorage fileStorage,
    ILogger<ExportSurveyPdfQueryHandler> logger)
    : IRequestHandler<ExportSurveyPdfQuery, Result<SurveyPdfResultDto>>
{
    private const string ArabicLanguage = "ar";
    private const string FileIdProperty = "fileId";
    private const string JsonArrayStart = "[";
    private const string JsonObjectStart = "{";

    public async Task<Result<SurveyPdfResultDto>> Handle(ExportSurveyPdfQuery request, CancellationToken cancellationToken)
    {
        var survey = await context.Surveys
            .AsNoTracking()
            .IncludeDetail()
            .FirstOrDefaultAsync(x => x.Id == request.SurveyId, cancellationToken);

        if (survey is null)
        {
            return Result<SurveyPdfResultDto>.Fail("Survey not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        var detailDto = await survey.ToDetailDtoAsync(context, cancellationToken);

        // The definition is what tells a file row apart: which answer is a signature, which is a
        // photo, which is a plain attachment. Without it every image lands in the same gallery.
        var fieldTypesByDataName = await LoadFieldTypesAsync(survey.TemplateId, cancellationToken);

        // Temporarily omitted from the exported PDF (سجل الحالة / Status Timeline).
        // var timeline = await context.SurveyStatusHistory
        //     .AsNoTracking()
        //     .Where(x => x.SurveyId == request.SurveyId)
        //     .OrderBy(x => x.ChangedDate)
        //     .ThenBy(x => x.Id)
        //     .ToListAsync(cancellationToken);
        var timeline = Array.Empty<SurveyStatusHistory>();

        var latestFillRow = await submissionStore.GetLatestBySurveyAsync(survey.TemplateId, request.SurveyId, cancellationToken);

        var fileRows = await LoadFileRowsAsync(request.SurveyId, latestFillRow, cancellationToken);

        var fileNamesByDataName = fileRows
            .GroupBy(f => f.DataName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(f => f.FileName).ToList(), StringComparer.OrdinalIgnoreCase);

        var answers = BuildAnswers(request.Language, detailDto, latestFillRow, fileNamesByDataName);

        var files = await LoadFilesAsync(request.SurveyId, fileRows, fieldTypesByDataName, cancellationToken);

        SurveyPdfFonts.EnsureRegistered();

        var document = new SurveyPdfDocument(detailDto, files, timeline, answers, request.Language);
        var pdfStream = new MemoryStream();
        document.GeneratePdf(pdfStream);
        pdfStream.Position = 0;

        var fileName = $"Survey_{detailDto.SurveyCode}_{DateTimeOffset.UtcNow:yyyyMMdd}.pdf";

        return Result<SurveyPdfResultDto>.Success(new SurveyPdfResultDto(pdfStream, "application/pdf", fileName));
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadFieldTypesAsync(long templateId, CancellationToken cancellationToken)
    {
        var definitionJson = await context.SurveyTemplates
            .AsNoTracking()
            .Where(t => t.Id == templateId)
            .Select(t => t.DefinitionJson)
            .FirstOrDefaultAsync(cancellationToken);

        var fieldTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var definition = SurveyDefinitionParser.Parse(definitionJson);
            foreach (var field in definition.Fields)
            {
                fieldTypes[field.DataName] = field.FieldType;
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Template {TemplateId} has an unreadable definition; the report falls back to content types.", templateId);
        }

        return fieldTypes;
    }

    /// <summary>
    /// The survey's media rows. Files uploaded before the survey existed can still be missing their
    /// survey id, so anything the submitted answers point at is pulled in by file id as well.
    /// </summary>
    private async Task<List<SubmissionFile>> LoadFileRowsAsync(
        long surveyId,
        IReadOnlyDictionary<string, object?>? latestFillRow,
        CancellationToken cancellationToken)
    {
        var files = await context.SubmissionFiles
            .AsNoTracking()
            .Where(f => f.SurveyId == surveyId && f.IsActive)
            .OrderByDescending(f => f.Created)
            .ToListAsync(cancellationToken);

        if (latestFillRow is null)
        {
            return files;
        }

        var knownFileIds = files.Select(f => f.FileId).ToHashSet();
        var referencedFileIds = new HashSet<Guid>();

        foreach (var kvp in latestFillRow.Where(k => !SubmissionColumns.IsBase(k.Key)))
        {
            foreach (var fileId in ExtractFileIds(kvp.Value))
            {
                if (!knownFileIds.Contains(fileId))
                {
                    referencedFileIds.Add(fileId);
                }
            }
        }

        if (referencedFileIds.Count == 0)
        {
            return files;
        }

        var extraFiles = await context.SubmissionFiles
            .AsNoTracking()
            .Where(f => referencedFileIds.Contains(f.FileId) && f.IsActive)
            .ToListAsync(cancellationToken);

        files.AddRange(extraFiles);

        return files;
    }

    /// <summary>
    /// Reads the bytes of every image so the report can embed them. A row whose bytes are gone is
    /// kept with <c>null</c> content and logged — the report lists it as unavailable rather than
    /// quietly leaving the file out, which reads as "the survey had no attachments".
    /// </summary>
    private async Task<IReadOnlyList<SurveyPdfFile>> LoadFilesAsync(
        long surveyId,
        IReadOnlyList<SubmissionFile> fileRows,
        IReadOnlyDictionary<string, string> fieldTypesByDataName,
        CancellationToken cancellationToken)
    {
        var files = new List<SurveyPdfFile>(fileRows.Count);

        foreach (var row in fileRows.OrderBy(f => f.DataName, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.Created))
        {
            fieldTypesByDataName.TryGetValue(row.DataName, out var fieldType);

            var isImage = row.ContentType.StartsWith(SurveyPdfFile.ImageContentTypePrefix, StringComparison.OrdinalIgnoreCase);
            var bytes = isImage ? await TryReadBytesAsync(surveyId, row, cancellationToken) : null;

            files.Add(new SurveyPdfFile(
                row.FileId,
                row.DataName,
                row.FileName,
                row.ContentType,
                row.SizeBytes,
                row.Created,
                fieldType,
                bytes));
        }

        return files;
    }

    private async Task<byte[]?> TryReadBytesAsync(long surveyId, SubmissionFile file, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await fileStorage.OpenReadAsync(file.RelativePath, cancellationToken);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            return buffer.ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            logger.LogWarning(
                ex,
                "Survey {SurveyId}: image {FileId} ('{FileName}') could not be read from storage at '{RelativePath}'. It is reported as unavailable.",
                surveyId,
                file.FileId,
                file.FileName,
                file.RelativePath);

            return null;
        }
    }

    private static List<Guid> ExtractFileIds(object? value)
    {
        var fileIds = new List<Guid>();

        var rawValue = value?.ToString();
        if (string.IsNullOrEmpty(rawValue) || !rawValue.TrimStart().StartsWith(JsonArrayStart, StringComparison.Ordinal))
        {
            return fileIds;
        }

        try
        {
            using var doc = JsonDocument.Parse(rawValue);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return fileIds;
            }

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object
                    && item.TryGetProperty(FileIdProperty, out var fileIdProp)
                    && fileIdProp.ValueKind == JsonValueKind.String
                    && Guid.TryParse(fileIdProp.GetString(), out var parsedId))
                {
                    fileIds.Add(parsedId);
                }
            }
        }
        catch (JsonException)
        {
            // Not a reference array; nothing to pull in.
        }

        return fileIds;
    }

    private static List<(string Label, string Value)> BuildAnswers(
        string language,
        SurveyDetailDto detailDto,
        IReadOnlyDictionary<string, object?>? latestFillRow,
        IReadOnlyDictionary<string, List<string>> fileNamesByDataName)
    {
        var answers = new List<(string Label, string Value)>();
        if (latestFillRow is null)
        {
            return answers;
        }

        var rawAnswers = latestFillRow
            .Where(kvp => !SubmissionColumns.IsBase(kvp.Key))
            .ToList();

        JsonElement? summaryRoot = null;
        try
        {
            using var doc = JsonDocument.Parse(detailDto.ResultSummaryJson);
            summaryRoot = doc.RootElement.Clone();
        }
        catch (JsonException) { }

        var isArabic = language == ArabicLanguage;

        foreach (var kvp in rawAnswers)
        {
            var key = kvp.Key;
            var rawValue = kvp.Value;

            string label = key;
            string displayValue = rawValue?.ToString() ?? "-";

            if (summaryRoot.HasValue && summaryRoot.Value.ValueKind == JsonValueKind.Object)
            {
                bool foundMeta = false;
                foreach (var roleProp in summaryRoot.Value.EnumerateObject())
                {
                    if (roleProp.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var fill in roleProp.Value.EnumerateArray())
                        {
                            if (fill.TryGetProperty("answers", out var fillAnswers) &&
                                fillAnswers.TryGetProperty(key, out var meta) &&
                                meta.ValueKind == JsonValueKind.Object)
                            {
                                var labelEn = meta.TryGetProperty("labelEn", out var len) ? len.GetString() : null;
                                var labelAr = meta.TryGetProperty("labelAr", out var lar) ? lar.GetString() : null;
                                var displayEn = meta.TryGetProperty("displayEn", out var den) ? den.GetString() : null;
                                var displayAr = meta.TryGetProperty("displayAr", out var dar) ? dar.GetString() : null;

                                label = isArabic ? (!string.IsNullOrWhiteSpace(labelAr) ? labelAr : (labelEn ?? key)) : (!string.IsNullOrWhiteSpace(labelEn) ? labelEn : (labelAr ?? key));

                                var preferredDisplay = isArabic ? (!string.IsNullOrWhiteSpace(displayAr) ? displayAr : displayEn) : (!string.IsNullOrWhiteSpace(displayEn) ? displayEn : displayAr);
                                if (!string.IsNullOrWhiteSpace(preferredDisplay))
                                {
                                    displayValue = preferredDisplay;
                                }
                                else if (meta.TryGetProperty("type", out var typeProp) && typeProp.GetString() == BuilderElementTypes.Geolocation)
                                {
                                    if (meta.TryGetProperty("value", out var valProp) && valProp.ValueKind == JsonValueKind.Object)
                                    {
                                        var lat = valProp.TryGetProperty("lat", out var l) ? l.GetDouble().ToString() : "";
                                        var lng = valProp.TryGetProperty("lng", out var lg) ? lg.GetDouble().ToString() : "";
                                        var address = valProp.TryGetProperty("address", out var a) ? a.GetString() : null;
                                        displayValue = string.IsNullOrEmpty(address) ? $"{lat}, {lng}" : $"{lat}, {lng} — {address}";
                                    }
                                }
                                else if (meta.TryGetProperty("value", out var valProp) && valProp.ValueKind == JsonValueKind.Array)
                                {
                                    var elements = valProp.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrEmpty(x));
                                    displayValue = string.Join(", ", elements);
                                }

                                foundMeta = true;
                                break;
                            }
                        }
                    }
                    if (foundMeta) break;
                }
            }

            if (displayValue.StartsWith(JsonArrayStart, StringComparison.Ordinal) || displayValue.StartsWith(JsonObjectStart, StringComparison.Ordinal))
            {
                try
                {
                    using var valDoc = JsonDocument.Parse(displayValue);
                    if (valDoc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        if (valDoc.RootElement.GetArrayLength() == 0)
                        {
                            displayValue = "-";
                        }
                        else if (valDoc.RootElement[0].TryGetProperty(FileIdProperty, out _))
                        {
                            displayValue = isArabic ? "مرفقات" : "Attachments";
                        }
                        else
                        {
                            var elements = valDoc.RootElement.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrEmpty(x));
                            displayValue = string.Join(", ", elements);
                        }
                    }
                    else if (valDoc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        if (valDoc.RootElement.TryGetProperty("lat", out var lat) && valDoc.RootElement.TryGetProperty("lng", out var lng))
                        {
                            var latStr = lat.GetDouble().ToString();
                            var lngStr = lng.GetDouble().ToString();
                            var address = valDoc.RootElement.TryGetProperty("address", out var addr) ? addr.GetString() : null;
                            displayValue = string.IsNullOrEmpty(address) ? $"{latStr}, {lngStr}" : $"{latStr}, {lngStr} — {address}";
                        }
                    }
                }
                catch (JsonException) { }
            }

            // A media answer reads as "Attachments" at best; the file names say what was actually uploaded.
            if (fileNamesByDataName.TryGetValue(key, out var fileNames) && fileNames.Count > 0)
            {
                displayValue = string.Join(", ", fileNames);
            }

            answers.Add((label, displayValue));
        }

        return answers;
    }
}
