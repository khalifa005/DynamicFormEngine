using System.Text.Json;
using System.Text.Json.Nodes;
using KH.Domain.Constants.Fsms;

namespace KH.Domain.Entities.Fsms.Surveys;

/// <summary>
/// Aggregate root for a survey instance — one field of work to be carried out against a published
/// template. Enters as <c>CREATED</c> (PENDING) from the inbound API or an operator, is allocated
/// to a field team (<c>ASSIGNED</c>), filled one or more times (<c>SUBMITTED</c>) and is then either
/// completed (<c>APPROVED</c>) or sent back (<c>RETURNED</c>). Every transition is guarded here and
/// appends a row to <see cref="StatusHistory"/>. Table: <c>SURVEYS</c>.
///
/// A filled survey once had to be "received" into <c>UNDER_REVIEW</c> before the back office could
/// act on it. That step recorded nothing the decision itself did not, so it was removed: a reviewer
/// now completes or returns straight from <c>SUBMITTED</c>. Rows and history written before the
/// change still carry <c>UNDER_REVIEW</c>, which is why both statuses are still accepted below.
/// </summary>
public sealed class Survey : BaseAuditableEntity<long>
{
    private const string EmptyJson = "{}";

    /// <summary>Trail note for an allocation that took the survey off another team.</summary>
    private const string ReallocationNote = "Re-allocated to a different field team.";

    private readonly List<SurveyStatusHistory> _statusHistory = [];
    private readonly List<SurveyAssignment> _assignments = [];

    private Survey()
    {
    }

    private Survey(
        string surveyCode,
        long templateId,
        long? templateVersionId,
        int? templateVersionNo,
        string source,
        string? faId,
        string? taskCode,
        string? faTypeCode,
        string? cbuCode,
        string? branchCode,
        string? operationAreaCode,
        int? departmentId,
        DateTimeOffset? dueDate,
        string? additionalDataJson,
        double? latitude = null,
        double? longitude = null,
        long? taskTypeId = null,
        string? customerName = null,
        long? customerTypeId = null,
        string? meterNumber = null,
        string? hcn = null,
        string? customerPhone = null,
        string? sourceComment = null)
    {
        SurveyCode = surveyCode;
        TemplateId = templateId;
        TemplateVersionId = templateVersionId;
        TemplateVersionNo = templateVersionNo;
        Source = source;
        FaId = faId;
        TaskCode = taskCode;
        FaTypeCode = faTypeCode;
        CbuCode = cbuCode;
        BranchCode = branchCode;
        OperationAreaCode = operationAreaCode;
        DepartmentId = departmentId;
        DueDate = dueDate;
        Latitude = latitude;
        Longitude = longitude;
        TaskTypeId = taskTypeId;
        CustomerName = customerName;
        CustomerPhone = customerPhone;
        CustomerTypeId = customerTypeId;
        MeterNumber = meterNumber;
        Hcn = hcn;
        SourceComment = sourceComment;
        IsExternalTask = ComputeIsExternalTask(faId, taskCode, faTypeCode);
        AdditionalDataJson = additionalDataJson ?? EmptyJson;
        ResultSummaryJson = EmptyJson;
        Status = SurveyStatuses.Created;
        IsActive = true;
    }

    public string SurveyCode { get; private set; } = default!;

    public long TemplateId { get; private set; }

    /// <summary>
    /// The published version snapshot this survey is pinned to. Pinning at creation means a later
    /// republish of the template never changes the form a field team is already working against.
    /// </summary>
    public long? TemplateVersionId { get; private set; }

    public int? TemplateVersionNo { get; private set; }

    /// <summary>See <see cref="SurveySources"/>.</summary>
    public string Source { get; private set; } = default!;

    /// <summary>See <see cref="SurveyStatuses"/>.</summary>
    public string Status { get; private set; } = default!;

    /// <summary>Identifier of the facility/asset in the originating system.</summary>
    public string? FaId { get; private set; }

    public string? TaskCode { get; private set; }
    public string? FaTypeCode { get; private set; }

    /// <summary>Optional <c>LKP_TASK_TYPE</c> id. Distinct from <see cref="FaTypeCode"/>.</summary>
    public long? TaskTypeId { get; private set; }

    public string? CustomerName { get; private set; }

    /// <summary>
    /// How the crew reaches the customer before arriving (رقم جوال العميل). A first-class column
    /// rather than a key in <see cref="AdditionalDataJson"/>: C2M sends it on every field activity,
    /// the field app has to show it, and an operator has to be able to correct it — none of which a
    /// JSON blob supports.
    /// </summary>
    public string? CustomerPhone { get; private set; }

    /// <summary>Optional <c>LKP_CUSTOMER_TYPE</c> id.</summary>
    public long? CustomerTypeId { get; private set; }

    /// <summary>Meter number (رقم العداد).</summary>
    public string? MeterNumber { get; private set; }

    /// <summary>Household / census number (رقم الحصر).</summary>
    public string? Hcn { get; private set; }

    /// <summary>
    /// True when the survey was raised against an external WFM task — any of
    /// <see cref="FaId"/>, <see cref="TaskCode"/> or <see cref="FaTypeCode"/> was set at create.
    /// </summary>
    public bool IsExternalTask { get; private set; }

    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }

    /// <summary>
    /// Where the work sits in the org geography, narrowing left to right. Together these decide
    /// which users may see the survey and which crews may be allocated to it, so they are set
    /// through <see cref="SetLocation"/> rather than being freely writable.
    /// </summary>
    public string? CbuCode { get; private set; }

    public string? BranchCode { get; private set; }
    public string? OperationAreaCode { get; private set; }
    public int? DepartmentId { get; private set; }

    /// <summary>
    /// Fill deadline (team must submit by this time). Kept as <c>DueDate</c> for existing
    /// field-team and list contracts; completion uses <see cref="CompletionDueDate"/>.
    /// </summary>
    public DateTimeOffset? DueDate { get; private set; }

    /// <summary>
    /// Back-office completion (approve) deadline. Defaults at allocate to
    /// allocate-time + team-fill SLA + completion SLA.
    /// </summary>
    public DateTimeOffset? CompletionDueDate { get; private set; }

    /// <summary>
    /// Snapshot of the template's team-fill SLA (calendar hours) at survey create.
    /// </summary>
    public int? TeamFillSlaHours { get; private set; }

    /// <summary>
    /// Snapshot of the template's completion SLA (calendar hours) at survey create.
    /// </summary>
    public int? CompletionSlaHours { get; private set; }

    /// <summary>
    /// When the crew's device says the survey was raised. Sent by the mobile app, which may have
    /// been offline for hours, so it is not comparable with <see cref="ReceivedDate"/> — that is
    /// the moment the API took delivery. Null for anything not raised in the field.
    /// </summary>
    public DateTimeOffset? DeviceCreatedDate { get; private set; }

    /// <summary>
    /// The dispatch instruction the originating system attached to the activity — C2M's
    /// <c>comment</c>. It is written for the crew ("Add Account"), so it is kept as a column the
    /// worklist and the field app can read, not left inside <see cref="AdditionalDataJson"/>.
    /// Distinct from <see cref="ReturnReason"/> and the timeline notes, which are FSMS's own words.
    /// </summary>
    public string? SourceComment { get; private set; }

    /// <summary>Whatever the originating system sent that the template does not model.</summary>
    public string AdditionalDataJson { get; private set; } = EmptyJson;

    /// <summary>
    /// A compact roll-up of every fill, keyed by <see cref="FilledByRoles"/>: each role maps to the
    /// array of summaries it contributed. Built by <see cref="MergeSummary"/> on each submission.
    /// </summary>
    public string ResultSummaryJson { get; private set; } = EmptyJson;

    public string? AssignedBy { get; private set; }
    public DateTimeOffset? AssignedDate { get; private set; }
    public DateTimeOffset? StartedDate { get; private set; }
    public DateTimeOffset? SubmittedDate { get; private set; }

    /// <summary>Which side filled the survey last — see <see cref="FilledByRoles"/>.</summary>
    public string? LastFilledByRole { get; private set; }

    public int SubmissionCount { get; private set; }

    /// <summary>Legacy: stamped by the retired receive step. Never set on a survey filled since.</summary>
    public string? ReceivedBy { get; private set; }

    /// <summary>Legacy: see <see cref="ReceivedBy"/>.</summary>
    public DateTimeOffset? ReceivedDate { get; private set; }

    public string? CompletedBy { get; private set; }
    public DateTimeOffset? CompletedDate { get; private set; }

    /// <summary>Free-text detail the reviewer wrote when returning the survey.</summary>
    public string? ReturnReason { get; private set; }

    /// <summary>
    /// Structured cause of the current return — a <c>LKP_RETURN_REASON</c> code. Paired with
    /// <see cref="ReturnReason"/>: the code is what a worklist can tag and report on, the reason is
    /// what the crew reads. Cleared on completion along with the reason.
    /// </summary>
    public string? ReturnReasonCode { get; private set; }

    public string? ReturnedBy { get; private set; }
    public DateTimeOffset? ReturnedDate { get; private set; }

    /// <summary>
    /// How many times this survey has been sent back. Unlike the reason, this is history rather than
    /// current state, so completing the survey does not reset it — a survey approved on the third
    /// attempt should still say so.
    /// </summary>
    public int ReturnCount { get; private set; }

    public IReadOnlyCollection<SurveyStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    public IReadOnlyCollection<SurveyAssignment> Assignments => _assignments.AsReadOnly();

    public static Survey Create(
        string surveyCode,
        long templateId,
        long? templateVersionId,
        int? templateVersionNo,
        string source,
        string? createdBy,
        DateTimeOffset createdAt,
        string? faId = null,
        string? taskCode = null,
        string? faTypeCode = null,
        string? cbuCode = null,
        string? branchCode = null,
        string? operationAreaCode = null,
        int? departmentId = null,
        DateTimeOffset? dueDate = null,
        string? additionalDataJson = null,
        double? latitude = null,
        double? longitude = null,
        long? taskTypeId = null,
        string? customerName = null,
        long? customerTypeId = null,
        string? meterNumber = null,
        string? hcn = null,
        string? customerPhone = null,
        string? sourceComment = null)
    {
        if (string.IsNullOrWhiteSpace(surveyCode))
        {
            throw new DomainException("A survey must have a code.");
        }

        if (templateId <= 0)
        {
            throw new DomainException("A survey must be created against a template.");
        }

        if (!SurveySources.IsDefined(source))
        {
            throw new DomainException($"Unknown survey source '{source}'.");
        }

        if (latitude is null)
        {
            throw new DomainException("Latitude is required.");
        }

        if (longitude is null)
        {
            throw new DomainException("Longitude is required.");
        }

        ValidateCoordinates(latitude, longitude);

        var survey = new Survey(
            surveyCode.Trim(),
            templateId,
            templateVersionId,
            templateVersionNo,
            source,
            Normalize(faId),
            Normalize(taskCode),
            Normalize(faTypeCode),
            Normalize(cbuCode),
            Normalize(branchCode),
            Normalize(operationAreaCode),
            departmentId,
            dueDate,
            additionalDataJson,
            latitude,
            longitude,
            taskTypeId,
            Normalize(customerName),
            customerTypeId,
            Normalize(meterNumber),
            Normalize(hcn),
            Normalize(customerPhone),
            Normalize(sourceComment));

        // The trail starts at creation so the timeline shows where the survey came from.
        survey._statusHistory.Add(SurveyStatusHistory.Create(
            null,
            SurveyStatuses.Created,
            createdBy,
            createdAt,
            $"Created from {source}."));

        return survey;
    }

    /// <summary>
    /// Copies template SLA hour defaults onto the survey at create. Absolute due dates are set
    /// later at allocate (or via create/edit overrides).
    /// </summary>
    public void ApplySlaDefaults(int teamFillSlaHours, int completionSlaHours)
    {
        if (teamFillSlaHours <= 0)
        {
            throw new DomainException("Team fill SLA hours must be greater than zero.");
        }

        if (completionSlaHours <= 0)
        {
            throw new DomainException("Completion SLA hours must be greater than zero.");
        }

        TeamFillSlaHours = teamFillSlaHours;
        CompletionSlaHours = completionSlaHours;
    }

    /// <summary>Sets the completion deadline at create when the caller supplies one before allocate.</summary>
    public void SetCompletionDueDate(DateTimeOffset? completionDueDate) =>
        CompletionDueDate = completionDueDate;

    /// <summary>
    /// Allocates the survey to a field team, and re-allocates it to a different one later. Exactly
    /// one team ever holds the survey: an allocation already live is superseded rather than left
    /// alongside the new one, so there is never a question of which crew owns the work. The retired
    /// allocation stays on the record as <c>REASSIGNED</c>.
    ///
    /// Only an unfilled survey can move. Once any fill has been recorded the survey is locked to
    /// the team that collected the data — handing it on would leave answers attributed to a crew
    /// that no longer holds the work. That leaves <c>CREATED</c>, <c>ASSIGNED</c> and
    /// <c>IN_PROGRESS</c>, all of which carry no submissions.
    ///
    /// The survey lands on <c>ASSIGNED</c>: the new team starts from scratch, so whatever progress
    /// the previous team had made no longer describes where the survey stands.
    /// </summary>
    public SurveyAssignment Assign(
        long fieldTeamId,
        string? assignedBy,
        DateTimeOffset assignedAt,
        DateTimeOffset? dueDate,
        string? note,
        DateTimeOffset? completionDueDate = null)
    {
        if (Status is not (SurveyStatuses.Created or SurveyStatuses.Assigned or SurveyStatuses.InProgress))
        {
            throw new DomainException($"A {Status} survey cannot be allocated.");
        }

        if (SubmissionCount > 0)
        {
            throw new DomainException("A survey that has already been filled cannot be re-allocated.");
        }

        if (_assignments.Any(a => a.IsActive && a.FieldTeamId == fieldTeamId))
        {
            throw new DomainException("The survey is already allocated to this field team.");
        }

        var superseded = false;
        foreach (var previous in ActiveAssignments().ToList())
        {
            previous.Supersede();
            superseded = true;
        }

        var assignment = SurveyAssignment.Create(fieldTeamId, assignedBy, assignedAt, dueDate ?? DueDate, note);
        _assignments.Add(assignment);

        AssignedBy = assignedBy;
        AssignedDate = assignedAt;

        if (dueDate is not null)
        {
            DueDate = dueDate;
        }

        if (completionDueDate is not null)
        {
            CompletionDueDate = completionDueDate;
        }

        MoveTo(SurveyStatuses.Assigned, assignedBy, assignedAt, note ?? (superseded ? ReallocationNote : null));
        return assignment;
    }

    /// <summary>
    /// Stamps a field-raised survey with both clocks: when the device recorded it and when the API
    /// took delivery. Only meaningful at creation — a survey already on the record was received once.
    /// </summary>
    public void StampFieldCapture(DateTimeOffset deviceCreatedAt, DateTimeOffset receivedAt)
    {
        if (DeviceCreatedDate is not null)
        {
            throw new DomainException("The field capture time has already been recorded.");
        }

        DeviceCreatedDate = deviceCreatedAt;
        ReceivedDate = receivedAt;
    }

    /// <summary>Marks work as started (a draft save): <c>ASSIGNED</c>/<c>RETURNED</c> -> <c>IN_PROGRESS</c>.</summary>
    public void Start(string? startedBy, DateTimeOffset startedAt, string? note = null)
    {
        if (Status is not (SurveyStatuses.Assigned or SurveyStatuses.Returned))
        {
            throw new DomainException($"A {Status} survey cannot be started.");
        }

        StartedDate ??= startedAt;

        foreach (var assignment in ActiveAssignments())
        {
            assignment.Start(startedAt);
        }

        MoveTo(SurveyStatuses.InProgress, startedBy, startedAt, note);
    }

    /// <summary>
    /// Records one fill of the survey. The field team and the back office each fill their own part,
    /// so this can run more than once. A fill from a pre-review state advances the survey to
    /// <c>SUBMITTED</c> (FILLED); a back-office fill while the survey is already under review is
    /// recorded without dragging the status backwards.
    /// </summary>
    public void RecordFill(
        string filledByRole,
        string? filledBy,
        DateTimeOffset filledAt,
        long? assignmentId = null,
        string? note = null)
    {
        if (!FilledByRoles.IsDefined(filledByRole))
        {
            throw new DomainException($"Unknown fill role '{filledByRole}'.");
        }

        if (Status is SurveyStatuses.Approved or SurveyStatuses.Expired)
        {
            throw new DomainException($"A {Status} survey cannot accept a new fill.");
        }

        LastFilledByRole = filledByRole;
        SubmissionCount++;
        StartedDate ??= filledAt;

        // Only a field-team fill closes out an allocation. A back-office fill that names no
        // assignment must not mark the team's work as done on its behalf. At most one allocation is
        // ever live, so an unnamed field-team fill is never ambiguous.
        var filledAssignments = _assignments.Where(a => assignmentId is long id
            ? a.Id == id
            : a.IsActive && filledByRole == FilledByRoles.FieldTeam);

        foreach (var assignment in filledAssignments)
        {
            assignment.Submit(filledAt);
        }

        if (Status is SurveyStatuses.Created or SurveyStatuses.Assigned or SurveyStatuses.InProgress or SurveyStatuses.Returned)
        {
            SubmittedDate = filledAt;
            MoveTo(SurveyStatuses.Submitted, filledBy, filledAt, note);
            return;
        }

        // Already at or past SUBMITTED — keep the status, but the fill still belongs on the trail.
        AppendHistory(Status, Status, filledBy, filledAt, note ?? $"Additional fill by {filledByRole}.");
    }

    /// <summary>
    /// Closes the survey: <c>SUBMITTED</c> -> <c>APPROVED</c>. A survey left in the retired
    /// <c>UNDER_REVIEW</c> state is accepted too, so work in flight when the receive step was removed
    /// can still be closed.
    /// </summary>
    public void Complete(string? completedBy, DateTimeOffset completedAt, string? note = null)
    {
        if (Status is not (SurveyStatuses.Submitted or SurveyStatuses.UnderReview))
        {
            throw new DomainException($"Only a {SurveyStatuses.Submitted} survey can be completed (current: {Status}).");
        }

        CompletedBy = completedBy;
        CompletedDate = completedAt;
        ReturnReason = null;
        ReturnReasonCode = null;

        foreach (var assignment in ActiveAssignments())
        {
            assignment.Approve();
        }

        MoveTo(SurveyStatuses.Approved, completedBy, completedAt, note);
    }

    /// <summary>
    /// Sends the survey back for rework: <c>SUBMITTED</c> -> <c>RETURNED</c>. The reviewer says
    /// what is wrong twice over — a <paramref name="reasonCode"/> the worklist can tag and filter on,
    /// and a <paramref name="reason"/> the crew reads — because a coloured tag is what gets noticed
    /// and a sentence is what explains.
    ///
    /// Passing <paramref name="reassignToFieldTeamId"/> hands the rework to a different crew: the
    /// live allocation is superseded and a fresh one is opened for the new team. Note this is the one
    /// path that moves a *filled* survey between teams — <see cref="Assign"/> refuses to, because
    /// re-allocating on a whim would leave collected answers attributed to a crew that no longer
    /// holds the work. A reviewer returning the survey is the sanctioned exception: they have looked
    /// at those answers and decided they must be collected again. Keeping the exception here rather
    /// than relaxing <see cref="Assign"/> means every other caller still gets the strict rule.
    /// </summary>
    /// <param name="reassignToFieldTeamId">
    /// The crew to hand the rework to. Null — or the team that already holds it — keeps the survey
    /// with the same crew and leaves its allocation live.
    /// </param>
    public void Return(
        string? returnedBy,
        DateTimeOffset returnedAt,
        string reasonCode,
        string reason,
        long? reassignToFieldTeamId = null)
    {
        if (Status is not (SurveyStatuses.Submitted or SurveyStatuses.UnderReview))
        {
            throw new DomainException($"Only a {SurveyStatuses.Submitted} survey can be returned (current: {Status}).");
        }

        if (string.IsNullOrWhiteSpace(reasonCode))
        {
            throw new DomainException("A returned survey must carry the code of why it was returned.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("A returned survey must carry the reason it was returned.");
        }

        ReturnReasonCode = reasonCode.Trim();
        ReturnReason = reason.Trim();
        ReturnedBy = returnedBy;
        ReturnedDate = returnedAt;
        ReturnCount++;

        // At most one allocation is ever live, so "the team that already holds it" is unambiguous.
        var currentTeamId = ActiveAssignments().Select(a => (long?)a.FieldTeamId).FirstOrDefault();
        var handingOver = reassignToFieldTeamId is long targetTeamId && targetTeamId != currentTeamId;

        if (handingOver)
        {
            foreach (var previous in ActiveAssignments().ToList())
            {
                previous.Supersede();
            }

            _assignments.Add(SurveyAssignment.Create(
                reassignToFieldTeamId!.Value,
                returnedBy,
                returnedAt,
                DueDate,
                ReturnReason));

            AssignedBy = returnedBy;
            AssignedDate = returnedAt;
        }
        else
        {
            foreach (var assignment in ActiveAssignments())
            {
                assignment.Return();
            }
        }

        MoveTo(SurveyStatuses.Returned, returnedBy, returnedAt, $"[{ReturnReasonCode}] {ReturnReason}");
    }

    /// <summary>
    /// Retires a survey whose deadline passed. Allowed from every state except a completed one:
    /// an approved survey is a finished record and expiring it would rewrite a decision the back
    /// office already made. A survey awaiting or under review can still be expired — the deadline
    /// outlives the hand-off, and leaving stale work parked in review is what this corrects.
    /// Re-expiring an already expired survey is refused because it would add a no-op history row.
    /// </summary>
    public void Expire(string? expiredBy, DateTimeOffset expiredAt, string? note = null)
    {
        if (Status is SurveyStatuses.Approved or SurveyStatuses.Expired)
        {
            throw new DomainException($"A {Status} survey cannot be expired.");
        }

        foreach (var assignment in ActiveAssignments())
        {
            assignment.Expire();
        }

        IsActive = false;
        MoveTo(SurveyStatuses.Expired, expiredBy, expiredAt, note);
    }

    /// <summary>
    /// Folds one fill's compact summary into <see cref="ResultSummaryJson"/> under its role. Each
    /// role keeps an array, so a second fill by the same side never overwrites the first.
    /// </summary>
    public void MergeSummary(string filledByRole, string? summaryJson)
    {
        if (!FilledByRoles.IsDefined(filledByRole))
        {
            throw new DomainException($"Unknown fill role '{filledByRole}'.");
        }

        if (string.IsNullOrWhiteSpace(summaryJson))
        {
            return;
        }

        JsonObject root;
        JsonNode? entry;

        try
        {
            root = JsonNode.Parse(ResultSummaryJson) as JsonObject ?? new JsonObject();
            entry = JsonNode.Parse(summaryJson);
        }
        catch (JsonException exception)
        {
            throw new DomainException($"The fill summary is not valid JSON: {exception.Message}");
        }

        if (root[filledByRole] is not JsonArray fills)
        {
            fills = [];
            root[filledByRole] = fills;
        }

        fills.Add(entry);
        ResultSummaryJson = root.ToJsonString();
    }

    /// <summary>
    /// Re-pins an in-flight survey to a newer published version of the same template. Pinning is
    /// what keeps a republish from changing the form under a crew mid-job, so moving off the pinned
    /// version is a deliberate back-office act rather than something a republish does on its own.
    ///
    /// Only forwards, and only while the survey is still in flight: a closed survey's answers were
    /// given against the version it was pinned to, and re-pointing it would misdescribe them.
    /// Answers already recorded are left untouched — a field the new version dropped stays in the
    /// submission row, and one it added comes back empty on the next fill.
    /// </summary>
    public void MigrateToTemplateVersion(long versionId, int versionNo, string? migratedBy, DateTimeOffset migratedAt, string? note)
    {
        if (Status is SurveyStatuses.Approved or SurveyStatuses.Expired)
        {
            throw new DomainException($"A {Status} survey cannot be moved to a different template version.");
        }

        if (versionId <= 0)
        {
            throw new DomainException("A target template version is required.");
        }

        if (TemplateVersionNo is int currentVersionNo && versionNo <= currentVersionNo)
        {
            throw new DomainException(
                $"The survey is already on template version {currentVersionNo}; it can only move to a newer one.");
        }

        var fromVersionNo = TemplateVersionNo;
        TemplateVersionId = versionId;
        TemplateVersionNo = versionNo;

        AppendHistory(
            Status,
            Status,
            migratedBy,
            migratedAt,
            note ?? $"Moved from template version {fromVersionNo?.ToString() ?? "none"} to {versionNo}.");
    }

    /// <summary>
    /// Corrects where the work sits — its CBU, branch, operation area, department and deadline.
    ///
    /// Only while the survey is still unfilled. The location is what decides who can see the survey
    /// and which crew may hold it, so moving it after data has been collected would hand answers to
    /// an audience that never had access to the work they describe, and could strand the survey
    /// outside the scope of the very crew that filled it. That leaves <c>CREATED</c>,
    /// <c>ASSIGNED</c> and <c>IN_PROGRESS</c> — the states <see cref="Assign"/> already treats as
    /// movable, for the same reason.
    /// </summary>
    public void SetLocation(
        string? cbuCode,
        string? branchCode,
        string? operationAreaCode,
        int? departmentId,
        DateTimeOffset? dueDate,
        string? changedBy,
        DateTimeOffset changedAt,
        string? note = null,
        double? latitude = null,
        double? longitude = null,
        DateTimeOffset? completionDueDate = null)
    {
        if (Status is not (SurveyStatuses.Created or SurveyStatuses.Assigned or SurveyStatuses.InProgress))
        {
            throw new DomainException($"A {Status} survey cannot be relocated.");
        }

        if (SubmissionCount > 0)
        {
            throw new DomainException("A survey that has already been filled cannot be relocated.");
        }

        ValidateCoordinates(latitude, longitude);

        CbuCode = Normalize(cbuCode);
        BranchCode = Normalize(branchCode);
        OperationAreaCode = Normalize(operationAreaCode);
        DepartmentId = departmentId;
        DueDate = dueDate;
        CompletionDueDate = completionDueDate;
        Latitude = latitude;
        Longitude = longitude;

        // Not a transition — the survey stays where it is in the lifecycle — but a change of
        // location is exactly the kind of thing the timeline exists to explain.
        AppendHistory(Status, Status, changedBy, changedAt, note ?? "Survey details updated.");
    }

    /// <summary>
    /// Corrects who the work is for — the customer's name and the number the crew calls before
    /// arriving. Both are contact details, not routing: unlike <see cref="SetLocation"/> they decide
    /// nothing about who may see the survey, so the only states refused are the settled ones. A
    /// wrong phone number is most often discovered once a crew is already out, and an approved
    /// survey is a record of what was found rather than a document still being prepared.
    /// </summary>
    public void SetCustomerContact(
        string? customerName,
        string? customerPhone,
        string? changedBy,
        DateTimeOffset changedAt,
        string? note = null)
    {
        if (Status is SurveyStatuses.Approved or SurveyStatuses.Expired)
        {
            throw new DomainException($"A {Status} survey's customer details cannot be changed.");
        }

        var previousName = CustomerName;
        var previousPhone = CustomerPhone;

        CustomerName = Normalize(customerName);
        CustomerPhone = Normalize(customerPhone);

        // Silent when nothing actually moved, so re-saving an unchanged dialog does not litter the
        // timeline with rows that say the same thing as the one above them.
        if (previousName == CustomerName && previousPhone == CustomerPhone)
        {
            return;
        }

        AppendHistory(Status, Status, changedBy, changedAt, note ?? "Customer details updated.");
    }

    /// <summary>Replaces the free-form payload carried from the originating system.</summary>
    public void SetAdditionalData(string? additionalDataJson) =>
        AdditionalDataJson = string.IsNullOrWhiteSpace(additionalDataJson) ? EmptyJson : additionalDataJson;

    private IEnumerable<SurveyAssignment> ActiveAssignments() => _assignments.Where(a => a.IsActive);

    private void MoveTo(string toStatus, string? changedBy, DateTimeOffset changedAt, string? note)
    {
        var fromStatus = Status;
        Status = toStatus;
        AppendHistory(fromStatus, toStatus, changedBy, changedAt, note);
    }

    private void AppendHistory(string? fromStatus, string toStatus, string? changedBy, DateTimeOffset changedAt, string? note) =>
        _statusHistory.Add(SurveyStatusHistory.Create(fromStatus, toStatus, changedBy, changedAt, note));

    /// <summary>
    /// An inbound or operator-raised survey is external when a field-activity reference
    /// was supplied at create.
    /// </summary>
    public static bool ComputeIsExternalTask(string? faId, string? taskCode, string? faTypeCode) =>
        !string.IsNullOrWhiteSpace(faId)
        || !string.IsNullOrWhiteSpace(taskCode)
        || !string.IsNullOrWhiteSpace(faTypeCode);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateCoordinates(double? latitude, double? longitude)
    {
        if (latitude.HasValue && (latitude < -90 || latitude > 90))
        {
            throw new DomainException("Latitude must be between -90 and 90.");
        }
        if (longitude.HasValue && (longitude < -180 || longitude > 180))
        {
            throw new DomainException("Longitude must be between -180 and 180.");
        }
    }
}
