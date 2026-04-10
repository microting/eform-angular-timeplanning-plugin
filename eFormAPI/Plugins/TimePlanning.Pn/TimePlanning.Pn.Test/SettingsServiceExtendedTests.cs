using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningSettingService;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class SettingsServiceExtendedTests : TestBaseSetup
{
    private ISettingService _settingsService;
    private IUserService _userService;
    private ITimePlanningLocalizationService _localizationService;
    private IEFormCoreService _coreService;
    private IPluginDbOptions<TimePlanningBaseSettings> _options;

    [SetUp]
    public async Task SetUp()
    {
        await base.Setup();
        _userService = Substitute.For<IUserService>();
        _userService.UserId.Returns(1);

        _localizationService = Substitute.For<ITimePlanningLocalizationService>();
        _localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

        _coreService = Substitute.For<IEFormCoreService>();
        var core = await GetCore();
        _coreService.GetCore().Returns(core);

        _options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        _options.Value.Returns(new TimePlanningBaseSettings
        {
            AutoBreakCalculationActive = "0",
            DayOfPayment = 20,
            GpsEnabled = "0",
            SnapshotEnabled = "0"
        });

        // Pass null for baseDbContext — avatar lookup will be skipped
        // when no matching user is found in SDK worker data
        _settingsService = new TimeSettingService(
            _options,
            TimePlanningPnDbContext,
            Substitute.For<ILogger<TimeSettingService>>(),
            _userService,
            _localizationService,
            null,
            _coreService);
    }

    // --- GetAvailableSites tests ---

    [Test]
    public async Task GetAvailableSites_InvalidToken_ReturnsTokenNotFound()
    {
        // Act
        var result = await _settingsService.GetAvailableSites("bad-token");

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Token not found"));
    }

    [Test]
    public async Task GetAvailableSites_NullToken_ReturnsSuccess()
    {
        // Act — null token skips the device token check
        var result = await _settingsService.GetAvailableSites(null);

        // Assert — succeeds even with no sites
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetAvailableSites_ValidToken_ReturnsSuccess()
    {
        // Arrange — create a registration device with a known token
        var device = new RegistrationDevice
        {
            Token = "valid-test-token",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await device.Create(TimePlanningPnDbContext);

        // Act
        var result = await _settingsService.GetAvailableSites("valid-test-token");

        // Assert — token is valid, returns empty site list
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
    }

    // --- GetAvailableSitesByCurrentUser tests ---

    [Test]
    public async Task GetAvailableSitesByCurrentUser_NoSites_ReturnsEmptyList()
    {
        // Act
        var result = await _settingsService.GetAvailableSitesByCurrentUser();

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetAvailableSitesByCurrentUser_ExcludesResignedSites()
    {
        // Arrange — create one active and one resigned AssignedSite
        var core = await _coreService.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();

        // Create SDK entities for active site
        var site1 = new Microting.eForm.Infrastructure.Data.Entities.Site
        {
            Name = "Active Worker",
            MicrotingUid = 100
        };
        await site1.Create(sdkDbContext);

        var worker1 = new Microting.eForm.Infrastructure.Data.Entities.Worker
        {
            FirstName = "Active",
            LastName = "Worker",
            Email = "active@test.com",
            MicrotingUid = 200
        };
        await worker1.Create(sdkDbContext);

        var siteWorker1 = new Microting.eForm.Infrastructure.Data.Entities.SiteWorker
        {
            SiteId = site1.Id,
            WorkerId = worker1.Id,
            MicrotingUid = 300
        };
        await siteWorker1.Create(sdkDbContext);

        var unit1 = new Microting.eForm.Infrastructure.Data.Entities.Unit
        {
            SiteId = site1.Id,
            MicrotingUid = 400,
            CustomerNo = 1,
            OtpCode = 1234
        };
        await unit1.Create(sdkDbContext);

        // Ensure language exists
        var language = await sdkDbContext.Languages.FirstOrDefaultAsync();
        if (language == null)
        {
            language = new Microting.eForm.Infrastructure.Data.Entities.Language
            {
                LanguageCode = "en",
                Name = "English"
            };
            await language.Create(sdkDbContext);
        }
        site1.LanguageId = language.Id;
        await site1.Update(sdkDbContext);

        // Create SDK entities for resigned site
        var site2 = new Microting.eForm.Infrastructure.Data.Entities.Site
        {
            Name = "Resigned Worker",
            MicrotingUid = 101
        };
        await site2.Create(sdkDbContext);

        // TimePlanning AssignedSites
        var activeSite = new AssignedSiteEntity
        {
            SiteId = 100,
            Resigned = false,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await activeSite.Create(TimePlanningPnDbContext);

        var resignedSite = new AssignedSiteEntity
        {
            SiteId = 101,
            Resigned = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await resignedSite.Create(TimePlanningPnDbContext);

        // Act
        var result = await _settingsService.GetAvailableSitesByCurrentUser();

        // Assert — only the active site should be returned
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Count, Is.EqualTo(1));
        Assert.That(result.Model[0].SiteName, Is.EqualTo("Active Worker"));
        Assert.That(result.Model[0].SiteId, Is.EqualTo(100));
    }

    [Test]
    public async Task GetAvailableSitesByCurrentUser_ReturnsSiteWithCorrectFields()
    {
        // Arrange
        var core = await _coreService.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();

        var site = new Microting.eForm.Infrastructure.Data.Entities.Site
        {
            Name = "Test Worker",
            MicrotingUid = 500
        };
        await site.Create(sdkDbContext);

        var worker = new Microting.eForm.Infrastructure.Data.Entities.Worker
        {
            FirstName = "Test",
            LastName = "Worker",
            Email = "test@test.com",
            PinCode = "1234",
            MicrotingUid = 600
        };
        await worker.Create(sdkDbContext);

        var siteWorker = new Microting.eForm.Infrastructure.Data.Entities.SiteWorker
        {
            SiteId = site.Id,
            WorkerId = worker.Id,
            MicrotingUid = 700
        };
        await siteWorker.Create(sdkDbContext);

        var unit = new Microting.eForm.Infrastructure.Data.Entities.Unit
        {
            SiteId = site.Id,
            MicrotingUid = 800,
            CustomerNo = 42,
            OtpCode = 5678
        };
        await unit.Create(sdkDbContext);

        var language = await sdkDbContext.Languages.FirstOrDefaultAsync();
        if (language == null)
        {
            language = new Microting.eForm.Infrastructure.Data.Entities.Language
            {
                LanguageCode = "da",
                Name = "Danish"
            };
            await language.Create(sdkDbContext);
        }
        site.LanguageId = language.Id;
        await site.Update(sdkDbContext);

        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 500,
            Resigned = false,
            AutoBreakCalculationActive = true,
            ThirdShiftActive = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        // Act
        var result = await _settingsService.GetAvailableSitesByCurrentUser();

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Count, Is.EqualTo(1));

        var s = result.Model[0];
        Assert.That(s.SiteId, Is.EqualTo(500));
        Assert.That(s.SiteName, Is.EqualTo("Test Worker"));
        Assert.That(s.FirstName, Is.EqualTo("Test"));
        Assert.That(s.LastName, Is.EqualTo("Worker"));
        Assert.That(s.Email, Is.EqualTo("test@test.com"));
        Assert.That(s.PinCode, Is.EqualTo("1234"));
        Assert.That(s.CustomerNo, Is.EqualTo(42));
        Assert.That(s.AutoBreakCalculationActive, Is.True);
        Assert.That(s.ThirdShiftActive, Is.True);
        Assert.That(s.HoursStarted, Is.False);
        Assert.That(s.PauseStarted, Is.False);
    }
}
