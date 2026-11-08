using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Kanban #5583: days approved before #5568 carry the flag the old name-based
/// mapping gave them (OtherAllowedAbsence for message 7/8/9 on base-seeded DBs).
/// AbsenceFlagRepair re-applies the id-based mapping at start-up.
/// </summary>
[TestFixture]
public class AbsenceFlagRepairTests : TestBaseSetup
{
    private const int ChildrenFirstSick = 7;
    private const int ChildrenSecondSick = 8;
    private const int TimeOff = 9;
    private const int Sick = 3;

    [Test]
    public async Task Run_FixesMisflaggedUnlockedDays_AndTouchesOnlyTheFlags()
    {
        var sick7 = await SeedApprovedDay(1, new DateTime(2024, 3, 1), ChildrenFirstSick, other: true);
        var sick8 = await SeedApprovedDay(1, new DateTime(2024, 3, 2), ChildrenSecondSick, other: true);
        var timeOff = await SeedApprovedDay(1, new DateTime(2024, 3, 3), TimeOff, other: true);

        var fixedCount = await AbsenceFlagRepair.Run(TimePlanningPnDbContext);

        Assert.That(fixedCount, Is.EqualTo(3));
        await AssertFlags(sick7.Id, sick: true);
        await AssertFlags(sick8.Id, sick: true);
        await AssertFlags(timeOff.Id, onVacation: true);

        var after = await Reload(timeOff.Id);
        Assert.That(after.PlanHoursInSeconds, Is.EqualTo(timeOff.PlanHoursInSeconds), "plan hours untouched");
        Assert.That(after.MessageId, Is.EqualTo(TimeOff), "message untouched");
    }

    [Test]
    public async Task Run_LeavesLockedDayAlone()
    {
        const int sdkSitId = 2;
        var locked = await SeedApprovedDay(sdkSitId, new DateTime(2024, 3, 1), ChildrenFirstSick, other: true);
        // A later reconciled day locks everything up to and including it.
        await new PlanRegistration
        {
            SdkSitId = sdkSitId,
            Date = new DateTime(2024, 3, 5),
            Reconciled = true,
            ReconciledAt = new DateTime(2024, 3, 6, 9, 0, 0),
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext);

        var fixedCount = await AbsenceFlagRepair.Run(TimePlanningPnDbContext);

        Assert.That(fixedCount, Is.Zero);
        await AssertFlags(locked.Id, other: true);
        Assert.That((await Reload(locked.Id)).Version, Is.EqualTo(locked.Version));
    }

    [Test]
    public async Task Run_LeavesCorrectAndNonRequestAndNonApprovedDaysAlone()
    {
        var correct = await SeedApprovedDay(3, new DateTime(2024, 3, 1), Sick, sick: true);
        var correctTimeOff = await SeedApprovedDay(3, new DateTime(2024, 3, 2), TimeOff, onVacation: true);
        var pending = await SeedApprovedDay(3, new DateTime(2024, 3, 3), TimeOff, other: true,
            status: AbsenceRequestStatus.Pending);

        // Flagged by hand, not by a request.
        var manual = new PlanRegistration
        {
            SdkSitId = 3,
            Date = new DateTime(2024, 3, 4),
            MessageId = TimeOff,
            OtherAllowedAbsence = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await manual.Create(TimePlanningPnDbContext);
        TimePlanningPnDbContext!.ChangeTracker.Clear();

        var fixedCount = await AbsenceFlagRepair.Run(TimePlanningPnDbContext);

        Assert.That(fixedCount, Is.Zero);
        await AssertFlags(correct.Id, sick: true);
        await AssertFlags(correctTimeOff.Id, onVacation: true);
        await AssertFlags(pending.Id, other: true);
        await AssertFlags(manual.Id, other: true);
        Assert.That((await Reload(correct.Id)).Version, Is.EqualTo(correct.Version));
        Assert.That((await Reload(manual.Id)).Version, Is.EqualTo(manual.Version));
    }

    [Test]
    public async Task Run_SecondRunIsANoOp()
    {
        var day = await SeedApprovedDay(4, new DateTime(2024, 3, 1), TimeOff, other: true);

        Assert.That(await AbsenceFlagRepair.Run(TimePlanningPnDbContext!), Is.EqualTo(1));
        TimePlanningPnDbContext!.ChangeTracker.Clear();
        var afterFirst = await Reload(day.Id);

        Assert.That(await AbsenceFlagRepair.Run(TimePlanningPnDbContext!), Is.Zero);
        var afterSecond = await Reload(day.Id);
        Assert.That(afterSecond.Version, Is.EqualTo(afterFirst.Version));
        Assert.That(afterSecond.UpdatedAt, Is.EqualTo(afterFirst.UpdatedAt));
        await AssertFlags(day.Id, onVacation: true);
    }

    /// <summary>
    /// A request with one day and the PlanRegistration approval left for it,
    /// carrying the given (possibly wrong) flags.
    /// </summary>
    private async Task<PlanRegistration> SeedApprovedDay(int sdkSitId, DateTime date, int messageId,
        bool onVacation = false, bool sick = false, bool other = false,
        AbsenceRequestStatus status = AbsenceRequestStatus.Approved)
    {
        var request = new AbsenceRequest
        {
            RequestedBySdkSitId = sdkSitId,
            DateFrom = date,
            DateTo = date,
            Status = status,
            RequestedAtUtc = DateTime.UtcNow,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await request.Create(TimePlanningPnDbContext);
        await new AbsenceRequestDay
        {
            AbsenceRequestId = request.Id,
            Date = date,
            MessageId = messageId,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext);

        var pr = new PlanRegistration
        {
            SdkSitId = sdkSitId,
            Date = date,
            PlanHoursInSeconds = 27000,
            MessageId = messageId,
            AbsenceRequestId = request.Id,
            AbsenceMessageId = messageId,
            OnVacation = onVacation,
            Sick = sick,
            OtherAllowedAbsence = other,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await pr.Create(TimePlanningPnDbContext);
        TimePlanningPnDbContext!.ChangeTracker.Clear();
        return pr;
    }

    private Task<PlanRegistration> Reload(int id) =>
        TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking().FirstAsync(x => x.Id == id);

    private async Task AssertFlags(int id, bool onVacation = false, bool sick = false, bool other = false)
    {
        var pr = await Reload(id);
        var day = pr.Date.ToString("yyyy-MM-dd");
        Assert.That(pr.OnVacation, Is.EqualTo(onVacation), $"OnVacation of {day}");
        Assert.That(pr.Sick, Is.EqualTo(sick), $"Sick of {day}");
        Assert.That(pr.OtherAllowedAbsence, Is.EqualTo(other), $"OtherAllowedAbsence of {day}");
        Assert.That(pr.AbsenceWithoutPermission, Is.False, $"AbsenceWithoutPermission of {day}");
    }
}
