using System;
using System.Threading.Tasks;
using BackendConfiguration.Pn.Integration.Test;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using Microting.EformAngularFrontendBase.Infrastructure.Data;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningSettingService;
using AssignedSiteModel = TimePlanning.Pn.Infrastructure.Models.Settings.AssignedSite;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class SettingsServiceTests : TestBaseSetup
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
            DayOfPayment = 20
        });
        
        _settingsService = new TimeSettingService(
            _options,
            TimePlanningPnDbContext,
            Substitute.For<ILogger<TimeSettingService>>(),
            _userService,
            _localizationService,
            null,
            _coreService);
    }

    [Test]
    public async Task GetAssignedSite_ReturnsAssignedSite_WithGpsAndSnapshotFlags()
    {
        // Arrange
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 1,
            GpsEnabled = true,
            SnapshotEnabled = false,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var core = await _coreService.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();
        var site = new Microting.eForm.Infrastructure.Data.Entities.Site
        {
            Name = "Test Site",
            MicrotingUid = 1
        };
        await site.Create(sdkDbContext);

        // Act
        var result = await _settingsService.GetAssignedSite(1);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.SiteId, Is.EqualTo(1));
        Assert.That(result.Model.GpsEnabled, Is.True);
        Assert.That(result.Model.SnapshotEnabled, Is.False);
    }

    [Test]
    public async Task UpdateAssignedSite_UpdatesGpsEnabled_Successfully()
    {
        // Arrange
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 2,
            GpsEnabled = false,
            SnapshotEnabled = false,
            UseGoogleSheetAsDefault = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var updateModel = new AssignedSiteModel
        {
            Id = assignedSite.Id,
            SiteId = 2,
            GpsEnabled = true,
            SnapshotEnabled = false,
            UseGoogleSheetAsDefault = true
        };

        // Act
        var result = await _settingsService.UpdateAssignedSite(updateModel);

        // Assert
        Assert.That(result.Success, Is.True);

        var updatedSite = await TimePlanningPnDbContext.AssignedSites
            .FirstOrDefaultAsync(x => x.Id == assignedSite.Id);
        Assert.That(updatedSite, Is.Not.Null);
        Assert.That(updatedSite.GpsEnabled, Is.True);
        Assert.That(updatedSite.SnapshotEnabled, Is.False);
    }

    [Test]
    public async Task UpdateAssignedSite_UpdatesSnapshotEnabled_Successfully()
    {
        // Arrange
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 3,
            GpsEnabled = false,
            SnapshotEnabled = false,
            UseGoogleSheetAsDefault = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var updateModel = new AssignedSiteModel
        {
            Id = assignedSite.Id,
            SiteId = 3,
            GpsEnabled = false,
            SnapshotEnabled = true,
            UseGoogleSheetAsDefault = true
        };

        // Act
        var result = await _settingsService.UpdateAssignedSite(updateModel);

        // Assert
        Assert.That(result.Success, Is.True);

        var updatedSite = await TimePlanningPnDbContext.AssignedSites
            .FirstOrDefaultAsync(x => x.Id == assignedSite.Id);
        Assert.That(updatedSite, Is.Not.Null);
        Assert.That(updatedSite.GpsEnabled, Is.False);
        Assert.That(updatedSite.SnapshotEnabled, Is.True);
    }

    [Test]
    public async Task UpdateAssignedSite_UpdatesBothGpsAndSnapshotEnabled_Successfully()
    {
        // Arrange
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 4,
            GpsEnabled = false,
            SnapshotEnabled = false,
            UseGoogleSheetAsDefault = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var updateModel = new AssignedSiteModel
        {
            Id = assignedSite.Id,
            SiteId = 4,
            GpsEnabled = true,
            SnapshotEnabled = true,
            UseGoogleSheetAsDefault = true
        };

        // Act
        var result = await _settingsService.UpdateAssignedSite(updateModel);

        // Assert
        Assert.That(result.Success, Is.True);

        var updatedSite = await TimePlanningPnDbContext.AssignedSites
            .FirstOrDefaultAsync(x => x.Id == assignedSite.Id);
        Assert.That(updatedSite, Is.Not.Null);
        Assert.That(updatedSite.GpsEnabled, Is.True);
        Assert.That(updatedSite.SnapshotEnabled, Is.True);
    }

    [Test]
    public async Task UpdateAssignedSite_CanDisableBothFlags_Successfully()
    {
        // Arrange
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 5,
            GpsEnabled = true,
            SnapshotEnabled = true,
            UseGoogleSheetAsDefault = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var updateModel = new AssignedSiteModel
        {
            Id = assignedSite.Id,
            SiteId = 5,
            GpsEnabled = false,
            SnapshotEnabled = false,
            UseGoogleSheetAsDefault = true
        };

        // Act
        var result = await _settingsService.UpdateAssignedSite(updateModel);

        // Assert
        Assert.That(result.Success, Is.True);

        var updatedSite = await TimePlanningPnDbContext.AssignedSites
            .FirstOrDefaultAsync(x => x.Id == assignedSite.Id);
        Assert.That(updatedSite, Is.Not.Null);
        Assert.That(updatedSite.GpsEnabled, Is.False);
        Assert.That(updatedSite.SnapshotEnabled, Is.False);
    }

    [Test]
    public async Task UpdateAssignedSite_ReturnsFalse_WhenSiteNotFound()
    {
        // Arrange
        var updateModel = new AssignedSiteModel
        {
            Id = 999,
            SiteId = 999,
            GpsEnabled = true,
            SnapshotEnabled = true,
            UseGoogleSheetAsDefault = true
        };

        // Act
        var result = await _settingsService.UpdateAssignedSite(updateModel);

        // Assert
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public async Task UpdateAssignedSite_PreservesOtherProperties_WhenUpdatingGpsAndSnapshotFlags()
    {
        // Arrange
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 6,
            GpsEnabled = false,
            SnapshotEnabled = false,
            UseGoogleSheetAsDefault = true,
            UseOneMinuteIntervals = true,
            AllowAcceptOfPlannedHours = true,
            AllowEditOfRegistrations = true,
            StartMonday = 480,
            EndMonday = 960,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var updateModel = new AssignedSiteModel
        {
            Id = assignedSite.Id,
            SiteId = 6,
            GpsEnabled = true,
            SnapshotEnabled = true,
            UseGoogleSheetAsDefault = true,
            UseOneMinuteIntervals = true,
            AllowAcceptOfPlannedHours = true,
            AllowEditOfRegistrations = true,
            StartMonday = 480,
            EndMonday = 960
        };

        // Act
        var result = await _settingsService.UpdateAssignedSite(updateModel);

        // Assert
        Assert.That(result.Success, Is.True);

        var updatedSite = await TimePlanningPnDbContext.AssignedSites
            .FirstOrDefaultAsync(x => x.Id == assignedSite.Id);
        Assert.That(updatedSite, Is.Not.Null);
        Assert.That(updatedSite.GpsEnabled, Is.True);
        Assert.That(updatedSite.SnapshotEnabled, Is.True);
        Assert.That(updatedSite.UseOneMinuteIntervals, Is.True);
        Assert.That(updatedSite.AllowAcceptOfPlannedHours, Is.True);
        Assert.That(updatedSite.AllowEditOfRegistrations, Is.True);
        Assert.That(updatedSite.StartMonday, Is.EqualTo(480));
        Assert.That(updatedSite.EndMonday, Is.EqualTo(960));
    }
}
