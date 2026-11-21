using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningSettingService;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;

namespace TimePlanning.Pn.Test;

/// <summary>
/// GetAssignedSiteByCurrentUserName for a caller whose worker has an active
/// site, but that site has no active time-planning AssignedSite row. The
/// lookup must answer "Site not found" (like the worker == null and
/// no-active-site paths) instead of throwing a NullReferenceException.
/// </summary>
[TestFixture]
public class SettingsServiceAssignedSiteByCurrentUserTests : TestBaseSetup
{
    private const string CallerEmail = "worker@example.com";

    private IUserService _userService;
    private IEFormCoreService _coreService;
    private ISettingService _settingsService;

    [SetUp]
    public async Task SetUp()
    {
        await base.Setup();
        _userService = Substitute.For<IUserService>();
        _userService.UserId.Returns(1);

        var localizationService = Substitute.For<ITimePlanningLocalizationService>();
        localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

        _coreService = Substitute.For<IEFormCoreService>();
        var core = await GetCore();
        _coreService.GetCore().Returns(core);

        var options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        options.Value.Returns(new TimePlanningBaseSettings
        {
            AutoBreakCalculationActive = "0",
            DayOfPayment = 20,
            GpsEnabled = "0",
            SnapshotEnabled = "0"
        });

        await SeedAdminCallerAsync(_userService, CallerEmail);

        _settingsService = new TimeSettingService(
            options,
            TimePlanningPnDbContext,
            Substitute.For<ILogger<TimeSettingService>>(),
            _userService,
            localizationService,
            SeededBaseDbContext,
            _coreService,
            Substitute.For<TimePlanning.Pn.Services.PushNotificationService.IPushNotificationService>());
    }

    private async Task CreateWorkerWithActiveSite(int siteMicrotingUid)
    {
        var core = await _coreService.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();

        var site = new Microting.eForm.Infrastructure.Data.Entities.Site
        {
            Name = "Site without assigned site",
            MicrotingUid = siteMicrotingUid
        };
        await site.Create(sdkDbContext);

        var worker = new Microting.eForm.Infrastructure.Data.Entities.Worker
        {
            FirstName = "Test",
            LastName = "Worker",
            Email = CallerEmail,
            MicrotingUid = 1001
        };
        await worker.Create(sdkDbContext);

        var siteWorker = new Microting.eForm.Infrastructure.Data.Entities.SiteWorker
        {
            SiteId = site.Id,
            WorkerId = worker.Id,
            MicrotingUid = 2001
        };
        await siteWorker.Create(sdkDbContext);
    }

    [Test]
    public async Task GetAssignedSiteByCurrentUserName_NoAssignedSite_ReturnsSiteNotFound()
    {
        await CreateWorkerWithActiveSite(100);

        var result = await _settingsService.GetAssignedSiteByCurrentUserName();

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Site not found"));
        Assert.That(result.Model, Is.Null);
    }

    [Test]
    public async Task GetAssignedSiteByCurrentUserName_OnlyRemovedAssignedSite_ReturnsSiteNotFound()
    {
        await CreateWorkerWithActiveSite(100);

        var removedAssignedSite = new AssignedSiteEntity
        {
            SiteId = 100,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await removedAssignedSite.Create(TimePlanningPnDbContext);
        await removedAssignedSite.Delete(TimePlanningPnDbContext);

        var result = await _settingsService.GetAssignedSiteByCurrentUserName();

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Site not found"));
        Assert.That(result.Model, Is.Null);
    }
}
