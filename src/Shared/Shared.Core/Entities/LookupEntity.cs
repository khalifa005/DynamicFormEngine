using System.ComponentModel.DataAnnotations;

namespace Shared.Core.Entities;

public abstract class LookupEntity : BaseAuditableEntity<int>
{
    public string Code { get; set; } = string.Empty;
    [MaxLength(300)]
    public string NameEn { get; set; } = string.Empty;
    [MaxLength(300)]
    public string NameAr { get; set; } = string.Empty;
    public string? Description { get; set; } = string.Empty;
}
