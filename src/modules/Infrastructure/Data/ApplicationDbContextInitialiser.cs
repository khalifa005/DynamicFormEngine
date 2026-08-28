using System.Text.RegularExpressions;
using KH.Application.Common.Options;
using KH.Domain.Constants;
using KH.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KH.Infrastructure.Data;

public static class InitialiserExtensions
{
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();

        await initialiser.InitialiseAsync();
    }
}

public class ApplicationDbContextInitialiser
{
    /// <summary>Where the build drops the <c>CREATE OR ALTER</c> scripts, relative to the app base.</summary>
    private const string ProgrammableObjectsPath = "sql/procedures";

    private readonly ILogger<ApplicationDbContextInitialiser> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly DatabaseStartupOptions _startupOptions;

    public ApplicationDbContextInitialiser(
        ILogger<ApplicationDbContextInitialiser> logger,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<DatabaseStartupOptions> startupOptions)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _startupOptions = startupOptions.Value;
    }

    public async Task InitialiseAsync()
    {
        _logger.LogInformation(
            "DatabaseStartup settings: ApplyMigrations={ApplyMigrations}, ApplySqlObjects={ApplySqlObjects}, SeedData={SeedData}.",
            _startupOptions.ApplyMigrations,
            _startupOptions.ApplySqlObjects,
            _startupOptions.SeedData);

        try
        {
            if (_startupOptions.ApplyMigrations)
            {
                _logger.LogInformation("Applying EF Core migrations (DatabaseStartup:ApplyMigrations=true).");
                await _context.Database.MigrateAsync();
                _logger.LogInformation("EF Core migrations completed.");
            }
            else
            {
                _logger.LogInformation("Skipping EF migrations (DatabaseStartup:ApplyMigrations=false).");
            }

            if (_startupOptions.ApplySqlObjects)
            {
                _logger.LogInformation(
                    "Applying SQL programmable objects from {Path} (DatabaseStartup:ApplySqlObjects=true).",
                    ProgrammableObjectsPath);
                await ApplyProgrammableObjectsAsync();
                _logger.LogInformation("SQL programmable objects step completed.");
            }
            else
            {
                _logger.LogInformation("Skipping SQL programmable objects (DatabaseStartup:ApplySqlObjects=false).");
            }

            if (_startupOptions.SeedData)
            {
                _logger.LogInformation("Seeding database (DatabaseStartup:SeedData=true).");
                await SeedAsync();
                _logger.LogInformation("Database seed completed.");
            }
            else
            {
                _logger.LogInformation("Skipping database seed (DatabaseStartup:SeedData=false).");
            }

            _logger.LogInformation("Database startup initialisation finished.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while initialising the database.");
            throw;
        }
    }

    /// <summary>
    /// Applies the stored procedures and functions under <c>sql/procedures</c>. They stay out of
    /// EF migrations deliberately: a procedure is a replaceable definition rather than a schema
    /// change, so <c>CREATE OR ALTER</c> on every startup keeps it in step with the file without
    /// accumulating one migration per edit.
    /// </summary>
    private async Task ApplyProgrammableObjectsAsync()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, ProgrammableObjectsPath);

        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("No programmable SQL objects found at {Path}.", directory);
            return;
        }

        var files = Directory.EnumerateFiles(directory, "*.sql").Order(StringComparer.Ordinal).ToList();
        _logger.LogInformation(
            "Found {Count} SQL object script(s) under {Path}.",
            files.Count,
            directory);

        foreach (var file in files)
        {
            var script = await File.ReadAllTextAsync(file);

            // GO is a client-side separator that ADO.NET does not understand, and CREATE OR ALTER
            // has to be the first statement of its batch — so the file is split before execution.
            var batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

            foreach (var batch in batches)
            {
                if (string.IsNullOrWhiteSpace(batch))
                {
                    continue;
                }

                await _context.Database.ExecuteSqlRawAsync(batch);
            }

            _logger.LogInformation("Applied SQL object script {File}.", Path.GetFileName(file));
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            await TrySeedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    public async Task TrySeedAsync()
    {
        // Roles first: everything below grants against them, and the rename step has to run before
        // any lookup by role name would miss.
        await RoleSeedData.SeedAsync(_context, _userManager, _roleManager, _logger);

        await PermissionSeedData.SeedAsync(_context);
        await FsmsSeedData.SeedPermissionsAsync(_context);
        await PermissionSeedData.DeactivateRemovedAsync(_context);

        await FsmsSeedData.SeedReferenceDataAsync(_context);
        await FsmsTemplateSeedData.SeedTemplatesAsync(_context);
        await FsmsFieldCatalogSeedData.SeedAsync(_context);
        await FsmsSeedData.SeedFieldTeamLoginsAsync(_context, _userManager, _roleManager);
        await FsmsSeedData.SeedBackOfficeUserAsync(_context, _userManager, _roleManager);

        // Default grants per role, then the super admin's blanket grant on top.
        await RolePermissionSeedData.SeedAsync(_context, _roleManager, _logger);

        var administratorRoleEntity = await _roleManager.FindByNameAsync(Roles.Administrator);

        if (administratorRoleEntity is not null)
        {
            await PermissionSeedData.AssignAllPermissionsToRoleAsync(_context, administratorRoleEntity.Id);
        }

        await SeedAdministratorUserAsync();
    }

    private async Task SeedAdministratorUserAsync()
    {
        const string administratorUserName = "administrator@localhost";

        if (_userManager.Users.Any(u => u.UserName == administratorUserName))
        {
            return;
        }

        var administrator = new ApplicationUser { UserName = administratorUserName, Email = administratorUserName };

        var createResult = await _userManager.CreateAsync(administrator, "Administrator1!");

        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to seed administrator user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        }

        await _userManager.AddToRoleAsync(administrator, Roles.Administrator);
    }
}
