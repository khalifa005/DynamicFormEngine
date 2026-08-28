using System.Text.Json;
using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Definition;
using KH.Application.Fsms.Submissions.Interfaces;
using KH.Application.Fsms.Surveys.Jobs;
using KH.Application.Fsms.Templates.Common;
using KH.Application.Fsms.Templates.Models;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Catalog;
using KH.Domain.Entities.Fsms.Templates;
using Shared.Core.Common;

namespace KH.Application.Fsms.Templates.Commands.PublishTemplate;

[Authorize(Policy = FsmsPolicies.ManageTemplates)]
public record PublishTemplateCommand : IRequest<Result<TemplateDetailDto>>
{
    public long TemplateId { get; init; }
}

public sealed class PublishTemplateCommandValidator : AbstractValidator<PublishTemplateCommand>
{
    public PublishTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateId)
            .GreaterThan(0).WithMessage("Template id is required.");
    }
}

public sealed class PublishTemplateCommandHandler(
    IApplicationDbContext context,
    ISurveySubmissionStore submissionStore,
    IBackgroundJobScheduler jobScheduler,
    IUser user,
    TimeProvider timeProvider)
    : IRequestHandler<PublishTemplateCommand, Result<TemplateDetailDto>>
{
    public async Task<Result<TemplateDetailDto>> Handle(PublishTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await context.SurveyTemplates
            .FirstOrDefaultAsync(x => x.Id == request.TemplateId, cancellationToken);

        if (template is null)
        {
            return Result<TemplateDetailDto>.Fail("Template not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        if (!SurveyDefinitionParser.HasElements(template.DefinitionJson))
        {
            return Result<TemplateDetailDto>.Fail(
                "A template must contain at least one field before publishing.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 400);
        }

        var definition = SurveyDefinitionParser.Parse(template.DefinitionJson);

        // Every data_name becomes a SQL column, so one that cannot be an identifier can never be
        // written. Caught here rather than at submit time, where the answer is simply dropped and
        // the fill looks successful with a NULL column behind it.
        var dataNameResult = ValidateDataNames(definition);
        if (!dataNameResult.IsSuccess)
        {
            return Result<TemplateDetailDto>.Fail(dataNameResult.Errors);
        }

        // Upsert the global field catalog; reject type conflicts on reused data names.
        var catalogResult = await UpsertCatalogAsync(definition, cancellationToken);
        if (!catalogResult.IsSuccess)
        {
            return Result<TemplateDetailDto>.Fail(catalogResult.Errors);
        }

        var snapshots = new[]
        {
            new TemplateVersionSnapshot(TargetClients.Formly, template.DefinitionJson, BuildTemplateSnapshotJson(template)),
        };

        template.Publish(user.Id, snapshots, timeProvider.GetUtcNow());

        await context.SaveChangesAsync(cancellationToken);

        // Reconcile the per-template physical submission table (create/add columns; never drops).
        await submissionStore.ReconcileTableAsync(template, cancellationToken);

        // Only after the reconcile: a survey moved onto the new version may be filled immediately,
        // and its answers need somewhere to land. Queuing is best-effort — a template still
        // publishes when no job processor is running, it just leaves its surveys on the old pin.
        if (template.AutoMigrateSurveysOnPublish && template.CurrentVersionNo is int publishedVersionNo)
        {
            var templateId = template.Id;
            var requestedBy = user.Id;

            jobScheduler.Enqueue<ISurveyVersionMigrationJob>(job =>
                job.MigrateTemplateSurveysAsync(templateId, publishedVersionNo, requestedBy, CancellationToken.None));
        }

        return Result<TemplateDetailDto>.Success(template.ToDetailDto());
    }

    /// <summary>Appended to a companion's labels so the catalog list reads as what it is.</summary>
    private const string OtherLabelSuffixEn = " (other)";

    private const string OtherLabelSuffixAr = " (أخرى)";

    /// <summary>
    /// Rejects any <c>data_name</c> that could not become a SQL column. The parser trims first, so a
    /// design carrying <c>"leak_type "</c> passes as the <c>leak_type</c> it becomes; only a name no
    /// amount of trimming can rescue — a space or dash inside it, a leading digit — is blocked.
    /// </summary>
    private static Result<bool> ValidateDataNames(SurveyDefinition definition)
    {
        var invalid = definition.Fields
            .Select(f => f.DataName)
            .Where(name => !SurveyDataName.IsValid(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (invalid.Count == 0)
        {
            return Result<bool>.Success(true);
        }

        var names = string.Join("', '", invalid);

        return Result<bool>.Fail(
            $"Field '{names}' cannot be published: {SurveyDataName.RuleDescription}.",
            ApiErrorCodes.ValidationError,
            httpStatusCode: 400);
    }

    private async Task<Result<bool>> UpsertCatalogAsync(SurveyDefinition definition, CancellationToken cancellationToken)
    {
        if (definition.Fields.Count == 0)
        {
            return Result<bool>.Success(true);
        }

        var entries = CatalogEntries(definition).ToList();
        var names = entries.Select(e => e.DataName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var existing = await context.FieldCatalog
            .Where(c => names.Contains(c.DataName))
            .ToListAsync(cancellationToken);

        var byName = existing.ToDictionary(c => c.DataName, StringComparer.OrdinalIgnoreCase);

        foreach (var field in entries)
        {
            if (byName.TryGetValue(field.DataName, out var entry))
            {
                if (!string.Equals(entry.FieldType, field.FieldType, StringComparison.OrdinalIgnoreCase))
                {
                    return Result<bool>.Fail(
                        $"Field '{field.DataName}' already exists in the catalog as type '{entry.FieldType}' and cannot be reused as '{field.FieldType}'.",
                        ApiErrorCodes.ValidationError,
                        httpStatusCode: 409);
                }

                continue;
            }

            var created = FieldCatalogEntry.Create(field.DataName, field.FieldType, field.LabelEn, field.LabelAr);
            context.FieldCatalog.Add(created);
            byName[field.DataName] = created;
        }

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// The names this template registers: its own fields, plus a <c>&lt;data_name&gt;_other</c>
    /// companion for each choice field offering "Other". The companion is a real column carrying the
    /// typed free text (see <see cref="SurveyChoiceOther"/>), so it belongs in the catalog like any
    /// other name — which is also what makes the type-conflict check above cover it.
    /// </summary>
    private static IEnumerable<SurveyDefinitionField> CatalogEntries(SurveyDefinition definition)
    {
        foreach (var field in definition.Fields)
        {
            yield return field;

            if (!SurveyChoiceOther.NeedsCompanion(field))
            {
                continue;
            }

            yield return new SurveyDefinitionField(
                SurveyChoiceOther.KeyFor(field.DataName),
                BuilderElementTypes.Text,
                Suffixed(field.LabelEn, OtherLabelSuffixEn),
                Suffixed(field.LabelAr, OtherLabelSuffixAr));
        }
    }

    private static string? Suffixed(string? label, string suffix) =>
        string.IsNullOrWhiteSpace(label) ? null : label.Trim() + suffix;

    private static string BuildTemplateSnapshotJson(SurveyTemplate template)
    {
        var snapshot = new
        {
            templateCode = template.TemplateCode,
            templateNameEn = template.TemplateNameEn,
            templateNameAr = template.TemplateNameAr,
            category = template.Category,
        };

        return JsonSerializer.Serialize(snapshot);
    }
}
