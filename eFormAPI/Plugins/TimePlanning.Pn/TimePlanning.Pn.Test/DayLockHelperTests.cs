using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Constants;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;
using PlanRegistrationEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration;

namespace TimePlanning.Pn.Test;

/// <summary>
/// The lock is derived, never stored: a day is locked when the worker has any
/// Reconciled day at or after it. These tests pin that derivation, because
/// every enforcement layer downstream trusts it.
/// </summary>
[TestFixture]
public class DayLockHelperTests : TestBaseSetup
{
    [SetUp]
    public async Task SetUpTest() => await base.Setup();

    // No workflowState parameter: PnBase.Create overwrites it with "created"
    // regardless of what is set here.
    private async Task Seed(int site, DateTime date, bool reconciled)
    {
        await new PlanRegistrationEntity
        {
            SdkSitId = site,
            Date = date,
            Reconciled = reconciled,
            ReconciledAt = reconciled ? new DateTime(2026, 1, 20, 9, 12, 0) : (DateTime?)null,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);
    }

    [Test]
    public async Task LockedThrough_NoReconciledDay_IsNull()
    {
        await Seed(700, new DateTime(2026, 1, 12), reconciled: false);

        var result = await DayLockHelper.LockedThroughAsync(TimePlanningPnDbContext!, 700);

        Assert.That(result, Is.Null, "a worker with nothing reconciled has no boundary");
    }

    [Test]
    public async Task LockedThrough_SeveralReconciled_IsTheLatest()
    {
        // The plain row sits ABOVE the boundary (2026-01-18, not the
        // 14th): with the interceptor attached, creating an unreconciled row
        // at or below an already-seeded boundary would throw during arrange,
        // before this test ever got to its assertion. Seeding it above also
        // strengthens the test: a plain row past the reconciled max proves
        // MAX ignores non-reconciled rows, not just that it ignores earlier
        // reconciled ones.
        await Seed(701, new DateTime(2026, 1, 12), reconciled: true);
        await Seed(701, new DateTime(2026, 1, 16), reconciled: true);
        await Seed(701, new DateTime(2026, 1, 18), reconciled: false);

        var result = await DayLockHelper.LockedThroughAsync(TimePlanningPnDbContext!, 701);

        Assert.That(result, Is.EqualTo(new DateTime(2026, 1, 16)));
    }

    [Test]
    public async Task LockedThrough_IgnoresRemovedRows()
    {
        // The fixture context carries the day-lock interceptor, so any write
        // that touches a PlanRegistration at or before the current boundary
        // (I3) is rejected. That rules out the obvious seeding -- reconcile a row,
        // then soft-delete it in a LATER, separate save -- because by the
        // time of that second save the boundary would already be the row
        // being deleted, and the delete would land ON the boundary day.
        //
        // Instead: seed 702/12 reconciled (this is and stays the boundary),
        // and seed 702/18 PLAIN (not reconciled, so it holds no boundary and
        // simply sits above the existing one). Then, in a SINGLE save on the
        // tracked 18th entity, set Reconciled + ReconciledAt and soft-delete
        // it via Delete() (PnBase.Delete only ever issues one SaveChanges
        // when there are pending changes). At the moment that save runs, the
        // DB boundary is still the 12th, so the write to the 18th is above
        // the boundary and permitted, with or without the interceptor
        // attached. The row ends up Removed, so it must never surface as the
        // new boundary.
        await Seed(702, new DateTime(2026, 1, 12), reconciled: true);
        await Seed(702, new DateTime(2026, 1, 18), reconciled: false);

        var later = await TimePlanningPnDbContext!.PlanRegistrations
            .FirstAsync(x => x.SdkSitId == 702 && x.Date == new DateTime(2026, 1, 18));
        later.Reconciled = true;
        later.ReconciledAt = new DateTime(2026, 1, 20, 9, 12, 0);
        await later.Delete(TimePlanningPnDbContext!);

        var result = await DayLockHelper.LockedThroughAsync(TimePlanningPnDbContext!, 702);

        Assert.That(result, Is.EqualTo(new DateTime(2026, 1, 12)),
            "a soft-deleted reconciled row must not hold the boundary");
    }

    [Test]
    public async Task LockedThrough_IsPerWorker()
    {
        await Seed(703, new DateTime(2026, 1, 16), reconciled: true);
        await Seed(704, new DateTime(2026, 1, 12), reconciled: false);

        // Await FIRST, then assert synchronously. Assert.Multiple(async () => ...)
        // binds to the Action overload, making the lambda async void: its
        // assertions can run after the block exits, and a failure is then lost
        // or blamed on the wrong test. NUnit.Analyzers flags this.
        var seven03 = await DayLockHelper.LockedThroughAsync(TimePlanningPnDbContext!, 703);
        var seven04 = await DayLockHelper.LockedThroughAsync(TimePlanningPnDbContext!, 704);

        Assert.Multiple(() =>
        {
            Assert.That(seven03, Is.EqualTo(new DateTime(2026, 1, 16)));
            Assert.That(seven04, Is.Null, "one worker's boundary must not leak onto another");
        });
    }

    [Test]
    public async Task LockedThroughForSites_ResolvesManyInOneQuery()
    {
        await Seed(705, new DateTime(2026, 1, 16), reconciled: true);
        await Seed(706, new DateTime(2026, 1, 13), reconciled: true);
        await Seed(707, new DateTime(2026, 1, 13), reconciled: false);

        var map = await DayLockHelper.LockedThroughForSitesAsync(
            TimePlanningPnDbContext!, new[] { 705, 706, 707 });

        Assert.Multiple(() =>
        {
            Assert.That(map[705], Is.EqualTo(new DateTime(2026, 1, 16)));
            Assert.That(map[706], Is.EqualTo(new DateTime(2026, 1, 13)));
            Assert.That(map[707], Is.Null);
            Assert.That(map.Count, Is.EqualTo(3), "every requested site gets an entry");
        });
    }

    [TestCase("2026-01-10", true,  TestName = "IsLocked_BeforeBoundary_True")]
    [TestCase("2026-01-16", true,  TestName = "IsLocked_AtBoundary_True")]
    [TestCase("2026-01-17", false, TestName = "IsLocked_AfterBoundary_False")]
    public void IsLocked_RelativeToBoundary(string date, bool expectedLocked)
    {
        var boundary = new DateTime(2026, 1, 16);

        var locked = DayLockHelper.IsLocked(boundary, DateTime.Parse(date));

        Assert.That(locked, Is.EqualTo(expectedLocked));
    }

    [Test]
    public void IsLocked_NoBoundary_NothingIsLocked()
    {
        Assert.That(DayLockHelper.IsLocked(null, new DateTime(2020, 1, 1)), Is.False);
    }

    [Test]
    public void IsLocked_IgnoresTimeOfDay()
    {
        var boundary = new DateTime(2026, 1, 16);

        Assert.That(DayLockHelper.IsLocked(boundary, new DateTime(2026, 1, 16, 23, 59, 59)),
            Is.True, "the boundary day is locked for its whole length");
    }

    /// <summary>
    /// UtcNow, not Now, on BOTH sides, because CanReconcile reads UtcNow. The
    /// shipped container has no TZ set so the two agree there; on a dev machine
    /// they diverge, and then DateTime.Now here breaks in either direction:
    ///  - UTC+N, just after local midnight (Now.Date is a day AHEAD of
    ///    UtcNow.Date): the "yesterday is reconcilable" assertion fails, because
    ///    Now.Date.AddDays(-1) IS UtcNow.Date, i.e. still today in UTC.
    ///  - UTC-N, late in the local evening (Now.Date is a day BEHIND): the
    ///    "today is not reconcilable" assertion fails instead, because Now.Date
    ///    is already yesterday in UTC and so genuinely reconcilable.
    /// </summary>
    [Test]
    public void CanReconcile_TodayAndFuture_False_Past_True()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DayLockHelper.CanReconcile(DateTime.UtcNow.Date), Is.False,
                "today must stay open so time can still be registered");
            Assert.That(DayLockHelper.CanReconcile(DateTime.UtcNow.Date.AddDays(1)), Is.False);
            Assert.That(DayLockHelper.CanReconcile(DateTime.UtcNow.Date.AddDays(-1)), Is.True);
            Assert.That(DayLockHelper.CanReconcile(DateTime.UtcNow.Date.AddHours(23)), Is.False,
                "a time-of-day on today is still today");
        });
    }

    /// <summary>
    /// WhereOpen is the SQL-side twin of IsLocked, used by bulk writers that
    /// must never load a locked row. Pins the equivalence, time of day
    /// included, so the two cannot drift apart.
    /// </summary>
    [Test]
    public void WhereOpen_KeepsExactlyTheDaysIsLockedLeavesOpen()
    {
        var boundary = new DateTime(2026, 1, 18);
        var dates = new[]
        {
            boundary.AddDays(-1),
            boundary,
            boundary.AddHours(23).AddMinutes(59),
            boundary.AddDays(1),
            boundary.AddDays(1).AddHours(6)
        };
        var rows = dates.Select(d => new PlanRegistrationEntity { Date = d }).AsQueryable();

        Assert.Multiple(() =>
        {
            Assert.That(rows.WhereOpen(boundary).Select(x => x.Date),
                Is.EqualTo(dates.Where(d => !DayLockHelper.IsLocked(boundary, d))));
            Assert.That(rows.WhereOpen(null).Count(), Is.EqualTo(dates.Length),
                "no boundary, nothing is locked");
        });
    }
}
