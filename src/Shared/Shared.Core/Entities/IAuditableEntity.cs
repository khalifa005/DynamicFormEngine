namespace Shared.Core.Entities;

public interface IAuditableEntity
{
    DateTimeOffset Created { get; set; }

    string? CreatedBy { get; set; }

    DateTimeOffset LastModified { get; set; }

    string? LastModifiedBy { get; set; }

    bool IsActive { get; set; }
}
