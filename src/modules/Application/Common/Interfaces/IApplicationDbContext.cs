using KH.Domain.Entities;
using KH.Domain.Entities.Fsms.Catalog;
using KH.Domain.Entities.Fsms.Lookups;
using KH.Domain.Entities.Fsms.Migration;
using KH.Domain.Entities.Fsms.Org;
using KH.Domain.Entities.Fsms.Submissions;
using KH.Domain.Entities.Fsms.Surveys;
using KH.Domain.Entities.Fsms.Teams;
using KH.Domain.Entities.Fsms.Templates;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace KH.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Shared.Core.Entities.AuditEntry> AuditLogs { get; }

    DbSet<UserRefreshToken> UserRefreshTokens { get; }

    DbSet<SsoAuthorizationCode> SsoAuthorizationCodes { get; }

    DbSet<Permission> Permissions { get; }

    DbSet<RolePermission> RolePermissions { get; }

    // FSMS — shared lookups (task-00)
    DbSet<FsmsDepartment> FsmsDepartments { get; }
    DbSet<FsmsFaType> FsmsFaTypes { get; }
    DbSet<FsmsTaskType> FsmsTaskTypes { get; }
    DbSet<FsmsCustomerType> FsmsCustomerTypes { get; }
    DbSet<FsmsContractor> FsmsContractors { get; }
    DbSet<FsmsReturnReason> FsmsReturnReasons { get; }

    // FSMS — the org geography: Cluster → CBU → (Branch | Operation Area), the last two siblings
    DbSet<FsmsCluster> FsmsClusters { get; }
    DbSet<FsmsCbu> FsmsCbus { get; }
    DbSet<FsmsBranch> FsmsBranches { get; }
    DbSet<FsmsOperationArea> FsmsOperationAreas { get; }

    // FSMS — which parts of the geography a team, user or template covers
    DbSet<OrgScope> OrgScopes { get; }

    // FSMS — local team tables (task-00)
    DbSet<FieldTeam> FieldTeams { get; }
    DbSet<TeamSchedule> TeamSchedules { get; }
    DbSet<FieldTeamDepartment> FieldTeamDepartments { get; }

    // FSMS — template management (form-builder backed)
    DbSet<SurveyTemplate> SurveyTemplates { get; }
    DbSet<TemplateVersion> TemplateVersions { get; }

    // FSMS — global field catalog (Data Name reuse)
    DbSet<FieldCatalogEntry> FieldCatalog { get; }

    // FSMS — media uploaded per field, linked to a submission on submit
    DbSet<SubmissionFile> SubmissionFiles { get; }

    // FSMS — survey instances and their lifecycle (task-03/04)
    DbSet<Survey> Surveys { get; }
    DbSet<SurveyStatusHistory> SurveyStatusHistory { get; }
    DbSet<SurveyAssignment> SurveyAssignments { get; }

    // FSMS — imports of historical data from external systems (Fulcrum, and later others)
    DbSet<DataMigrationRun> DataMigrationRuns { get; }
    DbSet<DataMigrationRecord> DataMigrationRecords { get; }

    /// <summary>
    /// Exposed so a slice that writes both through EF and through native SQL can put the two in one
    /// transaction — a native write commits as it is issued, so without one, a failure part-way
    /// leaves rows behind that the entities they belong to never caught up with. Almost nothing
    /// needs this: a handler whose whole unit of work is tracked entities gets atomicity from
    /// <see cref="SaveChangesAsync"/> alone.
    ///
    /// Connection retries are off, so a transaction can be opened here directly. Were they turned
    /// back on, every such transaction would have to be driven through
    /// <c>Database.CreateExecutionStrategy()</c> instead — the retrying strategy refuses a
    /// transaction it did not open, since it cannot replay half of one.
    /// </summary>
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
