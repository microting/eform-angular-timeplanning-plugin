using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Data.Entities;
using Tag = Microting.eForm.Infrastructure.Data.Entities.Tag;
using SiteTag = Microting.eForm.Infrastructure.Data.Entities.SiteTag;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Services.PlanPatternApplyService;
using TimePlanning.Pn.Services.PlannerService;
using TimePlanning.Pn.Services.PushNotificationService;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningPlanningService;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;

#nullable enable
namespace TimePlanning.Pn.Test;

/// <summary>
/// Wiring shared by the #5489 planner fixtures: a PlannerService over the
/// real planning service (for the grid's lazy row creation) and the real
/// pattern apply service. Not a test class.
/// </summary>
public abstract class PlannerTestBase : TestBaseSetup
{
    protected static DateTime Today => DateTime.UtcNow.Date;

    protected IUserService UserService = null!;
    protected IEFormCoreService CoreService = null!;
    protected ITimePlanningLocalizationService Localization = null!;
    protected IPluginDbOptions<TimePlanningBaseSettings> Options = null!;

    /// <summary>Receives the fire-and-forget pushes; <see cref="PushSent"/> is released per send.</summary>
    protected IPushNotificationService PushService = null!;
    protected SemaphoreSlim PushSent = null!;
    private IServiceScopeFactory _pushScopeFactory = null!;

    [SetUp]
    public async Task SetUpPlanner()
    {
        UserService = Substitute.For<IUserService>();
        UserService.UserId.Returns(1);

        Localization = Substitute.For<ITimePlanningLocalizationService>();
        Localization.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());
        Localization.GetString(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(x => $"{x[0]}:{string.Join(",", (object[])x[1])}");

        CoreService = Substitute.For<IEFormCoreService>();
        var core = await GetCore();
        CoreService.GetCore().Returns(core);

        // The pushes run in their own DI scope, as in PushNotificationIntegrationTests.
        PushSent = new SemaphoreSlim(0);
        PushService = Substitute.For<IPushNotificationService>();
        PushService.SendToSiteAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>?>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => PushSent.Release());
        var pushProvider = Substitute.For<IServiceProvider>();
        pushProvider.GetService(typeof(IPushNotificationService)).Returns(PushService);
        var pushScope = Substitute.For<IServiceScope>();
        pushScope.ServiceProvider.Returns(pushProvider);
        _pushScopeFactory = Substitute.For<IServiceScopeFactory>();
        _pushScopeFactory.CreateScope().Returns(pushScope);

        Options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        Options.Value.Returns(new TimePlanningBaseSettings
        {
            AutoBreakCalculationActive = "0",
            DayOfPayment = 20,
            GpsEnabled = "0",
            SnapshotEnabled = "0"
        });
    }

    [TearDown]
    public void TearDownPlanner() => PushSent.Dispose();

    protected PlanPatternApplyService BuildApplyService() =>
        new(TimePlanningPnDbContext!, UserService,
            Substitute.For<ILogger<PlanPatternApplyService>>(), Options,
            Substitute.For<ILogger<TimePlanningPlanningService>>());

    /// <summary>Call after the caller is seeded: the services read <see cref="TestBaseSetup.SeededBaseDbContext"/>.</summary>
    protected PlannerService BuildService(PlanPatternApplyService? applyService = null)
    {
        var dbContextHelper = Substitute.For<ITimePlanningDbContextHelper>();
        // Index runs sites concurrently and disposes each context it is given.
        dbContextHelper.GetDbContext().Returns(_ => CreateTimePlanningPnDbContext());
        var planningService = new TimePlanningPlanningService(
            Substitute.For<ILogger<TimePlanningPlanningService>>(),
            Options,
            TimePlanningPnDbContext!,
            dbContextHelper,
            UserService,
            Localization,
            SeededBaseDbContext!,
            CoreService);

        return new PlannerService(
            TimePlanningPnDbContext!,
            UserService,
            Localization,
            CoreService,
            SeededBaseDbContext!,
            Substitute.For<ILogger<PlannerService>>(),
            planningService,
            applyService ?? BuildApplyService(),
            _pushScopeFactory);
    }

    /// <summary>A worker on the weekday template (7:00-15:00/0.5 every day) with an SDK site, optionally tagged.</summary>
    protected async Task<AssignedSiteEntity> CreateWorkerAsync(int siteUid, int? tagId = null,
        bool useGoogleSheet = false)
    {
        var site = await PlanPatternTestSeed.CreateTemplateWorkerAsync(TimePlanningPnDbContext!, siteUid, useGoogleSheet);
        var sdk = (await GetCore()).DbContextHelper.GetDbContext();
        var sdkSite = await sdk.Sites.FirstOrDefaultAsync(x => x.MicrotingUid == siteUid);
        if (sdkSite == null)
        {
            sdkSite = new Microting.eForm.Infrastructure.Data.Entities.Site { Name = $"Worker {siteUid}", MicrotingUid = siteUid };
            await sdkSite.Create(sdk);
        }

        if (tagId != null)
        {
            await sdk.SiteTags.AddAsync(new SiteTag { SiteId = sdkSite.Id, TagId = tagId });
            await sdk.SaveChangesAsync();
        }

        return site;
    }

    protected async Task<int> CreateTagAsync(string name)
    {
        var sdk = (await GetCore()).DbContextHelper.GetDbContext();
        var tag = new Tag { Name = name };
        await tag.Create(sdk);
        return tag.Id;
    }

    /// <summary>The Monday after today, so every shown day is in the future.</summary>
    protected static DateTime NextMonday => PlanPatternTestSeed.MondayOnOrBefore(Today).AddDays(7);

    protected async Task<List<Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration>> RowsAsync(int siteUid)
    {
        await using var fresh = CreateTimePlanningPnDbContext();
        return await PlanPatternTestSeed.LiveRowsAsync(fresh, siteUid);
    }

    protected async Task<Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration> RowAsync(int siteUid, DateTime date) =>
        (await RowsAsync(siteUid)).First(x => x.Date == date.Date);

    protected async Task<bool> IsRetiredAsync()
    {
        await using var fresh = CreateTimePlanningPnDbContext();
        return await GoogleSheetRetirement.IsRetiredAsync(fresh);
    }
}
