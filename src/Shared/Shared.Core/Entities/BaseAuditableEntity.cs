namespace Shared.Core.Entities;

public abstract class BaseAuditableEntity<T> : BaseEntity<T>, IAuditableEntity
{
    public DateTimeOffset Created { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset LastModified { get; set; }

    public string? LastModifiedBy { get; set; }

    //added by kh 
    public bool IsActive { get; set; }
}

public abstract class BaseAuditableEntity : BaseAuditableEntity<int>
{
}
