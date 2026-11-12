using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Constants;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Models.Planner;
using static TimePlanning.Pn.Test.PlanPatternTestSeed;

#nullable enable
namespace TimePlanning.Pn.Test;

/// <summary>
/// #5489 planner: a manager's absence over a period, the one-off import that
/// retires the Google Sheet, and the planner settings.
/// </summary>
[TestFixture]
public class PlannerAbsenceAndImportTests : PlannerTestBase
{
    private const int ManagerUid = 8510;
    private const int WorkerUid = 8511;

    // -------------------------------------------------------------- absence

    [Test]
    public async Task Absence_IsOneApprovedRequestDecidedByTheManager_OverThePlannedDaysOnly()
    {
        await SeedManagerCallerAsync(UserService, ManagerUid, WorkerUid);
        var worker = await CreateWorkerAsync(WorkerUid);
        var (d1, d2, d3, d4) = (Today.AddDays(10), Today.AddDays(11), Today.AddDays(12), Today.AddDays(13));
        foreach (var d in new[] { d1, d2, d4 })
        {
            await CreateRowAsync(TimePlanningPnDbContext!, WorkerUid, d, r => r.PlanHours = 7.5);
        }
        // A day off set by hand: the template leaves it without a plan.
        await CreateRowAsync(TimePlanningPnDbContext!, WorkerUid, d3, r => r.PlanChangedByAdmin = true);
        // d5 and d6 have no rows yet; the template plans them once they exist.
        var (d5, d6) = (Today.AddDays(14), Today.AddDays(15));

        var result = await BuildService().CreateAbsenceAsync(new PlannerAbsenceCreateModel
        {
            AssignedSiteId = worker.Id, From = d1, To = d6, MessageId = 2, Comment = "sommerferie"
        });

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(result.Model!.AppliedDates, Is.EqualTo(new[] { d1, d2, d4, d5, d6 }),
            "days with no row yet are created like the grid does and count when planned");
        Assert.That(result.Model.SkippedDates, Is.EqualTo(new[] { d3 }));

        var request = await TimePlanningPnDbContext!.AbsenceRequests.AsNoTracking().Include(x => x.Days)
            .SingleAsync(x => x.Id == result.Model.AbsenceRequestId);
        Assert.That((request.Status, request.RequestedBySdkSitId, request.DecidedBySdkSitId, request.DateFrom, request.DateTo),
            Is.EqualTo((AbsenceRequestStatus.Approved, WorkerUid, (int?)ManagerUid, d1, d6)));
        Assert.That(request.DecidedAtUtc, Is.Not.Null);
        Assert.That(request.Days.Select(x => (x.Date, x.MessageId)).OrderBy(x => x.Date),
            Is.EqualTo(new[] { (d1, 2), (d2, 2), (d4, 2), (d5, 2), (d6, 2) }));

        var rows = await RowsAsync(WorkerUid);
        foreach (var d in new[] { d1, d2, d4, d5, d6 })
        {
            var row = rows.Single(r => r.Date == d);
            Assert.That((row.MessageId, row.OnVacation, row.AbsenceRequestId, row.AbsenceApprovedBySdkSitId, row.PlanHours),
                Is.EqualTo(((int?)2, true, (int?)request.Id, (int?)ManagerUid, 7.5)), d.ToString("d"));
        }
        Assert.That(rows.Single(r => r.Date == d3).MessageId, Is.Null, "a day with no plan is skipped");

        // The worker is told, with the push ApproveAsync sends.
        Assert.That(await PushSent.WaitAsync(5000), Is.True, "no push was sent");
        await PushService.Received(1).SendToSiteAsync(WorkerUid, "Absence request approved",
            "Your absence request has been approved",
            Arg.Is<Dictionary<string, string>?>(d => d != null && d["type"] == "absence_decided"
                                                     && d["action"] == "approved"
                                                     && d["absenceRequestId"] == request.Id.ToString()));

        // An overlapping absence is refused, as for a worker's own request.
        var overlap = await BuildService().CreateAbsenceAsync(new PlannerAbsenceCreateModel
        {
            AssignedSiteId = worker.Id, From = d2, To = d2, MessageId = 3
        });
        Assert.That(overlap.Success, Is.False);
    }

    [Test]
    public async Task Absence_ALockedDayBlocksAll_AFailureLeavesNothing_AndANonManagerIsRefused()
    {
        await SeedManagerCallerAsync(UserService, ManagerUid, WorkerUid);
        var worker = await CreateWorkerAsync(WorkerUid);
        var other = await CreateWorkerAsync(8512);
        var (d1, d2, d3) = (Today.AddDays(10), Today.AddDays(11), Today.AddDays(12));
        foreach (var d in new[] { d1, d2, d3 })
        {
            await CreateRowAsync(TimePlanningPnDbContext!, WorkerUid, d, r => r.PlanHours = 7.5);
            await CreateRowAsync(TimePlanningPnDbContext!, 8512, d, r => r.PlanHours = 7.5);
        }

        // A failure after some days were written rolls the whole request back.
        var failing = BuildService();
        failing.BeforeAbsenceCommit = () => throw new InvalidOperationException("boom");
        var failed = await failing.CreateAbsenceAsync(new PlannerAbsenceCreateModel
        {
            AssignedSiteId = worker.Id, From = d1, To = d3, MessageId = 2
        });
        Assert.That(failed.Success, Is.False);
        Assert.That(await TimePlanningPnDbContext!.AbsenceRequests.CountAsync(), Is.EqualTo(0));
        Assert.That((await RowsAsync(WorkerUid)).All(r => r.MessageId == null), Is.True, "one transaction");

        // A worker outside the caller's scope is not found; one inside it the
        // caller does not manage (here: the manager's own site) is refused.
        var outOfScope = await BuildService().CreateAbsenceAsync(new PlannerAbsenceCreateModel
        {
            AssignedSiteId = other.Id, From = d1, To = d3, MessageId = 2
        });
        Assert.That(outOfScope.Message, Is.EqualTo("PlannerWorkerNotFound"));
        var ownSite = await TimePlanningPnDbContext!.AssignedSites.AsNoTracking().SingleAsync(x => x.SiteId == ManagerUid);
        var notManaged = await BuildService().CreateAbsenceAsync(new PlannerAbsenceCreateModel
        {
            AssignedSiteId = ownSite.Id, From = d1, To = d3, MessageId = 2
        });
        Assert.That(notManaged.Success, Is.False);
        Assert.That(notManaged.Message, Is.EqualTo("UnauthorizedToApprove"));

        // Locked days refuse the whole request and are listed.
        // d2 already has a row (one row per worker and day), so reconcile it.
        await TimePlanningPnDbContext!.PlanRegistrations
            .Where(r => r.SdkSitId == WorkerUid && r.Date == d2)
            .ExecuteUpdateAsync(x => x
                .SetProperty(r => r.Reconciled, true)
                .SetProperty(r => r.ReconciledAt, DateTime.UtcNow));
        var locked = await BuildService().CreateAbsenceAsync(new PlannerAbsenceCreateModel
        {
            AssignedSiteId = worker.Id, From = d1, To = d3, MessageId = 2
        });
        Assert.That(locked.Success, Is.False);
        Assert.That(locked.Model!.LockedDates, Is.EqualTo(new[] { d1, d2 }));
        Assert.That(await TimePlanningPnDbContext!.AbsenceRequests.CountAsync(), Is.EqualTo(0));
        Assert.That((await RowsAsync(WorkerUid)).All(r => r.MessageId == null), Is.True);
        await PushService.DidNotReceiveWithAnyArgs().SendToSiteAsync(default, default!, default!, default);
    }

    // --------------------------------------------------------------- import

    private static DateTime WindowStart =>
        MondayOnOrBefore(Today).AddDays(-7 * RotationInference.HistoryWeeks);

    /// <summary>Rows from the window start to two weeks ahead, PlanText per (week, weekday).</summary>
    private async Task SeedHistoryAsync(int siteUid, Func<int, int, string> text)
    {
        var end = MondayOnOrBefore(Today).AddDays(14);
        for (var date = WindowStart; date < end; date = date.AddDays(1))
        {
            var offset = (date - WindowStart).Days;
            TimePlanningPnDbContext!.PlanRegistrations.Add(new PlanRegistration
            {
                SdkSitId = siteUid, Date = date, PlanText = text(offset / 7, offset % 7),
                CommentOffice = "", CommentOfficeAll = "", CreatedByUserId = 1, UpdatedByUserId = 1,
                WorkflowState = Constants.WorkflowStates.Created, Version = 1,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
        }
        await TimePlanningPnDbContext!.SaveChangesAsync();
    }

    private static string ThreeWeekRotation(int week, int dow) =>
        dow >= 5 ? "" : (week % 3) switch { 0 => "7-15/0,5", 1 => "15:00-23:00/0.5", _ => "Fri" };

    private async Task<(AssignedSite Recognised, AssignedSite Inconsistent, AssignedSite OffSheet)> SeedImportAsync(
        bool asAdmin = true)
    {
        if (asAdmin)
        {
            await SeedAdminCallerAsync(UserService);
        }

        var recognised = await CreateWorkerAsync(8601, useGoogleSheet: true);
        await SeedHistoryAsync(8601, ThreeWeekRotation);
        var inconsistent = await CreateWorkerAsync(8602, useGoogleSheet: true);
        // The text cycles every 9 days, which no rotation of 1..8 weeks can match.
        await SeedHistoryAsync(8602, (week, dow) => $"{6 + (week * 7 + dow) % 9}:00-{10 + (week * 7 + dow) % 9}:00");
        var offSheet = await CreateWorkerAsync(8603);
        var resigned = await CreateWorkerAsync(8604, useGoogleSheet: true);
        resigned.Resigned = true;
        await resigned.Update(TimePlanningPnDbContext!);
        return (recognised, inconsistent, offSheet);
    }

    [Test]
    public async Task ImportPreview_InfersTheRotationOfEachSheetWorker()
    {
        var (recognised, inconsistent, _) = await SeedImportAsync();

        var preview = await BuildService().GetImportPreviewAsync();

        Assert.That(preview.Success, Is.True, preview.Message);
        Assert.That(preview.Model!.WindowStart, Is.EqualTo(WindowStart));
        Assert.That(preview.Model.Workers.Select(w => w.AssignedSiteId),
            Is.EquivalentTo(new[] { recognised.Id, inconsistent.Id }), "only active workers on the sheet");
        var r = preview.Model.Workers.Single(w => w.AssignedSiteId == recognised.Id);
        Assert.That((r.Recognised, r.RotationWeeks, r.AnchorDate, r.SiteId, r.SiteName),
            Is.EqualTo((true, (int?)3, (DateTime?)WindowStart, 8601, "Worker 8601")));
        Assert.That(r.Days, Has.Count.EqualTo(21));
        Assert.That(r.Days.Single(d => d.WeekIndex == 1 && d.DayOfWeek == 0).PlanText, Is.EqualTo("15:00-23:00/0.5"));
        Assert.That(r.Days.Single(d => d.WeekIndex == 0 && d.DayOfWeek == 0).PlanText, Is.EqualTo("7:00-15:00/0.5"));
        Assert.That(r.Days.Single(d => d.WeekIndex == 2 && d.DayOfWeek == 0).PlanText, Is.EqualTo(""));
        var i = preview.Model.Workers.Single(w => w.AssignedSiteId == inconsistent.Id);
        Assert.That((i.Recognised, i.Reason, i.Days.Count), Is.EqualTo((false, (string?)RotationInference.ReasonInconsistent, 0)));
    }

    [Test]
    public async Task ImportApply_CreatesThePatterns_TakesEveryoneOffTheSheet_AndRetiresIt_SurvivingARestart()
    {
        var (recognised, inconsistent, offSheet) = await SeedImportAsync();
        var service = BuildService();
        var preview = (await service.GetImportPreviewAsync()).Model!;
        var proposal = preview.Workers.Single(w => w.Recognised);

        var applied = await service.ApplyImportAsync(new PlannerImportApplyModel
        {
            Patterns =
            [
                new PlannerImportPatternModel
                {
                    AssignedSiteId = proposal.AssignedSiteId, RotationWeeks = proposal.RotationWeeks!.Value,
                    AnchorDate = proposal.AnchorDate!.Value, Days = proposal.Days
                }
            ]
        });

        Assert.That(applied.Success, Is.True, applied.Message);
        Assert.That(applied.Model!.Workers.Single().AssignedSiteId, Is.EqualTo(recognised.Id));
        Assert.That(applied.Model.Workers.Single().RowsWritten, Is.GreaterThan(0));
        Assert.That(await IsRetiredAsync(), Is.True);
        await using (var fresh = CreateTimePlanningPnDbContext())
        {
            var pattern = await fresh.PlanPatterns.AsNoTracking()
                .SingleAsync(x => x.AssignedSiteId == recognised.Id && x.WorkflowState != Constants.WorkflowStates.Removed);
            Assert.That((pattern.RotationWeeks, pattern.AnchorDate, pattern.ValidFrom), Is.EqualTo((3, WindowStart, Today)));
            Assert.That(await fresh.AssignedSites.AsNoTracking()
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .AllAsync(x => !x.UseGoogleSheetAsDefault), Is.True, "every worker is off the sheet");
            Assert.That(await fresh.PlanPatterns.AnyAsync(x => x.AssignedSiteId == inconsistent.Id), Is.False);
        }

        // The rows from today follow the pattern.
        var expected = ThreeWeekRotation((MondayOnOrBefore(Today.AddDays(21)) - WindowStart).Days / 7, 0);
        Assert.That((await RowAsync(8601, MondayOnOrBefore(Today.AddDays(21)))).PlanText,
            Is.EqualTo(PlannerShortForm.Canonicalise(expected)));

        // A plugin restart neither un-retires the sheet nor puts anyone back on it.
        new EformTimePlanningPlugin().SeedDatabase(PluginConnectionString);
        Assert.That(await IsRetiredAsync(), Is.True);
        await using (var fresh = CreateTimePlanningPnDbContext())
        {
            Assert.That(await fresh.AssignedSites.AsNoTracking().AnyAsync(x => x.UseGoogleSheetAsDefault), Is.False);
        }

        // Once retired there is nothing to import.
        Assert.That((await service.GetImportPreviewAsync()).Success, Is.False);
        Assert.That((await service.ApplyImportAsync(new PlannerImportApplyModel())).Success, Is.False);
        _ = offSheet;
    }

    [Test]
    public async Task ImportApply_IsAtomic_AndRefusesAnInvalidPattern()
    {
        var (recognised, inconsistent, offSheet) = await SeedImportAsync();
        var service = BuildService();
        var proposal = (await service.GetImportPreviewAsync()).Model!.Workers.Single(w => w.Recognised);
        var good = new PlannerImportPatternModel
        {
            AssignedSiteId = proposal.AssignedSiteId, RotationWeeks = 3, AnchorDate = proposal.AnchorDate!.Value,
            Days = proposal.Days
        };

        var invalid = await service.ApplyImportAsync(new PlannerImportApplyModel
        {
            Patterns = [good, new PlannerImportPatternModel { AssignedSiteId = inconsistent.Id, RotationWeeks = 9, AnchorDate = WindowStart }]
        });
        Assert.That(invalid.Success, Is.False);

        // The apply service names a missing worker with a literal that is not
        // a localization key; the planner answers with its own key.
        var missing = await service.ApplyImportAsync(new PlannerImportApplyModel
        {
            Patterns = [new PlannerImportPatternModel
            {
                AssignedSiteId = recognised.Id + 100000, RotationWeeks = 3, AnchorDate = proposal.AnchorDate!.Value,
                Days = proposal.Days
            }]
        });
        Assert.That((missing.Success, missing.Message), Is.EqualTo((false, "PlannerWorkerNotFound")));

        // Only workers on the sheet get a pattern from the import.
        var notOnSheet = await service.ApplyImportAsync(new PlannerImportApplyModel
        {
            Patterns = [good, new PlannerImportPatternModel
            {
                AssignedSiteId = offSheet.Id, RotationWeeks = 3, AnchorDate = proposal.AnchorDate!.Value,
                Days = proposal.Days
            }]
        });
        Assert.That((notOnSheet.Success, notOnSheet.Message), Is.EqualTo((false, "PlannerWorkerNotFound")));

        var failingApply = BuildApplyService();
        failingApply.BeforeImportCommit = () => throw new InvalidOperationException("boom");
        var failed = await BuildService(failingApply).ApplyImportAsync(new PlannerImportApplyModel { Patterns = [good] });
        Assert.That(failed.Success, Is.False);

        Assert.That(await IsRetiredAsync(), Is.False);
        await using var fresh = CreateTimePlanningPnDbContext();
        Assert.That(await fresh.PlanPatterns.CountAsync(), Is.EqualTo(0));
        Assert.That((await fresh.AssignedSites.AsNoTracking().SingleAsync(x => x.Id == recognised.Id)).UseGoogleSheetAsDefault,
            Is.True);
    }

    // Retiring or restoring the sheet affects every worker on it: a caller who
    // cannot plan all of them may preview their own, but not apply or toggle.
    [Test]
    public async Task ImportAndSettings_NeedFullScope_AndThePreviewIsScoped()
    {
        await SeedManagerCallerAsync(UserService, 8610, 8601);
        var (recognised, inconsistent, _) = await SeedImportAsync(asAdmin: false);
        var service = BuildService();

        var preview = await service.GetImportPreviewAsync();
        Assert.That(preview.Success, Is.True, preview.Message);
        var ids = preview.Model!.Workers.Select(w => w.AssignedSiteId).ToList();
        Assert.That(ids, Does.Contain(recognised.Id));
        Assert.That(ids, Does.Not.Contain(inconsistent.Id), "a sheet worker outside the caller's scope");
        var proposal = preview.Model.Workers.Single(w => w.AssignedSiteId == recognised.Id);

        var apply = await service.ApplyImportAsync(new PlannerImportApplyModel
        {
            Patterns = [new PlannerImportPatternModel
            {
                AssignedSiteId = recognised.Id, RotationWeeks = 3, AnchorDate = proposal.AnchorDate!.Value,
                Days = proposal.Days
            }]
        });
        Assert.That((apply.Success, apply.Message), Is.EqualTo((false, "PlannerNeedsFullScope")));

        foreach (var retired in new[] { true, false })
        {
            var put = await service.UpdateSettingsAsync(new PlannerSettingsModel { GoogleSheetRetired = retired });
            Assert.That((put.Success, put.Message), Is.EqualTo((false, "PlannerNeedsFullScope")), $"retired={retired}");
        }

        Assert.That((await service.GetSettingsAsync()).Success, Is.True, "the settings stay readable");
        Assert.That(await IsRetiredAsync(), Is.False);
        await using var fresh = CreateTimePlanningPnDbContext();
        Assert.That(await fresh.PlanPatterns.CountAsync(), Is.EqualTo(0));
        Assert.That(await fresh.AssignedSites.AsNoTracking()
            .Where(x => x.Id == recognised.Id || x.Id == inconsistent.Id)
            .AllAsync(x => x.UseGoogleSheetAsDefault), Is.True, "nothing written");
    }

    // Full scope means the whole organisation, not the workers on the sheet:
    // once the sheet is retired nobody is on it, and restoring it re-seeds
    // everyone onto it at the next start.
    [Test]
    public async Task SettingsAndImport_NeedOrgWideScope_EvenWhenNobodyIsOnTheSheet()
    {
        await SeedAdminCallerAsync(UserService);
        await CreateWorkerAsync(8801);
        await CreateWorkerAsync(8802);
        var service = BuildService();
        Assert.That((await service.UpdateSettingsAsync(new PlannerSettingsModel { GoogleSheetRetired = true })).Success,
            Is.True, "an admin sees everyone");

        // Now a manager of 8801 only; 8802 is outside their scope.
        await SeedManagerCallerAsync(UserService, 8810, 8801);
        await TimePlanningPnDbContext!.AssignedSites
            .ExecuteUpdateAsync(x => x.SetProperty(a => a.UseGoogleSheetAsDefault, false));

        var restore = await service.UpdateSettingsAsync(new PlannerSettingsModel { GoogleSheetRetired = false });
        Assert.That((restore.Success, restore.Message), Is.EqualTo((false, "PlannerNeedsFullScope")));
        Assert.That(await IsRetiredAsync(), Is.True);

        await GoogleSheetRetirement.SetRetiredAsync(TimePlanningPnDbContext!, false, 1);
        var apply = await service.ApplyImportAsync(new PlannerImportApplyModel());
        Assert.That((apply.Success, apply.Message), Is.EqualTo((false, "PlannerNeedsFullScope")),
            "no sheet workers at all, but the caller still does not see everyone");
        Assert.That(await IsRetiredAsync(), Is.False);
    }

    // ------------------------------------------------------------- settings

    [Test]
    public async Task Settings_ToggleTheRetiredFlag_AndSwitchingBackKeepsPatterns()
    {
        await SeedAdminCallerAsync(UserService);
        var worker = await CreateWorkerAsync(8701);
        await CreatePatternAsync(TimePlanningPnDbContext!, worker, Today);
        var service = BuildService();

        Assert.That((await service.GetSettingsAsync()).Model!.GoogleSheetRetired, Is.False);

        Assert.That((await service.UpdateSettingsAsync(new PlannerSettingsModel { GoogleSheetRetired = true })).Success, Is.True);
        Assert.That((await service.GetSettingsAsync()).Model!.GoogleSheetRetired, Is.True);
        Assert.That(await IsRetiredAsync(), Is.True);

        Assert.That((await service.UpdateSettingsAsync(new PlannerSettingsModel { GoogleSheetRetired = false })).Success, Is.True);
        Assert.That(await IsRetiredAsync(), Is.False);
        await using var fresh = CreateTimePlanningPnDbContext();
        Assert.That(await fresh.PlanPatterns.AnyAsync(x => x.AssignedSiteId == worker.Id
                                                         && x.WorkflowState != Constants.WorkflowStates.Removed), Is.True,
            "switching back does not delete patterns");
        Assert.That(await fresh.PluginConfigurationValueVersions.CountAsync(x => x.Name == GoogleSheetRetirement.SettingName),
            Is.EqualTo(2), "each change keeps a version, as the settings page does");
    }
}
