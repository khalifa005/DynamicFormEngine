using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Lookups.Statuses;

/// <summary>
/// The status vocabulary the system speaks, in both languages — every survey lifecycle state and
/// every field-team allocation state, with the labels to show and the flags to group them by.
///
/// Built for the mobile app, which needs to know which codes it may send to the worklist filter and
/// what to print next to each survey, without hard-coding a table that then drifts from the server.
/// Served from constants rather than a lookup table: these codes are compiled into the domain's
/// transition rules, so a row an operator could edit would be a row that could contradict them.
/// </summary>
[Authorize(Policy = FsmsPolicies.ViewSurveys)]
public record GetSurveyStatusCatalogQuery : IRequest<Result<SurveyStatusCatalogDto>>;

/// <summary>One status code, ready to render.</summary>
public sealed class FsmsStatusOptionDto
{
    public string Code { get; init; } = default!;
    public string NameEn { get; init; } = default!;
    public string NameAr { get; init; } = default!;

    /// <summary>Display order — the lifecycle's own order, not alphabetical.</summary>
    public int SortOrder { get; init; }

    /// <summary>
    /// The crew still has something to do here: fill the survey, or fill it again after it came
    /// back. A worklist's default "my work" tab is exactly this set.
    /// </summary>
    public bool IsOpenForFieldTeam { get; init; }

    /// <summary>Nothing moves out of this state — the work is closed.</summary>
    public bool IsTerminal { get; init; }

    /// <summary>
    /// Retired: nothing is written into this state any more, but rows and history from before the
    /// change still carry it, so a client must still be able to display it.
    /// </summary>
    public bool IsLegacy { get; init; }
}

/// <summary>
/// The two vocabularies together, because a client that shows one nearly always shows the other: the
/// survey's own state, and the state of this crew's allocation of it.
/// </summary>
public sealed class SurveyStatusCatalogDto
{
    /// <summary>Survey lifecycle states — what <c>surveys.status</c> holds, and what the worklist filter takes.</summary>
    public IReadOnlyList<FsmsStatusOptionDto> SurveyStatuses { get; init; } = [];

    /// <summary>Allocation states — what a survey's <c>assignmentStatus</c> holds on the crew's own list.</summary>
    public IReadOnlyList<FsmsStatusOptionDto> AssignmentStatuses { get; init; } = [];
}

public sealed class GetSurveyStatusCatalogQueryHandler
    : IRequestHandler<GetSurveyStatusCatalogQuery, Result<SurveyStatusCatalogDto>>
{
    // Labels match the back-office web app's own translations, so a crew and an operator discussing
    // the same survey read the same word for its state.
    private const string PendingEn = "Pending";
    private const string PendingAr = "قيد الانتظار";
    private const string AllocatedEn = "Allocated";
    private const string AllocatedAr = "موزّع";
    private const string InProgressEn = "In Progress";
    private const string InProgressAr = "قيد التنفيذ";
    private const string FilledEn = "Filled";
    private const string FilledAr = "معبّأ";
    private const string SubmittedEn = "Submitted";
    private const string SubmittedAr = "مُرسل";
    private const string ReviewedEn = "Reviewed";
    private const string ReviewedAr = "تمت المراجعة";
    private const string CompletedEn = "Completed";
    private const string CompletedAr = "مكتمل";
    private const string ApprovedEn = "Approved";
    private const string ApprovedAr = "معتمد";
    private const string ReturnedEn = "Returned";
    private const string ReturnedAr = "مُعاد";
    private const string ExpiredEn = "Expired";
    private const string ExpiredAr = "منتهي";
    private const string ReassignedEn = "Reassigned";
    private const string ReassignedAr = "أُعيد توزيعه";

    private static readonly SurveyStatusCatalogDto Catalog = new()
    {
        SurveyStatuses =
        [
            Option(SurveyStatuses.Created, PendingEn, PendingAr, 1),
            Option(SurveyStatuses.Assigned, AllocatedEn, AllocatedAr, 2, isOpenForFieldTeam: true),
            Option(SurveyStatuses.InProgress, InProgressEn, InProgressAr, 3, isOpenForFieldTeam: true),
            Option(SurveyStatuses.Submitted, FilledEn, FilledAr, 4),
            Option(SurveyStatuses.UnderReview, FilledEn, FilledAr, 5, isLegacy: true),
            Option(SurveyStatuses.Returned, ReturnedEn, ReturnedAr, 6, isOpenForFieldTeam: true),
            Option(SurveyStatuses.Approved, CompletedEn, CompletedAr, 7, isTerminal: true),
            Option(SurveyStatuses.Expired, ExpiredEn, ExpiredAr, 8, isTerminal: true),
        ],
        AssignmentStatuses =
        [
            Option(AssignmentStatuses.Pending, PendingEn, PendingAr, 1, isOpenForFieldTeam: true),
            Option(AssignmentStatuses.InProgress, InProgressEn, InProgressAr, 2, isOpenForFieldTeam: true),
            Option(AssignmentStatuses.Submitted, SubmittedEn, SubmittedAr, 3),
            Option(AssignmentStatuses.Reviewed, ReviewedEn, ReviewedAr, 4, isLegacy: true),
            Option(AssignmentStatuses.Returned, ReturnedEn, ReturnedAr, 5, isOpenForFieldTeam: true),
            Option(AssignmentStatuses.Approved, ApprovedEn, ApprovedAr, 6, isTerminal: true),
            Option(AssignmentStatuses.Expired, ExpiredEn, ExpiredAr, 7, isTerminal: true),
            Option(AssignmentStatuses.Reassigned, ReassignedEn, ReassignedAr, 8, isTerminal: true),
        ],
    };

    public Task<Result<SurveyStatusCatalogDto>> Handle(
        GetSurveyStatusCatalogQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(Result<SurveyStatusCatalogDto>.Success(Catalog));

    private static FsmsStatusOptionDto Option(
        string code,
        string nameEn,
        string nameAr,
        int sortOrder,
        bool isOpenForFieldTeam = false,
        bool isTerminal = false,
        bool isLegacy = false) =>
        new()
        {
            Code = code,
            NameEn = nameEn,
            NameAr = nameAr,
            SortOrder = sortOrder,
            IsOpenForFieldTeam = isOpenForFieldTeam,
            IsTerminal = isTerminal,
            IsLegacy = isLegacy,
        };
}
