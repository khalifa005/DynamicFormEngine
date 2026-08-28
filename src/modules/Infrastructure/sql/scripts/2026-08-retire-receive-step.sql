/*
    Retires the receive step: SUBMITTED is now the awaiting-review state, and a reviewer completes
    or returns straight from it.

    Run once per environment, after deploying the build that removed POST /surveys/{id}/receive.

    This is a one-off data fix, not a migration — the schema does not change, only the rows that
    were sitting in a state nothing can put them into any more. The code accepts UNDER_REVIEW as
    well as SUBMITTED, so skipping this script strands nothing; it only leaves those surveys
    reporting under a status the dashboard no longer counts.

    SURVEY_STATUS_HISTORY is deliberately left alone. It is the audit trail, and it has to keep
    saying what actually happened — including the receive transitions that did.
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

UPDATE dbo.SURVEYS
SET [Status] = N'SUBMITTED',
    LastModified = SYSDATETIMEOFFSET()
WHERE [Status] = N'UNDER_REVIEW';

PRINT CONCAT(N'Surveys moved UNDER_REVIEW -> SUBMITTED: ', @@ROWCOUNT);

UPDATE dbo.SURVEY_ASSIGNMENTS
SET [Status] = N'SUBMITTED',
    LastModified = SYSDATETIMEOFFSET()
WHERE [Status] = N'REVIEWED';

PRINT CONCAT(N'Assignments moved REVIEWED -> SUBMITTED: ', @@ROWCOUNT);

COMMIT TRANSACTION;
