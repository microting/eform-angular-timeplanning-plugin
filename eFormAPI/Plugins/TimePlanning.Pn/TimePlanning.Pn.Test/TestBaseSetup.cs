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

    protected async Task<Core> GetCore()
    {
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

        // ConnectionString = _mariadbTestcontainer.GetConnectionString();

        var DbContext = GetContext(_mariadbTestcontainer.GetConnectionString());

        DbContext!.Database.SetCommandTimeout(300);
        // Console.WriteLine($"{DateTime.Now} : Starting MariaDb Container...");
        // await _mariadbTestcontainer.StartAsync();
        // Console.WriteLine($"{DateTime.Now} : Started MariaDb Container");
        //
        TimePlanningPnDbContext = GetTimePlanningPnDbContext(_mariadbTestcontainer.GetConnectionString());
        //
        // TimePlanningPnDbContext!.Database.SetCommandTimeout(300);
        //
        // MicrotingDbContext = GetContext(_mariadbTestcontainer.GetConnectionString());
        //
        // MicrotingDbContext!.Database.SetCommandTimeout(300);

    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        Console.WriteLine($"{DateTime.Now} : Stopping MariaDb Container...");
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