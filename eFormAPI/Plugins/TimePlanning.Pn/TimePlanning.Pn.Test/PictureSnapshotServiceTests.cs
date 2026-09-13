using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using eFormCore;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NSubstitute.Extensions;
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
    private Microsoft.Extensions.Logging.ILogger<TimePlanningPictureSnapshotService> _logger;

    [SetUp]
    public async Task SetUp()
    {
        await base.Setup();
        _userService = Substitute.For<IUserService>();
        _userService.UserId.Returns(1);

        _localizationService = Substitute.For<ITimePlanningLocalizationService>();
        _localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

        _coreService = Substitute.For<IEFormCoreService>();
        _logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<TimePlanningPictureSnapshotService>>();

        _pictureSnapshotService = new TimePlanningPictureSnapshotService(
            _logger,
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
            SdkSiteId = 1,
            Date = planRegistration.Date,
            PictureHash = "test-hash-123",
            RegistrationType = "CheckIn"
        };

        // Act
        var result = await _pictureSnapshotService.Create(model, null, null);

        // Assert
        Assert.That(result.Success, Is.False);
    }

    // SQL/420_SDK.sql seeds s3Enabled = False, so the SDK has no S3 client and the
    // upload fails without a network call (SDK 10.0.39+ no longer swallows it).
    [Test]
    public async Task Create_ReturnsFailureAndSavesNoSnapshot_WhenFileUploadFails()
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
        var mockFile = Substitute.For<IFormFile>();
        mockFile.Length.Returns(1024);
        mockFile.FileName.Returns("test.jpg");
        mockFile.ContentType.Returns("image/jpeg");

        var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes("test file content"));
        mockFile.OpenReadStream().Returns(memoryStream);
        mockFile.CopyToAsync(Arg.Any<Stream>()).Returns(Task.CompletedTask);

        var core = await GetCore();

        _coreService.GetCore().Returns(Task.FromResult(core));


        var model = new PictureSnapshotCreateModel
        {
            SdkSiteId = 1,
            Date = planRegistration.Date,
            PictureHash = "test-hash-123",
            RegistrationType = "CheckIn",
            FileName = "test.jpg"
        };

        // Act
        var result = await _pictureSnapshotService.Create(model, mockFile, null);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(
            await TimePlanningPnDbContext.PictureSnapshots.CountAsync(x => x.PlanRegistrationId == planRegistration.Id),
            Is.EqualTo(0),
            "a snapshot row must not point at a file that was never stored");
        // The upload itself failed for lack of an S3 client, not an earlier lookup;
        // a real S3 call would fail with an AmazonS3Exception instead.
        _logger.Received(1).Log(
            Microsoft.Extensions.Logging.LogLevel.Error,
            Arg.Any<Microsoft.Extensions.Logging.EventId>(),
            Arg.Any<Arg.AnyType>(),
            Arg.Is<Exception>(e => e is InvalidOperationException),
            Arg.Any<Func<Arg.AnyType, Exception, string>>());
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
            SdkSiteId = 1,
            Date = planRegistration.Date,
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
