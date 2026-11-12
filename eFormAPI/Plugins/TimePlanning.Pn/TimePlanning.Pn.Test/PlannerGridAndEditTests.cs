using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Constants;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.PlanPattern;
using TimePlanning.Pn.Infrastructure.Models.Planner;
using static TimePlanning.Pn.Test.PlanPatternTestSeed;

#nullable enable
namespace TimePlanning.Pn.Test;

/// <summary>
/// #5489 planner: the grid, bulk cell edits and the pattern editor endpoints.
/// Every test re-migrates its database, so each covers one whole behaviour.
/// </summary>
[TestFixture]
public class PlannerGridAndEditTests : PlannerTestBase
{
    // ------------------------------------------------------------------ grid

    [Test]
    public async Task Grid_ScopesByTag_CreatesRowsThroughThePattern_AndSumsWeeksAndHeadcount()
    {
        await SeedAdminCallerAsync(UserService);
        var department = await CreateTagAsync("Køkken");
        var other = await CreateTagAsync("Bar");
        var from = NextMonday;

        var normWorker = await CreateWorkerAsync(8101, department);
        var ruleSet = new WorkingTimeRuleSet { Name = "37 t", WeeklyNormalSeconds = 37 * 3600, CreatedByUserId = 1, UpdatedByUserId = 1 };
        await ruleSet.Create(TimePlanningPnDbContext!);
        normWorker.WorkingTimeRuleSetId = ruleSet.Id;
        await normWorker.Update(TimePlanningPnDbContext!);
        var patterned = await CreateWorkerAsync(8102, department);
        await CreatePatternAsync(TimePlanningPnDbContext!, patterned, Today);
        await CreateWorkerAsync(8103, other);

        // normWorker: locked through `from`, a manual day and an absence day.
        await LockThroughAsync(TimePlanningPnDbContext!, 8101, from);
        await CreateRowAsync(TimePlanningPnDbContext!, 8101, from.AddDays(2), r =>
        {
            r.PlanText = "10:00-12:00";
            r.PlanHours = 2;
            r.PlannedStartOfShift1 = 600;
            r.PlannedEndOfShift1 = 720;
            r.PlanChangedByAdmin = true;
        });
        await CreateRowAsync(TimePlanningPnDbContext!, 8101, from.AddDays(3), r => r.MessageId = 2);
        // patterned: a pending vacation request.
        await new AbsenceRequest
        {
            RequestedBySdkSitId = 8102, DateFrom = from.AddDays(4), DateTo = from.AddDays(5),
            Status = AbsenceRequestStatus.Pending, CreatedByUserId = 1, UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext!);

        var result = await BuildService().GetGridAsync(new PlannerGridRequestModel
        {
            From = from, Days = 14, TagIds = [department]
        });

        Assert.That(result.Success, Is.True, result.Message);
        var grid = result.Model!;
        Assert.That(grid.Dates, Is.EqualTo(Enumerable.Range(0, 14).Select(i => from.AddDays(i)).ToList()));
        Assert.That(grid.GoogleSheetRetired, Is.False);
        Assert.That(grid.Workers.Select(w => w.SiteId), Is.EquivalentTo(new[] { 8101, 8102 }), "tag scope");

        var p = grid.Workers.Single(w => w.SiteId == 8102);
        Assert.That(p.AssignedSiteId, Is.EqualTo(patterned.Id));
        Assert.That(p.HasPattern, Is.True);
        Assert.That(p.RotationWeeks, Is.EqualTo(1));
        Assert.That(p.CanApproveAbsence, Is.False, "an admin who is no manager cannot decide requests");
        Assert.That(p.Cells, Has.Count.EqualTo(14));
        Assert.That(p.Cells.All(c => c.PlanText == PatternText && c.PlanHours == 7.5 && c.PlanRegistrationId != null),
            Is.True, "missing rows are created through the pattern branch");
        Assert.That(p.Cells.Where(c => c.HasPendingAbsenceRequest).Select(c => c.Date),
            Is.EqualTo(new[] { from.AddDays(4), from.AddDays(5) }));
        Assert.That(p.WeeklyNormHours, Is.Null);
        Assert.That(p.Weeks.Select(w => (w.WeekStart, w.PlannedHours, w.NormHours)),
            Is.EqualTo(new[] { (from, 52.5, (double?)null), (from.AddDays(7), 52.5, (double?)null) }));

        var n = grid.Workers.Single(w => w.SiteId == 8101);
        Assert.That(n.HasPattern, Is.False);
        Assert.That(n.WeeklyNormHours, Is.EqualTo(37));
        var locked = n.Cells.Single(c => c.Date == from);
        Assert.That(locked.Locked, Is.True);
        Assert.That(n.Cells.Where(c => c.Date > from).All(c => !c.Locked), Is.True);
        var manual = n.Cells.Single(c => c.Date == from.AddDays(2));
        Assert.That((manual.PlanChangedByAdmin, manual.PlanHours, manual.PlanText), Is.EqualTo((true, 2.0, "10:00-12:00")));
        Assert.That(n.Cells.Single(c => c.Date == from.AddDays(3)).MessageId, Is.EqualTo(2));
        Assert.That(n.Cells.Single(c => c.Date == from.AddDays(1)).PlanHours, Is.EqualTo(TemplateHours), "template day");
        Assert.That(n.Cells.All(c => !c.IsPaidDay), Is.True, "future days are not paid");
        var week1 = n.Weeks.Single(w => w.WeekStart == from);
        Assert.That(week1.IsoWeek, Is.EqualTo(System.Globalization.ISOWeek.GetWeekOfYear(from)));
        // The locked row was made by the test with no plan: 5 template days + 2 h.
        Assert.That(week1.PlannedHours, Is.EqualTo(5 * TemplateHours + 2));
        Assert.That(week1.NormHours, Is.EqualTo(37));
        Assert.That(week1.DifferenceHours, Is.EqualTo(5 * TemplateHours + 2 - 37));

        Assert.That(grid.DayTotals.Single(d => d.Date == from.AddDays(1)).PlannedHeadcount, Is.EqualTo(2));
        Assert.That(grid.DayTotals.Single(d => d.Date == from.AddDays(3)).PlannedHeadcount, Is.EqualTo(1),
            "an absent worker is not planned");
        Assert.That(grid.DayTotals.Single(d => d.Date == from.AddDays(4)).PendingAbsenceRequests, Is.EqualTo(1));
        Assert.That(grid.DayTotals.Single(d => d.Date == from.AddDays(6)).PendingAbsenceRequests, Is.EqualTo(0));
    }

    [Test]
    public async Task Grid_ManagerSeesOnlyTheirWorkers_AndMayApproveTheirs()
    {
        await SeedManagerCallerAsync(UserService, 8110, 8111);
        await CreateWorkerAsync(8111);
        await CreateWorkerAsync(8112);

        var result = await BuildService().GetGridAsync(new PlannerGridRequestModel { From = NextMonday, Days = 7 });

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(result.Model!.Dates, Has.Count.EqualTo(7));
        Assert.That(result.Model.Workers.Select(w => w.SiteId), Is.EquivalentTo(new[] { 8110, 8111 }));
        Assert.That(result.Model.Workers.Single(w => w.SiteId == 8111).CanApproveAbsence, Is.True);
        Assert.That(result.Model.Workers.Single(w => w.SiteId == 8110).CanApproveAbsence, Is.False);
    }

    [Test]
    public async Task Grid_RefusesARangeOtherThanOneOrTwoWeeks()
    {
        await SeedAdminCallerAsync(UserService);

        var result = await BuildService().GetGridAsync(new PlannerGridRequestModel { From = NextMonday, Days = 30 });

        Assert.That(result.Success, Is.False);
    }

    // ------------------------------------------------------------ bulk edit

    [Test]
    public async Task Cells_OnlyTheseDays_WritesCanonicalPlan_FlagsIt_SkipsLockedDays_AndHandlesAbsenceAndClear()
    {
        await SeedAdminCallerAsync(UserService);
        var worker = await CreateWorkerAsync(8201);
        var lockedDay = Today.AddDays(3);
        await LockThroughAsync(TimePlanningPnDbContext!, 8201, lockedDay);
        var d1 = Today.AddDays(5);
        var d2 = Today.AddDays(6);
        await CreateRowAsync(TimePlanningPnDbContext!, 8201, d1);
        var service = BuildService();

        var plan = await service.UpdateCellsAsync(new PlannerCellsUpdateModel
        {
            Cells = [Cell(worker.Id, lockedDay), Cell(worker.Id, d1), Cell(worker.Id, d2), Cell(999999, d1)],
            ShortForm = "7-15/0,5",
            Mode = PlannerEditModes.OnlyTheseDays
        });

        Assert.That(plan.Success, Is.True, plan.Message);
        Assert.That(plan.Model!.UpdatedCount, Is.EqualTo(2));
        Assert.That(plan.Model.LockedCells.Select(c => (c.AssignedSiteId, c.Date)), Is.EqualTo(new[] { (worker.Id, lockedDay) }));
        Assert.That(plan.Model.LockedMessage, Is.EqualTo("PlannerDaysLockedNotChanged:1"));
        Assert.That(plan.Model.Errors.Select(e => (e.AssignedSiteId, e.Key)),
            Is.EqualTo(new[] { (999999, "PlannerWorkerNotFound") }));
        foreach (var date in new[] { d1, d2 })
        {
            var row = await RowAsync(8201, date);
            Assert.That((row.PlanText, row.PlanHours, row.PlannedStartOfShift1, row.PlannedEndOfShift1, row.PlannedBreakOfShift1, row.PlanChangedByAdmin),
                Is.EqualTo(("7:00-15:00/0.5", 7.5, 420, 900, 30, true)), date.ToString("d"));
        }
        Assert.That((await RowAsync(8201, lockedDay)).PlanText, Is.EqualTo(""), "a locked day is not changed");

        var absence = await service.UpdateCellsAsync(new PlannerCellsUpdateModel
        {
            Cells = [Cell(worker.Id, d1)], MessageId = 2, Mode = PlannerEditModes.OnlyTheseDays
        });
        Assert.That(absence.Success, Is.True, absence.Message);
        var absent = await RowAsync(8201, d1);
        Assert.That((absent.MessageId, absent.PlanText, absent.PlanHours), Is.EqualTo(((int?)2, "7:00-15:00/0.5", 7.5)),
            "absence sets the message like the manual edit and keeps the plan");

        var dayOff = await service.UpdateCellsAsync(new PlannerCellsUpdateModel
        {
            Cells = [Cell(worker.Id, d2)], ShortForm = "Fri", Mode = PlannerEditModes.OnlyTheseDays
        });
        Assert.That(dayOff.Success, Is.True, dayOff.Message);
        var off = await RowAsync(8201, d2);
        Assert.That((off.PlanText, off.PlanHours, off.PlannedStartOfShift1, off.PlanChangedByAdmin), Is.EqualTo(("", 0.0, 0, true)));

        var clear = await service.UpdateCellsAsync(new PlannerCellsUpdateModel
        {
            Cells = [Cell(worker.Id, d1)], ShortForm = "", Mode = PlannerEditModes.OnlyTheseDays
        });
        Assert.That(clear.Success, Is.True, clear.Message);
        var cleared = await RowAsync(8201, d1);
        Assert.That((cleared.PlanText, cleared.PlanHours, cleared.MessageId), Is.EqualTo(("", 0.0, (int?)null)));

        var invalid = await service.UpdateCellsAsync(new PlannerCellsUpdateModel
        {
            Cells = [Cell(worker.Id, d1)], ShortForm = "7-15;hej", Mode = PlannerEditModes.OnlyTheseDays
        });
        Assert.That(invalid.Success, Is.False);
        Assert.That(invalid.Message, Is.EqualTo("PlannerShortFormInvalid"));
    }

    // Owner decision: only the clicked cell's rotation week changes. With a
    // 3-week rotation anchored on this Monday, next Monday starts week 2
    // (index 1), so its Wednesday is week 2's; weeks 1 and 3 keep theirs.
    [Test]
    public async Task Cells_EveryWeekdayFrom_EditsOnlyThatRotationWeeksDay_AndReappliesItFromThatDateOnly()
    {
        await SeedAdminCallerAsync(UserService);
        var patterned = await CreateWorkerAsync(8301);
        var plain = await CreateWorkerAsync(8302);
        var anchor = MondayOnOrBefore(Today);
        var save = await BuildApplyService().SavePatternAsync(patterned.Id, 3, anchor, Today,
            AllDays(3, _ => PatternText), false);
        Assert.That(save.Success, Is.True, save.Message);

        var aWednesday = NextMonday.AddDays(2); // rotation week index 1
        var fromDate = anchor.AddDays(21); // the next week index 0, before any later week index 1
        var result = await BuildService().UpdateCellsAsync(new PlannerCellsUpdateModel
        {
            Cells = [Cell(patterned.Id, aWednesday), Cell(plain.Id, aWednesday)],
            ShortForm = "9-13",
            Mode = PlannerEditModes.EveryWeekdayFrom,
            FromDate = fromDate
        });

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(result.Model!.Errors.Select(e => (e.AssignedSiteId, e.Key)),
            Is.EqualTo(new[] { (plain.Id, "PlannerWorkerHasNoPattern") }));
        Assert.That(result.Model.UpdatedCount, Is.GreaterThan(0));

        var pattern = await TimePlanningPnDbContext!.PlanPatterns.AsNoTracking()
            .Include(x => x.Days)
            .SingleAsync(x => x.AssignedSiteId == patterned.Id && x.WorkflowState != Constants.WorkflowStates.Removed);
        Assert.That(pattern.ValidFrom, Is.EqualTo(Today), "the pattern keeps its ValidFrom");
        var liveDays = pattern.Days.Where(d => d.WorkflowState != Constants.WorkflowStates.Removed).ToList();
        Assert.That(liveDays.Single(d => d.WeekIndex == 1 && d.DayOfWeek == 2).PlanText, Is.EqualTo("9:00-13:00"));
        Assert.That(liveDays.Where(d => !(d.WeekIndex == 1 && d.DayOfWeek == 2)).All(d => d.PlanText == PatternText),
            Is.True, "weeks 1 and 3, and the other weekdays, are untouched");

        var rows = await RowsAsync(8301);
        // From the from-date: week index 1 Wednesdays change, index 0 and 2 Wednesdays do not.
        Assert.That(rows.Single(r => r.Date == fromDate.AddDays(7 + 2)).PlanText, Is.EqualTo("9:00-13:00"));
        Assert.That(rows.Single(r => r.Date == fromDate.AddDays(28 + 2)).PlanText, Is.EqualTo("9:00-13:00"));
        Assert.That(rows.Single(r => r.Date == fromDate.AddDays(2)).PlanText, Is.EqualTo(PatternText), "week 1");
        Assert.That(rows.Single(r => r.Date == fromDate.AddDays(14 + 2)).PlanText, Is.EqualTo(PatternText), "week 3");
        Assert.That(rows.Single(r => r.Date == aWednesday).PlanText, Is.EqualTo(PatternText),
            "the clicked Wednesday is before the from-date and is not touched");
        Assert.That(rows.Single(r => r.Date == fromDate.AddDays(7 + 3)).PlanText, Is.EqualTo(PatternText),
            "other weekdays keep the pattern");
        Assert.That(rows.Where(r => r.Date >= fromDate).All(r => !r.PlanChangedByAdmin), Is.True);
    }

    // ---------------------------------------------------------------- pattern

    [Test]
    public async Task Pattern_PrefillSaveCopyManualDaysAndDelete()
    {
        await SeedAdminCallerAsync(UserService);
        var worker = await CreateWorkerAsync(8401);
        // Monday also has a 2nd and a 5th shift: the prefill reads the template
        // by property name, so a misspelt name shows up here.
        worker.StartMonday2NdShift = 16 * 60;
        worker.EndMonday2NdShift = 20 * 60;
        worker.BreakMonday2NdShift = 0;
        worker.StartMonday5ThShift = 21 * 60;
        worker.EndMonday5ThShift = 22 * 60;
        worker.BreakMonday5ThShift = 15;
        await worker.Update(TimePlanningPnDbContext!);
        var source = await CreateWorkerAsync(8402);
        var target = await CreateWorkerAsync(8403);
        var service = BuildService();

        // Prefill from the weekday template.
        var prefill = await service.GetPatternAsync(worker.Id);
        Assert.That(prefill.Success, Is.True, prefill.Message);
        Assert.That((prefill.Model!.IsPrefill, prefill.Model.PlanPatternId, prefill.Model.RotationWeeks),
            Is.EqualTo((true, (int?)null, 1)));
        Assert.That(prefill.Model.AnchorDate, Is.EqualTo(MondayOnOrBefore(Today)));
        Assert.That(prefill.Model.ValidFrom, Is.EqualTo(Today));
        Assert.That(prefill.Model.Days.Select(d => (d.WeekIndex, d.DayOfWeek, d.PlanText)),
            Is.EqualTo(Enumerable.Range(0, 7).Select(d => (0, d,
                (string?)(d == 0 ? "7:00-15:00/0.5;16:00-20:00;21:00-22:00/0.25" : "7:00-15:00/0.5")))));

        // Save with short forms: stored canonical.
        var invalid = await service.SavePatternAsync(worker.Id, new PlannerPatternSaveModel
        {
            RotationWeeks = 1, AnchorDate = MondayOnOrBefore(Today), ValidFrom = Today,
            Days = AllDays(1, _ => "nonsense")
        });
        Assert.That(invalid.Success, Is.False);
        var saved = await service.SavePatternAsync(worker.Id, new PlannerPatternSaveModel
        {
            RotationWeeks = 1, AnchorDate = MondayOnOrBefore(Today), ValidFrom = Today,
            Days = AllDays(1, _ => "8-16/0,5")
        });
        Assert.That(saved.Success, Is.True, saved.Message);
        var read = await service.GetPatternAsync(worker.Id);
        Assert.That((read.Model!.IsPrefill, read.Model.PlanPatternId), Is.EqualTo((false, (int?)saved.Model!.PlanPatternId)));
        Assert.That(read.Model.Days.All(d => d.PlanText == "8:00-16:00/0.5"), Is.True);

        // Copy is a pure copy: rotation, anchor and days, nothing saved, no link.
        var sourceAnchor = MondayOnOrBefore(Today).AddDays(-21);
        Assert.That((await service.SavePatternAsync(source.Id, new PlannerPatternSaveModel
        {
            RotationWeeks = 2, AnchorDate = sourceAnchor, ValidFrom = Today,
            Days = AllDays(2, week => week == 0 ? PatternText : PatternTextShort)
        })).Success, Is.True);
        var copy = await service.CopyPatternAsync(target.Id, source.Id);
        Assert.That(copy.Success, Is.True, copy.Message);
        Assert.That((copy.Model!.AssignedSiteId, copy.Model.PlanPatternId, copy.Model.IsPrefill, copy.Model.RotationWeeks, copy.Model.AnchorDate),
            Is.EqualTo((target.Id, (int?)null, false, 2, sourceAnchor)));
        Assert.That(copy.Model.Days.Select(d => (d.WeekIndex, d.DayOfWeek, d.PlanText)),
            Is.EqualTo(AllDays(2, week => week == 0 ? PatternText : PatternTextShort).Select(d => (d.WeekIndex, d.DayOfWeek, d.PlanText))));
        Assert.That((await service.GetPatternAsync(target.Id)).Model!.IsPrefill, Is.True, "copying saves nothing");
        var copyMissing = await service.CopyPatternAsync(target.Id, worker.Id + 100000);
        Assert.That(copyMissing.Success, Is.False);

        copy.Model.Days[0].PlanText = "6:00-7:00";
        Assert.That((await service.SavePatternAsync(target.Id, new PlannerPatternSaveModel
        {
            RotationWeeks = copy.Model.RotationWeeks, AnchorDate = copy.Model.AnchorDate, ValidFrom = copy.Model.ValidFrom,
            Days = copy.Model.Days
        })).Success, Is.True);
        var sourceAfter = await service.GetPatternAsync(source.Id);
        Assert.That(sourceAfter.Model!.Days.Single(d => d.WeekIndex == 0 && d.DayOfWeek == 0).PlanText, Is.EqualTo(PatternText),
            "editing the copy does not change the source");
        Assert.That((await service.GetPatternAsync(target.Id)).Model!.Days.Single(d => d.WeekIndex == 0 && d.DayOfWeek == 0).PlanText,
            Is.EqualTo("6:00-7:00"));

        // Manual days in range.
        var manualDay = Today.AddDays(10);
        var manualRow = (await RowsAsync(8401)).Single(r => r.Date == manualDay);
        var tracked = await TimePlanningPnDbContext!.PlanRegistrations.SingleAsync(r => r.Id == manualRow.Id);
        tracked.PlanChangedByAdmin = true;
        await tracked.Update(TimePlanningPnDbContext);
        var manual = await service.GetManualDaysAsync(worker.Id, Today);
        Assert.That(manual.Success, Is.True, manual.Message);
        Assert.That(manual.Model, Is.EqualTo(new List<DateTime> { manualDay }));

        // Delete: back to the template prefill.
        var deleted = await service.DeletePatternAsync(worker.Id);
        Assert.That(deleted.Success, Is.True, deleted.Message);
        Assert.That((await service.GetPatternAsync(worker.Id)).Model!.IsPrefill, Is.True);
        Assert.That((await service.DeletePatternAsync(worker.Id)).Success, Is.False);
    }

    // ---------------------------------------------------------------- helpers

    private static PlannerCellRefModel Cell(int assignedSiteId, DateTime date) =>
        new() { AssignedSiteId = assignedSiteId, Date = date };

    private static List<PlanPatternDayModel> AllDays(int weeks, Func<int, string> text) =>
        Enumerable.Range(0, weeks)
            .SelectMany(w => Enumerable.Range(0, 7).Select(d => new PlanPatternDayModel
            {
                WeekIndex = w, DayOfWeek = d, PlanText = text(w)
            }))
            .ToList();
}
