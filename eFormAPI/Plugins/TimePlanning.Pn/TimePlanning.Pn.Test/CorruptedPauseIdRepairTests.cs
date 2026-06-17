using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Constants;
using Microting.TimePlanningBase.Infrastructure.Data;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class CorruptedPauseIdRepairTests : TestBaseSetup
{
    [SetUp]
    public async Task SetUp()
    {
        await base.Setup();
    }

    [TearDown]
    public new async Task TearDown()
    {
        await base.TearDown();
    }

    // ---- B0: characterize the timestamp-preferring netto writer ----------

    /// <summary>
    /// ComputeNettoSecondsFromDateTimeShifts prefers the pause DateTime delta
    /// and ignores a corrupt absolute-tick Pause1Id. This is the writer used by
    /// ApplyNettoFlexChainSecondPrecision (the flag-on / second-precision path).
    /// </summary>
    [Test]
    public void ComputeNetto_PrefersPauseTimestamps_OverCorruptPauseId()
    {
        var date = new DateTime(2026, 6, 10, 0, 0, 0);
        var pr = new PlanRegistration
        {
            Date = date,
            Start1StartedAt = date.AddHours(8),
            Stop1StoppedAt = date.AddHours(16),     // 8h work
            Pause1StartedAt = date.AddHours(12),
            Pause1StoppedAt = date.AddHours(12).AddMinutes(30), // 30m real pause
            Pause1Id = 145,                          // CORRUPT absolute tick (12:00)
        };

        var netto = PlanRegistrationHelper.ComputeNettoSecondsFromDateTimeShifts(pr);

        // 8h - 30m = 7h30m = 27000s, derived from timestamps not the id.
        Assert.That(netto, Is.EqualTo(27000));
    }

    // ---- B1 + B4: repair fixes only in-window 5-min corrupted rows --------

    [Test]
    public async Task Repair_FixesOnlyInWindow5MinCorruptedRows()
    {
        var ctx = TimePlanningPnDbContext!;
        // Anchor window membership to the rolling payroll-lock cutoff: the day
        // after the cutoff is in-window (repairable); the cutoff day itself is
        // locked / out-of-window.
        var inWindow = CorruptedPauseIdRepair.FirstUnlockedDate(DateTime.UtcNow.Date).AddDays(1);
        var locked = CorruptedPauseIdRepair.FirstUnlockedDate(DateTime.UtcNow.Date).AddDays(-1);

        // 5-min site (UseOneMinuteIntervals=false) and a 1-min site.
        var fiveMinSite = await SeedAssignedSite(ctx, siteId: 100, useOneMinute: false);
        var oneMinSite = await SeedAssignedSite(ctx, siteId: 200, useOneMinute: true);

        // (a) corrupted in-window: 30m real pause, Pause1Id=145 (absolute 12:00 tick).
        var corrupted = await SeedRow(ctx, fiveMinSite.SiteId, inWindow,
            pauseStart: 12, pauseStopMin: 30, pause1Id: 145, work: (8, 16));
        // (b) correct in-window: 30m pause, Pause1Id=7 ((30/5)+1).
        var correct = await SeedRow(ctx, fiveMinSite.SiteId, inWindow,
            pauseStart: 12, pauseStopMin: 30, pause1Id: 7, work: (8, 16));
        // (c) off-by-one in-window: Pause1Id=6 (min/5, missing +1) -> must be left alone.
        var offByOne = await SeedRow(ctx, fiveMinSite.SiteId, inWindow,
            pauseStart: 12, pauseStopMin: 30, pause1Id: 6, work: (8, 16));
        // (d) corrupted but out of window (on/before the locked cutoff).
        var oldRow = await SeedRow(ctx, fiveMinSite.SiteId, locked,
            pauseStart: 12, pauseStopMin: 30, pause1Id: 145, work: (8, 16));
        // (e) 1-min site, raw-minute id (30) -> not in scope.
        var oneMin = await SeedRow(ctx, oneMinSite.SiteId, inWindow,
            pauseStart: 12, pauseStopMin: 30, pause1Id: 30, work: (8, 16));

        await CorruptedPauseIdRepair.Run(ctx);

        Assert.That((await Reload(ctx, corrupted)).Pause1Id, Is.EqualTo(7));   // fixed
        Assert.That((await Reload(ctx, correct)).Pause1Id, Is.EqualTo(7));     // unchanged
        Assert.That((await Reload(ctx, offByOne)).Pause1Id, Is.EqualTo(6));    // left alone
        Assert.That((await Reload(ctx, oldRow)).Pause1Id, Is.EqualTo(145));    // out of window
        Assert.That((await Reload(ctx, oneMin)).Pause1Id, Is.EqualTo(30));     // 1-min site

        // B4: persisted netto for the repaired row is the timestamp-derived value
        // (8h work - 30m pause = 27000s), not the corrupt tick-derived netto.
        Assert.That((await Reload(ctx, corrupted)).NettoHoursInSeconds, Is.EqualTo(27000));
    }

    // ---- B3: idempotency ---------------------------------------------------

    [Test]
    public async Task Repair_IsIdempotent()
    {
        var ctx = TimePlanningPnDbContext!;
        var site = await SeedAssignedSite(ctx, siteId: 100, useOneMinute: false);
        var inWindow = CorruptedPauseIdRepair.FirstUnlockedDate(DateTime.UtcNow.Date).AddDays(1);
        var row = await SeedRow(ctx, site.SiteId, inWindow,
            pauseStart: 12, pauseStopMin: 30, pause1Id: 145, work: (8, 16));

        await CorruptedPauseIdRepair.Run(ctx);
        var afterFirst = (await Reload(ctx, row)).Pause1Id;
        await CorruptedPauseIdRepair.Run(ctx);
        var afterSecond = (await Reload(ctx, row)).Pause1Id;

        Assert.That(afterFirst, Is.EqualTo(7));
        Assert.That(afterSecond, Is.EqualTo(7));
    }

    // ---- anomaly path never writes ----------------------------------------

    /// <summary>
    /// A corrupt absolute-tick Pause1Id with NO usable pause timestamps is an
    /// unrepairable anomaly: the repair has no oracle to derive the true
    /// duration from, so it must NOT guess/write. The id is left untouched
    /// (the Sentry warning side-effect can't be asserted in this harness).
    /// </summary>
    [Test]
    public async Task Repair_DoesNotWrite_WhenCorruptIdHasNoTimestamps()
    {
        var ctx = TimePlanningPnDbContext!;
        var site = await SeedAssignedSite(ctx, siteId: 100, useOneMinute: false);

        // In-window 5-min row: corrupt absolute tick (145) but no pause
        // timestamps to repair from; shift span is a normal 08:00-16:00.
        var inWindow = CorruptedPauseIdRepair.FirstUnlockedDate(DateTime.UtcNow.Date).AddDays(1);
        var row = await SeedRow(ctx, site.SiteId, inWindow,
            pauseStart: 12, pauseStopMin: 30, pause1Id: 145, work: (8, 16),
            seedPauseTimestamps: false);

        await CorruptedPauseIdRepair.Run(ctx);

        Assert.That((await Reload(ctx, row)).Pause1Id, Is.EqualTo(145)); // untouched
    }

    // ---- helpers -----------------------------------------------------------

    private static async Task<AssignedSiteEntity> SeedAssignedSite(
        TimePlanningPnDbContext ctx, int siteId, bool useOneMinute)
    {
        var site = new AssignedSiteEntity
        {
            SiteId = siteId,
            UseOneMinuteIntervals = useOneMinute,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await site.Create(ctx);
        return site;
    }

    private static async Task<PlanRegistration> SeedRow(
        TimePlanningPnDbContext ctx, int sdkSitId, DateTime date,
        int pauseStart, int pauseStopMin, int pause1Id, (int Start, int Stop) work,
        bool seedPauseTimestamps = true)
    {
        var pr = new PlanRegistration
        {
            Date = date,
            SdkSitId = sdkSitId,
            Start1StartedAt = date.AddHours(work.Start),
            Stop1StoppedAt = date.AddHours(work.Stop),
            Pause1StartedAt = seedPauseTimestamps ? date.AddHours(pauseStart) : (DateTime?)null,
            Pause1StoppedAt = seedPauseTimestamps ? date.AddHours(pauseStart).AddMinutes(pauseStopMin) : (DateTime?)null,
            Pause1Id = pause1Id,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await pr.Create(ctx);
        return pr;
    }

    private static async Task<PlanRegistration> Reload(
        TimePlanningPnDbContext ctx, PlanRegistration row)
    {
        return await ctx.PlanRegistrations
            .AsNoTracking()
            .FirstAsync(x => x.Id == row.Id);
    }
}
