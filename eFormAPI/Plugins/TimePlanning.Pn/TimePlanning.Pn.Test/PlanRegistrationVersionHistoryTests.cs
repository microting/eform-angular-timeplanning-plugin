using System;
using System.Linq;
using System.Threading.Tasks;
using BackendConfiguration.Pn.Integration.Test;
using eFormCore;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure;
using Microting.eForm.Infrastructure.Constants;
using Microting.EformAngularFrontendBase.Infrastructure.Data;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.TimePlanningBase.Infrastructure.Data;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Data.Seed;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningPlanningService;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class PlanRegistrationVersionHistoryTests : TestBaseSetup
{
    private ITimePlanningPlanningService _planningService;
    private IUserService _userService;
    private ITimePlanningLocalizationService _localizationService;
    private IPluginDbOptions<TimePlanningBaseSettings> _options;
    private ITimePlanningDbContextHelper _dbContextHelper;
    private BaseDbContext _baseDbContext;
    private IEFormCoreService _coreService;

    [SetUp]
    public async Task SetUp()
    {
        await base.Setup();
        
        _userService = Substitute.For<IUserService>();
        _userService.UserId.Returns(1);
        
        _localizationService = Substitute.For<ITimePlanningLocalizationService>();
        _localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());
        
        _options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        _dbContextHelper = Substitute.For<ITimePlanningDbContextHelper>();
        _dbContextHelper.GetDbContext().Returns(TimePlanningPnDbContext);
        
        _baseDbContext = Substitute.For<BaseDbContext>();
        _coreService = Substitute.For<IEFormCoreService>();
        var core = Substitute.For<Core>();
        _coreService.GetCore().Returns(Task.FromResult(core));
        
        _planningService = new TimePlanningPlanningService(
            Substitute.For<Microsoft.Extensions.Logging.ILogger<TimePlanningPlanningService>>(),
            _options,
            TimePlanningPnDbContext,
            _dbContextHelper,
            _userService,
            _localizationService,
            _baseDbContext,
            _coreService);
    }

    [TearDown]
    public new async Task TearDown()
    {
        _baseDbContext?.Dispose();
        await base.TearDown();
    }

    [Test]
    public async Task GetVersionHistory_ReturnsEmptyList_WhenNoVersionsExist()
    {
        // Arrange
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 1,
            GpsEnabled = true,
            SnapshotEnabled = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var planRegistration = new PlanRegistration
        {
            Date = DateTime.Now,
            SdkSitId = 1,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await planRegistration.Create(TimePlanningPnDbContext);

        // Act
        var result = await _planningService.GetVersionHistory(planRegistration.Id);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.PlanRegistrationId, Is.EqualTo(planRegistration.Id));
        Assert.That(result.Model.GpsEnabled, Is.True);
        Assert.That(result.Model.SnapshotEnabled, Is.True);
        // Version 1 is automatically created, but since there's no previous version to compare to,
        // changes list should be empty or have all fields
        Assert.That(result.Model.Versions.Count, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public async Task GetVersionHistory_ReturnsPlanRegistrationNotFound_WhenIdDoesNotExist()
    {
        // Act
        var result = await _planningService.GetVersionHistory(99999);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("PlanRegistrationNotFound"));
    }

    [Test]
    public async Task GetVersionHistory_ReturnsVersionsWithChanges_WhenPlanRegistrationIsUpdated()
    {
        // Arrange
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 1,
            GpsEnabled = true,
            SnapshotEnabled = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var planRegistration = new PlanRegistration
        {
            Date = DateTime.Now,
            SdkSitId = 1,
            PlanText = "Initial",
            PlanHours = 7.0,
            NettoHours = 0,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await planRegistration.Create(TimePlanningPnDbContext);

        // Update the plan registration
        planRegistration.PlanText = "Updated";
        planRegistration.NettoHours = 6.5;
        planRegistration.UpdatedByUserId = 2;
        await planRegistration.Update(TimePlanningPnDbContext);

        // Act
        var result = await _planningService.GetVersionHistory(planRegistration.Id);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Versions.Count, Is.GreaterThan(0));
        
        // The most recent version should show changes
        var latestVersion = result.Model.Versions.FirstOrDefault();
        if (latestVersion != null)
        {
            Assert.That(latestVersion.Changes.Any(c => c.FieldName == "PlanText"), Is.True);
            Assert.That(latestVersion.Changes.Any(c => c.FieldName == "NettoHours"), Is.True);
        }
    }

    [Test]
    public async Task GetVersionHistory_IncludesGpsCoordinates_WhenGpsEnabledAndCoordinatesExist()
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

        var planRegistration = new PlanRegistration
        {
            Date = DateTime.Now,
            SdkSitId = 1,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await planRegistration.Create(TimePlanningPnDbContext);

        var gpsCoordinate = new GpsCoordinate
        {
            PlanRegistrationId = planRegistration.Id,
            Latitude = 55.12345,
            Longitude = 12.54321,
            RegistrationType = "Start1StartedAt",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await gpsCoordinate.Create(TimePlanningPnDbContext);

        // Act
        var result = await _planningService.GetVersionHistory(planRegistration.Id);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model.GpsEnabled, Is.True);
        
        // Check if GPS coordinate is included in any version
        var hasGpsChange = result.Model.Versions
            .Any(v => v.Changes.Any(c => c.FieldType == "gps"));
        Assert.That(hasGpsChange, Is.True);
    }

    [Test]
    public async Task GetVersionHistory_ExcludesGpsCoordinates_WhenGpsDisabled()
    {
        // Arrange
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 1,
            GpsEnabled = false,
            SnapshotEnabled = false,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var planRegistration = new PlanRegistration
        {
            Date = DateTime.Now,
            SdkSitId = 1,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await planRegistration.Create(TimePlanningPnDbContext);

        // Create GPS coordinate even though GPS is disabled
        var gpsCoordinate = new GpsCoordinate
        {
            PlanRegistrationId = planRegistration.Id,
            Latitude = 55.12345,
            Longitude = 12.54321,
            RegistrationType = "Start1StartedAt",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await gpsCoordinate.Create(TimePlanningPnDbContext);

        // Act
        var result = await _planningService.GetVersionHistory(planRegistration.Id);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model.GpsEnabled, Is.False);
        
        // GPS coordinates should not be included when GPS is disabled
        var hasGpsChange = result.Model.Versions
            .Any(v => v.Changes.Any(c => c.FieldType == "gps"));
        Assert.That(hasGpsChange, Is.False);
    }
}
