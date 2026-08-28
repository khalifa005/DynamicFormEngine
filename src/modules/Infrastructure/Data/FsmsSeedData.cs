using System.Reflection;
using System.Text.Json;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities;
using KH.Domain.Entities.Fsms.Lookups;
using KH.Domain.Entities.Fsms.Org;
using KH.Domain.Entities.Fsms.Teams;
using KH.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KH.Infrastructure.Data;

/// <summary>
/// Seeds FSMS permissions plus WFM-reference lookup/team data (from
/// Docs/business-logic/refernce-data-from-anotehr-sys.text). Upserts by natural key so re-runs
/// add missing rows without duplicating.
/// </summary>
internal static class FsmsSeedData
{
    private const string Module = "Fsms";

    /// <summary>Riyadh branch used for every seeded back-office and field-team scope.</summary>
    private const string RiyadhBranchCode = "1110";

    /// <summary>Development password for seeded logins. Rotate outside Development.</summary>
    private const string DefaultDevPassword = "Administrator1!";

    /// <summary>Local development crews that always stay active and receive Identity logins.</summary>
    private static readonly string[] LocalTestTeamUserCodes = ["FT-demo-admin", "FT-demo-user"];

    private const string ContractorSeedResourceName = "KH.Infrastructure.Data.FsmsContractorSeed.json";
    private const string FieldTeamSeedResourceName = "KH.Infrastructure.Data.FsmsFieldTeamSeed.json";

    private static readonly JsonSerializerOptions SeedJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    internal static readonly (string Code, string NameEn, string NameAr)[] PermissionDefinitions =
    [
        (FsmsPolicies.ManageTemplates, "Manage survey templates", "إدارة نماذج الاستبيان"),
        (FsmsPolicies.ViewTemplates, "View survey templates", "عرض نماذج الاستبيان"),
        (FsmsPolicies.ViewSurveys, "View surveys", "عرض الاستبيانات"),
        (FsmsPolicies.ManageAssignments, "Manage survey assignments", "إدارة إسناد الاستبيانات"),
        (FsmsPolicies.CreateSurveys, "Create surveys", "إنشاء الاستبيانات"),
        (FsmsPolicies.SubmitSurveys, "Submit surveys", "إرسال الاستبيانات"),
        (FsmsPolicies.ReviewSurveys, "Review surveys", "مراجعة الاستبيانات"),
        (FsmsPolicies.ManageTeams, "Manage field teams", "إدارة الفرق الميدانية"),
        (FsmsPolicies.ManageLookups, "Manage reference data", "إدارة البيانات المرجعية"),
        (FsmsPolicies.ManageUsers, "Manage user accounts", "إدارة حسابات المستخدمين"),
        (FsmsPolicies.ViewDashboard, "View dashboard", "عرض لوحة المعلومات"),
        (FsmsPolicies.ViewReports, "View reports", "عرض التقارير"),
        (FsmsPolicies.ImportData, "Import external data", "استيراد البيانات الخارجية")
    ];

    internal static async Task SeedPermissionsAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        foreach (var definition in PermissionDefinitions)
        {
            var exists = await context.Permissions.AnyAsync(x => x.Code == definition.Code, cancellationToken);

            if (exists)
            {
                continue;
            }

            context.Permissions.Add(new Permission(definition.Code, Module, definition.NameEn, definition.NameAr));
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    internal static async Task SeedReferenceDataAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        await UpsertDepartmentsAsync(context, cancellationToken);

        // The geography goes in broadest first: every level below checks that its parent code
        // exists, and the branch cleanup needs the CBUs already saved to know what a CBU code is.
        await UpsertClustersAsync(context, cancellationToken);
        await UpsertCbusAsync(context, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await UpsertBranchesAsync(context, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await UpsertOperationAreasAsync(context, cancellationToken);
        await UpsertTaskTypesAsync(context, cancellationToken);
        await UpsertFaTypesAsync(context, cancellationToken);
        await UpsertCustomerTypesAsync(context, cancellationToken);
        await UpsertReturnReasonsAsync(context, cancellationToken);
        await UpsertContractorsAsync(context, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await UpsertTeamsAsync(context, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        // Both of these read back the team ids the save above assigned.
        await UpsertTeamSchedulesAsync(context, cancellationToken);
        await UpsertTeamDepartmentsAndScopesAsync(context, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gives the two local test crews a login so the mobile flow is testable.
    /// </summary>
    internal static async Task SeedFieldTeamLoginsAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        CancellationToken cancellationToken = default)
    {
        var fieldTeamRole = await roleManager.FindByNameAsync(FsmsRoles.FieldTeam);

        if (fieldTeamRole is null)
        {
            return;
        }

        var teams = await context.FieldTeams
            .AsNoTracking()
            .Where(x => x.IsActive && LocalTestTeamUserCodes.Contains(x.UserCode))
            .Select(x => new { x.Id, x.UserCode })
            .ToListAsync(cancellationToken);

        foreach (var team in teams)
        {
            var existing = await userManager.FindByNameAsync(team.UserCode);

            if (existing is not null)
            {
                // An account seeded before the link existed still needs pointing at its team.
                if (existing.FieldTeamId != team.Id)
                {
                    existing.FieldTeamId = team.Id;
                    await userManager.UpdateAsync(existing);
                }

                continue;
            }

            var user = new ApplicationUser { UserName = team.UserCode, FieldTeamId = team.Id };
            var createResult = await userManager.CreateAsync(user, DefaultDevPassword);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to seed field-team user '{team.UserCode}': {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            }

            await userManager.AddToRoleAsync(user, FsmsRoles.FieldTeam);
        }
    }

    /// <summary>
    /// Seeds a Riyadh-scoped Back Office (<see cref="FsmsRoles.Dispatcher"/>) login for local
    /// development. Re-runs skip create when the username already exists, but still ensure the
    /// branch scope row is present so a partially seeded account can sign in.
    /// </summary>
    internal static async Task SeedBackOfficeUserAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        CancellationToken cancellationToken = default)
    {
        const string userName = "demo-admin";

        var dispatcherRole = await roleManager.FindByNameAsync(FsmsRoles.Dispatcher);

        if (dispatcherRole is null)
        {
            return;
        }

        var user = await userManager.FindByNameAsync(userName);

        if (user is null)
        {
            user = new ApplicationUser { UserName = userName };
            var createResult = await userManager.CreateAsync(user, DefaultDevPassword);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to seed back-office user '{userName}': {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            }

            await userManager.AddToRoleAsync(user, FsmsRoles.Dispatcher);
        }
        else if (!await userManager.IsInRoleAsync(user, FsmsRoles.Dispatcher))
        {
            await userManager.AddToRoleAsync(user, FsmsRoles.Dispatcher);
        }

        var scopeExists = await context.OrgScopes.AnyAsync(
            x => x.OwnerType == OrgScopeOwnerTypes.User
                && x.OwnerId == user.Id
                && x.Level == OrgScopeLevels.Branch
                && x.Code == RiyadhBranchCode
                && x.DepartmentId == null,
            cancellationToken);

        if (scopeExists)
        {
            return;
        }

        context.OrgScopes.Add(
            OrgScope.Create(OrgScopeOwnerTypes.User, user.Id, OrgScopeLevels.Branch, RiyadhBranchCode, departmentId: null));

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task UpsertDepartmentsAsync(ApplicationDbContext context, CancellationToken ct)
    {
        (int Id, string NameEn, string NameAr)[] departments =
        [
            (10, "Water Network", "شبكة المياه"),
            (11, "Waste Water", "شبكة الصرف الصحي"),
            (50, "New Connections", "إدارة التوصيلات المنزلية")
        ];

        foreach (var (id, nameEn, nameAr) in departments)
        {
            if (await context.FsmsDepartments.AnyAsync(x => x.Id == id, ct))
            {
                continue;
            }

            context.FsmsDepartments.Add(new FsmsDepartment { Id = id, NameEn = nameEn, NameAr = nameAr, IsActive = true });
        }
    }

    /// <summary>
    /// Top of the geography — the Saudi regional clusters. Seeded in full rather than only the two
    /// the sample data exercises, because a cluster with no CBUs yet is harmless while a missing one
    /// blocks the whole cascade the day its first CBU arrives.
    /// </summary>
    private static async Task UpsertClustersAsync(ApplicationDbContext context, CancellationToken ct)
    {
        (string Code, string NameEn, string NameAr)[] clusters =
        [
            ("CC", "Central Cluster", "القطاع الأوسط"),
            ("WC", "Western Cluster", "القطاع الغربي"),
            ("EC", "Eastern Cluster", "القطاع الشرقي"),
            ("SC", "Southern Cluster", "القطاع الجنوبي"),
            ("NC", "Northern Cluster", "القطاع الشمالي")
        ];

        foreach (var (code, nameEn, nameAr) in clusters)
        {
            if (await context.FsmsClusters.AnyAsync(x => x.Code == code, ct))
            {
                continue;
            }

            context.FsmsClusters.Add(new FsmsCluster
            {
                Code = code,
                NameEn = nameEn,
                NameAr = nameAr,
                IsActive = true
            });
        }
    }

    /// <summary>
    /// Every CBU the C2M branch feed (Docs/C2M/cbu-branches.text) refers to. Only RCBU and JCBU come
    /// with WFM identifiers in the reference extract, so the rest carry nulls for
    /// <see cref="FsmsCbu.OrgId"/>/<see cref="FsmsCbu.OrgCode"/>/<see cref="FsmsCbu.DefaultTaskZone"/>
    /// rather than a guessed value — those three are matching keys for inbound payloads, and a wrong
    /// one routes work to the wrong CBU where an empty one merely leaves it unrouted.
    /// </summary>
    private static async Task UpsertCbusAsync(ApplicationDbContext context, CancellationToken ct)
    {
        (string Code, string ClusterCode, string NameEn, string NameAr, long? OrgId, string? OrgCode, string? DefaultTaskZone)[] cbus =
        [
            // Central
            ("RCBU", "CC", "Riyadh City", "مدينة الرياض", 124, "NTW", "1110"),
            ("RI", "CC", "Riyadh", "الرياض", null, null, null),
            ("QS", "CC", "Qassim", "القصيم", null, null, null),
            ("HQ", "CC", "Head Office", "المركز الرئيسي", null, null, null),

            // Western
            ("JCBU", "WC", "Jeddah City", "مدينة جدة", 161, "NTW", "2200"),
            ("MCBU", "WC", "Makkah City", "مدينة مكة المكرمة", null, null, null),
            ("TCBU", "WC", "Taif City", "مدينة الطائف", null, null, null),
            ("MK", "WC", "Makkah", "مكة المكرمة", null, null, null),
            ("MD", "WC", "Madinah", "المدينة المنورة", null, null, null),

            // Eastern
            ("SH", "EC", "Eastern Province", "المنطقة الشرقية", null, null, null),

            // Southern
            ("AS", "SC", "Asir", "عسير", null, null, null),
            ("BA", "SC", "Al Baha", "الباحة", null, null, null),
            ("NJ", "SC", "Najran", "نجران", null, null, null),
            ("JZBU", "SC", "Jazan", "جازان", null, null, null),

            // Northern
            ("TB", "NC", "Tabuk", "تبوك", null, null, null),
            ("HA", "NC", "Hail", "حائل", null, null, null),
            ("JF", "NC", "Al Jouf", "الجوف", null, null, null),
            ("HS", "NC", "Northern Borders", "الحدود الشمالية", null, null, null)
        ];

        foreach (var cbu in cbus)
        {
            if (await context.FsmsCbus.AnyAsync(x => x.Code == cbu.Code, ct))
            {
                continue;
            }

            context.FsmsCbus.Add(new FsmsCbu
            {
                Code = cbu.Code,
                ClusterCode = cbu.ClusterCode,
                NameEn = cbu.NameEn,
                NameAr = cbu.NameAr,
                OrgId = cbu.OrgId,
                OrgCode = cbu.OrgCode,
                DefaultTaskZone = cbu.DefaultTaskZone,
                IsActive = true
            });
        }
    }

    private static async Task UpsertBranchesAsync(ApplicationDbContext context, CancellationToken ct)
    {
        (string Code, string CbuCode, string TaskZone, string BranchCode, string NameEn, string NameAr)[] branches =
        [
            ("1110", "RCBU", "1110", "R-16", "Al Moraba-Riyadh", "فرع المربع - الرياض"),
            ("2200", "JCBU", "2200", "J-01", "JCBU Main Back Office", "المكتب الخلفي الرئيسي - جدة")
        ];

        foreach (var z in branches)
        {
            var existing = await context.FsmsBranches.FirstOrDefaultAsync(x => x.Code == z.Code, ct);

            if (existing is null)
            {
                context.FsmsBranches.Add(new FsmsBranch
                {
                    Code = z.Code,
                    CbuCode = z.CbuCode,
                    TaskZone = z.TaskZone,
                    BranchCode = z.BranchCode,
                    NameEn = z.NameEn,
                    NameAr = z.NameAr,
                    IsActive = true
                });

                continue;
            }

            // Backfill parent CBU on rows seeded before the org hierarchy (e.g. 1110 → RCBU).
            existing.CbuCode = z.CbuCode;
            existing.TaskZone = z.TaskZone;
            existing.BranchCode = z.BranchCode;
            existing.NameEn = z.NameEn;
            existing.NameAr = z.NameAr;
        }

        await AddCatalogBranchesAsync(context, ct);
        await RemoveCbuCodedBranchesAsync(context, ct);
    }

    /// <summary>
    /// The CBU → branch catalogue from Docs/C2M/cbu-branches.text — the codes C2M and WFM stamp on an
    /// inbound field activity, so a branch missing here is work that cannot be scoped when it arrives.
    /// </summary>
    /// <remarks>
    /// The feed carries codes only, no names and no task zones, so a row is labelled from its CBU plus
    /// its own code and leaves <see cref="FsmsBranch.TaskZone"/> null until WFM supplies the real
    /// number. Unlike the two rows above this pass is insert-only: it never rewrites a branch that is
    /// already there, so a real name typed into the lookup admin survives the next re-run.
    ///
    /// <c>RC-01</c> appears in the feed under both <c>RCBU</c> and <c>HQ</c>. Branch codes are unique,
    /// and the C2M payload samples pair it with <c>RCBU</c>, so it is listed there and HQ is left with
    /// no branches of its own.
    /// </remarks>
    private static readonly (string CbuCode, string[] BranchCodes)[] CbuBranchCatalog =
    [
        ("RCBU", ["RC-01"]),
        ("RI", ["RI-01", "RI-02", "RI-03", "RI-04", "RI-05", "RI-06", "RI-07", "RI-08", "RI-09", "RI-10", "RI-11", "RI-12", "RI-13", "RI-14", "RI-15", "RI-16", "RI-17", "RI-18", "RI-19", "RI-20", "RI-21", "RI-22"]),
        ("QS", ["QS-01", "QS-02", "QS-03", "QS-04", "QS-05", "QS-06", "QS-07", "QS-08", "QS-09", "QS-10", "QS-11", "QS-12", "QS-13"]),
        ("JCBU", ["JC-01", "JC-02"]),
        ("MCBU", ["MC-01"]),
        ("TCBU", ["TC-01"]),
        ("MK", ["MK-02", "MK-03", "MK-04", "MK-05", "MK-06", "MK-07", "MK-08", "MK-09", "MK-10", "MK-12", "MK-13", "MK-19"]),
        ("MD", ["MD-01", "MD-02", "MD-03", "MD-04", "MD-05", "MD-06", "MD-07", "MD-08", "MD-09", "MD-10", "MD-11", "MD-12", "MD-13", "MD-14", "MD-15", "MD-16", "MD-17", "MD-18"]),
        ("SH", ["SH-01", "SH-02", "SH-03", "SH-04", "SH-05", "SH-06", "SH-07", "SH-08", "SH-09", "SH-10", "SH-11", "SH-12", "SH-13", "SH-14", "SH-15", "SH-16", "SH-17", "SH-18", "SH-19", "SH-20"]),
        ("AS", ["AS-01", "AS-02", "AS-03", "AS-04", "AS-05", "AS-06", "AS-07", "AS-08", "AS-09", "AS-10", "AS-11", "AS-12", "AS-13", "AS-14", "AS-15", "AS-16", "AS-17"]),
        ("BA", ["BA-01", "BA-02", "BA-03", "BA-04", "BA-05", "BA-06", "BA-07", "BA-08"]),
        ("NJ", ["NJ-01", "NJ-02", "NJ-03", "NJ-04", "NJ-05", "NJ-06", "NJ-07"]),

        // Jazan is the one CBU whose feed rows are bare sequence numbers rather than prefixed codes.
        ("JZBU", ["1", "2", "3", "4", "5", "6", "7", "8", "9", "10"]),

        ("TB", ["TB-01", "TB-02", "TB-03", "TB-04", "TB-05", "TB-06", "TB-07"]),
        ("HA", ["HA-01", "HA-02", "HA-03", "HA-04", "HA-05", "HA-06", "HA-07"]),
        ("JF", ["JF-01", "JF-02", "JF-03", "JF-04", "JF-05", "JF-06", "JF-07"]),
        ("HS", ["HS-01", "HS-02", "HS-03", "HS-04"])
    ];

    private static async Task AddCatalogBranchesAsync(ApplicationDbContext context, CancellationToken ct)
    {
        var cbuNames = await context.FsmsCbus
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Code, x => (x.NameEn, x.NameAr), StringComparer.OrdinalIgnoreCase, ct);

        var existingCodes = await context.FsmsBranches
            .AsNoTracking()
            .Select(x => x.Code)
            .ToListAsync(ct);

        var takenCodes = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);

        foreach (var (cbuCode, branchCodes) in CbuBranchCatalog)
        {
            // A branch without its CBU above it would be unreachable in every cascading picker.
            if (!cbuNames.TryGetValue(cbuCode, out var cbuName))
            {
                continue;
            }

            foreach (var branchCode in branchCodes)
            {
                if (!takenCodes.Add(branchCode))
                {
                    continue;
                }

                context.FsmsBranches.Add(new FsmsBranch
                {
                    Code = branchCode,
                    CbuCode = cbuCode,
                    TaskZone = null,
                    BranchCode = branchCode,
                    NameEn = $"{cbuName.NameEn} - {branchCode}",
                    NameAr = $"{cbuName.NameAr} - {branchCode}",
                    IsActive = true
                });
            }
        }
    }

    /// <summary>
    /// The flat lookup this hierarchy replaced held a "branch" per CBU code (<c>RCBU</c>), because it
    /// had nowhere else to put one. Those rows are now a level up, and leaving them behind would
    /// offer a branch with no CBU above it in every scope picker. Removed only when no survey points
    /// at the code — a stale label beats silently orphaning live work.
    /// </summary>
    private static async Task RemoveCbuCodedBranchesAsync(ApplicationDbContext context, CancellationToken ct)
    {
        var cbuCodes = await context.FsmsCbus.AsNoTracking().Select(x => x.Code).ToListAsync(ct);

        if (cbuCodes.Count == 0)
        {
            return;
        }

        var strayBranches = await context.FsmsBranches
            .Where(z => cbuCodes.Contains(z.Code))
            .ToListAsync(ct);

        foreach (var branch in strayBranches)
        {
            if (await context.Surveys.AnyAsync(s => s.BranchCode == branch.Code, ct))
            {
                continue;
            }

            context.FsmsBranches.Remove(branch);
        }
    }

    private static async Task UpsertOperationAreasAsync(ApplicationDbContext context, CancellationToken ct)
    {
        // Parented by CBU, not by branch: an operation area is the branch's sibling under the CBU.
        (string Code, string CbuCode, string MainAreaCode, string NameEn, string NameAr)[] operationAreas =
        [
            ("MA6", "RCBU", "MA6", "Riyadh Operation Area 6", "منطقة العمليات ٦ - الرياض"),
            ("RC-01", "RCBU", "MA6", "Al Moraba Operation Area", "منطقة عمليات المربع"),
            ("JISO", "JCBU", "JISO", "Jeddah ISO Operation Area", "منطقة عمليات جدة - أيزو")
        ];

        foreach (var area in operationAreas)
        {
            var exists = await context.FsmsOperationAreas
                .AnyAsync(x => x.CbuCode == area.CbuCode && x.Code == area.Code, ct);

            if (exists)
            {
                continue;
            }

            context.FsmsOperationAreas.Add(new FsmsOperationArea
            {
                Code = area.Code,
                CbuCode = area.CbuCode,
                MainAreaCode = area.MainAreaCode,
                NameEn = area.NameEn,
                NameAr = area.NameAr,
                IsActive = true
            });
        }
    }

    private static async Task UpsertTaskTypesAsync(ApplicationDbContext context, CancellationToken ct)
    {
        (long Id, string Code, string NameEn, string NameAr)[] taskTypes =
        [
            (20, "20", "Water Leakage", "تسرب عداد مياه"),
            (23, "23", "Water Network Leakage", "تسرب شبكة المياه"),
            (200, "200", "Plant Maintenance Water", "صيانة المحطات - مياه"),
            (400, "400", "Box Investigation", "فحص صندوق")
        ];

        foreach (var taskType in taskTypes)
        {
            if (await context.FsmsTaskTypes.AnyAsync(x => x.Id == taskType.Id, ct))
            {
                continue;
            }

            context.FsmsTaskTypes.Add(new FsmsTaskType
            {
                Id = taskType.Id,
                Code = taskType.Code,
                NameEn = taskType.NameEn,
                NameAr = taskType.NameAr,
                IsActive = true
            });
        }
    }

    private static async Task UpsertCustomerTypesAsync(ApplicationDbContext context, CancellationToken ct)
    {
        (string Code, string NameEn, string NameAr)[] customerTypes =
        [
            ("RES", "Residential", "سكني"),
            ("COM", "Commercial", "تجاري"),
            ("GOV", "Government", "حكومي"),
            ("IND", "Industrial", "صناعي")
        ];

        foreach (var customerType in customerTypes)
        {
            if (await context.FsmsCustomerTypes.AnyAsync(x => x.Code == customerType.Code, ct))
            {
                continue;
            }

            context.FsmsCustomerTypes.Add(new FsmsCustomerType
            {
                Code = customerType.Code,
                NameEn = customerType.NameEn,
                NameAr = customerType.NameAr,
                IsActive = true
            });
        }
    }

    private static async Task UpsertFaTypesAsync(ApplicationDbContext context, CancellationToken ct)
    {
        (string FaTypeCode, long TaskTypeId, string NameEn, string NameAr)[] faTypes =
        [
            ("F-INVEST", 20, "Water Leakage", "تسرب عداد مياه"),
            ("PMT_W", 200, "Plant Maintenance Water", "صيانة المحطات - مياه"),
            ("F-BOXINV", 400, "Box Investigation", "فحص صندوق"),
            ("F-NLIVST", 23, "Water Network Leakage", "تسرب شبكة المياه")
        ];

        foreach (var fa in faTypes)
        {
            if (await context.FsmsFaTypes.AnyAsync(x => x.FaTypeCode == fa.FaTypeCode, ct))
            {
                continue;
            }

            context.FsmsFaTypes.Add(new FsmsFaType
            {
                FaTypeCode = fa.FaTypeCode,
                TaskTypeId = fa.TaskTypeId,
                NameEn = fa.NameEn,
                NameAr = fa.NameAr,
                IsActive = true
            });
        }
    }

    /// <summary>
    /// The causes a reviewer can pick when sending a survey back. Seeded rather than hard-coded so
    /// support operations can add one later without a deployment; the sort order puts the everyday
    /// causes above <c>OTHER</c>.
    /// </summary>
    private static async Task UpsertReturnReasonsAsync(ApplicationDbContext context, CancellationToken ct)
    {
        (string Code, string NameEn, string NameAr, int SortOrder)[] reasons =
        [
            (SurveyReturnReasons.MissingData, "Missing or incomplete data", "بيانات ناقصة أو غير مكتملة", 10),
            (SurveyReturnReasons.WrongLocation, "Wrong location or asset", "موقع أو أصل غير صحيح", 20),
            (SurveyReturnReasons.PoorPhoto, "Unclear or missing photos", "صور غير واضحة أو مفقودة", 30),
            (SurveyReturnReasons.NeedsRevisit, "Site needs another visit", "الموقع يحتاج زيارة أخرى", 40),
            (SurveyReturnReasons.WrongTeam, "Allocated to the wrong team", "تم إسنادها للفريق الخطأ", 50),
            (SurveyReturnReasons.Other, "Other", "أخرى", 90)
        ];

        foreach (var reason in reasons)
        {
            if (await context.FsmsReturnReasons.AnyAsync(x => x.Code == reason.Code, ct))
            {
                continue;
            }

            context.FsmsReturnReasons.Add(new FsmsReturnReason
            {
                Code = reason.Code,
                NameEn = reason.NameEn,
                NameAr = reason.NameAr,
                SortOrder = reason.SortOrder,
                IsActive = true
            });
        }
    }

    private static async Task UpsertContractorsAsync(ApplicationDbContext context, CancellationToken ct)
    {
        IReadOnlyList<ContractorSeedRow> rows = ReadEmbeddedSeed<ContractorSeedRow>(ContractorSeedResourceName);

        Dictionary<string, FsmsContractor> existing = await context.FsmsContractors
            .ToDictionaryAsync(x => x.PoNumber, StringComparer.OrdinalIgnoreCase, ct);

        foreach (ContractorSeedRow row in rows)
        {
            if (existing.TryGetValue(row.PoNumber, out FsmsContractor? contractor))
            {
                contractor.NameEn = row.NameEn;
                contractor.NameAr = row.NameAr;
                contractor.CommercialRegistration = row.CommercialRegistration;
                contractor.IsActive = true;
                continue;
            }

            context.FsmsContractors.Add(new FsmsContractor
            {
                PoNumber = row.PoNumber,
                NameEn = row.NameEn,
                NameAr = row.NameAr,
                CommercialRegistration = row.CommercialRegistration,
                IsActive = true
            });
        }
    }

    private static async Task UpsertTeamsAsync(ApplicationDbContext context, CancellationToken ct)
    {
        (string UserCode, string Name, string? Mobile, string TeamType, string Departments)[] localTeams =
        [
            ("FT-demo-admin", "FT-demo-admin", null, "MOBFADG", "10"),
            ("FT-demo-user", "FT-demo-user", null, "MOBFADG", "10")
        ];

        IReadOnlyList<FieldTeamSeedRow> extracted = ReadEmbeddedSeed<FieldTeamSeedRow>(FieldTeamSeedResourceName);

        Dictionary<string, int> contractorIdsByPo = await context.FsmsContractors
            .AsNoTracking()
            .ToDictionaryAsync(x => x.PoNumber, x => x.Id, StringComparer.OrdinalIgnoreCase, ct);

        Dictionary<string, FieldTeam> existing = await context.FieldTeams
            .ToDictionaryAsync(x => x.UserCode, StringComparer.OrdinalIgnoreCase, ct);

        HashSet<string> keepActive = new(LocalTestTeamUserCodes, StringComparer.OrdinalIgnoreCase);

        foreach (var team in localTeams)
        {
            keepActive.Add(team.UserCode);
            UpsertLocalTeam(context, existing, team);
        }

        foreach (FieldTeamSeedRow row in extracted)
        {
            if (!contractorIdsByPo.TryGetValue(row.PoNumber, out int contractorId))
            {
                continue;
            }

            keepActive.Add(row.UserCode);

            if (existing.TryGetValue(row.UserCode, out FieldTeam? current))
            {
                current.Name = row.Name;
                current.Mobile = row.Mobile;
                current.ContractorId = contractorId;
                current.IsActive = true;
                continue;
            }

            context.FieldTeams.Add(new FieldTeam
            {
                UserCode = row.UserCode,
                Name = row.Name,
                Mobile = row.Mobile,
                ContractorId = contractorId,
                IsActive = true
            });
        }

        // Keep only the seeded crews active — older demo teams stay in the DB (FK-safe) but drop out
        // of allocation and field login until someone reactivates them by hand.
        List<string> keepActiveCodes = [.. keepActive];
        List<FieldTeam> retiredTeams = await context.FieldTeams
            .Where(x => x.IsActive && !keepActiveCodes.Contains(x.UserCode))
            .ToListAsync(ct);

        foreach (FieldTeam retired in retiredTeams)
        {
            retired.IsActive = false;
        }
    }

    private static void UpsertLocalTeam(
        ApplicationDbContext context,
        IReadOnlyDictionary<string, FieldTeam> existing,
        (string UserCode, string Name, string? Mobile, string TeamType, string Departments) team)
    {
        if (existing.TryGetValue(team.UserCode, out FieldTeam? current))
        {
            current.Name = team.Name;
            current.Mobile = team.Mobile;
            current.TeamType = team.TeamType;
            current.Departments = team.Departments;
            current.IsActive = true;
            return;
        }

        context.FieldTeams.Add(new FieldTeam
        {
            UserCode = team.UserCode,
            Name = team.Name,
            Mobile = team.Mobile,
            TeamType = team.TeamType,
            Departments = team.Departments,
            IsActive = true
        });
    }

    private static IReadOnlyList<T> ReadEmbeddedSeed<T>(string resourceName)
    {
        Assembly assembly = typeof(FsmsSeedData).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded seed resource '{resourceName}' was not found.");
        using StreamReader reader = new(stream);
        string json = reader.ReadToEnd();
        List<T> rows = JsonSerializer.Deserialize<List<T>>(json, SeedJsonOptions)
            ?? throw new InvalidOperationException($"Embedded seed resource '{resourceName}' is empty.");

        return rows;
    }

    private sealed record ContractorSeedRow(
        string PoNumber,
        string NameEn,
        string NameAr,
        string? CommercialRegistration);

    private sealed record FieldTeamSeedRow(
        string UserCode,
        string Name,
        string? Mobile,
        string PoNumber);

    /// <summary>
    /// Gives the seeded crews a territory and a department, so the scope rules are demonstrable the
    /// moment the app starts rather than only after someone edits a team by hand. Both Riyadh
    /// crews are scoped to branch <c>1110</c>.
    ///
    /// Departments come from the retired comma-separated column on each seeded team, which is also
    /// what an existing database still holds — so re-running this backfills the join table for rows
    /// created before it existed, rather than only for freshly seeded ones.
    /// </summary>
    private static async Task UpsertTeamDepartmentsAndScopesAsync(ApplicationDbContext context, CancellationToken ct)
    {
        var teams = await context.FieldTeams
            .AsNoTracking()
            .Where(x => LocalTestTeamUserCodes.Contains(x.UserCode))
            .Select(x => new { x.Id, x.UserCode, x.Departments })
            .ToListAsync(ct);

        if (teams.Count == 0)
        {
            return;
        }

        var departmentIds = await context.FsmsDepartments.AsNoTracking().Select(x => x.Id).ToListAsync(ct);

        (string UserCode, string Level, string Code)[] teamScopes =
        [
            ("FT-demo-admin", OrgScopeLevels.Branch, RiyadhBranchCode),
            ("FT-demo-user", OrgScopeLevels.Branch, RiyadhBranchCode)
        ];

        foreach (var team in teams)
        {
            var ownerId = team.Id.ToString();

            // A crew's departments now live on its scope rows, so a seeded territory is paired with
            // each department the crew works. Departments come from the retired join table first and
            // the even older comma-separated column second — a database seeded before either change
            // still holds one of them, and re-running this is how those rows catch up.
            var departments = await TeamDepartmentIdsAsync(context, team.Id, team.Departments, departmentIds, ct);

            foreach (var (_, level, code) in teamScopes.Where(s => s.UserCode == team.UserCode))
            {
                // No departments means one row covering every kind of work in that territory.
                var pairings = departments.Count > 0 ? departments : [(int?)null];

                foreach (var departmentId in pairings)
                {
                    var scopeExists = await context.OrgScopes.AnyAsync(
                        x => x.OwnerType == OrgScopeOwnerTypes.Team
                            && x.OwnerId == ownerId
                            && x.Level == level
                            && x.Code == code
                            && x.DepartmentId == departmentId,
                        ct);

                    if (scopeExists)
                    {
                        continue;
                    }

                    context.OrgScopes.Add(
                        OrgScope.Create(OrgScopeOwnerTypes.Team, ownerId, level, code, departmentId));
                }
            }
        }
    }

    /// <summary>
    /// The departments a seeded crew works, read from whichever retired store still holds them.
    /// Returns nullable ids so the caller can pair them straight onto scope rows.
    /// </summary>
    private static async Task<List<int?>> TeamDepartmentIdsAsync(
        ApplicationDbContext context,
        long teamId,
        string? legacyCsv,
        List<int> knownDepartmentIds,
        CancellationToken ct)
    {
        var fromJoinTable = await context.FieldTeamDepartments
            .AsNoTracking()
            .Where(x => x.TeamId == teamId)
            .Select(x => x.DepartmentId)
            .ToListAsync(ct);

        if (fromJoinTable.Count > 0)
        {
            return [.. fromJoinTable.Distinct().Select(id => (int?)id)];
        }

        if (string.IsNullOrWhiteSpace(legacyCsv))
        {
            return [];
        }

        return [.. legacyCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out var id) ? id : (int?)null)
            .Where(id => id.HasValue && knownDepartmentIds.Contains(id.Value))
            .Distinct()];
    }

    private static async Task UpsertTeamSchedulesAsync(ApplicationDbContext context, CancellationToken ct)
    {
        var teams = await context.FieldTeams.AsNoTracking().ToListAsync(ct);
        if (teams.Count == 0)
        {
            return;
        }

        var scheduleDate = new DateOnly(2026, 7, 30);

        var schedules = new List<(string UserCode, string WorkBranch, TimeOnly Start, TimeOnly End)>
        {
            ("FT-demo-admin", RiyadhBranchCode, new TimeOnly(8, 0), new TimeOnly(17, 0)),
            ("FT-demo-user", RiyadhBranchCode, new TimeOnly(8, 0), new TimeOnly(17, 0))
        };

        foreach (var (userCode, workBranch, start, end) in schedules)
        {
            var team = teams.FirstOrDefault(t => t.UserCode == userCode);
            if (team is null)
            {
                continue;
            }

            var exists = await context.TeamSchedules.AnyAsync(
                x => x.TeamId == team.Id && x.ScheduleDate == scheduleDate && x.WorkBranch == workBranch,
                ct);

            if (exists)
            {
                continue;
            }

            context.TeamSchedules.Add(new TeamSchedule
            {
                TeamId = team.Id,
                ScheduleDate = scheduleDate,
                WorkStart = start,
                WorkEnd = end,
                WorkBranch = workBranch,
                IsActive = true
            });
        }
    }
}
