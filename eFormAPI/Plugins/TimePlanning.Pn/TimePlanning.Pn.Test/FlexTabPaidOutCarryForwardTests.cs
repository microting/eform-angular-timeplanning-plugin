using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.eFormApi.BasePn.Infrastructure.Models.Common;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.Flex.Update;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Services.TimePlanningFlexService;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;

namespace TimePlanning.Pn.Test;

/// <summary>
/// R4 on the flex tab: the row the flex tab writes keeps exactly the balance
/// the flex tab gave it (an office-entered start balance included), and every
/// later row — including one pre-created for a future date — carries from
/// that row's SumFlexEnd.
/// </summary>
[TestFixture]
public class FlexTabPaidOutCarryForwardTests : TestBaseSetup
{
    private const int SdkSitId = 7901;
    private ITimePlanningFlexService _service = null!;

    [SetUp]
    public async Task SetUpTest()
    {
        await base.Setup();

        var userService = Substitute.For<IUserService>();
        userService.UserId.Returns(1);
        var localizationService = Substitute.For<ITimePlanningLocalizationService>();
        localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());
        var coreService = Substitute.For<IEFormCoreService>();
        var core = await GetCore();
        coreService.GetCore().Returns(core);
        var options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        options.Value.Returns(new TimePlanningBaseSettings());

        _service = new TimePlanningFlexService(
            Substitute.For<ILogger<TimePlanningFlexService>>(),
            TimePlanningPnDbContext!,
            userService,
            localizationService,
            coreService,
            options);
    }

    private async Task Seed(DateTime date, double planHours, double nettoHours, double sumFlexStart, double sumFlexEnd) =>
        await new PlanRegistration
        {
            SdkSitId = SdkSitId,
            Date = date,
            PlanHours = planHours,
            NettoHours = nettoHours,
            Flex = nettoHours - planHours,
            SumFlexStart = sumFlexStart,
            SumFlexEnd = sumFlexEnd,
            StatusCaseId = 0,
            CommentOffice = "",
            CommentOfficeAll = "",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext!);

    private async Task<PlanRegistration> Stored(DateTime date) =>
        await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .SingleAsync(x => x.SdkSitId == SdkSitId && x.Date == date
                              && x.WorkflowState != Constants.WorkflowStates.Removed);

    private static TimePlanningFlexUpdateModel Entry(DateTime date, double paidOut, double sumFlexStart) => new()
    {
        Date = date,
        Worker = new CommonDictionaryModel { Id = SdkSitId },
        PaidOutFlex = paidOut,
        SumFlexStart = sumFlexStart,
        CommentOffice = "flex tab",
        CommentOfficeAll = "flex tab"
    };

    [Test]
    public async Task PayingOutFlexOnAnExistingDay_KeepsThatDaysBalance_AndCarriesItThroughAFutureRow()
    {
        var d0 = DateTime.Now.Date.AddDays(-6);
        var d1 = DateTime.Now.Date.AddDays(-5);
        var future = DateTime.Now.Date.AddDays(25);
        await Seed(d0, planHours: 8, nettoHours: 8, sumFlexStart: 2, sumFlexEnd: 2);
        await Seed(d1, planHours: 7, nettoHours: 8, sumFlexStart: 2, sumFlexEnd: 3);
        await Seed(future, planHours: 0, nettoHours: 0, sumFlexStart: 3, sumFlexEnd: 3);

        var result = await _service.UpdateCreate(new List<TimePlanningFlexUpdateModel> { Entry(d1, paidOut: 1, sumFlexStart: 2) });

        Assert.That(result.Success, Is.True, result.Message);
        var paid = await Stored(d1);
        var last = await Stored(future);
        Assert.Multiple(() =>
        {
            Assert.That(paid.SumFlexStart, Is.EqualTo(2.0).Within(1e-9), "the edited row's start is left as it was");
            Assert.That(paid.SumFlexEnd, Is.EqualTo(2.0).Within(1e-9), "3 h minus the 1 h paid out, as the flex tab wrote it");
            Assert.That(last.SumFlexStart, Is.EqualTo(paid.SumFlexEnd).Within(1e-9),
                "SumFlexStart(n+1) == SumFlexEnd(n) through the future row");
            Assert.That(last.SumFlexEnd, Is.EqualTo(2.0).Within(1e-9));
        });
    }

    [Test]
    public async Task CreatingAFlexRowWithAnOfficeStartBalance_KeepsIt_AndCarriesItThroughAFutureRow()
    {
        var d0 = DateTime.Now.Date.AddDays(-6);
        var d1 = DateTime.Now.Date.AddDays(-5); // no row yet: the flex tab creates it
        var future = DateTime.Now.Date.AddDays(25);
        await Seed(d0, planHours: 8, nettoHours: 8, sumFlexStart: 2, sumFlexEnd: 2);
        await Seed(future, planHours: 0, nettoHours: 0, sumFlexStart: 2, sumFlexEnd: 2);

        // The office enters a start balance of 10 h (differs from d0's 2 h) and pays out 1 h.
        var result = await _service.UpdateCreate(new List<TimePlanningFlexUpdateModel> { Entry(d1, paidOut: 1, sumFlexStart: 10) });

        Assert.That(result.Success, Is.True, result.Message);
        var created = await Stored(d1);
        var last = await Stored(future);
        Assert.Multiple(() =>
        {
            Assert.That(created.SumFlexEnd, Is.EqualTo(9.0).Within(1e-9),
                "the office-entered start balance minus the payout is preserved, not re-carried from d0");
            Assert.That(created.PaiedOutFlex, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(last.SumFlexStart, Is.EqualTo(created.SumFlexEnd).Within(1e-9),
                "later rows carry from the created row");
            Assert.That(last.SumFlexEnd, Is.EqualTo(9.0).Within(1e-9));
        });
    }
}
