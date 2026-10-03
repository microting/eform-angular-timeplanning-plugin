using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Models.GpsCoordinate;
using TimePlanning.Pn.Services.TimePlanningGpsCoordinateService;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class GpsCoordinateServiceTests : TestBaseSetup
{
    private ITimePlanningGpsCoordinateService _gpsCoordinateService;
    private IUserService _userService;
    private ITimePlanningLocalizationService _localizationService;

    [SetUp]
    public async Task SetUp()
    {
        await base.Setup();
        _userService = Substitute.For<IUserService>();
        _userService.UserId.Returns(1);

        _localizationService = Substitute.For<ITimePlanningLocalizationService>();
        _localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

        _gpsCoordinateService = new TimePlanningGpsCoordinateService(
            Substitute.For<Microsoft.Extensions.Logging.ILogger<TimePlanningGpsCoordinateService>>(),
            TimePlanningPnDbContext,
            _userService,
            _localizationService);
    }

    [Test]
    public async Task Create_CreatesGpsCoordinate_Successfully()
    {
        // Arrange
        var planRegistration = new PlanRegistration
        {
            Date = DateTime.Now,
            SdkSitId = 1,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await planRegistration.Create(TimePlanningPnDbContext);

        var model = new GpsCoordinateCreateModel
        {
            SdkSiteId = 1,
            Date = planRegistration.Date,
            Latitude = 55.12345,
            Longitude = 12.54321,
            RegistrationType = "CheckIn"
        };

        // Act
        var result = await _gpsCoordinateService.Create(model);

        // Assert
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task GetById_ReturnsGpsCoordinate_WhenExists()
    {
        // Arrange
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
            RegistrationType = "CheckIn",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await gpsCoordinate.Create(TimePlanningPnDbContext);

        // Act
        var result = await _gpsCoordinateService.GetById(gpsCoordinate.Id);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Id, Is.EqualTo(gpsCoordinate.Id));
        Assert.That(result.Model.Latitude, Is.EqualTo(55.12345));
        Assert.That(result.Model.Longitude, Is.EqualTo(12.54321));
    }

    [Test]
    public async Task Index_ReturnsGpsCoordinates_ForPlanRegistration()
    {
        // Arrange
        var planRegistration = new PlanRegistration
        {
            Date = DateTime.Now,
            SdkSitId = 1,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await planRegistration.Create(TimePlanningPnDbContext);

        var gpsCoordinate1 = new GpsCoordinate
        {
            PlanRegistrationId = planRegistration.Id,
            Latitude = 55.12345,
            Longitude = 12.54321,
            RegistrationType = "CheckIn",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await gpsCoordinate1.Create(TimePlanningPnDbContext);

        var gpsCoordinate2 = new GpsCoordinate
        {
            PlanRegistrationId = planRegistration.Id,
            Latitude = 55.67890,
            Longitude = 12.09876,
            RegistrationType = "CheckOut",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await gpsCoordinate2.Create(TimePlanningPnDbContext);

        // Act
        var result = await _gpsCoordinateService.Index(planRegistration.Id);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Update_UpdatesGpsCoordinate_Successfully()
    {
        // Arrange
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
            RegistrationType = "CheckIn",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await gpsCoordinate.Create(TimePlanningPnDbContext);

        var updateModel = new GpsCoordinateUpdateModel
        {
            Id = gpsCoordinate.Id,
            SdkSiteId = 1,
            Date = planRegistration.Date,
            Latitude = 56.00000,
            Longitude = 13.00000,
            RegistrationType = "CheckOut"
        };

        // Act
        var result = await _gpsCoordinateService.Update(updateModel);

        // Assert
        Assert.That(result.Success, Is.True);

        var updated = await _gpsCoordinateService.GetById(gpsCoordinate.Id);
        Assert.That(updated.Model.Latitude, Is.EqualTo(56.00000));
        Assert.That(updated.Model.Longitude, Is.EqualTo(13.00000));
    }

    [Test]
    public async Task Delete_DeletesGpsCoordinate_Successfully()
    {
        // Arrange
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
            RegistrationType = "CheckIn",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await gpsCoordinate.Create(TimePlanningPnDbContext);

        // Act
        var result = await _gpsCoordinateService.Delete(gpsCoordinate.Id);

        // Assert
        Assert.That(result.Success, Is.True);

        var getResult = await _gpsCoordinateService.GetById(gpsCoordinate.Id);
        Assert.That(getResult.Success, Is.False);
    }

    private Task SeedAssignedSite(int sdkSiteId) =>
        new AssignedSite
        {
            SiteId = sdkSiteId,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext);

    private static GpsCoordinateCreateModel StartPosition(int sdkSiteId, DateTime date) => new()
    {
        SdkSiteId = sdkSiteId,
        Date = date,
        Latitude = 55.12345,
        Longitude = 12.54321,
        RegistrationType = "Start1StartedAt"
    };

    // #1746: the Start position can arrive before the save that creates the
    // day's PlanRegistration; it used to fail on planRegistration.Id.
    [Test]
    public async Task Create_CreatesTheDayRowAndLinksTheCoordinate_WhenTheDayHasNoPlanRegistration()
    {
        // Arrange
        const int sdkSiteId = 56;
        var today = DateTime.Now.Date;
        await SeedAssignedSite(sdkSiteId);
        await new PlanRegistration
        {
            Date = today.AddDays(-1),
            SdkSitId = sdkSiteId,
            SumFlexEnd = -1.25,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext);

        // Act
        var result = await _gpsCoordinateService.Create(StartPosition(sdkSiteId, today));

        // Assert
        Assert.That(result.Success, Is.True, result.Message);
        var rows = await TimePlanningPnDbContext.PlanRegistrations.AsNoTracking()
            .Where(x => x.SdkSitId == sdkSiteId && x.Date == today)
            .ToListAsync();
        Assert.That(rows, Has.Count.EqualTo(1), "exactly one row for the day");
        Assert.That(rows[0].SumFlexStart, Is.EqualTo(-1.25));
        Assert.That(rows[0].SumFlexEnd, Is.EqualTo(-1.25));
        var coordinate = await TimePlanningPnDbContext.GpsCoordinates.AsNoTracking()
            .SingleAsync(x => x.PlanRegistrationId == rows[0].Id);
        Assert.That(coordinate.RegistrationType, Is.EqualTo("Start1StartedAt"));
    }

    [Test]
    public async Task Create_OnAOneMinuteSite_CarriesThePredecessorsSecondsBalance()
    {
        // Arrange: the predecessor's seconds balance (3 h 0 min 37 s) is the
        // source of truth; its decimal (3.0) is stale.
        const int sdkSiteId = 60;
        var today = DateTime.Now.Date;
        await new AssignedSite
        {
            SiteId = sdkSiteId,
            UseOneMinuteIntervals = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext);
        await new PlanRegistration
        {
            Date = today.AddDays(-1),
            SdkSitId = sdkSiteId,
            SumFlexEnd = 3.0,
            SumFlexEndInSeconds = 10837,
            RegisteredUnderOneMinuteIntervals = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext);

        // Act
        var result = await _gpsCoordinateService.Create(StartPosition(sdkSiteId, today));

        // Assert
        Assert.That(result.Success, Is.True, result.Message);
        var row = await TimePlanningPnDbContext.PlanRegistrations.AsNoTracking()
            .SingleAsync(x => x.SdkSitId == sdkSiteId && x.Date == today);
        Assert.That(row.SumFlexStartInSeconds, Is.EqualTo(10837));
        Assert.That(row.SumFlexEndInSeconds, Is.EqualTo(10837));
    }

    [Test]
    public async Task Create_RefusesAndCreatesNoRow_WhenTheSiteHasNoTimeRegistration()
    {
        // Act: no AssignedSite for the site.
        var result = await _gpsCoordinateService.Create(StartPosition(57, DateTime.Now.Date));

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("SiteNotFound"));
        Assert.That(await TimePlanningPnDbContext.PlanRegistrations.CountAsync(), Is.EqualTo(0));
        Assert.That(await TimePlanningPnDbContext.GpsCoordinates.CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task Create_RefusesAndCreatesNoRow_WhenTheDayIsMoreThanOneDayFromToday()
    {
        // Arrange
        const int sdkSiteId = 58;
        await SeedAssignedSite(sdkSiteId);

        // Act
        var result = await _gpsCoordinateService.Create(StartPosition(sdkSiteId, DateTime.Now.Date.AddDays(2)));

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("ErrorWhileCreatingGpsCoordinate"));
        Assert.That(await TimePlanningPnDbContext.PlanRegistrations.CountAsync(), Is.EqualTo(0));
        Assert.That(await TimePlanningPnDbContext.GpsCoordinates.CountAsync(), Is.EqualTo(0));
    }

    // The registration the snapshot/position races with can create the day's
    // row between the helper's lookup and its insert. The unique index
    // (SdkSitId, Date, WorkflowState) rejects the second row; the helper must
    // adopt the existing one instead of failing.
    [Test]
    public async Task CreateOrGetExistingDayRow_ReturnsTheConcurrentlyCreatedRow_OnUniqueIndexViolation()
    {
        // Arrange
        const int sdkSiteId = 59;
        var today = DateTime.Now.Date;
        var winner = new PlanRegistration
        {
            Date = today,
            SdkSitId = sdkSiteId,
            PlanHours = 7.5,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await winner.Create(TimePlanningPnDbContext);
        var loser = new PlanRegistration
        {
            Date = today,
            SdkSitId = sdkSiteId,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };

        // Act
        var row = await PlanRegistrationHelper.CreateOrGetExistingDayRowAsync(TimePlanningPnDbContext, loser);

        // Assert
        Assert.That(row.Id, Is.EqualTo(winner.Id));
        Assert.That(
            await TimePlanningPnDbContext.PlanRegistrations.CountAsync(x => x.SdkSitId == sdkSiteId),
            Is.EqualTo(1));
        // The rejected row is no longer tracked, so the context stays usable.
        Assert.That(TimePlanningPnDbContext.Entry(loser).State, Is.EqualTo(EntityState.Detached));
        await new GpsCoordinate
        {
            PlanRegistrationId = row.Id,
            RegistrationType = "Start1StartedAt",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext);
    }
}
