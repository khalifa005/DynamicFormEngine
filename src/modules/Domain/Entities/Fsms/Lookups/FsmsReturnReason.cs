namespace KH.Domain.Entities.Fsms.Lookups;

/// <summary>
/// Why a reviewer sent a survey back for rework. Seeded from
/// <see cref="Constants.Fsms.SurveyReturnReasons"/> and extensible afterwards, so support operations
/// can add a cause the shipped list does not cover. Table: <c>LKP_RETURN_REASON</c>.
/// </summary>
public sealed class FsmsReturnReason : BaseEntity<long>
{
    public string Code { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public string NameAr { get; set; } = default!;

    /// <summary>Orders the picker so the common causes sit at the top. Lower shows first.</summary>
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
