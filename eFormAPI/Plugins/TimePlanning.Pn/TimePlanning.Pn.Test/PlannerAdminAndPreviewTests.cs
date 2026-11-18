using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.AbsenceRequest;
using TimePlanning.Pn.Infrastructure.Models.Planner;
using TimePlanning.Pn.Services.AbsenceRequestService;
using static TimePlanning.Pn.Test.PlanPatternTestSeed;

#nullable enable
namespace TimePlanning.Pn.Test;

/// <summary>
/// #5489 planner, grid parity: an administrator decides absence for every
/// worker (a manager only for theirs), the absence preview decides each day
/// exactly as creating the absence does, the month view and the badge count
/// of pending requests.
/// </summary>
[TestFixture]
public class PlannerAdminAndPreviewTests : PlannerTestBase
{
    private AbsenceRequestService BuildAbsenceRequestService() =>
        new(Substitute.For<ILogger<AbsenceRequestService>>(), TimePlanningPnDbContext!, UserService, Localization,
            CoreService, SeededBaseDbContext!, Substitute.For<IServiceScopeFactory>());

    private async Task<AbsenceRequest> CreatePendingRequestAsync(int siteUid, DateTime from, DateTime to,
        AbsenceRequestStatus status = AbsenceRequestStatus.Pending)
    {
        var request = new AbsenceRequest
        {
            RequestedBySdkSitId = siteUid, DateFrom = from, DateTo = to, Status = status,
            RequestedAtUtc = DateTime.UtcNow, CreatedByUserId = 1, UpdatedByUserId = 1
        };
        await request.Create(TimePlanningPnDbContext!);
        await new AbsenceRequestDay
        {
            AbsenceRequestId = request.Id, Date = from, MessageId = 2, CreatedByUserId = 1, UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext!);
        return request;
    }

    // ------------------------------------------------------- admin override

    [Test]
    public async Task Admin_WhoIsNoManager_MayApproveEveryone_RegisterAbsence_AndDecideRequests()
    {
        await SeedAdminCallerAsync(UserService);
        var a = await CreateWorkerAsync(8701);
        await CreateWorkerAsync(8702);
        var toApprove = await CreatePendingRequestAsync(8702, Today.AddDays(40), Today.AddDays(40));
        var toReject = await CreatePendingRequestAsync(8701, Today.AddDays(60), Today.AddDays(60));

        var grid = await BuildService().GetGridAsync(new PlannerGridRequestModel { From = NextMonday, Days = 7 });
        Assert.That(grid.Success, Is.True, grid.Message);
        Assert.That(grid.Model!.Workers.Select(w => w.SiteId), Is.EquivalentTo(new[] { 8701, 8702 }));
        Assert.That(grid.Model.Workers.All(w => w.CanApproveAbsence), Is.True);
        Assert.That(grid.Model.PendingRequestCount, Is.EqualTo(2), "pending requests outside the window count");

        // Registering an absence: the admin has no SDK site, so no decider is stored.
        var (d1, d2) = (Today.AddDays(10), Today.AddDays(11));
        var created = await BuildService().CreateAbsenceAsync(new PlannerAbsenceCreateModel
        {
            AssignedSiteId = a.Id, From = d1, To = d2, MessageId = 2
        });
        Assert.That(created.Success, Is.True, created.Message);
        var request = await TimePlanningPnDbContext!.AbsenceRequests.AsNoTracking()
            .SingleAsync(x => x.Id == created.Model!.AbsenceRequestId);
        Assert.That((request.Status, request.DecidedBySdkSitId), Is.EqualTo((AbsenceRequestStatus.Approved, (int?)null)));
        Assert.That(request.DecidedAtUtc, Is.Not.Null);
        var rows = await RowsAsync(8701);
        foreach (var d in new[] { d1, d2 })
        {
            var row = rows.Single(r => r.Date == d);
            Assert.That((row.MessageId, row.AbsenceRequestId, row.AbsenceApprovedBySdkSitId),
                Is.EqualTo(((int?)2, (int?)request.Id, (int?)null)));
        }

        // The app's path (gRPC) keeps the manager-only rule; the web's lets an admin decide.
        var service = BuildAbsenceRequestService();
        var appInbox = await service.GetInboxAsync();
        Assert.That(appInbox.Model, Is.Empty);
        var webInbox = await service.GetInboxAsync(adminMayDecide: true);
        Assert.That(webInbox.Model!.Select(x => x.Id), Is.EquivalentTo(new[] { toApprove.Id, toReject.Id }));

        var refused = await service.ApproveAsync(toApprove.Id, new AbsenceRequestDecisionModel());
        Assert.That((refused.Success, refused.Message), Is.EqualTo((false, "UnauthorizedToApprove")));

        var approved = await service.ApproveAsync(toApprove.Id, new AbsenceRequestDecisionModel(), adminMayDecide: true);
        Assert.That(approved.Success, Is.True, approved.Message);
        var rejected = await service.RejectAsync(toReject.Id, new AbsenceRequestDecisionModel(), adminMayDecide: true);
        Assert.That(rejected.Success, Is.True, rejected.Message);

        await using var fresh = CreateTimePlanningPnDbContext();
        var decided = await fresh.AbsenceRequests.AsNoTracking()
            .Where(x => x.Id == toApprove.Id || x.Id == toReject.Id)
            .ToDictionaryAsync(x => x.Id);
        Assert.That((decided[toApprove.Id].Status, decided[toApprove.Id].DecidedBySdkSitId),
            Is.EqualTo((AbsenceRequestStatus.Approved, (int?)null)));
        Assert.That((decided[toReject.Id].Status, decided[toReject.Id].DecidedBySdkSitId),
            Is.EqualTo((AbsenceRequestStatus.Rejected, (int?)null)));
    }

    [Test]
    public async Task NeitherAdminNorManager_IsStillRefused()
    {
        await SeedSiteCallerAsync(UserService, 8720);
        var own = await CreateWorkerAsync(8720);
        var pending = await CreatePendingRequestAsync(8720, Today.AddDays(30), Today.AddDays(30));

        var grid = await BuildService().GetGridAsync(new PlannerGridRequestModel { From = NextMonday, Days = 7 });
        Assert.That(grid.Success, Is.True, grid.Message);
        Assert.That(grid.Model!.Workers.Single().CanApproveAbsence, Is.False);
        Assert.That(grid.Model.PendingRequestCount, Is.EqualTo(0), "not the caller's to decide");

        var body = new PlannerAbsenceCreateModel
        {
            AssignedSiteId = own.Id, From = Today.AddDays(10), To = Today.AddDays(11), MessageId = 2
        };
        var preview = await BuildService().PreviewAbsenceAsync(body);
        Assert.That((preview.Success, preview.Message), Is.EqualTo((false, "UnauthorizedToApprove")));
        var created = await BuildService().CreateAbsenceAsync(body);
        Assert.That((created.Success, created.Message), Is.EqualTo((false, "UnauthorizedToApprove")));

        var approve = await BuildAbsenceRequestService()
            .ApproveAsync(pending.Id, new AbsenceRequestDecisionModel(), adminMayDecide: true);
        Assert.That((approve.Success, approve.Message), Is.EqualTo((false, "UnauthorizedToApprove")));
    }

    // -------------------------------------------------------------- preview

    [Test]
    public async Task Preview_DecidesEachDayAsCreateDoes_AndWritesNothing()
    {
        await SeedAdminCallerAsync(UserService);
        var worker = await CreateWorkerAsync(8730);
        // No plan on Saturdays and Sundays: those template days are days off.
        worker.SaturdayPlanHours = 0;
        worker.SundayPlanHours = 0;
        await worker.Update(TimePlanningPnDbContext!);

        var from = NextMonday.AddDays(7);
        var to = from.AddDays(8); // Monday to the Tuesday after
        // Monday: an open row with a stale plan, which the template rewrites.
        await CreateRowAsync(TimePlanningPnDbContext!, 8730, from, r => r.PlanHours = 3);
        // Tuesday: a day off set by hand.
        await CreateRowAsync(TimePlanningPnDbContext!, 8730, from.AddDays(1), r => r.PlanChangedByAdmin = true);
        // Wednesday: changed by hand to a short day.
        await CreateRowAsync(TimePlanningPnDbContext!, 8730, from.AddDays(2), r =>
        {
            r.PlanChangedByAdmin = true;
            r.PlanText = "9:00-13:00";
            r.PlanHours = 4;
        });
        // The rest have no row yet.

        var rowsBefore = await RowsAsync(8730);
        var requestsBefore = await TimePlanningPnDbContext!.AbsenceRequests.CountAsync();
        var body = new PlannerAbsenceCreateModel { AssignedSiteId = worker.Id, From = from, To = to, MessageId = 2 };

        var preview = await BuildService().PreviewAbsenceAsync(body);

        Assert.That(preview.Success, Is.True, preview.Message);
        var days = preview.Model!.Days;
        Assert.That(days.Select(d => d.Date), Is.EqualTo(Enumerable.Range(0, 9).Select(i => from.AddDays(i))));
        Assert.That(days.Select(d => d.Outcome), Is.EqualTo(new[]
        {
            "apply", "dayOff", "apply", "apply", "apply", "dayOff", "dayOff", "apply", "apply"
        }));
        Assert.That(days.Select(d => d.PlanHours), Is.EqualTo(new[]
        {
            TemplateHours, 0, 4, TemplateHours, TemplateHours, 0, 0, TemplateHours, TemplateHours
        }));
        Assert.That(days[2].PlanText, Is.EqualTo("9:00-13:00"));
        Assert.That(preview.Model.WorkDays, Is.EqualTo(6));
        Assert.That(preview.Model.PlannedHours, Is.EqualTo(5 * TemplateHours + 4));

        // Nothing written: no row created or changed, no request.
        var rowsAfter = await RowsAsync(8730);
        Assert.That(rowsAfter.Select(r => (r.Id, r.Date, r.PlanHours, r.MessageId)),
            Is.EqualTo(rowsBefore.Select(r => (r.Id, r.Date, r.PlanHours, r.MessageId))));
        Assert.That(await TimePlanningPnDbContext!.AbsenceRequests.CountAsync(), Is.EqualTo(requestsBefore));

        var created = await BuildService().CreateAbsenceAsync(body);

        Assert.That(created.Success, Is.True, created.Message);
        Assert.That(created.Model!.AppliedDates,
            Is.EqualTo(days.Where(d => d.Outcome == "apply").Select(d => d.Date)));
        Assert.That(created.Model.SkippedDates,
            Is.EqualTo(days.Where(d => d.Outcome != "apply").Select(d => d.Date)));
        var rows = await RowsAsync(8730);
        foreach (var day in days)
        {
            Assert.That(rows.Single(r => r.Date == day.Date).PlanHours, Is.EqualTo(day.PlanHours), day.Date.ToString("d"));
        }

        // Now the days are held by the approved request: preview says so, create refuses.
        var again = await BuildService().PreviewAbsenceAsync(body);
        Assert.That(again.Model!.Days.All(d => d.Outcome == "overlap"), Is.True);
        Assert.That(again.Model.WorkDays, Is.EqualTo(0));
        var refused = await BuildService().CreateAbsenceAsync(body);
        Assert.That(refused.Message, Is.EqualTo("OverlappingAbsenceRequestExists"));
    }

    [Test]
    public async Task Preview_ShowsTheLockedDaysCreateRefusesWith()
    {
        await SeedAdminCallerAsync(UserService);
        var worker = await CreateWorkerAsync(8731);
        var (d1, d2, d3) = (Today.AddDays(10), Today.AddDays(11), Today.AddDays(12));
        await LockThroughAsync(TimePlanningPnDbContext!, 8731, d2);
        var body = new PlannerAbsenceCreateModel { AssignedSiteId = worker.Id, From = d1, To = d3, MessageId = 2 };

        var preview = await BuildService().PreviewAbsenceAsync(body);

        Assert.That(preview.Success, Is.True, preview.Message);
        Assert.That(preview.Model!.Days.Select(d => (d.Date, d.Outcome)),
            Is.EqualTo(new[] { (d1, "locked"), (d2, "locked"), (d3, "apply") }));
        Assert.That(preview.Model.WorkDays, Is.EqualTo(1));

        var created = await BuildService().CreateAbsenceAsync(body);
        Assert.That(created.Success, Is.False);
        Assert.That(created.Model!.LockedDates,
            Is.EqualTo(preview.Model.Days.Where(d => d.Outcome == "locked").Select(d => d.Date)));
    }

    // ------------------------------------------------------ month and badge

    [Test]
    public async Task Grid_AcceptsAWholeMonth_AndRefusesOtherRanges()
    {
        await SeedAdminCallerAsync(UserService);
        await CreateWorkerAsync(8750);
        var first = new DateTime(Today.Year, Today.Month, 1).AddMonths(1);
        var daysInMonth = DateTime.DaysInMonth(first.Year, first.Month);

        var month = await BuildService().GetGridAsync(new PlannerGridRequestModel { From = first, Days = daysInMonth });

        Assert.That(month.Success, Is.True, month.Message);
        Assert.That(month.Model!.Dates, Is.EqualTo(Enumerable.Range(0, daysInMonth).Select(i => first.AddDays(i))));
        Assert.That(month.Model.To, Is.EqualTo(first.AddMonths(1).AddDays(-1)));
        Assert.That(month.Model.Workers.Single().Cells, Has.Count.EqualTo(daysInMonth));

        foreach (var (from, days) in new[] { (first, 20), (first.AddDays(1), daysInMonth), (first, daysInMonth + 1) })
        {
            var refused = await BuildService().GetGridAsync(new PlannerGridRequestModel { From = from, Days = days });
            Assert.That((refused.Success, refused.Message), Is.EqualTo((false, "PlannerGridRangeInvalid")),
                $"{from:d} + {days}");
        }
    }

    [Test]
    public async Task Grid_PendingRequestCount_IsTheManagersOwnToDecide()
    {
        await SeedManagerCallerAsync(UserService, 8760, 8761);
        await CreateWorkerAsync(8761);
        await CreateWorkerAsync(8762); // outside the manager's scope
        await CreatePendingRequestAsync(8761, Today.AddDays(3), Today.AddDays(4));
        await CreatePendingRequestAsync(8761, Today.AddDays(200), Today.AddDays(200));
        await CreatePendingRequestAsync(8761, Today.AddDays(90), Today.AddDays(90), AbsenceRequestStatus.Approved);
        await CreatePendingRequestAsync(8760, Today.AddDays(5), Today.AddDays(5)); // the manager's own
        await CreatePendingRequestAsync(8762, Today.AddDays(5), Today.AddDays(5));

        var grid = await BuildService().GetGridAsync(new PlannerGridRequestModel { From = NextMonday, Days = 14 });

        Assert.That(grid.Success, Is.True, grid.Message);
        Assert.That(grid.Model!.PendingRequestCount, Is.EqualTo(2));
    }
}
