using System.Reflection;
using KH.Application.Common.Interfaces;
using KH.Domain.Entities;
using KH.Domain.Entities.Fsms.Catalog;
using KH.Domain.Entities.Fsms.Lookups;
using KH.Domain.Entities.Fsms.Migration;
using KH.Domain.Entities.Fsms.Org;
using KH.Domain.Entities.Fsms.Submissions;
using KH.Domain.Entities.Fsms.Surveys;
using KH.Domain.Entities.Fsms.Teams;
using KH.Domain.Entities.Fsms.Templates;
using KH.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KH.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Shared.Core.Entities.AuditEntry> AuditLogs => Set<Shared.Core.Entities.AuditEntry>();

    public DbSet<UserRefreshToken> UserRefreshTokens => Set<UserRefreshToken>();

    public DbSet<SsoAuthorizationCode> SsoAuthorizationCodes => Set<SsoAuthorizationCode>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<FsmsDepartment> FsmsDepartments => Set<FsmsDepartment>();

    public DbSet<FsmsFaType> FsmsFaTypes => Set<FsmsFaType>();

    public DbSet<FsmsTaskType> FsmsTaskTypes => Set<FsmsTaskType>();

    public DbSet<FsmsCustomerType> FsmsCustomerTypes => Set<FsmsCustomerType>();

    public DbSet<FsmsContractor> FsmsContractors => Set<FsmsContractor>();

    public DbSet<FsmsReturnReason> FsmsReturnReasons => Set<FsmsReturnReason>();

    public DbSet<FsmsCluster> FsmsClusters => Set<FsmsCluster>();

    public DbSet<FsmsCbu> FsmsCbus => Set<FsmsCbu>();

    public DbSet<FsmsBranch> FsmsBranches => Set<FsmsBranch>();

    public DbSet<FsmsOperationArea> FsmsOperationAreas => Set<FsmsOperationArea>();

    public DbSet<OrgScope> OrgScopes => Set<OrgScope>();

    public DbSet<FieldTeam> FieldTeams => Set<FieldTeam>();

    public DbSet<TeamSchedule> TeamSchedules => Set<TeamSchedule>();

    public DbSet<FieldTeamDepartment> FieldTeamDepartments => Set<FieldTeamDepartment>();

    public DbSet<SurveyTemplate> SurveyTemplates => Set<SurveyTemplate>();

    public DbSet<TemplateVersion> TemplateVersions => Set<TemplateVersion>();

    public DbSet<FieldCatalogEntry> FieldCatalog => Set<FieldCatalogEntry>();

    public DbSet<SubmissionFile> SubmissionFiles => Set<SubmissionFile>();

    public DbSet<Survey> Surveys => Set<Survey>();

    public DbSet<SurveyStatusHistory> SurveyStatusHistory => Set<SurveyStatusHistory>();

    public DbSet<SurveyAssignment> SurveyAssignments => Set<SurveyAssignment>();

    public DbSet<DataMigrationRun> DataMigrationRuns => Set<DataMigrationRun>();

    public DbSet<DataMigrationRecord> DataMigrationRecords => Set<DataMigrationRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
