namespace Shared.Core.Constants;

/// <summary>
/// Semantic labels persisted on <see cref="Entities.AuditEntry.EventName"/> for request-response audit trails.
/// </summary>
public abstract class AuditEventNames
{
    public const string FsmsFieldTeamCreateSurvey = "Fsms.FieldTeam.CreateSurvey";
    public const string FsmsUserCreateSurvey = "Fsms.User.CreateSurvey";
    public const string FsmsUserBulkAllocateSurvey = "Fsms.User.BulkAllocateSurvey";
    public const string FsmsFieldTeamSubmitSurvey = "Fsms.FieldTeam.SubmitSurvey";
    public const string FsmsUserSubmitSurvey = "Fsms.User.SubmitSurvey";

    /// <summary>A mobile crew draining its offline queue — one entry covering the whole run.</summary>
    public const string FsmsFieldTeamBulkSubmitSurveys = "Fsms.FieldTeam.BulkSubmitSurveys";
}
