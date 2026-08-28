using KH.Domain.Entities.Fsms.Templates;
using Microsoft.EntityFrameworkCore;

namespace KH.Application.Fsms.Templates.Common;

public static class TemplateQueryExtensions
{
    /// <summary>Eager-loads a template's published version snapshots.</summary>
    public static IQueryable<SurveyTemplate> IncludeFullDefinition(this IQueryable<SurveyTemplate> query) =>
        query.Include(t => t.Versions);
}
