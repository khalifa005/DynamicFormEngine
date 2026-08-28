using KH.Domain.Constants.Fsms;

namespace KH.Domain.Entities.Fsms.Submissions;

/// <summary>
/// One media file uploaded against a survey field. Created the moment the user picks the file —
/// before the form is submitted — so the browser never carries bytes into the submit payload; the
/// submission row only stores a reference array of <see cref="FileId"/> + <see cref="RelativePath"/>.
/// Table: <c>SUBMISSION_FILES</c>.
/// </summary>
public sealed class SubmissionFile : BaseAuditableEntity<long>
{
    private SubmissionFile()
    {
    }

    private SubmissionFile(
        Guid fileId,
        long templateId,
        string dataName,
        string fileName,
        string contentType,
        long sizeBytes,
        string relativePath,
        long? surveyId,
        string storageKind)
    {
        FileId = fileId;
        TemplateId = templateId;
        DataName = dataName;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        RelativePath = relativePath;
        SurveyId = surveyId;
        StorageKind = storageKind;
        Status = SubmissionFileStatuses.Pending;
        IsActive = true;
    }

    /// <summary>Public handle returned to the client; the row id is never exposed.</summary>
    public Guid FileId { get; private set; }

    /// <summary>Null while the file is <c>PENDING</c>; set when the form is submitted.</summary>
    public long? SubmissionId { get; private set; }

    public long? SurveyId { get; private set; }

    public long TemplateId { get; private set; }

    /// <summary>The field (<c>data_name</c>) the file was picked for.</summary>
    public string DataName { get; private set; } = default!;

    public string FileName { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long SizeBytes { get; private set; }

    /// <summary>Storage-root-relative path — resolved through <c>IFileStorage</c>, never used directly.</summary>
    public string RelativePath { get; private set; } = default!;

    /// <summary>See <see cref="SubmissionFileStatuses"/>.</summary>
    public string Status { get; private set; } = default!;

    /// <summary>
    /// Whether this application owns the bytes — see <see cref="SubmissionFileStorageKinds"/>.
    /// Everything written through an upload is <c>MANAGED</c>; only a referenced archive is
    /// <c>STAGED</c>.
    /// </summary>
    public string StorageKind { get; private set; } = default!;

    public static SubmissionFile CreatePending(
        Guid fileId,
        long templateId,
        string dataName,
        string fileName,
        string contentType,
        long sizeBytes,
        string relativePath,
        long? surveyId = null) =>
        Create(
            fileId,
            templateId,
            dataName,
            fileName,
            contentType,
            sizeBytes,
            relativePath,
            surveyId,
            SubmissionFileStorageKinds.Managed);

    /// <summary>
    /// References a file already sitting on the server rather than one this application wrote — the
    /// historical media archive placed under the storage root by whoever moved it across.
    ///
    /// Identical to <see cref="CreatePending"/> in every respect a reader cares about; the only
    /// difference is that the row disclaims ownership of the bytes, so nothing later moves or
    /// deletes them. That matters at archive scale: the files may be shared between records, they
    /// may be a terabyte, and they are somebody else's master copy.
    /// </summary>
    public static SubmissionFile CreateMigrated(
        Guid fileId,
        long templateId,
        string dataName,
        string fileName,
        string contentType,
        long sizeBytes,
        string relativePath,
        long? surveyId = null) =>
        Create(
            fileId,
            templateId,
            dataName,
            fileName,
            contentType,
            sizeBytes,
            relativePath,
            surveyId,
            SubmissionFileStorageKinds.Migrated);

    private static SubmissionFile Create(
        Guid fileId,
        long templateId,
        string dataName,
        string fileName,
        string contentType,
        long sizeBytes,
        string relativePath,
        long? surveyId,
        string storageKind)
    {
        if (fileId == Guid.Empty)
        {
            throw new DomainException("An uploaded file must have a file id.");
        }

        if (templateId <= 0)
        {
            throw new DomainException("An uploaded file must belong to a template.");
        }

        if (string.IsNullOrWhiteSpace(dataName))
        {
            throw new DomainException("An uploaded file must name the field it belongs to.");
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new DomainException("An uploaded file must have a file name.");
        }

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new DomainException("An uploaded file must have a storage path.");
        }

        if (sizeBytes <= 0)
        {
            throw new DomainException("An uploaded file must not be empty.");
        }

        if (!SubmissionFileStorageKinds.IsDefined(storageKind))
        {
            throw new DomainException($"Unknown file storage kind '{storageKind}'.");
        }

        return new SubmissionFile(
            fileId,
            templateId,
            dataName.Trim(),
            fileName.Trim(),
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim(),
            sizeBytes,
            relativePath.Trim(),
            surveyId,
            storageKind);
    }

    /// <summary>
    /// Ties a still-pending file to the submission that referenced it, and backfills the survey it
    /// now belongs to. A file uploaded while filling allocated work already carries its
    /// <see cref="SurveyId"/> — the client knew it at upload time — but a file uploaded while the crew
    /// was still raising the survey could not: the survey did not exist yet. Passing it here is what
    /// stamps those field-raised files with their survey rather than leaving <see cref="SurveyId"/>
    /// null. Only ever backfills; an id already on the row is left as it is.
    /// </summary>
    public void LinkTo(long submissionId, string newRelativePath, long? surveyId = null)
    {
        if (submissionId <= 0)
        {
            throw new DomainException("A submission id is required to link an uploaded file.");
        }

        if (Status != SubmissionFileStatuses.Pending)
        {
            throw new DomainException($"File '{FileId}' is already linked to submission {SubmissionId}.");
        }

        SubmissionId = submissionId;
        Status = SubmissionFileStatuses.Linked;

        if (SurveyId is null && surveyId is long resolvedSurveyId)
        {
            SurveyId = resolvedSurveyId;
        }

        if (!string.IsNullOrWhiteSpace(newRelativePath))
        {
            RelativePath = newRelativePath;
        }
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public bool IsPending => Status == SubmissionFileStatuses.Pending;

    /// <summary>
    /// True when the bytes belong to the migration archive rather than to this application. Callers
    /// that would move or delete the file must check this first — reading never needs to.
    /// </summary>
    public bool IsMigrated => StorageKind == SubmissionFileStorageKinds.Migrated;
}
