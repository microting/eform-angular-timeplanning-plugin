using System;
using System.Threading.Tasks;
using BackendConfiguration.Pn.Integration.Test;
using Microsoft.EntityFrameworkCore;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.ContentHandover;
using TimePlanning.Pn.Services.ContentHandoverService;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class ContentHandoverServiceTests : TestBaseSetup
{
    private IContentHandoverService _contentHandoverService;
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

        _contentHandoverService = new ContentHandoverService(
            Substitute.For<Microsoft.Extensions.Logging.ILogger<ContentHandoverService>>(),
            TimePlanningPnDbContext,
            _userService,
            _localizationService);
    }

    [Test]
    public async Task CreateAsync_CreatesHandoverRequest_WhenSourceHasContent()
    {
        // Arrange
        var date = new DateTime(2024, 1, 1);
        var sourcePR = new PlanRegistration
        {
            Date = date,
            SdkSitId = 1,
            PlanHoursInSeconds = 28800, // 8 hours
            PlanText = "Important work",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await sourcePR.Create(TimePlanningPnDbContext);

        var targetPR = new PlanRegistration
        {
            Date = date,
            SdkSitId = 2,
            PlanHoursInSeconds = 0,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await targetPR.Create(TimePlanningPnDbContext);

        var model = new ContentHandoverRequestCreateModel
        {
            ToSdkSitId = 2,
            RequestComment = "Need to transfer work"
        };

        // Act
        var result = await _contentHandoverService.CreateAsync(sourcePR.Id, model);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Status, Is.EqualTo("Pending"));
        Assert.That(result.Model.FromSdkSitId, Is.EqualTo(1));
        Assert.That(result.Model.ToSdkSitId, Is.EqualTo(2));
    }

    [Test]
    public async Task CreateAsync_Fails_WhenSourceHasNoContent()
    {
        // Arrange
        var date = new DateTime(2024, 1, 1);
        var sourcePR = new PlanRegistration
        {
            Date = date,
            SdkSitId = 1,
            PlanHoursInSeconds = 0,
            PlanText = null,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await sourcePR.Create(TimePlanningPnDbContext);

        var targetPR = new PlanRegistration
        {
            Date = date,
            SdkSitId = 2,
            PlanHoursInSeconds = 0,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await targetPR.Create(TimePlanningPnDbContext);

        var model = new ContentHandoverRequestCreateModel
        {
            ToSdkSitId = 2
        };

        // Act
        var result = await _contentHandoverService.CreateAsync(sourcePR.Id, model);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("SourcePlanRegistrationHasNoContent"));
    }

    [Test]
    public async Task AcceptAsync_MovesContent_FromSourceToTarget()
    {
        // Arrange
        var date = new DateTime(2024, 1, 1);
        var sourcePR = new PlanRegistration
        {
            Date = date,
            SdkSitId = 1,
            PlanHours = 8,
            PlanHoursInSeconds = 28800,
            PlanText = "Important work",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await sourcePR.Create(TimePlanningPnDbContext);

        var targetPR = new PlanRegistration
        {
            Date = date,
            SdkSitId = 2,
            PlanHours = 0,
            PlanHoursInSeconds = 0,
            PlanText = null,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await targetPR.Create(TimePlanningPnDbContext);

        var request = new PlanRegistrationContentHandoverRequest
        {
            FromSdkSitId = 1,
            ToSdkSitId = 2,
            Date = date,
            FromPlanRegistrationId = sourcePR.Id,
            ToPlanRegistrationId = targetPR.Id,
            Status = HandoverRequestStatus.Pending,
            RequestedAtUtc = DateTime.UtcNow,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await request.Create(TimePlanningPnDbContext);

        var model = new ContentHandoverDecisionModel
        {
            DecisionComment = "Accepted"
        };

        // Act
        var result = await _contentHandoverService.AcceptAsync(request.Id, 2, model);

        // Assert
        Console.WriteLine($"Result Success: {result.Success}, Message: {result.Message}");
        Assert.That(result.Success, Is.True, $"Expected success but got error: {result.Message}");

        // Verify content moved
        var updatedSource = await TimePlanningPnDbContext.PlanRegistrations.FindAsync(sourcePR.Id);
        var updatedTarget = await TimePlanningPnDbContext.PlanRegistrations.FindAsync(targetPR.Id);

        Assert.That(updatedSource.PlanHoursInSeconds, Is.EqualTo(0));
        Assert.That(updatedSource.PlanText, Is.Null);

        Assert.That(updatedTarget.PlanHoursInSeconds, Is.EqualTo(28800));
        Assert.That(updatedTarget.PlanText, Is.EqualTo("Important work"));

        // Verify request status
        var updatedRequest = await TimePlanningPnDbContext.PlanRegistrationContentHandoverRequests.FindAsync(request.Id);
        Assert.That(updatedRequest.Status, Is.EqualTo(HandoverRequestStatus.Accepted));
    }

    [Test]
    public async Task AcceptAsync_Fails_WhenTargetHasContent()
    {
        // Arrange
        var date = new DateTime(2024, 1, 1);
        var sourcePR = new PlanRegistration
        {
            Date = date,
            SdkSitId = 1,
            PlanHours = 8,
            PlanHoursInSeconds = 28800,
            PlanText = "Important work",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await sourcePR.Create(TimePlanningPnDbContext);

        var targetPR = new PlanRegistration
        {
            Date = date,
            SdkSitId = 2,
            PlanHours = 4,
            PlanHoursInSeconds = 14400, // Target has content
            PlanText = "Existing work",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await targetPR.Create(TimePlanningPnDbContext);

        var request = new PlanRegistrationContentHandoverRequest
        {
            FromSdkSitId = 1,
            ToSdkSitId = 2,
            Date = date,
            FromPlanRegistrationId = sourcePR.Id,
            ToPlanRegistrationId = targetPR.Id,
            Status = HandoverRequestStatus.Pending,
            RequestedAtUtc = DateTime.UtcNow,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await request.Create(TimePlanningPnDbContext);

        var model = new ContentHandoverDecisionModel
        {
            DecisionComment = "Accepted"
        };

        // Act
        var result = await _contentHandoverService.AcceptAsync(request.Id, 2, model);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("TargetPlanRegistrationMustBeEmpty"));
    }

    [Test]
    public async Task RejectAsync_ChangesStatus_WithoutMovingContent()
    {
        // Arrange
        var date = new DateTime(2024, 1, 1);
        var sourcePR = new PlanRegistration
        {
            Date = date,
            SdkSitId = 1,
            PlanHoursInSeconds = 28800,
            PlanText = "Important work",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await sourcePR.Create(TimePlanningPnDbContext);

        var targetPR = new PlanRegistration
        {
            Date = date,
            SdkSitId = 2,
            PlanHoursInSeconds = 0,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await targetPR.Create(TimePlanningPnDbContext);

        var request = new PlanRegistrationContentHandoverRequest
        {
            FromSdkSitId = 1,
            ToSdkSitId = 2,
            Date = date,
            FromPlanRegistrationId = sourcePR.Id,
            ToPlanRegistrationId = targetPR.Id,
            Status = HandoverRequestStatus.Pending,
            RequestedAtUtc = DateTime.UtcNow,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await request.Create(TimePlanningPnDbContext);

        var model = new ContentHandoverDecisionModel
        {
            DecisionComment = "Rejected"
        };

        // Act
        var result = await _contentHandoverService.RejectAsync(request.Id, 2, model);

        // Assert
        Assert.That(result.Success, Is.True);

        // Verify status changed
        var updatedRequest = await TimePlanningPnDbContext.PlanRegistrationContentHandoverRequests.FindAsync(request.Id);
        Assert.That(updatedRequest.Status, Is.EqualTo(HandoverRequestStatus.Rejected));

        // Verify content not moved
        var updatedSource = await TimePlanningPnDbContext.PlanRegistrations.FindAsync(sourcePR.Id);
        var updatedTarget = await TimePlanningPnDbContext.PlanRegistrations.FindAsync(targetPR.Id);

        Assert.That(updatedSource.PlanHoursInSeconds, Is.EqualTo(28800));
        Assert.That(updatedTarget.PlanHoursInSeconds, Is.EqualTo(0));
    }

    [Test]
    public async Task CancelAsync_ChangesStatus_WhenRequestedBySourceWorker()
    {
        // Arrange
        var date = new DateTime(2024, 1, 1);
        var sourcePR = new PlanRegistration
        {
            Date = date,
            SdkSitId = 1,
            PlanHoursInSeconds = 28800,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await sourcePR.Create(TimePlanningPnDbContext);

        var targetPR = new PlanRegistration
        {
            Date = date,
            SdkSitId = 2,
            PlanHoursInSeconds = 0,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await targetPR.Create(TimePlanningPnDbContext);

        var request = new PlanRegistrationContentHandoverRequest
        {
            FromSdkSitId = 1,
            ToSdkSitId = 2,
            Date = date,
            FromPlanRegistrationId = sourcePR.Id,
            ToPlanRegistrationId = targetPR.Id,
            Status = HandoverRequestStatus.Pending,
            RequestedAtUtc = DateTime.UtcNow,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await request.Create(TimePlanningPnDbContext);

        // Act
        var result = await _contentHandoverService.CancelAsync(request.Id, 1);

        // Assert
        Assert.That(result.Success, Is.True);

        var updatedRequest = await TimePlanningPnDbContext.PlanRegistrationContentHandoverRequests.FindAsync(request.Id);
        Assert.That(updatedRequest.Status, Is.EqualTo(HandoverRequestStatus.Cancelled));
    }

    [Test]
    public async Task GetInboxAsync_ReturnsPendingRequestsForReceiver()
    {
        // Arrange
        var date = new DateTime(2024, 1, 1);
        var sourcePR = new PlanRegistration
        {
            Date = date,
            SdkSitId = 1,
            PlanHoursInSeconds = 28800,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await sourcePR.Create(TimePlanningPnDbContext);

        var targetPR = new PlanRegistration
        {
            Date = date,
            SdkSitId = 2,
            PlanHoursInSeconds = 0,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await targetPR.Create(TimePlanningPnDbContext);

        var request = new PlanRegistrationContentHandoverRequest
        {
            FromSdkSitId = 1,
            ToSdkSitId = 2,
            Date = date,
            FromPlanRegistrationId = sourcePR.Id,
            ToPlanRegistrationId = targetPR.Id,
            Status = HandoverRequestStatus.Pending,
            RequestedAtUtc = DateTime.UtcNow,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await request.Create(TimePlanningPnDbContext);

        // Act
        var result = await _contentHandoverService.GetInboxAsync(2);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model.Count, Is.EqualTo(1));
        Assert.That(result.Model[0].Status, Is.EqualTo("Pending"));
    }

    [Test]
    public async Task GetMineAsync_ReturnsRequestsFromSender()
    {
        // Arrange
        var date = new DateTime(2024, 1, 1);
        var sourcePR = new PlanRegistration
        {
            Date = date,
            SdkSitId = 1,
            PlanHoursInSeconds = 28800,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await sourcePR.Create(TimePlanningPnDbContext);

        var targetPR = new PlanRegistration
        {
            Date = date,
            SdkSitId = 2,
            PlanHoursInSeconds = 0,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await targetPR.Create(TimePlanningPnDbContext);

        var request = new PlanRegistrationContentHandoverRequest
        {
            FromSdkSitId = 1,
            ToSdkSitId = 2,
            Date = date,
            FromPlanRegistrationId = sourcePR.Id,
            ToPlanRegistrationId = targetPR.Id,
            Status = HandoverRequestStatus.Pending,
            RequestedAtUtc = DateTime.UtcNow,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await request.Create(TimePlanningPnDbContext);

        // Act
        var result = await _contentHandoverService.GetMineAsync(1);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model.Count, Is.EqualTo(1));
        Assert.That(result.Model[0].FromSdkSitId, Is.EqualTo(1));
    }
}
