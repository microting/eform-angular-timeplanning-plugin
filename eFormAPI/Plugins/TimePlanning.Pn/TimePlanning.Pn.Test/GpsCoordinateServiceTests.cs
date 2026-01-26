using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BackendConfiguration.Pn.Integration.Test;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
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
}
