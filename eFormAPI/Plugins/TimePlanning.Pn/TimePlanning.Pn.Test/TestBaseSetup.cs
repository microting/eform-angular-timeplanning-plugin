using System;
using System.IO;
using System.Threading.Tasks;
using DotNet.Testcontainers.Containers;
using eFormCore;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure;
using Microting.EformAngularFrontendBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data;
using NUnit.Framework;
using Testcontainers.MariaDb;
using TimePlanning.Pn.Infrastructure.Data.Seed;

#nullable enable
namespace TimePlanning.Pn.Test;

public abstract class TestBaseSetup
{
    private readonly MariaDbContainer _mariadbTestcontainer = new MariaDbBuilder()
        .WithImage("mariadb:11")
        .WithDatabase(
            "myDb").WithUsername("bla").WithPassword("secretpassword")
        .WithEnvironment("MYSQL_ROOT_PASSWORD", "Qq1234567$")
        .WithCommand("--max_allowed_packet", "32505856")
        .Build();

    protected TimePlanningPnDbContext? TimePlanningPnDbContext;
    protected MicrotingDbContext? MicrotingDbContext;

    private TimePlanningPnDbContext GetTimePlanningPnDbContext(string connectionStr)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TimePlanningPnDbContext>();

        optionsBuilder.UseMySql(
            connectionStr.Replace("myDb", "420_eform-angular-items-planning-plugin").Replace("bla", "root"),
            new MariaDbServerVersion(
                ServerVersion.AutoDetect(connectionStr)),
            mySqlOptionsAction: builder => {
                builder.EnableRetryOnFailure();
            });

        var backendConfigurationPnDbContext = new TimePlanningPnDbContext(optionsBuilder.Options);

        // Drop and recreate the database fresh for each test to avoid state pollution
        backendConfigurationPnDbContext.Database.EnsureDeleted();
        // Use only migrations to create the schema - don't use EnsureCreated() or SQL scripts
        // as they conflict with migrations
        backendConfigurationPnDbContext.Database.Migrate();
        
        // Seed configuration data after migrations
        TimePlanningPluginSeed.SeedData(backendConfigurationPnDbContext);

        return backendConfigurationPnDbContext;
    }

    private MicrotingDbContext GetContext(string connectionStr)
    {
        var dbContextOptionsBuilder = new DbContextOptionsBuilder();

        dbContextOptionsBuilder.UseMySql(connectionStr.Replace("myDb", "420_SDK").Replace("bla", "root")
            , new MariaDbServerVersion(
                ServerVersion.AutoDetect(connectionStr)),
            mySqlOptionsAction: builder => {
                builder.EnableRetryOnFailure();
            });
        var microtingDbContext = new MicrotingDbContext(dbContextOptionsBuilder.Options);
        var file = Path.Combine("SQL", "420_SDK.sql");
        var rawSql = File.ReadAllText(file);

        microtingDbContext.Database.EnsureCreated();
        microtingDbContext.Database.ExecuteSqlRaw(rawSql);
        microtingDbContext.Database.Migrate();

        return microtingDbContext;
    }

    /// <summary>
    /// Builds a fresh, empty BaseDbContext (the eform-angular-frontend user/
    /// role store) against the test container — same pattern as the
    /// BackendConfiguration integration tests. Dropped and recreated per call
    /// so each test seeds its own users/roles without cross-test pollution.
    /// Needed by tests that exercise service paths gated on
    /// BaseDbContext.Users (e.g. TimePlanningPlanningService.Index()).
    /// </summary>
    protected BaseDbContext GetBaseDbContext()
    {
        var connectionStr = _mariadbTestcontainer.GetConnectionString();
        var optionsBuilder = new DbContextOptionsBuilder<BaseDbContext>();

        optionsBuilder.UseMySql(
            connectionStr.Replace("myDb", "420_Angular").Replace("bla", "root"),
            new MariaDbServerVersion(ServerVersion.AutoDetect(connectionStr)),
            mySqlOptionsAction: builder => {
                builder.EnableRetryOnFailure();
            });

        var baseDbContext = new BaseDbContext(optionsBuilder.Options);
        baseDbContext.Database.EnsureDeleted();
        baseDbContext.Database.EnsureCreated();
        return baseDbContext;
    }

    /// <summary>
    /// Builds a NEW TimePlanningPnDbContext against the same (already
    /// migrated) plugin database as <see cref="TimePlanningPnDbContext"/> —
    /// WITHOUT dropping it. Use this to make ITimePlanningDbContextHelper
    /// substitutes hand out a fresh context per call, mirroring production,
    /// for service paths that run per-site work concurrently (Index()).
    /// </summary>
    protected TimePlanningPnDbContext CreateTimePlanningPnDbContext()
    {
        var connectionStr = _mariadbTestcontainer.GetConnectionString();
        var optionsBuilder = new DbContextOptionsBuilder<TimePlanningPnDbContext>();

        optionsBuilder.UseMySql(
            connectionStr.Replace("myDb", "420_eform-angular-items-planning-plugin").Replace("bla", "root"),
            new MariaDbServerVersion(ServerVersion.AutoDetect(connectionStr)),
            mySqlOptionsAction: builder => {
                builder.EnableRetryOnFailure();
            });

        return new TimePlanningPnDbContext(optionsBuilder.Options);
    }

    /// <summary>
    /// Provisions the SDK database (420_SDK) the first time it is needed and
    /// memoizes the schema/connection for the lifetime of this fixture
    /// instance — <see cref="MicrotingDbContext.Database.Migrate"/> (~44-48s)
    /// is the expensive part and only needs to run ONCE per fixture, not once
    /// per test. This method ONLY provisions; it never resets SDK data, so
    /// calling <see cref="GetCore"/> more than once within a single test is
    /// harmless. Per-test data isolation is handled separately, in
    /// <see cref="Setup"/> (see ResetSdkDbDataAsync below) — resetting here,
    /// on every call, wiped out data a test had just written via an earlier
    /// GetCore() call in the same test (Stage 0 review round 2).
    /// </summary>
    private async Task EnsureSdkDbProvisionedAsync()
    {
        if (_mariadbTestcontainer.State == TestcontainersStates.Undefined)
        {
            await _mariadbTestcontainer.StartAsync();
        }

        if (MicrotingDbContext == null)
        {
            var dbContext = GetContext(_mariadbTestcontainer.GetConnectionString());
            dbContext.Database.SetCommandTimeout(300);
            MicrotingDbContext = dbContext;
        }
    }

    /// <summary>
    /// Resets SDK *data* to the dump's known-good snapshot (~7s) by
    /// replaying the SQL dump against the already-migrated schema, WITHOUT
    /// re-running Migrate() (~44-48s, kept once-per-fixture). Called once per
    /// test from <see cref="Setup"/> — not from <see cref="GetCore"/> — so a
    /// test that calls GetCore() more than once doesn't have its own SDK
    /// writes wiped out mid-test, while every *new* test still starts from
    /// clean SDK data.
    /// </summary>
    private async Task ResetSdkDbDataAsync()
    {
        var file = Path.Combine("SQL", "420_SDK.sql");
        var rawSql = await File.ReadAllTextAsync(file);
        await MicrotingDbContext!.Database.ExecuteSqlRawAsync(rawSql);
    }

    protected async Task<Core> GetCore()
    {
        // Core.StartSqlOnly only connects and validates settings - it does not
        // migrate - so the SDK database must already be provisioned before it runs.
        // This only provisions (once per fixture) and never resets data -- see
        // EnsureSdkDbProvisionedAsync's doc comment.
        await EnsureSdkDbProvisionedAsync();

        var core = new Core();
        await core.StartSqlOnly(_mariadbTestcontainer.GetConnectionString().Replace("myDb", "420_SDK")
            .Replace("bla", "root"));
        return core;
    }

    [SetUp]
    public async Task Setup()
    {
        if (_mariadbTestcontainer.State == TestcontainersStates.Undefined)
        {
            await _mariadbTestcontainer.StartAsync();
        }

        // Reset SDK data once per test -- but only if an earlier test in this
        // fixture already provisioned it (MicrotingDbContext != null). On the
        // first test of a fixture, provisioning (triggered lazily by that
        // test's own GetCore() call, if it makes one) already leaves the SDK
        // database freshly loaded from the dump, so resetting here too would
        // just be a redundant ~7s replay.
        if (MicrotingDbContext != null)
        {
            await ResetSdkDbDataAsync();
        }

        TimePlanningPnDbContext = GetTimePlanningPnDbContext(_mariadbTestcontainer.GetConnectionString());
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        Console.WriteLine($"{DateTime.Now} : Stopping MariaDb Container...");
        if (MicrotingDbContext != null)
        {
            await MicrotingDbContext.DisposeAsync();
        }
        await _mariadbTestcontainer.StopAsync();
        await _mariadbTestcontainer.DisposeAsync();
        Console.WriteLine($"{DateTime.Now} : Stopped MariaDb Container");
    }

    [TearDown]
    public async Task TearDown()
    {
        await TimePlanningPnDbContext!.DisposeAsync();
    }
}