using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using BackendConfiguration.Pn.Integration.Test;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.PictureSnapshot;
using TimePlanning.Pn.Services.TimePlanningPictureSnapshotService;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class PictureSnapshotServiceTests : TestBaseSetup
{
    private ITimePlanningPictureSnapshotService _pictureSnapshotService;
    private IUserService _userService;
    private ITimePlanningLocalizationService _localizationService;
    private IEFormCoreService _coreService;

    [SetUp]
    public async Task SetUp()
    {
        await base.Setup();
        _userService = Substitute.For<IUserService>();
        _userService.UserId.Returns(1);
        
        _localizationService = Substitute.For<ITimePlanningLocalizationService>();
        _localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());
        
        _coreService = Substitute.For<IEFormCoreService>();
        
        _pictureSnapshotService = new TimePlanningPictureSnapshotService(
            Substitute.For<Microsoft.Extensions.Logging.ILogger<TimePlanningPictureSnapshotService>>(),
            TimePlanningPnDbContext,
            _userService,
            _localizationService,
            _coreService);
    }

    [Test]
    public async Task Create_CreatesPictureSnapshot_WithoutFile()
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

        var model = new PictureSnapshotCreateModel
        {
            PlanRegistrationId = planRegistration.Id,
            PictureHash = "test-hash-123",
            RegistrationType = "CheckIn"
        };

        // Act
        var result = await _pictureSnapshotService.Create(model);

        // Assert
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task GetById_ReturnsPictureSnapshot_WhenExists()
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

        var pictureSnapshot = new PictureSnapshot
        {
            PlanRegistrationId = planRegistration.Id,
            PictureHash = "test-hash-123",
            RegistrationType = "CheckIn",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await pictureSnapshot.Create(TimePlanningPnDbContext);

        // Act
        var result = await _pictureSnapshotService.GetById(pictureSnapshot.Id);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Id, Is.EqualTo(pictureSnapshot.Id));
        Assert.That(result.Model.PictureHash, Is.EqualTo("test-hash-123"));
    }

    [Test]
    public async Task Index_ReturnsPictureSnapshots_ForPlanRegistration()
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

        var pictureSnapshot1 = new PictureSnapshot
        {
            PlanRegistrationId = planRegistration.Id,
            PictureHash = "test-hash-1",
            RegistrationType = "CheckIn",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await pictureSnapshot1.Create(TimePlanningPnDbContext);

        var pictureSnapshot2 = new PictureSnapshot
        {
            PlanRegistrationId = planRegistration.Id,
            PictureHash = "test-hash-2",
            RegistrationType = "CheckOut",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await pictureSnapshot2.Create(TimePlanningPnDbContext);

        // Act
        var result = await _pictureSnapshotService.Index(planRegistration.Id);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Update_UpdatesPictureSnapshot_Successfully()
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

        var pictureSnapshot = new PictureSnapshot
        {
            PlanRegistrationId = planRegistration.Id,
            PictureHash = "test-hash-123",
            RegistrationType = "CheckIn",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await pictureSnapshot.Create(TimePlanningPnDbContext);

        var updateModel = new PictureSnapshotUpdateModel
        {
            Id = pictureSnapshot.Id,
            PlanRegistrationId = planRegistration.Id,
            PictureHash = "updated-hash-456",
            RegistrationType = "CheckOut"
        };

        // Act
        var result = await _pictureSnapshotService.Update(updateModel);

        // Assert
        Assert.That(result.Success, Is.True);

        var updated = await _pictureSnapshotService.GetById(pictureSnapshot.Id);
        Assert.That(updated.Model.PictureHash, Is.EqualTo("updated-hash-456"));
        Assert.That(updated.Model.RegistrationType, Is.EqualTo("CheckOut"));
    }

    [Test]
    public async Task Delete_DeletesPictureSnapshot_Successfully()
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

        var pictureSnapshot = new PictureSnapshot
        {
            PlanRegistrationId = planRegistration.Id,
            PictureHash = "test-hash-123",
            RegistrationType = "CheckIn",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await pictureSnapshot.Create(TimePlanningPnDbContext);

        // Act
        var result = await _pictureSnapshotService.Delete(pictureSnapshot.Id);

        // Assert
        Assert.That(result.Success, Is.True);

        var getResult = await _pictureSnapshotService.GetById(pictureSnapshot.Id);
        Assert.That(getResult.Success, Is.False);
    }
}
