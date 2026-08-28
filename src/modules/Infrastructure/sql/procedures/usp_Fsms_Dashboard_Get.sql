-- =============================================================================
-- usp_Fsms_Dashboard_Get
--
-- Everything the FSMS dashboard renders, in one round trip: ten result sets over
-- a single filtered survey set. The page needs the same WHERE clause aggregated
-- ten different ways, so filtering once into #Scoped and reading it repeatedly
-- costs one pass over SURVEYS instead of ten.
--
-- Deployed by ApplicationDbContextInitialiser after MigrateAsync(), not by an EF
-- migration. CREATE OR ALTER keeps that re-runnable. The initialiser splits the file
-- on GO before executing, since CREATE OR ALTER must begin its own batch.
--
-- Conventions this file relies on (see FsmsTableNames.cs / the model snapshot):
--   * Tables are SCREAMING_SNAKE; columns are PascalCase (no HasColumnName anywhere).
--   * Every status is an nvarchar string, never an int.
--   * The org hierarchy joins by Code, not Id.
--   * SURVEY_ASSIGNMENTS.IsActive = 1 means "current allocation", not "not deleted".
--   * SURVEY_STATUS_HISTORY.ChangedBy holds the Identity GUID (user.Id).
--   * SUBMISSIONS is deliberately untouched: its column set varies at runtime.
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_SURVEY_STATUS_HISTORY_ChangedBy_ChangedDate'
      AND object_id = OBJECT_ID(N'dbo.SURVEY_STATUS_HISTORY'))
BEGIN
    -- Sets 6-8 pivot on who did what. The existing index leads with SurveyId, which
    -- is the wrong column for that question.
    CREATE NONCLUSTERED INDEX IX_SURVEY_STATUS_HISTORY_ChangedBy_ChangedDate
        ON dbo.SURVEY_STATUS_HISTORY (ChangedBy, ChangedDate)
        INCLUDE (SurveyId, FromStatus, ToStatus);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_SURVEYS_Created_Status'
      AND object_id = OBJECT_ID(N'dbo.SURVEYS'))
BEGIN
    -- Seeds #Scoped: every call filters on a Created window first.
    CREATE NONCLUSTERED INDEX IX_SURVEYS_Created_Status
        ON dbo.SURVEYS (Created, Status)
        INCLUDE (CbuCode, BranchCode, OperationAreaCode, DepartmentId, TemplateId, FaTypeCode);
END;

GO

-- Percentage change between two windows. Growth from a zero baseline has no
-- meaningful percentage, so it reports 100 when something appeared and 0 when
-- nothing did, rather than dividing by zero or returning NULL for the UI to guess at.
CREATE OR ALTER FUNCTION dbo.fn_Fsms_DeltaPercent
(
    @Current  DECIMAL(18, 4),
    @Previous DECIMAL(18, 4)
)
RETURNS DECIMAL(9, 2)
AS
BEGIN
    SET @Current  = ISNULL(@Current, 0);
    SET @Previous = ISNULL(@Previous, 0);

    IF @Previous = 0
        RETURN CASE WHEN @Current = 0 THEN 0 ELSE 100 END;

    RETURN CONVERT(DECIMAL(9, 2), (@Current - @Previous) * 100.0 / ABS(@Previous));
END;

GO

CREATE OR ALTER PROCEDURE dbo.usp_Fsms_Dashboard_Get
    @FromDate           DATETIMEOFFSET  = NULL,
    @ToDate             DATETIMEOFFSET  = NULL,
    @ClusterCode        NVARCHAR(50)    = NULL,
    @CbuCode            NVARCHAR(50)    = NULL,
    @BranchCode           NVARCHAR(50)    = NULL,
    @OperationAreaCode  NVARCHAR(50)    = NULL,
    @DepartmentId       INT             = NULL,
    @TemplateId         BIGINT          = NULL,
    @FaTypeCode         NVARCHAR(50)    = NULL,
    @Status             NVARCHAR(30)    = NULL,
    @Source             NVARCHAR(20)    = NULL,
    @UserId             NVARCHAR(450)   = NULL,
    @PeerScope          NVARCHAR(20)    = N'ROLE',
    @RoleName           NVARCHAR(256)   = NULL,
    @TrendGrain         NVARCHAR(10)    = N'MONTH',
    @TopN               INT             = 10,
    @IsUnrestricted     BIT             = 0,
    @ScopeGroupsJson    NVARCHAR(MAX)   = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- ---------------------------------------------------------------------
    -- Defaults and guards
    -- ---------------------------------------------------------------------
    DECLARE @Now DATETIMEOFFSET = SYSDATETIMEOFFSET();

    SET @ToDate   = ISNULL(@ToDate, @Now);
    SET @FromDate = ISNULL(@FromDate, DATEADD(MONTH, -6, @ToDate));

    IF @FromDate > @ToDate
    BEGIN
        DECLARE @Swap DATETIMEOFFSET = @FromDate;
        SET @FromDate = @ToDate;
        SET @ToDate = @Swap;
    END;

    SET @TopN = CASE WHEN @TopN IS NULL OR @TopN < 1 THEN 10
                     WHEN @TopN > 50 THEN 50
                     ELSE @TopN END;

    SET @TrendGrain = UPPER(ISNULL(NULLIF(LTRIM(RTRIM(@TrendGrain)), N''), N'MONTH'));
    IF @TrendGrain NOT IN (N'DAY', N'WEEK', N'MONTH') SET @TrendGrain = N'MONTH';

    SET @PeerScope = UPPER(ISNULL(NULLIF(LTRIM(RTRIM(@PeerScope)), N''), N'ROLE'));
    IF @PeerScope NOT IN (N'ROLE', N'TEAM', N'DEPARTMENT', N'BRANCH') SET @PeerScope = N'ROLE';

    -- The comparison window: the same span, immediately before @FromDate. Every KPI
    -- delta is measured against it.
    -- Measured in minutes, not seconds: DATEADD's offset argument overflows int, and a
    -- multi-year window is more seconds than an int holds.
    DECLARE @WindowMinutes INT =
        CASE WHEN DATEDIFF_BIG(MINUTE, @FromDate, @ToDate) > 2000000000
             THEN 2000000000
             ELSE CONVERT(INT, DATEDIFF_BIG(MINUTE, @FromDate, @ToDate)) END;
    DECLARE @PrevTo   DATETIMEOFFSET = @FromDate;
    DECLARE @PrevFrom DATETIMEOFFSET = DATEADD(MINUTE, -@WindowMinutes, @FromDate);

    -- ---------------------------------------------------------------------
    -- Row-level security. Mirrors CallerScopePredicate in GetSurveys.cs: an OR
    -- across the caller's scope groups, each group pairing ONE department with the
    -- territory codes that department reaches. ANDing "my departments" against "my
    -- territories" instead would hand someone covering water-in-Riyadh and
    -- wastewater-in-Jeddah the two cross-product combinations they do not cover.
    -- ---------------------------------------------------------------------
    CREATE TABLE #ScopeGroup
    (
        GroupId             INT NOT NULL PRIMARY KEY,
        DepartmentId        INT NULL,
        CoversAllTerritory  BIT NOT NULL
    );

    CREATE TABLE #ScopeCode
    (
        GroupId INT           NOT NULL,
        Level   NVARCHAR(20)  NOT NULL,
        Code    NVARCHAR(50)  NOT NULL
    );

    CREATE CLUSTERED INDEX IX_ScopeCode ON #ScopeCode (GroupId, Level, Code);

    IF @IsUnrestricted = 0 AND @ScopeGroupsJson IS NOT NULL AND LTRIM(RTRIM(@ScopeGroupsJson)) <> N''
    BEGIN
        INSERT INTO #ScopeGroup (GroupId, DepartmentId, CoversAllTerritory)
        SELECT
            CONVERT(INT, g.[key]),
            TRY_CONVERT(INT, JSON_VALUE(g.[value], '$.departmentId')),
            CASE WHEN JSON_VALUE(g.[value], '$.coversAllTerritory') IN ('true', '1') THEN 1 ELSE 0 END
        FROM OPENJSON(@ScopeGroupsJson) AS g;

        INSERT INTO #ScopeCode (GroupId, Level, Code)
        SELECT CONVERT(INT, g.[key]), N'Cbu', c.[value]
        FROM OPENJSON(@ScopeGroupsJson) AS g
        CROSS APPLY OPENJSON(g.[value], '$.cbuCodes') AS c
        WHERE c.[value] IS NOT NULL
        UNION ALL
        SELECT CONVERT(INT, g.[key]), N'Branch', c.[value]
        FROM OPENJSON(@ScopeGroupsJson) AS g
        CROSS APPLY OPENJSON(g.[value], '$.branchCodes') AS c
        WHERE c.[value] IS NOT NULL
        UNION ALL
        SELECT CONVERT(INT, g.[key]), N'OperationArea', c.[value]
        FROM OPENJSON(@ScopeGroupsJson) AS g
        CROSS APPLY OPENJSON(g.[value], '$.operationAreaCodes') AS c
        WHERE c.[value] IS NOT NULL;
    END;

    -- No groups at all means no coverage, which must read as "sees nothing" rather
    -- than "sees everything" — the empty-scope case is the one that matters.
    DECLARE @ApplyScope BIT =
        CASE WHEN @IsUnrestricted = 1 THEN 0 ELSE 1 END;

    -- ---------------------------------------------------------------------
    -- @ClusterCode expands to its CBUs; the two org codes then filter together.
    -- ---------------------------------------------------------------------
    CREATE TABLE #FilterCbu (Code NVARCHAR(50) NOT NULL PRIMARY KEY);

    IF @ClusterCode IS NOT NULL OR @CbuCode IS NOT NULL
    BEGIN
        INSERT INTO #FilterCbu (Code)
        SELECT DISTINCT x.Code
        FROM (
            SELECT cbu.Code
            FROM dbo.LKP_CBU AS cbu
            WHERE @ClusterCode IS NOT NULL AND cbu.ClusterCode = @ClusterCode
            UNION
            SELECT @CbuCode
            WHERE @CbuCode IS NOT NULL
        ) AS x
        WHERE x.Code IS NOT NULL;
    END;

    DECLARE @HasCbuFilter BIT = CASE WHEN EXISTS (SELECT 1 FROM #FilterCbu) THEN 1 ELSE 0 END;

    -- ---------------------------------------------------------------------
    -- #Scoped — the one filtered read. Everything below aggregates this.
    -- ---------------------------------------------------------------------
    CREATE TABLE #Scoped
    (
        Id                 BIGINT          NOT NULL PRIMARY KEY,
        SurveyCode         NVARCHAR(60)    NOT NULL,
        [Status]           NVARCHAR(30)    NOT NULL,
        [Source]           NVARCHAR(20)    NOT NULL,
        TemplateId         BIGINT          NOT NULL,
        FaTypeCode         NVARCHAR(50)    NULL,
        CbuCode            NVARCHAR(50)    NULL,
        BranchCode           NVARCHAR(50)    NULL,
        OperationAreaCode  NVARCHAR(50)    NULL,
        DepartmentId       INT             NULL,
        ReturnReasonCode   NVARCHAR(50)    NULL,
        Created            DATETIMEOFFSET  NOT NULL,
        AssignedDate       DATETIMEOFFSET  NULL,
        StartedDate        DATETIMEOFFSET  NULL,
        SubmittedDate      DATETIMEOFFSET  NULL,
        CompletedDate      DATETIMEOFFSET  NULL,
        ReturnedDate       DATETIMEOFFSET  NULL,
        DueDate            DATETIMEOFFSET  NULL,
        CompletionDueDate  DATETIMEOFFSET  NULL,
        ReturnCount        INT             NOT NULL,
        SubmissionCount    INT             NOT NULL,
        ActiveTeamId       BIGINT          NULL,
        LastTeamId         BIGINT          NULL
    );

    INSERT INTO #Scoped
    SELECT
        s.Id, s.SurveyCode, s.[Status], s.[Source], s.TemplateId, s.FaTypeCode,
        s.CbuCode, s.BranchCode, s.OperationAreaCode, s.DepartmentId, s.ReturnReasonCode,
        s.Created, s.AssignedDate, s.StartedDate, s.SubmittedDate, s.CompletedDate,
        s.ReturnedDate, s.DueDate, s.CompletionDueDate, s.ReturnCount, s.SubmissionCount,
        a.FieldTeamId, l.FieldTeamId
    FROM dbo.SURVEYS AS s
    OUTER APPLY (
        SELECT TOP (1) sa.FieldTeamId
        FROM dbo.SURVEY_ASSIGNMENTS AS sa
        WHERE sa.SurveyId = s.Id AND sa.IsActive = 1
        ORDER BY sa.AssignedDate DESC, sa.Id DESC
    ) AS a
    -- The team that *held* the survey, active or not. Approving a survey deactivates its
    -- assignment, so ActiveTeamId is NULL for everything finished — asking "who did this"
    -- of completed work needs the last allocation, not the current one.
    OUTER APPLY (
        SELECT TOP (1) sa.FieldTeamId
        FROM dbo.SURVEY_ASSIGNMENTS AS sa
        WHERE sa.SurveyId = s.Id
        ORDER BY sa.IsActive DESC, sa.AssignedDate DESC, sa.Id DESC
    ) AS l
    WHERE s.Created >= @FromDate
      AND s.Created <= @ToDate
      AND (@Status IS NULL OR s.[Status] = @Status)
      AND (@Source IS NULL OR s.[Source] = @Source)
      AND (@TemplateId IS NULL OR s.TemplateId = @TemplateId)
      AND (@FaTypeCode IS NULL OR s.FaTypeCode = @FaTypeCode)
      AND (@DepartmentId IS NULL OR s.DepartmentId = @DepartmentId)
      AND (@BranchCode IS NULL OR s.BranchCode = @BranchCode)
      AND (@OperationAreaCode IS NULL OR s.OperationAreaCode = @OperationAreaCode)
      AND (@HasCbuFilter = 0 OR s.CbuCode IN (SELECT Code FROM #FilterCbu))
      AND (
            @ApplyScope = 0
            OR EXISTS (
                SELECT 1
                FROM #ScopeGroup AS g
                WHERE
                    -- A survey with no department is admitted by every group: the field is
                    -- optional, and hiding unclassified work loses it rather than protects it.
                    (g.DepartmentId IS NULL OR s.DepartmentId IS NULL OR s.DepartmentId = g.DepartmentId)
                    AND (
                        g.CoversAllTerritory = 1
                        OR EXISTS (
                            SELECT 1 FROM #ScopeCode AS sc
                            WHERE sc.GroupId = g.GroupId
                              AND (
                                    (sc.Level = N'Cbu'           AND sc.Code = s.CbuCode)
                                 OR (sc.Level = N'Branch'          AND sc.Code = s.BranchCode)
                                 OR (sc.Level = N'OperationArea' AND sc.Code = s.OperationAreaCode)
                              )
                        )
                    )
            )
          );

    -- The preceding window, for deltas. Only the columns the KPI set needs.
    CREATE TABLE #Prev
    (
        Id                 BIGINT         NOT NULL PRIMARY KEY,
        [Status]           NVARCHAR(30)   NOT NULL,
        SubmittedDate      DATETIMEOFFSET NULL,
        CompletedDate      DATETIMEOFFSET NULL,
        DueDate            DATETIMEOFFSET NULL,
        CompletionDueDate  DATETIMEOFFSET NULL,
        ReturnCount        INT            NOT NULL,
        ActiveTeamId       BIGINT         NULL
    );

    INSERT INTO #Prev
    SELECT s.Id, s.[Status], s.SubmittedDate, s.CompletedDate, s.DueDate, s.CompletionDueDate,
           s.ReturnCount, a.FieldTeamId
    FROM dbo.SURVEYS AS s
    OUTER APPLY (
        SELECT TOP (1) sa.FieldTeamId
        FROM dbo.SURVEY_ASSIGNMENTS AS sa
        WHERE sa.SurveyId = s.Id AND sa.IsActive = 1
        ORDER BY sa.AssignedDate DESC, sa.Id DESC
    ) AS a
    WHERE s.Created >= @PrevFrom
      AND s.Created < @PrevTo
      AND (@Status IS NULL OR s.[Status] = @Status)
      AND (@Source IS NULL OR s.[Source] = @Source)
      AND (@TemplateId IS NULL OR s.TemplateId = @TemplateId)
      AND (@FaTypeCode IS NULL OR s.FaTypeCode = @FaTypeCode)
      AND (@DepartmentId IS NULL OR s.DepartmentId = @DepartmentId)
      AND (@BranchCode IS NULL OR s.BranchCode = @BranchCode)
      AND (@OperationAreaCode IS NULL OR s.OperationAreaCode = @OperationAreaCode)
      AND (@HasCbuFilter = 0 OR s.CbuCode IN (SELECT Code FROM #FilterCbu))
      AND (
            @ApplyScope = 0
            OR EXISTS (
                SELECT 1
                FROM #ScopeGroup AS g
                WHERE (g.DepartmentId IS NULL OR s.DepartmentId IS NULL OR s.DepartmentId = g.DepartmentId)
                  AND (
                        g.CoversAllTerritory = 1
                        OR EXISTS (
                            SELECT 1 FROM #ScopeCode AS sc
                            WHERE sc.GroupId = g.GroupId
                              AND (
                                    (sc.Level = N'Cbu'           AND sc.Code = s.CbuCode)
                                 OR (sc.Level = N'Branch'          AND sc.Code = s.BranchCode)
                                 OR (sc.Level = N'OperationArea' AND sc.Code = s.OperationAreaCode)
                              )
                        )
                    )
            )
          );

    -- =====================================================================
    -- RESULT SET 1 — KPIs (current window, previous window, and the delta)
    -- =====================================================================
    DECLARE
        @Total INT, @Approved INT, @Returned INT, @InProgress INT,
        @Submitted INT, @Overdue INT, @ActiveTeams INT,
        @AvgHours DECIMAL(18, 2), @OnTime INT, @Completed INT;

    -- SUBMITTED is the awaiting-review state: a filled survey is completed or returned
    -- straight from it, so there is no separate "received" count to report.
    SELECT
        @Total       = COUNT(*),
        @Approved    = SUM(CASE WHEN [Status] = N'APPROVED'     THEN 1 ELSE 0 END),
        @Returned    = SUM(CASE WHEN [Status] = N'RETURNED'     THEN 1 ELSE 0 END),
        @InProgress  = SUM(CASE WHEN [Status] = N'IN_PROGRESS'  THEN 1 ELSE 0 END),
        @Submitted   = SUM(CASE WHEN [Status] = N'SUBMITTED'    THEN 1 ELSE 0 END),
        -- Overdue: open fill past DueDate, or awaiting-review past CompletionDueDate.
        -- An approved survey that missed a deadline was late, not overdue.
        @Overdue     = SUM(CASE
                            WHEN DueDate IS NOT NULL AND DueDate < @Now
                                 AND [Status] IN (N'CREATED', N'ASSIGNED', N'IN_PROGRESS', N'RETURNED')
                            THEN 1
                            WHEN CompletionDueDate IS NOT NULL AND CompletionDueDate < @Now
                                 AND [Status] = N'SUBMITTED'
                            THEN 1
                            ELSE 0 END),
        @Completed   = SUM(CASE WHEN CompletedDate IS NOT NULL THEN 1 ELSE 0 END),
        -- On time only when both clocks are respected (null deadline = on time for that clock).
        @OnTime      = SUM(CASE WHEN CompletedDate IS NOT NULL
                                 AND (DueDate IS NULL OR SubmittedDate IS NULL OR SubmittedDate <= DueDate)
                                 AND (CompletionDueDate IS NULL OR CompletedDate <= CompletionDueDate)
                            THEN 1 ELSE 0 END),
        -- Measured from assignment, not creation: the clock a field team is judged
        -- on starts when the work reaches them.
        @AvgHours    = AVG(CASE WHEN CompletedDate IS NOT NULL AND AssignedDate IS NOT NULL
                            THEN CONVERT(DECIMAL(18, 2), DATEDIFF_BIG(MINUTE, AssignedDate, CompletedDate)) / 60.0
                          END)
    FROM #Scoped;

    SELECT @ActiveTeams = COUNT(DISTINCT ActiveTeamId) FROM #Scoped WHERE ActiveTeamId IS NOT NULL;

    DECLARE
        @PrevTotal INT, @PrevApproved INT, @PrevSubmitted INT, @PrevReturned INT,
        @PrevInProgress INT, @PrevOverdue INT, @PrevActiveTeams INT,
        @PrevAvgHours DECIMAL(18, 2), @PrevOnTime INT, @PrevCompleted INT;

    SELECT
        @PrevTotal       = COUNT(*),
        @PrevApproved    = SUM(CASE WHEN [Status] = N'APPROVED'     THEN 1 ELSE 0 END),
        @PrevSubmitted   = SUM(CASE WHEN [Status] = N'SUBMITTED'    THEN 1 ELSE 0 END),
        @PrevReturned    = SUM(CASE WHEN [Status] = N'RETURNED'     THEN 1 ELSE 0 END),
        @PrevInProgress  = SUM(CASE WHEN [Status] = N'IN_PROGRESS'  THEN 1 ELSE 0 END),
        @PrevOverdue     = SUM(CASE
                                WHEN DueDate IS NOT NULL AND DueDate < @PrevTo
                                     AND [Status] IN (N'CREATED', N'ASSIGNED', N'IN_PROGRESS', N'RETURNED')
                                THEN 1
                                WHEN CompletionDueDate IS NOT NULL AND CompletionDueDate < @PrevTo
                                     AND [Status] = N'SUBMITTED'
                                THEN 1
                                ELSE 0 END),
        @PrevCompleted   = SUM(CASE WHEN CompletedDate IS NOT NULL THEN 1 ELSE 0 END),
        @PrevOnTime      = SUM(CASE WHEN CompletedDate IS NOT NULL
                                     AND (DueDate IS NULL OR SubmittedDate IS NULL OR SubmittedDate <= DueDate)
                                     AND (CompletionDueDate IS NULL OR CompletedDate <= CompletionDueDate)
                                THEN 1 ELSE 0 END)
    FROM #Prev;

    SELECT @PrevActiveTeams = COUNT(DISTINCT ActiveTeamId) FROM #Prev WHERE ActiveTeamId IS NOT NULL;

    SET @Total = ISNULL(@Total, 0);
    SET @PrevTotal = ISNULL(@PrevTotal, 0);

    DECLARE @CompletionRate DECIMAL(9, 2) =
        CASE WHEN @Total > 0 THEN CONVERT(DECIMAL(9, 2), ISNULL(@Approved, 0) * 100.0 / @Total) ELSE 0 END;
    DECLARE @PrevCompletionRate DECIMAL(9, 2) =
        CASE WHEN @PrevTotal > 0 THEN CONVERT(DECIMAL(9, 2), ISNULL(@PrevApproved, 0) * 100.0 / @PrevTotal) ELSE 0 END;
    DECLARE @ReturnRate DECIMAL(9, 2) =
        CASE WHEN @Total > 0
             THEN CONVERT(DECIMAL(9, 2), (SELECT COUNT(*) FROM #Scoped WHERE ReturnCount > 0) * 100.0 / @Total)
             ELSE 0 END;
    DECLARE @PrevReturnRate DECIMAL(9, 2) =
        CASE WHEN @PrevTotal > 0
             THEN CONVERT(DECIMAL(9, 2), (SELECT COUNT(*) FROM #Prev WHERE ReturnCount > 0) * 100.0 / @PrevTotal)
             ELSE 0 END;
    DECLARE @OnTimeRate DECIMAL(9, 2) =
        CASE WHEN ISNULL(@Completed, 0) > 0 THEN CONVERT(DECIMAL(9, 2), @OnTime * 100.0 / @Completed) ELSE 0 END;
    DECLARE @PrevOnTimeRate DECIMAL(9, 2) =
        CASE WHEN ISNULL(@PrevCompleted, 0) > 0 THEN CONVERT(DECIMAL(9, 2), @PrevOnTime * 100.0 / @PrevCompleted) ELSE 0 END;

    SELECT
        @FromDate                                                    AS FromDate,
        @ToDate                                                      AS ToDate,
        @Total                                                       AS TotalSurveys,
        ISNULL(@Approved, 0)                                         AS ApprovedSurveys,
        ISNULL(@Returned, 0)                                         AS ReturnedSurveys,
        ISNULL(@InProgress, 0)                                       AS InProgressSurveys,
        ISNULL(@Submitted, 0)                                        AS SubmittedSurveys,
        ISNULL(@Overdue, 0)                                          AS OverdueSurveys,
        ISNULL(@ActiveTeams, 0)                                      AS ActiveTeams,
        ISNULL(@AvgHours, 0)                                         AS AvgCompletionHours,
        @CompletionRate                                              AS CompletionRatePercent,
        @ReturnRate                                                  AS ReturnRatePercent,
        @OnTimeRate                                                  AS OnTimeRatePercent,
        dbo.fn_Fsms_DeltaPercent(@Total, @PrevTotal)                 AS TotalSurveysDelta,
        dbo.fn_Fsms_DeltaPercent(@Approved, @PrevApproved)           AS ApprovedSurveysDelta,
        dbo.fn_Fsms_DeltaPercent(@Submitted, @PrevSubmitted)         AS SubmittedSurveysDelta,
        dbo.fn_Fsms_DeltaPercent(@Returned, @PrevReturned)           AS ReturnedSurveysDelta,
        dbo.fn_Fsms_DeltaPercent(@InProgress, @PrevInProgress)       AS InProgressSurveysDelta,
        dbo.fn_Fsms_DeltaPercent(@Overdue, @PrevOverdue)             AS OverdueSurveysDelta,
        dbo.fn_Fsms_DeltaPercent(@ActiveTeams, @PrevActiveTeams)     AS ActiveTeamsDelta,
        dbo.fn_Fsms_DeltaPercent(@CompletionRate, @PrevCompletionRate) AS CompletionRateDelta,
        dbo.fn_Fsms_DeltaPercent(@ReturnRate, @PrevReturnRate)       AS ReturnRateDelta,
        dbo.fn_Fsms_DeltaPercent(@OnTimeRate, @PrevOnTimeRate)       AS OnTimeRateDelta;

    -- =====================================================================
    -- RESULT SET 2 — Trend
    --
    -- Bucketed off SURVEY_STATUS_HISTORY.ChangedDate, the date a transition actually
    -- happened, rather than off SURVEYS date columns — the latter only hold the most
    -- recent occurrence, so a survey returned twice would show one return.
    -- Left-joined onto a generated calendar so empty buckets come back as zero
    -- instead of vanishing and shifting the chart's x-axis.
    -- =====================================================================
    ;WITH Numbers AS (
        SELECT TOP (1000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS n
        FROM sys.all_objects
    ),
    Buckets AS (
        SELECT CASE @TrendGrain
                    WHEN N'DAY'  THEN DATEADD(DAY,   n, CONVERT(DATE, @FromDate))
                    WHEN N'WEEK' THEN DATEADD(WEEK,  n, DATEADD(DAY, -(DATEPART(WEEKDAY, @FromDate) - 1), CONVERT(DATE, @FromDate)))
                    ELSE              DATEADD(MONTH, n, DATEFROMPARTS(YEAR(@FromDate), MONTH(@FromDate), 1))
               END AS Bucket
        FROM Numbers
    ),
    Windowed AS (
        SELECT Bucket FROM Buckets WHERE Bucket <= CONVERT(DATE, @ToDate)
    ),
    Events AS (
        SELECT
            CASE @TrendGrain
                WHEN N'DAY'  THEN CONVERT(DATE, h.ChangedDate)
                WHEN N'WEEK' THEN DATEADD(DAY, -(DATEPART(WEEKDAY, h.ChangedDate) - 1), CONVERT(DATE, h.ChangedDate))
                ELSE              DATEFROMPARTS(YEAR(h.ChangedDate), MONTH(h.ChangedDate), 1)
            END AS Bucket,
            h.ToStatus
        FROM dbo.SURVEY_STATUS_HISTORY AS h
        INNER JOIN #Scoped AS s ON s.Id = h.SurveyId
        WHERE h.ChangedDate >= @FromDate AND h.ChangedDate <= @ToDate
    )
    SELECT
        w.Bucket                                                                          AS Bucket,
        CONVERT(NVARCHAR(10), w.Bucket, 23)                                               AS BucketKey,
        ISNULL(SUM(CASE WHEN e.ToStatus = N'CREATED'   THEN 1 ELSE 0 END), 0)             AS CreatedCount,
        ISNULL(SUM(CASE WHEN e.ToStatus = N'ASSIGNED'  THEN 1 ELSE 0 END), 0)             AS AssignedCount,
        ISNULL(SUM(CASE WHEN e.ToStatus = N'SUBMITTED' THEN 1 ELSE 0 END), 0)             AS SubmittedCount,
        ISNULL(SUM(CASE WHEN e.ToStatus = N'APPROVED'  THEN 1 ELSE 0 END), 0)             AS ApprovedCount,
        ISNULL(SUM(CASE WHEN e.ToStatus = N'RETURNED'  THEN 1 ELSE 0 END), 0)             AS ReturnedCount
    FROM Windowed AS w
    LEFT JOIN Events AS e ON e.Bucket = w.Bucket
    GROUP BY w.Bucket
    ORDER BY w.Bucket
    OPTION (MAXRECURSION 0);

    -- =====================================================================
    -- RESULT SET 3 — Status distribution
    -- Every status is emitted, including the zeroes, so the doughnut's slices and
    -- colours stay in a stable order between refreshes.
    -- =====================================================================
    ;WITH AllStatuses AS (
        SELECT v.[Status], v.SortOrder
        FROM (VALUES
            (N'CREATED', 1), (N'ASSIGNED', 2), (N'IN_PROGRESS', 3), (N'SUBMITTED', 4),
            (N'UNDER_REVIEW', 5), (N'APPROVED', 6), (N'RETURNED', 7), (N'EXPIRED', 8)
        ) AS v([Status], SortOrder)
    )
    SELECT
        a.[Status]                                                                    AS [Status],
        ISNULL(c.Cnt, 0)                                                              AS [Count],
        CASE WHEN @Total > 0 THEN CONVERT(DECIMAL(9, 2), ISNULL(c.Cnt, 0) * 100.0 / @Total) ELSE 0 END AS [Percent],
        a.SortOrder                                                                   AS SortOrder
    FROM AllStatuses AS a
    LEFT JOIN (SELECT [Status], COUNT(*) AS Cnt FROM #Scoped GROUP BY [Status]) AS c
        ON c.[Status] = a.[Status]
    ORDER BY a.SortOrder;

    -- =====================================================================
    -- RESULT SET 4 — Teams per CBU / Branch / Operation Area
    --
    -- A team's coverage lives in ORG_SCOPES at whatever level it was granted, so a
    -- team scoped at 'Cbu' has to count toward every branch and operation area under
    -- that CBU. Branch and operation area are siblings, not a chain, so each leaf is
    -- resolved from the CBU independently and neither can be derived from the other.
    -- #TeamUnit holds one (team, level, code) row per unit a team reaches; it is a heap
    -- and may hold duplicates, which the COUNT(DISTINCT TeamId) rollup below absorbs —
    -- that is also what stops overlapping grants double-counting a team.
    -- =====================================================================
    CREATE TABLE #TeamUnit
    (
        TeamId  BIGINT       NOT NULL,
        [Level] NVARCHAR(20) NOT NULL,
        Code    NVARCHAR(50) NOT NULL
    );

    CREATE CLUSTERED INDEX IX_TeamUnit ON #TeamUnit ([Level], Code, TeamId);

    -- Branches: granted directly, or reached through the CBU / cluster above them.
    INSERT INTO #TeamUnit (TeamId, [Level], Code)
    SELECT DISTINCT
        TRY_CONVERT(BIGINT, os.OwnerId), N'Branch', b.Code
    FROM dbo.ORG_SCOPES AS os
    INNER JOIN dbo.LKP_CBU    AS cbu ON cbu.IsActive = 1
    INNER JOIN dbo.LKP_BRANCH AS b   ON b.CbuCode = cbu.Code AND b.IsActive = 1
    WHERE os.OwnerType = N'Team'
      AND os.IsActive = 1
      AND TRY_CONVERT(BIGINT, os.OwnerId) IS NOT NULL
      AND (@DepartmentId IS NULL OR os.DepartmentId IS NULL OR os.DepartmentId = @DepartmentId)
      AND (
            os.Level IS NULL OR os.Code IS NULL                            -- department-everywhere grant
         OR (os.Level = N'Cluster' AND os.Code = cbu.ClusterCode)
         OR (os.Level = N'Cbu'     AND os.Code = cbu.Code)
         OR (os.Level = N'Branch'  AND os.Code = b.Code)
          );

    -- Operation areas: granted directly, or reached through the CBU / cluster above them.
    -- A branch grant deliberately does not appear here — it reaches no operation area.
    INSERT INTO #TeamUnit (TeamId, [Level], Code)
    SELECT DISTINCT
        TRY_CONVERT(BIGINT, os.OwnerId), N'OperationArea', oa.Code
    FROM dbo.ORG_SCOPES AS os
    INNER JOIN dbo.LKP_CBU            AS cbu ON cbu.IsActive = 1
    INNER JOIN dbo.LKP_OPERATION_AREA AS oa  ON oa.CbuCode = cbu.Code AND oa.IsActive = 1
    WHERE os.OwnerType = N'Team'
      AND os.IsActive = 1
      AND TRY_CONVERT(BIGINT, os.OwnerId) IS NOT NULL
      AND (@DepartmentId IS NULL OR os.DepartmentId IS NULL OR os.DepartmentId = @DepartmentId)
      AND (
            os.Level IS NULL OR os.Code IS NULL
         OR (os.Level = N'Cluster'       AND os.Code = cbu.ClusterCode)
         OR (os.Level = N'Cbu'           AND os.Code = cbu.Code)
         OR (os.Level = N'OperationArea' AND os.Code = oa.Code)
          );

    -- CBUs: granted directly, or inferred upwards from a grant at either leaf level, so a
    -- branch-scoped team still counts toward the CBU it sits in.
    INSERT INTO #TeamUnit (TeamId, [Level], Code)
    SELECT DISTINCT
        TRY_CONVERT(BIGINT, os.OwnerId), N'Cbu', cbu.Code
    FROM dbo.ORG_SCOPES AS os
    INNER JOIN dbo.LKP_CBU AS cbu ON cbu.IsActive = 1
    WHERE os.OwnerType = N'Team'
      AND os.IsActive = 1
      AND TRY_CONVERT(BIGINT, os.OwnerId) IS NOT NULL
      AND (@DepartmentId IS NULL OR os.DepartmentId IS NULL OR os.DepartmentId = @DepartmentId)
      AND (
            os.Level IS NULL OR os.Code IS NULL
         OR (os.Level = N'Cluster' AND os.Code = cbu.ClusterCode)
         OR (os.Level = N'Cbu'     AND os.Code = cbu.Code)
         OR (os.Level = N'Branch'  AND EXISTS (
                SELECT 1 FROM dbo.LKP_BRANCH AS b
                WHERE b.Code = os.Code AND b.CbuCode = cbu.Code AND b.IsActive = 1))
         OR (os.Level = N'OperationArea' AND EXISTS (
                SELECT 1 FROM dbo.LKP_OPERATION_AREA AS oa
                WHERE oa.Code = os.Code AND oa.CbuCode = cbu.Code AND oa.IsActive = 1))
          );

    -- Head-count per team, from the Identity side. Not an FK by design
    -- (TEAMS is mirrored from WFM), so this is a plain join.
    CREATE TABLE #TeamMembers (TeamId BIGINT NOT NULL PRIMARY KEY, MemberCount INT NOT NULL);

    INSERT INTO #TeamMembers (TeamId, MemberCount)
    SELECT u.FieldTeamId, COUNT(*)
    FROM dbo.AspNetUsers AS u
    WHERE u.FieldTeamId IS NOT NULL
    GROUP BY u.FieldTeamId;

    ;WITH TeamStats AS (
        SELECT tu.[Level], tu.Code, tu.TeamId
        FROM #TeamUnit AS tu
        GROUP BY tu.[Level], tu.Code, tu.TeamId
    ),
    TeamAgg AS (
        SELECT
            ts.[Level], ts.Code,
            COUNT(DISTINCT ts.TeamId)          AS TeamCount,
            SUM(ISNULL(tm.MemberCount, 0))     AS MemberCount
        FROM TeamStats AS ts
        LEFT JOIN #TeamMembers AS tm ON tm.TeamId = ts.TeamId
        GROUP BY ts.[Level], ts.Code
    ),
    SurveyAgg AS (
        SELECT N'Cbu' AS [Level], CbuCode AS Code,
               COUNT(*) AS SurveyCount,
               SUM(CASE WHEN [Status] IN (N'CREATED', N'ASSIGNED', N'IN_PROGRESS', N'RETURNED') THEN 1 ELSE 0 END) AS OpenCount,
               SUM(CASE WHEN [Status] = N'APPROVED' THEN 1 ELSE 0 END) AS ApprovedCount,
               COUNT(DISTINCT ActiveTeamId) AS WorkingTeamCount,
               AVG(CASE WHEN CompletedDate IS NOT NULL AND AssignedDate IS NOT NULL
                        THEN CONVERT(DECIMAL(18, 2), DATEDIFF_BIG(MINUTE, AssignedDate, CompletedDate)) / 60.0 END) AS AvgHours
        FROM #Scoped WHERE CbuCode IS NOT NULL GROUP BY CbuCode
        UNION ALL
        SELECT N'Branch', BranchCode,
               COUNT(*),
               SUM(CASE WHEN [Status] IN (N'CREATED', N'ASSIGNED', N'IN_PROGRESS', N'RETURNED') THEN 1 ELSE 0 END),
               SUM(CASE WHEN [Status] = N'APPROVED' THEN 1 ELSE 0 END),
               COUNT(DISTINCT ActiveTeamId),
               AVG(CASE WHEN CompletedDate IS NOT NULL AND AssignedDate IS NOT NULL
                        THEN CONVERT(DECIMAL(18, 2), DATEDIFF_BIG(MINUTE, AssignedDate, CompletedDate)) / 60.0 END)
        FROM #Scoped WHERE BranchCode IS NOT NULL GROUP BY BranchCode
        UNION ALL
        SELECT N'OperationArea', OperationAreaCode,
               COUNT(*),
               SUM(CASE WHEN [Status] IN (N'CREATED', N'ASSIGNED', N'IN_PROGRESS', N'RETURNED') THEN 1 ELSE 0 END),
               SUM(CASE WHEN [Status] = N'APPROVED' THEN 1 ELSE 0 END),
               COUNT(DISTINCT ActiveTeamId),
               AVG(CASE WHEN CompletedDate IS NOT NULL AND AssignedDate IS NOT NULL
                        THEN CONVERT(DECIMAL(18, 2), DATEDIFF_BIG(MINUTE, AssignedDate, CompletedDate)) / 60.0 END)
        FROM #Scoped WHERE OperationAreaCode IS NOT NULL GROUP BY OperationAreaCode
    ),
    Nodes AS (
        SELECT N'Cbu' AS [Level], cbu.Code, cbu.NameEn, cbu.NameAr, cbu.ClusterCode AS ParentCode
        FROM dbo.LKP_CBU AS cbu WHERE cbu.IsActive = 1
        UNION ALL
        SELECT N'Branch', z.Code, z.NameEn, z.NameAr, z.CbuCode FROM dbo.LKP_BRANCH AS z WHERE z.IsActive = 1
        UNION ALL
        SELECT N'OperationArea', oa.Code, oa.NameEn, oa.NameAr, oa.CbuCode
        FROM dbo.LKP_OPERATION_AREA AS oa WHERE oa.IsActive = 1
    )
    SELECT
        n.[Level]                                    AS [Level],
        n.Code                                      AS Code,
        n.NameEn                                    AS NameEn,
        n.NameAr                                    AS NameAr,
        n.ParentCode                                AS ParentCode,
        ISNULL(t.TeamCount, 0)                      AS TeamCount,
        ISNULL(sa.WorkingTeamCount, 0)              AS WorkingTeamCount,
        ISNULL(t.MemberCount, 0)                    AS MemberCount,
        ISNULL(sa.SurveyCount, 0)                   AS SurveyCount,
        ISNULL(sa.OpenCount, 0)                     AS OpenSurveyCount,
        ISNULL(sa.ApprovedCount, 0)                 AS ApprovedSurveyCount,
        CASE WHEN ISNULL(t.TeamCount, 0) > 0
             THEN CONVERT(DECIMAL(18, 2), ISNULL(sa.SurveyCount, 0) * 1.0 / t.TeamCount)
             ELSE 0 END                             AS SurveysPerTeam,
        ISNULL(sa.AvgHours, 0)                      AS AvgCompletionHours
    FROM Nodes AS n
    LEFT JOIN TeamAgg   AS t  ON t.[Level] = n.[Level] AND t.Code = n.Code
    LEFT JOIN SurveyAgg AS sa ON sa.[Level] = n.[Level] AND sa.Code = n.Code
    -- Nodes with neither a team nor a survey are noise on a dashboard.
    WHERE ISNULL(t.TeamCount, 0) > 0 OR ISNULL(sa.SurveyCount, 0) > 0
    ORDER BY
        CASE n.[Level] WHEN N'Cbu' THEN 1 WHEN N'Branch' THEN 2 ELSE 3 END,
        ISNULL(sa.SurveyCount, 0) DESC,
        n.Code;

    -- =====================================================================
    -- RESULT SET 5 — Teams per department
    -- Reads TEAM_DEPARTMENTS, not the legacy comma-separated TEAMS.Departments.
    -- =====================================================================
    SELECT
        d.Id                                                      AS DepartmentId,
        d.NameEn                                                  AS NameEn,
        d.NameAr                                                  AS NameAr,
        ISNULL(t.TeamCount, 0)                                    AS TeamCount,
        ISNULL(s.SurveyCount, 0)                                  AS SurveyCount,
        ISNULL(s.OpenCount, 0)                                    AS OpenSurveyCount,
        ISNULL(s.ApprovedCount, 0)                                AS ApprovedSurveyCount,
        CASE WHEN ISNULL(t.TeamCount, 0) > 0
             THEN CONVERT(DECIMAL(18, 2), ISNULL(s.SurveyCount, 0) * 1.0 / t.TeamCount)
             ELSE 0 END                                           AS SurveysPerTeam
    FROM dbo.LKP_DEPARTMENT AS d
    LEFT JOIN (
        SELECT td.DepartmentId, COUNT(DISTINCT td.TeamId) AS TeamCount
        FROM dbo.TEAM_DEPARTMENTS AS td
        GROUP BY td.DepartmentId
    ) AS t ON t.DepartmentId = d.Id
    LEFT JOIN (
        SELECT DepartmentId,
               COUNT(*) AS SurveyCount,
               SUM(CASE WHEN [Status] IN (N'CREATED', N'ASSIGNED', N'IN_PROGRESS', N'RETURNED') THEN 1 ELSE 0 END) AS OpenCount,
               SUM(CASE WHEN [Status] = N'APPROVED' THEN 1 ELSE 0 END) AS ApprovedCount
        FROM #Scoped WHERE DepartmentId IS NOT NULL GROUP BY DepartmentId
    ) AS s ON s.DepartmentId = d.Id
    WHERE d.IsActive = 1
      AND (ISNULL(t.TeamCount, 0) > 0 OR ISNULL(s.SurveyCount, 0) > 0)
    ORDER BY ISNULL(s.SurveyCount, 0) DESC, d.NameEn;

    -- =====================================================================
    -- Per-user transaction ledger, shared by sets 6-8.
    --
    -- SURVEY_STATUS_HISTORY is the only place a person's actions are recorded, and
    -- ChangedBy holds the Identity GUID (every command passes user.Id). Restricted
    -- to #Scoped so a caller never counts transactions on surveys they cannot see.
    -- =====================================================================
    CREATE TABLE #UserActivity
    (
        UserId           NVARCHAR(450)  NOT NULL PRIMARY KEY,
        TransactionCount INT            NOT NULL,
        SurveysTouched   INT            NOT NULL,
        AssignedCount    INT            NOT NULL,
        SubmittedCount   INT            NOT NULL,
        ApprovedCount    INT            NOT NULL,
        ReturnedCount    INT            NOT NULL,
        LastActivityDate DATETIMEOFFSET NULL
    );

    INSERT INTO #UserActivity
    SELECT
        h.ChangedBy,
        COUNT(*),
        COUNT(DISTINCT h.SurveyId),
        SUM(CASE WHEN h.ToStatus = N'ASSIGNED'     THEN 1 ELSE 0 END),
        SUM(CASE WHEN h.ToStatus = N'SUBMITTED'    THEN 1 ELSE 0 END),
        SUM(CASE WHEN h.ToStatus = N'APPROVED'     THEN 1 ELSE 0 END),
        SUM(CASE WHEN h.ToStatus = N'RETURNED'     THEN 1 ELSE 0 END),
        MAX(h.ChangedDate)
    FROM dbo.SURVEY_STATUS_HISTORY AS h
    INNER JOIN #Scoped AS s ON s.Id = h.SurveyId
    WHERE h.ChangedBy IS NOT NULL
      AND h.ChangedDate >= @FromDate AND h.ChangedDate <= @ToDate
    GROUP BY h.ChangedBy;

    -- Average hours between a user picking a survey up and their own next action on
    -- it — their handling time, as opposed to the survey's end-to-end duration.
    CREATE TABLE #UserHandling (UserId NVARCHAR(450) NOT NULL PRIMARY KEY, AvgHandlingHours DECIMAL(18, 2) NULL);

    INSERT INTO #UserHandling
    SELECT x.ChangedBy,
           AVG(CONVERT(DECIMAL(18, 2), DATEDIFF_BIG(MINUTE, x.PrevChangedDate, x.ChangedDate)) / 60.0)
    FROM (
        SELECT h.ChangedBy, h.ChangedDate,
               LAG(h.ChangedDate) OVER (PARTITION BY h.SurveyId ORDER BY h.ChangedDate, h.Id) AS PrevChangedDate
        FROM dbo.SURVEY_STATUS_HISTORY AS h
        INNER JOIN #Scoped AS s ON s.Id = h.SurveyId
        WHERE h.ChangedBy IS NOT NULL
          AND h.ChangedDate >= @FromDate AND h.ChangedDate <= @ToDate
    ) AS x
    WHERE x.PrevChangedDate IS NOT NULL
    GROUP BY x.ChangedBy;

    -- =====================================================================
    -- The caller's peer cohort. @RoleName pins it explicitly; otherwise it is
    -- every role the caller holds. A user with no roles compares against nobody,
    -- which is correct — an empty cohort beats a fabricated one.
    -- =====================================================================
    CREATE TABLE #Cohort
    (
        UserId      NVARCHAR(450) NOT NULL PRIMARY KEY,
        DisplayName NVARCHAR(256) NULL,
        RoleName    NVARCHAR(256) NULL
    );

    IF @UserId IS NOT NULL
    BEGIN
        DECLARE @CallerTeamId BIGINT =
            (SELECT TOP (1) u.FieldTeamId FROM dbo.AspNetUsers AS u WHERE u.Id = @UserId);

        IF @PeerScope = N'TEAM'
        BEGIN
            INSERT INTO #Cohort (UserId, DisplayName, RoleName)
            SELECT u.Id, u.UserName, r.Name
            FROM dbo.AspNetUsers AS u
            OUTER APPLY (
                SELECT TOP (1) r2.Name
                FROM dbo.AspNetUserRoles AS ur2
                INNER JOIN dbo.AspNetRoles AS r2 ON r2.Id = ur2.RoleId
                WHERE ur2.UserId = u.Id ORDER BY r2.Name
            ) AS r
            WHERE @CallerTeamId IS NOT NULL AND u.FieldTeamId = @CallerTeamId;
        END
        ELSE IF @PeerScope IN (N'DEPARTMENT', N'BRANCH')
        BEGIN
            -- Peers are whoever holds an overlapping ORG_SCOPES grant — the same
            -- department, or the same territory code, depending on the mode.
            INSERT INTO #Cohort (UserId, DisplayName, RoleName)
            SELECT DISTINCT u.Id, u.UserName, r.Name
            FROM dbo.ORG_SCOPES AS mine
            INNER JOIN dbo.ORG_SCOPES AS theirs
                ON theirs.OwnerType = N'User'
               AND theirs.IsActive = 1
               AND (
                     (@PeerScope = N'DEPARTMENT' AND theirs.DepartmentId = mine.DepartmentId)
                  OR (@PeerScope = N'BRANCH'       AND theirs.Level = mine.Level AND theirs.Code = mine.Code)
                   )
            INNER JOIN dbo.AspNetUsers AS u ON u.Id = theirs.OwnerId
            OUTER APPLY (
                SELECT TOP (1) r2.Name
                FROM dbo.AspNetUserRoles AS ur2
                INNER JOIN dbo.AspNetRoles AS r2 ON r2.Id = ur2.RoleId
                WHERE ur2.UserId = u.Id ORDER BY r2.Name
            ) AS r
            WHERE mine.OwnerType = N'User' AND mine.OwnerId = @UserId AND mine.IsActive = 1;
        END
        ELSE -- ROLE
        BEGIN
            INSERT INTO #Cohort (UserId, DisplayName, RoleName)
            SELECT DISTINCT u.Id, u.UserName, r.Name
            FROM dbo.AspNetUserRoles AS ur
            INNER JOIN dbo.AspNetRoles AS r ON r.Id = ur.RoleId
            INNER JOIN dbo.AspNetUsers AS u ON u.Id = ur.UserId
            WHERE r.Id IN (
                SELECT ur2.RoleId
                FROM dbo.AspNetUserRoles AS ur2
                INNER JOIN dbo.AspNetRoles AS r2 ON r2.Id = ur2.RoleId
                WHERE ur2.UserId = @UserId
                  AND (@RoleName IS NULL OR r2.Name = @RoleName)
            );
        END;

        -- The caller is always in their own cohort, even when the scope query missed
        -- them (no org scope rows, no team). Otherwise set 7 has no "me" row to mark.
        IF NOT EXISTS (SELECT 1 FROM #Cohort WHERE UserId = @UserId)
        BEGIN
            INSERT INTO #Cohort (UserId, DisplayName, RoleName)
            SELECT TOP (1) u.Id, u.UserName, r.Name
            FROM dbo.AspNetUsers AS u
            OUTER APPLY (
                SELECT TOP (1) r2.Name
                FROM dbo.AspNetUserRoles AS ur2
                INNER JOIN dbo.AspNetRoles AS r2 ON r2.Id = ur2.RoleId
                WHERE ur2.UserId = u.Id ORDER BY r2.Name
            ) AS r
            WHERE u.Id = @UserId;
        END;
    END;

    -- =====================================================================
    -- RESULT SET 6 — The caller's own statistics
    -- Always exactly one row, zeroed when the caller did nothing in the window, so
    -- the UI never has to handle an absent row.
    -- =====================================================================
    SELECT
        @UserId                                                          AS UserId,
        (SELECT TOP (1) UserName FROM dbo.AspNetUsers WHERE Id = @UserId) AS DisplayName,
        (SELECT TOP (1) RoleName FROM #Cohort WHERE UserId = @UserId)     AS RoleName,
        ISNULL(a.TransactionCount, 0)                                    AS TransactionCount,
        ISNULL(a.SurveysTouched, 0)                                      AS SurveysTouched,
        ISNULL(a.AssignedCount, 0)                                       AS AssignedCount,
        ISNULL(a.SubmittedCount, 0)                                      AS SubmittedCount,
        ISNULL(a.ApprovedCount, 0)                                       AS ApprovedCount,
        ISNULL(a.ReturnedCount, 0)                                       AS ReturnedCount,
        ISNULL(h.AvgHandlingHours, 0)                                    AS AvgHandlingHours,
        a.LastActivityDate                                               AS LastActivityDate,
        (SELECT COUNT(*) FROM #Cohort)                                   AS CohortSize,
        @PeerScope                                                       AS PeerScope
    FROM (SELECT 1 AS One) AS anchor
    LEFT JOIN #UserActivity AS a ON a.UserId = @UserId
    LEFT JOIN #UserHandling AS h ON h.UserId = @UserId;

    -- =====================================================================
    -- RESULT SET 7 — Peer comparison
    --
    -- Ranked over the whole cohort, then trimmed to the top @TopN with the caller's
    -- own row unioned back in. Trimming before ranking would misreport the rank; not
    -- re-adding the caller would leave a low-ranked user with no row for themselves.
    -- =====================================================================
    ;WITH Cohort AS (
        SELECT
            c.UserId, c.DisplayName, c.RoleName,
            ISNULL(a.TransactionCount, 0) AS TransactionCount,
            ISNULL(a.SurveysTouched, 0)   AS SurveysTouched,
            ISNULL(a.ApprovedCount, 0)    AS ApprovedCount,
            ISNULL(a.ReturnedCount, 0)    AS ReturnedCount,
            ISNULL(h.AvgHandlingHours, 0) AS AvgHandlingHours,
            a.LastActivityDate
        FROM #Cohort AS c
        LEFT JOIN #UserActivity AS a ON a.UserId = c.UserId
        LEFT JOIN #UserHandling AS h ON h.UserId = c.UserId
    ),
    Ranked AS (
        SELECT
            *,
            RANK()         OVER (ORDER BY TransactionCount DESC)                       AS [Rank],
            PERCENT_RANK() OVER (ORDER BY TransactionCount)                            AS PercentileRank,
            AVG(CONVERT(DECIMAL(18, 2), TransactionCount)) OVER ()                     AS CohortAvg,
            PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY TransactionCount) OVER ()       AS CohortMedian,
            MAX(TransactionCount) OVER ()                                              AS CohortMax,
            COUNT(*) OVER ()                                                           AS CohortSize
        FROM Cohort
    )
    SELECT
        UserId, DisplayName, RoleName,
        CASE WHEN UserId = @UserId THEN CONVERT(BIT, 1) ELSE CONVERT(BIT, 0) END AS IsCurrentUser,
        TransactionCount, SurveysTouched, ApprovedCount, ReturnedCount,
        AvgHandlingHours, LastActivityDate,
        [Rank]                                             AS [Rank],
        CONVERT(DECIMAL(9, 2), PercentileRank * 100.0)    AS PercentileRank,
        CONVERT(DECIMAL(18, 2), CohortAvg)                AS CohortAvg,
        CONVERT(DECIMAL(18, 2), CohortMedian)             AS CohortMedian,
        CohortMax                                         AS CohortMax,
        CohortSize                                        AS CohortSize
    FROM Ranked
    WHERE [Rank] <= @TopN OR UserId = @UserId
    ORDER BY [Rank], DisplayName;

    -- =====================================================================
    -- RESULT SET 8 — Transaction feed ("who did what")
    -- Replaces the mocked activity list and backs the peer drill-down. Capped at
    -- @TopN * 5 rows so a busy estate cannot blow up the payload.
    -- =====================================================================
    SELECT TOP (@TopN * 5)
        h.Id                    AS HistoryId,
        h.SurveyId              AS SurveyId,
        s.SurveyCode            AS SurveyCode,
        h.ChangedBy             AS UserId,
        u.UserName              AS DisplayName,
        r.Name                  AS RoleName,
        h.FromStatus            AS FromStatus,
        h.ToStatus              AS ToStatus,
        h.ChangedDate           AS ChangedDate,
        h.Note                  AS Note,
        s.CbuCode               AS CbuCode,
        s.BranchCode              AS BranchCode,
        s.OperationAreaCode     AS OperationAreaCode,
        tm.Name                 AS FieldTeamName,
        CASE WHEN h.ChangedBy = @UserId THEN CONVERT(BIT, 1) ELSE CONVERT(BIT, 0) END AS IsCurrentUser
    FROM dbo.SURVEY_STATUS_HISTORY AS h
    INNER JOIN #Scoped        AS s  ON s.Id = h.SurveyId
    LEFT JOIN dbo.AspNetUsers AS u  ON u.Id = h.ChangedBy
    LEFT JOIN dbo.TEAMS       AS tm ON tm.Id = s.LastTeamId
    OUTER APPLY (
        SELECT TOP (1) r2.Name
        FROM dbo.AspNetUserRoles AS ur
        INNER JOIN dbo.AspNetRoles AS r2 ON r2.Id = ur.RoleId
        WHERE ur.UserId = h.ChangedBy ORDER BY r2.Name
    ) AS r
    WHERE h.ChangedDate >= @FromDate AND h.ChangedDate <= @ToDate
    ORDER BY h.ChangedDate DESC, h.Id DESC;

    -- =====================================================================
    -- RESULT SET 9 — Recent surveys
    -- =====================================================================
    SELECT TOP (@TopN)
        s.Id                    AS Id,
        s.SurveyCode            AS SurveyCode,
        s.TemplateId            AS TemplateId,
        t.TemplateNameEn        AS TemplateNameEn,
        t.TemplateNameAr        AS TemplateNameAr,
        s.BranchCode              AS BranchCode,
        z.NameEn                AS BranchNameEn,
        z.NameAr                AS BranchNameAr,
        s.CbuCode               AS CbuCode,
        s.LastTeamId            AS FieldTeamId,
        tm.Name                 AS FieldTeamName,
        s.[Source]              AS [Source],
        s.[Status]              AS [Status],
        s.Created               AS Created,
        s.DueDate               AS DueDate,
        CASE WHEN (s.DueDate IS NOT NULL AND s.DueDate < @Now
                   AND s.[Status] IN (N'CREATED', N'ASSIGNED', N'IN_PROGRESS', N'RETURNED'))
                  OR (s.CompletionDueDate IS NOT NULL AND s.CompletionDueDate < @Now
                      AND s.[Status] = N'SUBMITTED')
             THEN CONVERT(BIT, 1) ELSE CONVERT(BIT, 0) END AS IsOverdue
    FROM #Scoped AS s
    LEFT JOIN dbo.TEMPLATES AS t  ON t.Id = s.TemplateId
    LEFT JOIN dbo.LKP_BRANCH  AS z  ON z.Code = s.BranchCode
    LEFT JOIN dbo.TEAMS     AS tm ON tm.Id = s.LastTeamId
    ORDER BY s.Created DESC, s.Id DESC;

    -- =====================================================================
    -- RESULT SET 10 — Breakdowns
    -- One tall set covering four dimensions rather than four result sets: the UI
    -- renders them with the same table, and one set keeps the reader loop short.
    -- =====================================================================
    ;WITH Breakdown AS (
        SELECT N'FaType' AS Dimension, s.FaTypeCode AS Code,
               MAX(ft.NameEn) AS NameEn, MAX(ft.NameAr) AS NameAr, COUNT(*) AS Cnt
        FROM #Scoped AS s
        LEFT JOIN dbo.LKP_FA_TYPE AS ft ON ft.FaTypeCode = s.FaTypeCode
        WHERE s.FaTypeCode IS NOT NULL
        GROUP BY s.FaTypeCode

        UNION ALL

        SELECT N'ReturnReason', s.ReturnReasonCode,
               MAX(rr.NameEn), MAX(rr.NameAr), COUNT(*)
        FROM #Scoped AS s
        LEFT JOIN dbo.LKP_RETURN_REASON AS rr ON rr.Code = s.ReturnReasonCode
        WHERE s.ReturnReasonCode IS NOT NULL
        GROUP BY s.ReturnReasonCode

        UNION ALL

        SELECT N'Template', CONVERT(NVARCHAR(50), s.TemplateId),
               MAX(t.TemplateNameEn), MAX(t.TemplateNameAr), COUNT(*)
        FROM #Scoped AS s
        LEFT JOIN dbo.TEMPLATES AS t ON t.Id = s.TemplateId
        GROUP BY s.TemplateId

        UNION ALL

        SELECT N'Source', s.[Source], s.[Source], s.[Source], COUNT(*)
        FROM #Scoped AS s
        GROUP BY s.[Source]
    )
    SELECT
        Dimension                                                                     AS Dimension,
        Code                                                                          AS Code,
        NameEn                                                                        AS NameEn,
        NameAr                                                                        AS NameAr,
        Cnt                                                                           AS [Count],
        CASE WHEN @Total > 0 THEN CONVERT(DECIMAL(9, 2), Cnt * 100.0 / @Total) ELSE 0 END AS [Percent]
    FROM Breakdown
    ORDER BY Dimension, Cnt DESC, Code;

    -- =====================================================================
    -- RESULT SET 11 — Late surveys
    --
    -- "Late" covers both halves of the distinction the KPI cards keep apart:
    --   OVERDUE        — still open past fill due, or SUBMITTED past completion due.
    --   COMPLETED_LATE — finished, but missed fill and/or completion deadline.
    -- DeadlineKind names which clock was breached (FILL / COMPLETION). Surveys with
    -- neither deadline can never be late and are excluded.
    -- =====================================================================
    SELECT TOP (@TopN * 2)
        late.Id                     AS Id,
        late.SurveyCode             AS SurveyCode,
        late.[Status]               AS [Status],
        late.LatenessKind           AS LatenessKind,
        late.DeadlineKind           AS DeadlineKind,
        late.DueDate                AS DueDate,
        late.CompletedDate          AS CompletedDate,
        late.DaysLate               AS DaysLate,
        late.FieldTeamId            AS FieldTeamId,
        tm.Name                     AS FieldTeamName,
        late.CbuCode                AS CbuCode,
        late.BranchCode               AS BranchCode,
        z.NameEn                    AS BranchNameEn,
        z.NameAr                    AS BranchNameAr,
        t.TemplateNameEn            AS TemplateNameEn,
        t.TemplateNameAr            AS TemplateNameAr,
        late.Created                AS Created
    FROM (
        -- Fill overdue (open work)
        SELECT
            s.Id, s.SurveyCode, s.[Status],
            N'OVERDUE' AS LatenessKind,
            N'FILL' AS DeadlineKind,
            s.DueDate AS DueDate,
            s.CompletedDate,
            DATEDIFF(DAY, s.DueDate, @Now) AS DaysLate,
            s.LastTeamId AS FieldTeamId,
            s.CbuCode, s.BranchCode, s.TemplateId, s.Created
        FROM #Scoped AS s
        WHERE s.DueDate IS NOT NULL
          AND s.DueDate < @Now
          AND s.[Status] IN (N'CREATED', N'ASSIGNED', N'IN_PROGRESS', N'RETURNED')

        UNION ALL

        -- Completion overdue (awaiting review)
        SELECT
            s.Id, s.SurveyCode, s.[Status],
            N'OVERDUE' AS LatenessKind,
            N'COMPLETION' AS DeadlineKind,
            s.CompletionDueDate AS DueDate,
            s.CompletedDate,
            DATEDIFF(DAY, s.CompletionDueDate, @Now) AS DaysLate,
            s.LastTeamId AS FieldTeamId,
            s.CbuCode, s.BranchCode, s.TemplateId, s.Created
        FROM #Scoped AS s
        WHERE s.CompletionDueDate IS NOT NULL
          AND s.CompletionDueDate < @Now
          AND s.[Status] = N'SUBMITTED'

        UNION ALL

        -- Completed late on fill clock
        SELECT
            s.Id, s.SurveyCode, s.[Status],
            N'COMPLETED_LATE' AS LatenessKind,
            N'FILL' AS DeadlineKind,
            s.DueDate AS DueDate,
            s.CompletedDate,
            DATEDIFF(DAY, s.DueDate, ISNULL(s.SubmittedDate, s.CompletedDate)) AS DaysLate,
            s.LastTeamId AS FieldTeamId,
            s.CbuCode, s.BranchCode, s.TemplateId, s.Created
        FROM #Scoped AS s
        WHERE s.CompletedDate IS NOT NULL
          AND s.DueDate IS NOT NULL
          AND s.SubmittedDate IS NOT NULL
          AND s.SubmittedDate > s.DueDate

        UNION ALL

        -- Completed late on completion clock
        SELECT
            s.Id, s.SurveyCode, s.[Status],
            N'COMPLETED_LATE' AS LatenessKind,
            N'COMPLETION' AS DeadlineKind,
            s.CompletionDueDate AS DueDate,
            s.CompletedDate,
            DATEDIFF(DAY, s.CompletionDueDate, s.CompletedDate) AS DaysLate,
            s.LastTeamId AS FieldTeamId,
            s.CbuCode, s.BranchCode, s.TemplateId, s.Created
        FROM #Scoped AS s
        WHERE s.CompletedDate IS NOT NULL
          AND s.CompletionDueDate IS NOT NULL
          AND s.CompletedDate > s.CompletionDueDate
    ) AS late
    LEFT JOIN dbo.TEAMS     AS tm ON tm.Id = late.FieldTeamId
    LEFT JOIN dbo.LKP_BRANCH  AS z  ON z.Code = late.BranchCode
    LEFT JOIN dbo.TEMPLATES AS t  ON t.Id = late.TemplateId
    ORDER BY
        CASE WHEN late.LatenessKind = N'OVERDUE' THEN 0 ELSE 1 END,
        late.DaysLate DESC,
        late.DueDate;

    -- =====================================================================
    -- RESULT SET 12 — Late teams
    --
    -- The same lateness, rolled up to the team currently holding the work, so the
    -- question "who is behind" has an answer that is not a survey list. Only teams
    -- with at least one late survey are returned — a team with a clean record is
    -- not news on a lateness table.
    --
    -- Attribution is to the CURRENT allocation (SURVEY_ASSIGNMENTS.IsActive = 1).
    -- A survey reassigned after falling behind counts against whoever holds it now,
    -- not the team that let it slip.
    -- =====================================================================
    SELECT TOP (@TopN)
        s.LastTeamId                                                            AS FieldTeamId,
        tm.Name                                                                 AS FieldTeamName,
        tm.UserCode                                                             AS UserCode,
        COUNT(*)                                                                AS SurveyCount,
        SUM(CASE
                WHEN s.DueDate IS NOT NULL AND s.DueDate < @Now
                 AND s.[Status] IN (N'CREATED', N'ASSIGNED', N'IN_PROGRESS', N'RETURNED')
                THEN 1
                WHEN s.CompletionDueDate IS NOT NULL AND s.CompletionDueDate < @Now
                 AND s.[Status] = N'SUBMITTED'
                THEN 1
                ELSE 0 END)                                                     AS OverdueCount,
        SUM(CASE WHEN s.CompletedDate IS NOT NULL
                  AND (
                        (s.DueDate IS NOT NULL AND s.SubmittedDate IS NOT NULL AND s.SubmittedDate > s.DueDate)
                     OR (s.CompletionDueDate IS NOT NULL AND s.CompletedDate > s.CompletionDueDate)
                      )
                 THEN 1 ELSE 0 END)                                             AS CompletedLateCount,
        SUM(CASE WHEN s.CompletedDate IS NOT NULL
                  AND (s.DueDate IS NULL OR s.SubmittedDate IS NULL OR s.SubmittedDate <= s.DueDate)
                  AND (s.CompletionDueDate IS NULL OR s.CompletedDate <= s.CompletionDueDate)
                 THEN 1 ELSE 0 END)                                             AS OnTimeCount,
        ISNULL(AVG(CASE
                    WHEN s.CompletedDate IS NOT NULL
                     AND s.CompletionDueDate IS NOT NULL
                     AND s.CompletedDate > s.CompletionDueDate
                        THEN CONVERT(DECIMAL(18, 2), DATEDIFF(DAY, s.CompletionDueDate, s.CompletedDate))
                    WHEN s.CompletedDate IS NOT NULL
                     AND s.DueDate IS NOT NULL
                     AND s.SubmittedDate IS NOT NULL
                     AND s.SubmittedDate > s.DueDate
                        THEN CONVERT(DECIMAL(18, 2), DATEDIFF(DAY, s.DueDate, s.SubmittedDate))
                    WHEN s.CompletionDueDate IS NOT NULL AND s.[Status] = N'SUBMITTED'
                     AND s.CompletionDueDate < @Now
                        THEN CONVERT(DECIMAL(18, 2), DATEDIFF(DAY, s.CompletionDueDate, @Now))
                    WHEN s.DueDate IS NOT NULL AND s.DueDate < @Now
                     AND s.[Status] IN (N'CREATED', N'ASSIGNED', N'IN_PROGRESS', N'RETURNED')
                        THEN CONVERT(DECIMAL(18, 2), DATEDIFF(DAY, s.DueDate, @Now))
                  END), 0)                                                      AS AvgDaysLate,
        ISNULL(MAX(CASE
                    WHEN s.CompletionDueDate IS NOT NULL AND s.[Status] = N'SUBMITTED'
                     AND s.CompletionDueDate < @Now
                        THEN DATEDIFF(DAY, s.CompletionDueDate, @Now)
                    WHEN s.DueDate IS NOT NULL AND s.DueDate < @Now
                     AND s.[Status] IN (N'CREATED', N'ASSIGNED', N'IN_PROGRESS', N'RETURNED')
                        THEN DATEDIFF(DAY, s.DueDate, @Now)
                  END), 0)                                                      AS MaxDaysOverdue,
        CONVERT(DECIMAL(9, 2),
            CASE WHEN SUM(CASE WHEN s.CompletedDate IS NOT NULL THEN 1 ELSE 0 END) > 0
                 THEN SUM(CASE WHEN s.CompletedDate IS NOT NULL
                                AND (s.DueDate IS NULL OR s.SubmittedDate IS NULL OR s.SubmittedDate <= s.DueDate)
                                AND (s.CompletionDueDate IS NULL OR s.CompletedDate <= s.CompletionDueDate)
                               THEN 1 ELSE 0 END) * 100.0
                      / SUM(CASE WHEN s.CompletedDate IS NOT NULL THEN 1 ELSE 0 END)
                 ELSE 0 END)                                                    AS OnTimeRatePercent
    FROM #Scoped AS s
    LEFT JOIN dbo.TEAMS AS tm ON tm.Id = s.LastTeamId
    WHERE s.LastTeamId IS NOT NULL
    GROUP BY s.LastTeamId, tm.Name, tm.UserCode
    HAVING SUM(CASE
                    WHEN s.DueDate IS NOT NULL AND s.DueDate < @Now
                     AND s.[Status] IN (N'CREATED', N'ASSIGNED', N'IN_PROGRESS', N'RETURNED')
                    THEN 1
                    WHEN s.CompletionDueDate IS NOT NULL AND s.CompletionDueDate < @Now
                     AND s.[Status] = N'SUBMITTED'
                    THEN 1
                    ELSE 0 END)
             + SUM(CASE WHEN s.CompletedDate IS NOT NULL
                         AND (
                               (s.DueDate IS NOT NULL AND s.SubmittedDate IS NOT NULL AND s.SubmittedDate > s.DueDate)
                            OR (s.CompletionDueDate IS NOT NULL AND s.CompletedDate > s.CompletionDueDate)
                             )
                        THEN 1 ELSE 0 END) > 0
    ORDER BY OverdueCount DESC, CompletedLateCount DESC, MaxDaysOverdue DESC;

    DROP TABLE #Scoped, #Prev, #ScopeGroup, #ScopeCode, #FilterCbu,
               #TeamUnit, #TeamMembers, #UserActivity, #UserHandling, #Cohort;
END;
