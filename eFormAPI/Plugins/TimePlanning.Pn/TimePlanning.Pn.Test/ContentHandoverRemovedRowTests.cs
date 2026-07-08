using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.EformAngularFrontendBase.Infrastructure.Data;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.ContentHandover;
using TimePlanning.Pn.Services.ContentHandoverService;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Regression coverage for ContentHandover treating a soft-removed
/// PlanRegistration as still-present. CreateAsync loaded the source PR by Id
/// with no WorkflowState filter, so a handover could be created from a shift
/// that had been removed. The fix routes a Removed hit into the existing
/// "not found" path.
///
/// Structured to FAIL pre-fix (removed source PR is loaded, so the flow moves
/// past the source guard) and PASS post-fix (SourcePlanRegistrationNotFound).
/// </summary>
[TestFixture]
public class ContentHandoverRemovedRowTests : TestBaseSetup
{
    private IContentHandoverService _service;

    [SetUp]
    public async Task SetUpTest()
    {
        await base.Setup();

        var userService = Substitute.For<IUserService>();
        userService.UserId.Returns(1);

        var localizationService = Substitute.For<ITimePlanningLocalizationService>();
        localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

        _service = new ContentHandoverService(
            Substitute.For<Microsoft.Extensions.Logging.ILogger<ContentHandoverService>>(),
            TimePlanningPnDbContext,
            userService,
            localizationService,
            Substitute.For<IEFormCoreService>(),
            Substitute.For<BaseDbContext>(new DbContextOptions<BaseDbContext>()),
            Substitute.For<TimePlanning.Pn.Services.PushNotificationService.IPushNotificationService>());
    }

    [Test]
    public async Task CreateAsync_TreatsRemovedSourceAsNotFound()
    {
        var date = new DateTime(2024, 2, 1);
        var source = new PlanRegistration
        {
            Date = date,
            SdkSitId = 1,
            PlanHoursInSeconds = 28800,
            PlanText = "Removed shift",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await source.Create(TimePlanningPnDbContext);
        await source.Delete(TimePlanningPnDbContext); // WorkflowState -> Removed

        var model = new ContentHandoverRequestCreateModel
        {
            ToSdkSitId = 2,
            ShiftIndices = new List<int> { 1 }
        };

        var result = await _service.CreateAsync(source.Id, model);

        // Post-fix: the removed source PR is invisible -> not-found path.
        // Pre-fix: the source loads and the flow advances past this guard,
        // producing a different message (TargetPlanRegistrationNotFound).
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("SourcePlanRegistrationNotFound"),
            "A removed source PlanRegistration must be treated as not found");
    }
}
