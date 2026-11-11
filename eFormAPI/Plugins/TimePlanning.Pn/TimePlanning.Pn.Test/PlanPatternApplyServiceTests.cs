using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.PlanPattern;
using TimePlanning.Pn.Services.PlanPatternApplyService;
using static TimePlanning.Pn.Test.PlanPatternTestSeed;

#nullable enable
namespace TimePlanning.Pn.Test;

/// <summary>
/// #5489: saving a pattern replaces the worker's active pattern, takes the
/// worker off the sheet, and writes PlanRegistration rows from
/// max(ValidFrom, today) to today + 180 days through the same pattern branch
/// the existing row writers use.
/// </summary>
[TestFixture]
public class PlanPatternApplyServiceTests : TestBaseSetup
{
    private const int SiteId = 5490;

    private static DateTime Today => DateTime.UtcNow.Date;
    private static DateTime Anchor => MondayOnOrBefore(Today);

    private PlanPatternApplyService _service = null!;
    private List<(int SdkSitId, DateTime From)> _flexCalls = null!;

    [SetUp]
    public void SetUpService()
    {
        var userService = Substitute.For<IUserService>();
        userService.UserId.Returns(1);
        _flexCalls = new List<(int, DateTime)>();
        _service = new PlanPatternApplyService(
            TimePlanningPnDbContext!,
            userService,
            Substitute.For<ILogger<PlanPatternApplyService>>())
        {
            FlexRecompute = (_, _, sdkSitId, from) =>
            {
                _flexCalls.Add((sdkSitId, from));
                return Task.FromResult(0);
            }
        };
    }

    private static List<PlanPatternDayModel> EveryDay(string planText, int weeks = 1) =>
        Enumerable.Range(0, weeks)
            .SelectMany(w => Enumerable.Range(0, 7).Select(d => new PlanPatternDayModel
            {
                WeekIndex = w, DayOfWeek = d, PlanText = planText
            }))
            .ToList();

    // --------------------------------------------------------------- validation

    // One test for every rejected input: each test in this assembly
    // re-migrates its database, which costs far more than the assertions.
    [Test]
    public async Task Save_InvalidInput_RejectedWithoutWriting()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        var dayOutsideRotation = EveryDay(PatternText);
        dayOutsideRotation.Add(new PlanPatternDayModel { WeekIndex = 1, DayOfWeek = 0, PlanText = PatternText });
        var duplicateDay = EveryDay(PatternText);
        duplicateDay.Add(new PlanPatternDayModel { WeekIndex = 0, DayOfWeek = 3, PlanText = PatternTextShort });
        var badDayOfWeek = EveryDay(PatternText);
        badDayOfWeek.Add(new PlanPatternDayModel { WeekIndex = 0, DayOfWeek = 7, PlanText = PatternText });

        var cases = new (string Name, int Rotation, DateTime Anchor, List<PlanPatternDayModel> Days)[]
        {
            ("rotation 0", 0, Anchor, EveryDay(PatternText)),
            ("rotation 9", 9, Anchor, EveryDay(PatternText)),
            ("anchor on a Tuesday", 1, Anchor.AddDays(1), EveryDay(PatternText)),
            ("week index outside the rotation", 1, Anchor, dayOutsideRotation),
            ("day of week 7", 1, Anchor, badDayOfWeek),
            ("the same day twice", 1, Anchor, duplicateDay)
        };

        foreach (var c in cases)
        {
            var result = await _service.SavePatternAsync(site.Id, c.Rotation, c.Anchor, Today, c.Days, false);
            Assert.That(result.Success, Is.False, c.Name);
        }

        Assert.That(await TimePlanningPnDbContext!.PlanPatterns.CountAsync(), Is.Zero);
        Assert.That(await TimePlanningPnDbContext.PlanRegistrations.CountAsync(x => x.SdkSitId == SiteId), Is.Zero);
        Assert.That(_flexCalls, Is.Empty);
    }

    // ------------------------------------------------------------------- apply

    [Test]
    public async Task Save_WritesRowsFromValidFromThroughTodayPlus180()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        var before = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(5), r => r.PlanHours = 3);
        var validFrom = Today.AddDays(10);

        var result = await _service.SavePatternAsync(site.Id, 1, Anchor, validFrom, EveryDay(PatternText), false);

        Assert.That(result.Success, Is.True, result.Message);
        var rows = await LiveRowsAsync(TimePlanningPnDbContext!, SiteId);
        var patterned = rows.Where(r => r.PlanText == PatternText).Select(r => r.Date).ToList();
        Assert.That(patterned.Min(), Is.EqualTo(validFrom));
        Assert.That(patterned.Max(), Is.EqualTo(Today.AddDays(180)));
        Assert.That(patterned, Has.Count.EqualTo(171), "one row per day, no gaps, no duplicates");
        Assert.That(rows.Where(r => r.Date < validFrom).Select(r => r.Id), Is.EqualTo(new[] { before.Id }),
            "no row is created before ValidFrom");
        var storedBefore = rows.Single(r => r.Id == before.Id);
        Assert.That(storedBefore.PlanText, Is.EqualTo(""));
        Assert.That(storedBefore.PlanHours, Is.EqualTo(3));
        Assert.That(rows.Where(r => r.PlanText == PatternText).Select(r => r.PlanHours), Is.All.EqualTo(7.5));
    }

    [Test]
    public async Task Save_ValidFromInThePast_StartsAtToday()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        var yesterday = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(-1), r => r.PlanHours = 3);

        var result = await _service.SavePatternAsync(site.Id, 1, Anchor, Today.AddDays(-20), EveryDay(PatternText), false);

        Assert.That(result.Success, Is.True, result.Message);
        var stored = await ReadRowAsync(TimePlanningPnDbContext!, yesterday.Id);
        Assert.That(stored.PlanText, Is.EqualTo(""));
        Assert.That(stored.PlanHours, Is.EqualTo(3));
        var rows = await LiveRowsAsync(TimePlanningPnDbContext!, SiteId);
        Assert.That(rows.Where(r => r.PlanText == PatternText).Min(r => r.Date), Is.EqualTo(Today));
    }

    [Test]
    public async Task Save_UsesTheRotation()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        var days = EveryDay(PatternText).Concat(EveryDay(PatternTextShort).Select(d =>
        {
            d.WeekIndex = 1;
            return d;
        })).ToList();

        var result = await _service.SavePatternAsync(site.Id, 2, Anchor, Anchor, days, false);

        Assert.That(result.Success, Is.True, result.Message);
        var rows = await LiveRowsAsync(TimePlanningPnDbContext!, SiteId);
        Assert.That(rows.Single(r => r.Date == Anchor.AddDays(7)).PlanText, Is.EqualTo(PatternTextShort));
        Assert.That(rows.Single(r => r.Date == Anchor.AddDays(14)).PlanText, Is.EqualTo(PatternText));
        Assert.That(rows.Single(r => r.Date == Anchor.AddDays(21)).PlanHours, Is.EqualTo(4));
    }

    [Test]
    public async Task Save_TakesWorkerOffTheSheet()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId, useGoogleSheet: true);

        var result = await _service.SavePatternAsync(site.Id, 1, Anchor, Today, EveryDay(PatternText), false);

        Assert.That(result.Success, Is.True, result.Message);
        await using var fresh = CreateTimePlanningPnDbContext();
        var stored = await fresh.AssignedSites.AsNoTracking().SingleAsync(x => x.Id == site.Id);
        Assert.That(stored.UseGoogleSheetAsDefault, Is.False);
        Assert.That(await fresh.PlanPatterns.CountAsync(x => x.AssignedSiteId == site.Id
                                                             && x.WorkflowState != Constants.WorkflowStates.Removed),
            Is.EqualTo(1), "the pattern is committed");
    }

    [Test]
    public async Task Save_Twice_LeavesOneActivePatternWithItsDays()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);

        var first = await _service.SavePatternAsync(site.Id, 1, Anchor, Today, EveryDay(PatternText), false);
        var second = await _service.SavePatternAsync(site.Id, 1, Anchor, Today, EveryDay(PatternTextShort), false);

        Assert.That(first.Success && second.Success, Is.True);
        var patterns = await TimePlanningPnDbContext!.PlanPatterns.AsNoTracking()
            .Where(x => x.AssignedSiteId == site.Id).ToListAsync();
        var active = patterns.Where(x => x.WorkflowState != Constants.WorkflowStates.Removed).ToList();
        Assert.That(patterns, Has.Count.EqualTo(2));
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].Id, Is.EqualTo(second.Model.PlanPatternId));

        var oldDays = await TimePlanningPnDbContext.PlanPatternDays.AsNoTracking()
            .Where(x => x.PlanPatternId == first.Model.PlanPatternId).ToListAsync();
        Assert.That(oldDays.Select(x => x.WorkflowState), Is.All.EqualTo(Constants.WorkflowStates.Removed));
        Assert.That(await TimePlanningPnDbContext.PlanPatternVersions
            .CountAsync(x => x.PlanPatternId == first.Model.PlanPatternId
                             && x.WorkflowState == Constants.WorkflowStates.Removed), Is.EqualTo(1),
            "the soft delete is versioned");

        var rows = await LiveRowsAsync(TimePlanningPnDbContext, SiteId);
        Assert.That(rows.Select(r => r.PlanText), Is.All.EqualTo(PatternTextShort),
            "the second save regenerates every row the first wrote");
    }

    [Test]
    public async Task Save_KeepsManualDaysByDefault()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        var manual = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(3), r =>
        {
            r.PlanChangedByAdmin = true;
            r.PlanText = "10:00-12:00";
            r.PlanHours = 2;
        });

        var result = await _service.SavePatternAsync(site.Id, 1, Anchor, Today, EveryDay(PatternText), false);

        Assert.That(result.Success, Is.True, result.Message);
        var stored = await ReadRowAsync(TimePlanningPnDbContext!, manual.Id);
        Assert.That(stored.PlanText, Is.EqualTo("10:00-12:00"));
        Assert.That(stored.PlanHours, Is.EqualTo(2));
        Assert.That(stored.PlanChangedByAdmin, Is.True);
    }

    [Test]
    public async Task Save_Overwrite_RegeneratesManualDaysAndClearsFlag()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        var manual = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(3), r =>
        {
            r.PlanChangedByAdmin = true;
            r.PlanText = "10:00-12:00";
            r.PlanHours = 2;
        });

        var result = await _service.SavePatternAsync(site.Id, 1, Anchor, Today, EveryDay(PatternText), true);

        Assert.That(result.Success, Is.True, result.Message);
        var stored = await ReadRowAsync(TimePlanningPnDbContext!, manual.Id);
        Assert.That(stored.PlanText, Is.EqualTo(PatternText));
        Assert.That(stored.PlanHours, Is.EqualTo(7.5));
        Assert.That(stored.PlanChangedByAdmin, Is.False);
    }

    [Test]
    public async Task Save_LockedDays_ReportedAndUntouched()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        var locked = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(1), r => r.PlanHours = 3);
        // A boundary in the future cannot be set through the app, but the
        // writer must still respect it.
        await LockThroughAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(2));

        var result = await _service.SavePatternAsync(site.Id, 1, Anchor, Today, EveryDay(PatternText), true);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(result.Model.LockedDays, Is.EqualTo(new[] { Today, Today.AddDays(1), Today.AddDays(2) }));
        var stored = await ReadRowAsync(TimePlanningPnDbContext!, locked.Id);
        Assert.That(stored.PlanText, Is.EqualTo(""));
        Assert.That(stored.PlanHours, Is.EqualTo(3));
        var rows = await LiveRowsAsync(TimePlanningPnDbContext!, SiteId);
        Assert.That(rows.Any(r => r.Date == Today), Is.False, "no row is created on a locked day");
        Assert.That(rows.Where(r => r.PlanText == PatternText).Min(r => r.Date), Is.EqualTo(Today.AddDays(3)));
    }

    [Test]
    public async Task Save_AbsenceDay_TakesPatternHoursAndKeepsAbsence()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        var message = await TimePlanningPnDbContext!.Messages.FirstAsync();
        var absence = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(4), r =>
        {
            r.OnVacation = true;
            r.MessageId = message.Id;
        });

        var result = await _service.SavePatternAsync(site.Id, 1, Anchor, Today, EveryDay(PatternText), false);

        Assert.That(result.Success, Is.True, result.Message);
        var stored = await ReadRowAsync(TimePlanningPnDbContext!, absence.Id);
        Assert.That(stored.PlanText, Is.EqualTo(PatternText));
        Assert.That(stored.PlanHours, Is.EqualTo(7.5));
        Assert.That(stored.OnVacation, Is.True);
        Assert.That(stored.MessageId, Is.EqualTo(message.Id));
    }

    [Test]
    public async Task Save_RecomputesFlexOnceFromTheFirstWrittenDay()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        var validFrom = Today.AddDays(10);

        var result = await _service.SavePatternAsync(site.Id, 1, Anchor, validFrom, EveryDay(PatternText), false);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(_flexCalls, Is.EqualTo(new[] { (SiteId, validFrom) }));
    }

    [Test]
    public async Task Save_RealFlexRecompute_CarriesTheBalanceThroughTheNewRows()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        var userService = Substitute.For<IUserService>();
        userService.UserId.Returns(1);
        var service = new PlanPatternApplyService(TimePlanningPnDbContext!, userService,
            Substitute.For<ILogger<PlanPatternApplyService>>());

        var result = await service.SavePatternAsync(site.Id, 1, Anchor, Today, EveryDay(PatternText), false);

        Assert.That(result.Success, Is.True, result.Message);
        var rows = await LiveRowsAsync(TimePlanningPnDbContext!, SiteId);
        // Nothing worked yet: every planned day adds -7.5 h to the running balance.
        Assert.That(rows.Last().SumFlexEnd, Is.EqualTo(-7.5 * rows.Count).Within(1e-6));
    }

    [Test]
    public async Task Save_ResignedWorker_GetsNoRowsAfterTheResignationDate()
    {
        var leaving = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        leaving.Resigned = true;
        leaving.ResignedAtDate = Today.AddDays(5);
        await leaving.Update(TimePlanningPnDbContext!);
        var gone = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId + 1);
        gone.Resigned = true;
        gone.ResignedAtDate = Today.AddDays(-3);
        await gone.Update(TimePlanningPnDbContext!);

        var leavingResult = await _service.SavePatternAsync(leaving.Id, 1, Anchor, Today, EveryDay(PatternText), false);
        var goneResult = await _service.SavePatternAsync(gone.Id, 1, Anchor, Today, EveryDay(PatternText), false);

        Assert.That(leavingResult.Success && goneResult.Success, Is.True);
        var leavingRows = await LiveRowsAsync(TimePlanningPnDbContext!, SiteId);
        Assert.That(leavingRows.Select(r => r.Date),
            Is.EqualTo(Enumerable.Range(0, 6).Select(i => Today.AddDays(i))),
            "rows up to and including the resignation date, none after");
        Assert.That(await LiveRowsAsync(TimePlanningPnDbContext!, SiteId + 1), Is.Empty,
            "a worker who has already left gets no rows");
    }

    // ------------------------------------------------------------------ delete

    [Test]
    public async Task Delete_RemovesPattern_AndKeepsWorkerOffTheSheet()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId, useGoogleSheet: true);
        var saved = await _service.SavePatternAsync(site.Id, 1, Anchor, Today, EveryDay(PatternText), false);

        var result = await _service.DeletePatternAsync(site.Id);

        Assert.That(result.Success, Is.True, result.Message);
        await using var fresh = CreateTimePlanningPnDbContext();
        var pattern = await fresh.PlanPatterns.AsNoTracking().SingleAsync(x => x.Id == saved.Model.PlanPatternId);
        Assert.That(pattern.WorkflowState, Is.EqualTo(Constants.WorkflowStates.Removed));
        Assert.That(await fresh.PlanPatternDays.AsNoTracking()
                .Where(x => x.PlanPatternId == pattern.Id).Select(x => x.WorkflowState).ToListAsync(),
            Is.All.EqualTo(Constants.WorkflowStates.Removed));
        var stored = await fresh.AssignedSites.AsNoTracking().SingleAsync(x => x.Id == site.Id);
        Assert.That(stored.UseGoogleSheetAsDefault, Is.False);
    }

    // ------------------------------------------------------------- manual days

    [Test]
    public async Task GetManualDaysInRange_ReturnsOnlyFlaggedDaysInTheApplyRange()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(-2), r => r.PlanChangedByAdmin = true);
        await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(4), r => r.PlanChangedByAdmin = true);
        await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(9), r => r.PlanChangedByAdmin = true);
        await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(10));
        await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(200), r => r.PlanChangedByAdmin = true);

        var days = await _service.GetManualDaysInRangeAsync(site.Id, Today.AddDays(3));

        Assert.That(days, Is.EqualTo(new[] { Today.AddDays(4), Today.AddDays(9) }));
    }
}
