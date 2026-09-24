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
/// R4 on the flex tab. The web client posts `sumFlex`, which does not bind to
/// the server's SumFlexStart, so every flex-tab write arrives with
/// SumFlexStart = 0. The row the flex tab writes is therefore re-carried from
/// its predecessor (SumFlexStart = predecessor.SumFlexEnd, SumFlexEnd = that +
/// netto - plan - paid out), and every later row — including one pre-created
/// for a future date — carries from it. The payloads below are the ones the UI
/// actually sends.
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
    public async Task PayingOutFlexOnAnExistingDay_RecarriesThatDayFromItsPredecessor_AndCarriesItThroughAFutureRow()
    {
        var d0 = DateTime.Now.Date.AddDays(-6);
        var d1 = DateTime.Now.Date.AddDays(-5);
        var future = DateTime.Now.Date.AddDays(25);
        // d0 ends at X = 5 h. d1 (+1 h worked) holds a stale balance of 1 h, as a
        // zero-start flex-tab create used to leave it; the future row carries from d1.
        await Seed(d0, planHours: 8, nettoHours: 8, sumFlexStart: 5, sumFlexEnd: 5);
        await Seed(d1, planHours: 7, nettoHours: 8, sumFlexStart: 0, sumFlexEnd: 1);
        await Seed(future, planHours: 0, nettoHours: 0, sumFlexStart: 1, sumFlexEnd: 1);

        // What the UI sends: SumFlexStart never binds, so it is 0.
        var result = await _service.UpdateCreate(new List<TimePlanningFlexUpdateModel> { Entry(d1, paidOut: 1, sumFlexStart: 0) });

        Assert.That(result.Success, Is.True, result.Message);
        var paid = await Stored(d1);
        var last = await Stored(future);
        Assert.Multiple(() =>
        {
            Assert.That(paid.PaiedOutFlex, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(paid.SumFlexStart, Is.EqualTo(5.0).Within(1e-9), "carried from d0's SumFlexEnd");
            Assert.That(paid.SumFlexEnd, Is.EqualTo(5.0).Within(1e-9), "X + netto - plan - paid out = 5 + 8 - 7 - 1");
            Assert.That(last.SumFlexStart, Is.EqualTo(paid.SumFlexEnd).Within(1e-9),
                "SumFlexStart(n+1) == SumFlexEnd(n) through the future row");
            Assert.That(last.SumFlexEnd, Is.EqualTo(5.0).Within(1e-9));
        });
    }

    [Test]
    public async Task CreatingAFlexRowWithTheUiPayload_CarriesThePredecessorsBalance_NotZero()
    {
        var d0 = DateTime.Now.Date.AddDays(-6);
        var d1 = DateTime.Now.Date.AddDays(-5); // no row yet: the flex tab creates it
        var future = DateTime.Now.Date.AddDays(25);
        // d0 ends at X = 5 h; the future row still holds a stale 0.
        await Seed(d0, planHours: 8, nettoHours: 8, sumFlexStart: 5, sumFlexEnd: 5);
        await Seed(future, planHours: 0, nettoHours: 0, sumFlexStart: 0, sumFlexEnd: 0);

        // What the UI sends: SumFlexStart = 0, paying out p = 2 h.
        var result = await _service.UpdateCreate(new List<TimePlanningFlexUpdateModel> { Entry(d1, paidOut: 2, sumFlexStart: 0) });

        Assert.That(result.Success, Is.True, result.Message);
        var created = await Stored(d1);
        var last = await Stored(future);
        Assert.Multiple(() =>
        {
            Assert.That(created.PaiedOutFlex, Is.EqualTo(2.0).Within(1e-9));
            Assert.That(created.SumFlexStart, Is.EqualTo(5.0).Within(1e-9), "carried from d0, not the posted 0");
            Assert.That(created.SumFlexEnd, Is.EqualTo(3.0).Within(1e-9), "X - p = 5 - 2, not 0 - 2");
            Assert.That(last.SumFlexStart, Is.EqualTo(created.SumFlexEnd).Within(1e-9),
                "later rows carry from the created row");
            Assert.That(last.SumFlexEnd, Is.EqualTo(3.0).Within(1e-9));
        });
    }
}
