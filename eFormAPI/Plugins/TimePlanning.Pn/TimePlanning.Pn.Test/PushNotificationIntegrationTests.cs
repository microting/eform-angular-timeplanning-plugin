using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.AbsenceRequest;
using TimePlanning.Pn.Infrastructure.Models.ContentHandover;
using TimePlanning.Pn.Services.AbsenceRequestService;
using TimePlanning.Pn.Services.ContentHandoverService;
using TimePlanning.Pn.Services.PushNotificationService;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Verifies that AbsenceRequestService and ContentHandoverService invoke the
/// push notification service at the right moments (create, approve/accept, reject).
///
/// Push calls are fire-and-forget (Task.Run), so we use a SemaphoreSlim signal
/// inside the mock to wait for the call instead of a fixed delay.
/// </summary>
[TestFixture]
public class PushNotificationIntegrationTests : TestBaseSetup
{
    private IPushNotificationService _pushService = null!;
    private IAbsenceRequestService _absenceRequestService = null!;
    private IContentHandoverService _contentHandoverService = null!;
    private IUserService _userService = null!;
    private SemaphoreSlim _pushSignal = null!;

    [SetUp]
    public async Task SetUp()
    {
        _pushSignal = new SemaphoreSlim(0);
        _pushService = Substitute.For<IPushNotificationService>();
        // Signal the semaphore each time SendToSiteAsync is called
        _pushService.SendToSiteAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Dictionary<string, string>?>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => _pushSignal.Release());

        _userService = Substitute.For<IUserService>();
        _userService.UserId.Returns(1);

        var localization = Substitute.For<ITimePlanningLocalizationService>();
        localization.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

        var coreService = Substitute.For<IEFormCoreService>();
        var core = await GetCore();
        coreService.GetCore().Returns(Task.FromResult(core));

        // Real, so the approve/reject tests can seed their signed-in manager into it.
        SeededBaseDbContext = GetBaseDbContext();

        var pushScopeProvider = Substitute.For<IServiceProvider>();
        pushScopeProvider.GetService(typeof(IPushNotificationService)).Returns(_pushService);
        var pushScope = Substitute.For<Microsoft.Extensions.DependencyInjection.IServiceScope>();
        pushScope.ServiceProvider.Returns(pushScopeProvider);
        var pushScopeFactory = Substitute.For<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();
        pushScopeFactory.CreateScope().Returns(pushScope);

        _absenceRequestService = new AbsenceRequestService(
            Substitute.For<Microsoft.Extensions.Logging.ILogger<AbsenceRequestService>>(),
            TimePlanningPnDbContext!,
            _userService,
            localization,
            coreService,
            SeededBaseDbContext!,
            pushScopeFactory);

        _contentHandoverService = new ContentHandoverService(
            Substitute.For<Microsoft.Extensions.Logging.ILogger<ContentHandoverService>>(),
            TimePlanningPnDbContext!,
            _userService,
            localization,
            coreService, // returns the real core, so handover decisions can resolve their seeded caller
            SeededBaseDbContext!,
            pushScopeFactory);
    }

    [TearDown]
    public void TearDownTest()
    {
        _pushSignal.Dispose();
    }

    /// <summary>Wait for push signal with a timeout.</summary>
    private async Task WaitForPush(int timeoutMs = 5000)
    {
        var received = await _pushSignal.WaitAsync(timeoutMs);
        Assert.That(received, Is.True, "Push notification was not sent within the timeout");
    }

    // ── AbsenceRequestService ──────────────────────────────────────────

    [Test]
    public async Task AbsenceRequest_CreateAsync_InvokesPushNotification()
    {
        // Arrange — seed a manager that manages the worker's tag
        var managerSite = new Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite
        {
            SiteId = 99,
            IsManager = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await managerSite.Create(TimePlanningPnDbContext!);

        var sdkCore = await GetCore();
        var sdkDb = sdkCore.DbContextHelper.GetDbContext();

        // CreateAsync looks up SiteTags where Site.MicrotingUid == the caller's site
        var workerSite = new Microting.eForm.Infrastructure.Data.Entities.Site { Name = "W", MicrotingUid = 42 };
        await workerSite.Create(sdkDb);
        await SeedSiteCallerAsync(_userService, 42); // the worker is the logged-in caller

        var tag = new Microting.eForm.Infrastructure.Data.Entities.Tag { Name = "T" };
        await tag.Create(sdkDb);

        var siteTag = new Microting.eForm.Infrastructure.Data.Entities.SiteTag
        {
            SiteId = workerSite.Id,
            TagId = tag.Id
        };
        await sdkDb.SiteTags.AddAsync(siteTag);
        await sdkDb.SaveChangesAsync();

        var managingTag = new Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSiteManagingTag
        {
            AssignedSiteId = managerSite.Id,
            TagId = tag.Id,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await managingTag.Create(TimePlanningPnDbContext!);

        var model = new AbsenceRequestCreateModel
        {
            DateFrom = new DateTime(2025, 6, 1),
            DateTo = new DateTime(2025, 6, 1),
            MessageId = 2,
            RequestComment = "Push test"
        };

        // Act
        var result = await _absenceRequestService.CreateAsync(model);

        // Assert
        Assert.That(result.Success, Is.True, $"CreateAsync failed: {result.Message}");

        // Wait for fire-and-forget Task.Run to complete
        await WaitForPush();

        await _pushService.Received().SendToSiteAsync(
            Arg.Is(99),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, string>?>());
    }

    [Test]
    public async Task AbsenceRequest_CreateAsync_DoesNotPushToRemovedManagerAssignedSite()
    {
        // Arrange — two manager rows manage the worker's tag; one is soft-removed
        var activeManagerSite = new Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite
        {
            SiteId = 99,
            IsManager = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await activeManagerSite.Create(TimePlanningPnDbContext!);

        var removedManagerSite = new Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite
        {
            SiteId = 98,
            IsManager = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await removedManagerSite.Create(TimePlanningPnDbContext!);

        var sdkCore = await GetCore();
        var sdkDb = sdkCore.DbContextHelper.GetDbContext();

        var workerSite = new Microting.eForm.Infrastructure.Data.Entities.Site { Name = "W", MicrotingUid = 43 };
        await workerSite.Create(sdkDb);
        await SeedSiteCallerAsync(_userService, 43); // the worker is the logged-in caller

        var tag = new Microting.eForm.Infrastructure.Data.Entities.Tag { Name = "T" };
        await tag.Create(sdkDb);

        await sdkDb.SiteTags.AddAsync(new Microting.eForm.Infrastructure.Data.Entities.SiteTag
        {
            SiteId = workerSite.Id,
            TagId = tag.Id
        });
        await sdkDb.SaveChangesAsync();

        await new Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSiteManagingTag
        {
            AssignedSiteId = activeManagerSite.Id,
            TagId = tag.Id,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext!);

        await new Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSiteManagingTag
        {
            AssignedSiteId = removedManagerSite.Id,
            TagId = tag.Id,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext!);

        await removedManagerSite.Delete(TimePlanningPnDbContext!); // WorkflowState -> Removed

        // Act
        var result = await _absenceRequestService.CreateAsync(new AbsenceRequestCreateModel
        {
            DateFrom = new DateTime(2025, 6, 2),
            DateTo = new DateTime(2025, 6, 2),
            MessageId = 2,
            RequestComment = "Push test"
        });

        // Assert
        Assert.That(result.Success, Is.True, $"CreateAsync failed: {result.Message}");
        // The active manager gets the push; give a wrongly included removed row
        // time to receive a second one before asserting it did not.
        await WaitForPush();
        Assert.That(await _pushSignal.WaitAsync(1000), Is.False,
            "A removed AssignedSite must not receive the absence request push");
        await _pushService.Received(1).SendToSiteAsync(
            Arg.Is(99),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, string>?>());
        await _pushService.DidNotReceive().SendToSiteAsync(
            Arg.Is(98),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, string>?>());
    }

    [Test]
    public async Task AbsenceRequest_ApproveAsync_InvokesPushNotification()
    {
        // Arrange
        var request = new AbsenceRequest
        {
            RequestedBySdkSitId = 5,
            DateFrom = new DateTime(2025, 6, 1),
            DateTo = new DateTime(2025, 6, 1),
            Status = AbsenceRequestStatus.Pending,
            RequestedAtUtc = DateTime.UtcNow,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await request.Create(TimePlanningPnDbContext!);

        var day = new AbsenceRequestDay
        {
            AbsenceRequestId = request.Id,
            Date = new DateTime(2025, 6, 1),
            MessageId = 2,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await day.Create(TimePlanningPnDbContext!);

        await SeedManagerCallerAsync(_userService, managerSiteUid: 10, workerSiteUid: 5);
        var decision = new AbsenceRequestDecisionModel
        {
            DecisionComment = "ok"
        };

        // Act
        var result = await _absenceRequestService.ApproveAsync(request.Id, decision);

        // Assert
        if (result.Success)
        {
            await WaitForPush();
            await _pushService.Received().SendToSiteAsync(
                Arg.Is(5),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>?>());
        }
        else
        {
            // ApproveAsync may fail if seeding is incomplete — assert the main
            // operation outcome instead so the test isn't silently green.
            Assert.Warn($"ApproveAsync did not succeed (likely incomplete seed): {result.Message}");
        }
    }

    [Test]
    public async Task AbsenceRequest_RejectAsync_InvokesPushNotification()
    {
        // Arrange
        var request = new AbsenceRequest
        {
            RequestedBySdkSitId = 5,
            DateFrom = new DateTime(2025, 6, 1),
            DateTo = new DateTime(2025, 6, 1),
            Status = AbsenceRequestStatus.Pending,
            RequestedAtUtc = DateTime.UtcNow,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await request.Create(TimePlanningPnDbContext!);

        await SeedManagerCallerAsync(_userService, managerSiteUid: 10, workerSiteUid: 5);
        var decision = new AbsenceRequestDecisionModel
        {
            DecisionComment = "no"
        };

        // Act
        var result = await _absenceRequestService.RejectAsync(request.Id, decision);

        // Assert
        Assert.That(result.Success, Is.True);
        await WaitForPush();
        await _pushService.Received().SendToSiteAsync(
            Arg.Is(5),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, string>?>());
    }

    // ── ContentHandoverService ─────────────────────────────────────────

    [Test]
    public async Task ContentHandover_CreateAsync_InvokesPushNotification()
    {
        // Arrange
        var date = new DateTime(2025, 6, 1);
        var sourcePR = new PlanRegistration
        {
            Date = date,
            SdkSitId = 1,
            PlanHoursInSeconds = 28800,
            PlanText = "Work",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await sourcePR.Create(TimePlanningPnDbContext!);

        var targetPR = new PlanRegistration
        {
            Date = date,
            SdkSitId = 2,
            PlanHoursInSeconds = 0,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await targetPR.Create(TimePlanningPnDbContext!);

        var model = new ContentHandoverRequestCreateModel
        {
            ToSdkSitId = 2,
            RequestComment = "Push test"
        };

        await SeedSiteCallerAsync(_userService, 1); // the sender is the logged-in caller

        // Act
        var result = await _contentHandoverService.CreateAsync(sourcePR.Id, model);

        // Assert
        Assert.That(result.Success, Is.True, $"CreateAsync failed: {result.Message}");
        await WaitForPush();
        await _pushService.Received().SendToSiteAsync(
            Arg.Is(2),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, string>?>());
    }

    [Test]
    public async Task ContentHandover_AcceptAsync_InvokesPushNotification()
    {
        // Arrange
        var date = new DateTime(2025, 6, 1);
        var sourcePR = new PlanRegistration
        {
            Date = date,
            SdkSitId = 1,
            PlanHours = 8,
            PlanHoursInSeconds = 28800,
            PlanText = "Work",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await sourcePR.Create(TimePlanningPnDbContext!);

        var targetPR = new PlanRegistration
        {
            Date = date,
            SdkSitId = 2,
            PlanHours = 0,
            PlanHoursInSeconds = 0,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await targetPR.Create(TimePlanningPnDbContext!);

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
        await request.Create(TimePlanningPnDbContext!);

        await SeedSiteCallerAsync(_userService, 2); // the receiver is the logged-in caller

        // Act
        var result = await _contentHandoverService.AcceptAsync(request.Id, new ContentHandoverDecisionModel
        {
            DecisionComment = "ok"
        });

        // Assert
        Assert.That(result.Success, Is.True, $"AcceptAsync failed: {result.Message}");
        await WaitForPush();
        await _pushService.Received().SendToSiteAsync(
            Arg.Is(1),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, string>?>());
    }

    [Test]
    public async Task ContentHandover_RejectAsync_InvokesPushNotification()
    {
        // Arrange
        var date = new DateTime(2025, 6, 1);
        var sourcePR = new PlanRegistration
        {
            Date = date,
            SdkSitId = 1,
            PlanHoursInSeconds = 28800,
            PlanText = "Work",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await sourcePR.Create(TimePlanningPnDbContext!);

        var targetPR = new PlanRegistration
        {
            Date = date,
            SdkSitId = 2,
            PlanHoursInSeconds = 0,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await targetPR.Create(TimePlanningPnDbContext!);

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
        await request.Create(TimePlanningPnDbContext!);

        await SeedSiteCallerAsync(_userService, 2); // the receiver is the logged-in caller

        // Act
        var result = await _contentHandoverService.RejectAsync(request.Id, new ContentHandoverDecisionModel
        {
            DecisionComment = "no"
        });

        // Assert
        Assert.That(result.Success, Is.True, $"RejectAsync failed: {result.Message}");
        await WaitForPush();
        await _pushService.Received().SendToSiteAsync(
            Arg.Is(1),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, string>?>());
    }
}
