using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Constants;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Interceptors;
using PlanRegistrationEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration;

namespace TimePlanning.Pn.Test;

/// <summary>
/// The interceptor is the only layer that is COMPLETE -- it covers all 32
/// PlanRegistration write sites and anything added later. These tests go
/// through a context built exactly as production builds it (interceptor
/// attached), so they prove the wiring, not just the class.
/// </summary>
[TestFixture]
public class DayLockInterceptorTests : TestBaseSetup
{
    [SetUp]
    public async Task SetUpTest() => await base.Setup();

    private async Task<PlanRegistrationEntity> SeedReconciled(int site, DateTime date)
    {
        var row = new PlanRegistrationEntity
        {
            SdkSitId = site, Date = date, Reconciled = true,
            ReconciledAt = new DateTime(2026, 1, 20, 9, 12, 0),
            PlanText = "", CommentOffice = "", CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1, UpdatedByUserId = 1,
        };
        await row.Create(TimePlanningPnDbContext!);
        return row;
    }

    private async Task<PlanRegistrationEntity> SeedPlain(int site, DateTime date)
    {
        var row = new PlanRegistrationEntity
        {
            SdkSitId = site, Date = date,
            PlanText = "", CommentOffice = "", CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1, UpdatedByUserId = 1,
        };
        await row.Create(TimePlanningPnDbContext!);
        return row;
    }

    [Test]
    public async Task Modifying_ADayBelowTheBoundary_IsRejected()
    {
        // Order matters: create the earlier row FIRST. Seeding the boundary
        // first would put this Create inside the locked range, and the arrange
        // step would throw before the assertion was ever reached.
        var earlier = await SeedPlain(800, new DateTime(2026, 1, 13));
        await SeedReconciled(800, new DateTime(2026, 1, 16));

        earlier.PlanHours = 9;

        Assert.ThrowsAsync<DayLockedException>(async () =>
            await earlier.Update(TimePlanningPnDbContext!));
    }

    [Test]
    public async Task Modifying_TheBoundaryDayItself_IsRejected()
    {
        var boundary = await SeedReconciled(801, new DateTime(2026, 1, 16));

        boundary.PlanHours = 9;

        Assert.ThrowsAsync<DayLockedException>(async () =>
            await boundary.Update(TimePlanningPnDbContext!));
    }

    [Test]
    public async Task Creating_ARowInsideALockedRange_IsRejected()
    {
        await SeedReconciled(802, new DateTime(2026, 1, 16));

        Assert.ThrowsAsync<DayLockedException>(async () =>
            await SeedPlain(802, new DateTime(2026, 1, 14)));
    }

    [Test]
    public async Task SoftDeleting_ALockedRow_IsRejected()
    {
        // PnBase.Delete is a soft delete -- it sets WorkflowState = Removed via
        // UpdateInternal, so this arrives at the interceptor as Modified, not
        // Deleted. The name says SoftDeleting so nobody reads a pass here as
        // proof that the EntityState.Deleted arm works.
        var earlier = await SeedPlain(803, new DateTime(2026, 1, 13));
        await SeedReconciled(803, new DateTime(2026, 1, 16));

        Assert.ThrowsAsync<DayLockedException>(async () =>
            await earlier.Delete(TimePlanningPnDbContext!));
    }

    [Test]
    public async Task Modifying_ADayAboveTheBoundary_IsAllowed()
    {
        var later = await SeedPlain(804, new DateTime(2026, 1, 18));
        await SeedReconciled(804, new DateTime(2026, 1, 16));

        later.PlanHours = 9;
        await later.Update(TimePlanningPnDbContext!);

        // AsNoTracking: reading the tracked instance back off the same context
        // would prove nothing -- it already holds the in-memory value whether
        // or not the write ever reached the database.
        var reloaded = await TimePlanningPnDbContext!.PlanRegistrations
            .AsNoTracking()
            .FirstAsync(x => x.Id == later.Id);
        Assert.That(reloaded.PlanHours, Is.EqualTo(9),
            "days after the boundary must stay fully editable");
    }

    [Test]
    public async Task ANotherWorkersBoundary_DoesNotLockThisWorker()
    {
        var other = await SeedPlain(806, new DateTime(2026, 1, 13));
        await SeedReconciled(805, new DateTime(2026, 1, 16));

        other.PlanHours = 9;
        await other.Update(TimePlanningPnDbContext!);

        var reloaded = await TimePlanningPnDbContext!.PlanRegistrations
            .AsNoTracking()
            .FirstAsync(x => x.Id == other.Id);
        Assert.That(reloaded.PlanHours, Is.EqualTo(9),
            "the boundary is per worker; it must not leak across sites");
    }

    [Test]
    public async Task SettingReconciled_OnTheBoundaryDay_IsAllowed()
    {
        // Unlocking must not be blocked by the very lock it is clearing.
        var boundary = await SeedReconciled(807, new DateTime(2026, 1, 16));

        boundary.Reconciled = false;
        boundary.ReconciledAt = null;
        await boundary.Update(TimePlanningPnDbContext!);

        var reloaded = await TimePlanningPnDbContext!.PlanRegistrations
            .AsNoTracking()
            .FirstAsync(x => x.Id == boundary.Id);
        Assert.That(reloaded.Reconciled, Is.False,
            "clearing the flag on the boundary day is how unlocking works");
    }

    [Test]
    public async Task SettingTheTransferredToPayrollFlag_OnALockedDay_IsAllowed()
    {
        // Mirrors PayrollExportService.ExportPayroll exactly (ruling F10):
        // Reconciled and TransferredToPayroll are independent, so exporting a
        // reconciled period must not be blocked by the very lock reconciling
        // it created.
        var earlier = await SeedPlain(808, new DateTime(2026, 1, 13));
        await SeedReconciled(808, new DateTime(2026, 1, 16));

        earlier.TransferredToPayroll = true;
        earlier.TransferredToPayrollAt = DateTime.UtcNow;
        await earlier.Update(TimePlanningPnDbContext!);

        var reloaded = await TimePlanningPnDbContext!.PlanRegistrations
            .AsNoTracking()
            .FirstAsync(x => x.Id == earlier.Id);
        Assert.That(reloaded.TransferredToPayroll, Is.True,
            "the payroll flag must persist on a locked day");
    }

    [Test]
    public async Task ChangingHours_AlongsideThePayrollFlag_IsRejected()
    {
        // Proves the payroll exemption is not a loophole: as soon as anything
        // else about the row changes in the same save, the lock still applies.
        var earlier = await SeedPlain(809, new DateTime(2026, 1, 13));
        await SeedReconciled(809, new DateTime(2026, 1, 16));

        earlier.TransferredToPayroll = true;
        earlier.PlanHours = 9;

        Assert.ThrowsAsync<DayLockedException>(async () =>
            await earlier.Update(TimePlanningPnDbContext!));
    }

    [Test]
    public async Task MovingALockedRowOutOfTheLockedRange_IsRejected()
    {
        // I3 is keyed on (SdkSitId, Date) at the time of the write -- it must
        // also be checked against the ORIGINAL slot, or moving a locked row's
        // Date to a day above the boundary would let it escape the lock it
        // started inside.
        var earlier = await SeedPlain(810, new DateTime(2026, 1, 13));
        await SeedReconciled(810, new DateTime(2026, 1, 16));

        earlier.Date = new DateTime(2026, 1, 20);

        Assert.ThrowsAsync<DayLockedException>(async () =>
            await earlier.Update(TimePlanningPnDbContext!));
    }

    [Test]
    public async Task UnlockingAndChangingHours_OnTheBoundaryDay_IsRejected()
    {
        // Proves the unlock exemption is not a loophole either: as soon as
        // anything besides Reconciled/ReconciledAt changes in the same save,
        // the lock still applies -- even on the boundary day itself.
        var boundary = await SeedReconciled(811, new DateTime(2026, 1, 16));

        boundary.Reconciled = false;
        boundary.ReconciledAt = null;
        boundary.PlanHours = 9;

        Assert.ThrowsAsync<DayLockedException>(async () =>
            await boundary.Update(TimePlanningPnDbContext!));
    }

    [Test]
    public async Task ClearingReconciled_BelowTheBoundary_IsRejected()
    {
        // Unlocking only ever works ON the boundary day itself -- a day
        // below it cannot be unlocked, because clearing its flag would not
        // move MAX(Date) (see DayLockHelper's doc comment): the row would
        // still sit below the (unchanged) boundary set by the later
        // reconciled day.
        var earlier = await SeedReconciled(812, new DateTime(2026, 1, 12));
        await SeedReconciled(812, new DateTime(2026, 1, 16));

        earlier.Reconciled = false;
        earlier.ReconciledAt = null;

        Assert.ThrowsAsync<DayLockedException>(async () =>
            await earlier.Update(TimePlanningPnDbContext!));
    }

    [Test]
    public async Task MovingALockedRowToAnotherWorker_IsRejected()
    {
        // Same escape as MovingALockedRowOutOfTheLockedRange_IsRejected, but
        // via SdkSitId instead of Date: without checking the ORIGINAL site,
        // reassigning a locked row to a worker with no boundary of their own
        // would let it through.
        var earlier = await SeedPlain(814, new DateTime(2026, 1, 13));
        await SeedReconciled(814, new DateTime(2026, 1, 16));

        earlier.SdkSitId = 815;

        Assert.ThrowsAsync<DayLockedException>(async () =>
            await earlier.Update(TimePlanningPnDbContext!));
    }

    [Test]
    public async Task ClearingReconciledButKeepingReconciledAt_OnTheBoundaryDay_IsRejected()
    {
        // Guards invariant I1 at the choke point: Reconciled=false with
        // ReconciledAt still set is not a valid unlock, even on the boundary
        // day itself -- the row must never end up in a state I1 forbids.
        var boundary = await SeedReconciled(816, new DateTime(2026, 1, 16));

        boundary.Reconciled = false;

        Assert.ThrowsAsync<DayLockedException>(async () =>
            await boundary.Update(TimePlanningPnDbContext!));
    }
}
