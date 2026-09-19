using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Services.ReconciliationSummaryService;
using PlanRegistrationEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Spec §3/§7. Runs against a real MariaDB (TestBaseSetup), because the
/// counting is a set of EF queries and the boundaries come from DayLockHelper.
///
/// SEEDING ORDER MATTERS. The fixture context carries
/// ReconciledDayLockInterceptor: creating or changing a row at or before a
/// site's current boundary throws DayLockedException. So per site, create the
/// plain rows first, then the reconciled rows in ascending date order (see
/// DayLockHelperTests for the full reasoning).
///
/// Unless a test says otherwise, "today" is 2026-09-20 and there is no
/// settings row, so the period is 2026-08-20..2026-09-19 (cutoff 19).
/// </summary>
[TestFixture]
public class ReconciliationSummaryServiceTests : TestBaseSetup
{
    private static readonly DateTime Today = D("2026-09-20");

    [SetUp]
    public async Task SetUpTest() => await base.Setup();

    private static DateTime D(string s) => DateTime.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private ReconciliationSummaryService Service(TimePlanningPnDbContext db = null) =>
        new(db ?? TimePlanningPnDbContext!, Substitute.For<ILogger<ReconciliationSummaryService>>());

    // PnBase.Create overwrites WorkflowState with "created"; soft-delete with row.Delete().
    private async Task<PlanRegistrationEntity> Seed(
        int site, string date,
        int planSeconds = 0, int nettoSeconds = 0,
        double planHours = 0, double nettoHours = 0,
        DateTime? start1StartedAt = null,
        DateTime? reconciledAt = null)
    {
        var row = new PlanRegistrationEntity
        {
            SdkSitId = site,
            Date = D(date),
            PlanHoursInSeconds = planSeconds,
            NettoHoursInSeconds = nettoSeconds,
            PlanHours = planHours,
            NettoHours = nettoHours,
            Start1StartedAt = start1StartedAt,
            Reconciled = reconciledAt.HasValue,
            ReconciledAt = reconciledAt,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        };
        await row.Create(TimePlanningPnDbContext!);
        return row;
    }

    [Test]
    public async Task Counts_ClassifyLockedBehindNeverAndIgnoreOutOfScopeRows()
    {
        // 801 LOCKED: hours in period, boundary == periodEnd.
        await Seed(801, "2026-08-25", planSeconds: 27000);
        await Seed(801, "2026-09-19", reconciledAt: new DateTime(2026, 9, 20, 6, 0, 0));
        // 802 BEHIND: hours in period, boundary inside the period.
        await Seed(802, "2026-09-01", nettoSeconds: 25200);
        await Seed(802, "2026-09-05", reconciledAt: new DateTime(2026, 9, 6, 8, 0, 0));
        // 803 NEVER: hours in period, nothing reconciled.
        await Seed(803, "2026-09-10", planSeconds: 27000);
        // 804 LOCKED: boundary after periodEnd still locks the whole period.
        await Seed(804, "2026-09-03", planSeconds: 27000);
        await Seed(804, "2026-09-25", reconciledAt: new DateTime(2026, 9, 26, 7, 0, 0));
        // 805 NOT IN PERIOD (only a zero-hours row in it) but HAS reconciled (outside the period).
        await Seed(805, "2026-06-10", reconciledAt: new DateTime(2026, 6, 11, 7, 0, 0));
        await Seed(805, "2026-09-12");
        // 806 NOT IN PERIOD: its only in-period row is soft-deleted.
        var removed = await Seed(806, "2026-09-02", planSeconds: 27000);
        await removed.Delete(TimePlanningPnDbContext!);
        // 807 NOT IN PERIOD: rows one day either side of the period.
        await Seed(807, "2026-08-19", planSeconds: 27000);
        await Seed(807, "2026-09-20", planSeconds: 27000);
        // 808 NEVER: row exactly on periodStart counts (inclusive).
        await Seed(808, "2026-08-20", planSeconds: 27000);

        var result = await Service().GetSummaryAsync(Today);

        Assert.That(result.Success, Is.True, result.Message);
        var m = result.Model;
        Assert.Multiple(() =>
        {
            Assert.That(m.CutoffDay, Is.EqualTo(19));
            Assert.That(m.PeriodStart, Is.EqualTo("2026-08-20"));
            Assert.That(m.PeriodEnd, Is.EqualTo("2026-09-19"));
            Assert.That(m.WorkersInPeriod, Is.EqualTo(5), "801, 802, 803, 804, 808");
            Assert.That(m.WorkersLockedThroughPeriod, Is.EqualTo(2), "801, 804");
            Assert.That(m.WorkersNeverReconciled, Is.EqualTo(2), "803, 808");
            Assert.That(m.CoveragePercent, Is.EqualTo(40.0));
            Assert.That(m.OldestBoundary, Is.EqualTo("2026-09-05"), "802's boundary; 805 is not in the period");
            Assert.That(m.WorkersWithAnyReconciled, Is.EqualTo(4), "801, 802, 804, 805 -- not limited to the period");
        });
    }

    [Test]
    public async Task RowWithOnlyAStart1Stamp_IsCounted()
    {
        // One-minute-interval mode: an exact start stamp is registered time,
        // even before net seconds have been computed for the day.
        await Seed(830, "2026-09-01", start1StartedAt: new DateTime(2026, 9, 1, 6, 58, 0));

        var result = await Service().GetSummaryAsync(Today);

        Assert.That(result.Model.WorkersInPeriod, Is.EqualTo(1));
    }

    [Test]
    public async Task RowWithOnlyPlanHoursDouble_IsCounted()
    {
        // Normal planning writers only ever set the PlanHours double --
        // PlanHoursInSeconds is written only after a content handover -- so a
        // planned-but-never-clocked worker must still count (user decision
        // 2026-09-19).
        await Seed(831, "2026-09-01", planHours: 7.5);

        var result = await Service().GetSummaryAsync(Today);

        Assert.That(result.Model.WorkersInPeriod, Is.EqualTo(1));
    }

    [Test]
    public async Task RowWithOnlyNettoHoursDouble_IsNotCounted()
    {
        // All customers run UseOneMinuteIntervals=true; the legacy NettoHours
        // double alone does not make a worker count (user decision 2026-09-19).
        await Seed(832, "2026-09-02", nettoHours: 6.25);

        var result = await Service().GetSummaryAsync(Today);

        Assert.That(result.Model.WorkersInPeriod, Is.Zero);
    }

    [Test]
    public async Task NoSettingsRow_FallsBackToCutoff19()
    {
        Assert.That(await TimePlanningPnDbContext!.PayrollIntegrationSettings
            .CountAsync(x => x.WorkflowState != Constants.WorkflowStates.Removed), Is.Zero,
            "precondition: the plugin seed creates no PayrollIntegrationSettings row");

        var result = await Service().GetSummaryAsync(Today);

        Assert.Multiple(() =>
        {
            Assert.That(result.Model.CutoffDay, Is.EqualTo(19));
            Assert.That(result.Model.PeriodStart, Is.EqualTo("2026-08-20"));
            Assert.That(result.Model.PeriodEnd, Is.EqualTo("2026-09-19"));
        });
    }

    [Test]
    public async Task SettingsRow_CutoffDrivesThePeriod()
    {
        await new PayrollIntegrationSettings { CutoffDay = 5, CreatedByUserId = 1, UpdatedByUserId = 1 }
            .Create(TimePlanningPnDbContext!);

        var result = await Service().GetSummaryAsync(Today);

        Assert.Multiple(() =>
        {
            Assert.That(result.Model.CutoffDay, Is.EqualTo(5));
            Assert.That(result.Model.PeriodStart, Is.EqualTo("2026-08-06"));
            Assert.That(result.Model.PeriodEnd, Is.EqualTo("2026-09-05"));
        });
    }

    [Test]
    public async Task RemovedSettingsRow_IsIgnored()
    {
        var settings = new PayrollIntegrationSettings { CutoffDay = 5, CreatedByUserId = 1, UpdatedByUserId = 1 };
        await settings.Create(TimePlanningPnDbContext!);
        await settings.Delete(TimePlanningPnDbContext!);

        var result = await Service().GetSummaryAsync(Today);

        Assert.That(result.Model.CutoffDay, Is.EqualTo(19));
    }

    [Test]
    public async Task SettingsRowOutOfRange_IsClampedAndReported()
    {
        await new PayrollIntegrationSettings { CutoffDay = 45, CreatedByUserId = 1, UpdatedByUserId = 1 }
            .Create(TimePlanningPnDbContext!);

        var result = await Service().GetSummaryAsync(Today);

        Assert.Multiple(() =>
        {
            Assert.That(result.Model.CutoffDay, Is.EqualTo(31));
            Assert.That(result.Model.PeriodEnd, Is.EqualTo("2026-08-31"));
        });
    }

    [Test]
    public async Task NoWorkers_CoverageAndBoundariesAreNull()
    {
        var result = await Service().GetSummaryAsync(Today);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.Multiple(() =>
        {
            Assert.That(result.Model.WorkersInPeriod, Is.Zero);
            Assert.That(result.Model.WorkersLockedThroughPeriod, Is.Zero);
            Assert.That(result.Model.WorkersNeverReconciled, Is.Zero);
            Assert.That(result.Model.CoveragePercent, Is.Null);
            Assert.That(result.Model.OldestBoundary, Is.Null);
            Assert.That(result.Model.WorkersWithAnyReconciled, Is.Zero);
            Assert.That(result.Model.LastReconciledAt, Is.Null);
        });
    }

    [Test]
    public async Task WorkersWithAnyReconciled_SpansOutsideThePeriod()
    {
        await Seed(850, "2025-01-15", reconciledAt: new DateTime(2025, 1, 16, 9, 0, 0));

        var result = await Service().GetSummaryAsync(Today);

        Assert.Multiple(() =>
        {
            Assert.That(result.Model.WorkersInPeriod, Is.Zero);
            Assert.That(result.Model.CoveragePercent, Is.Null);
            Assert.That(result.Model.WorkersWithAnyReconciled, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task CoveragePercent_IsRoundedToOneDecimal()
    {
        await Seed(860, "2026-09-01", planSeconds: 3600);
        await Seed(860, "2026-09-19", reconciledAt: new DateTime(2026, 9, 20, 6, 0, 0));
        await Seed(861, "2026-09-01", planSeconds: 3600);
        await Seed(862, "2026-09-01", planSeconds: 3600);

        var result = await Service().GetSummaryAsync(Today);

        Assert.That(result.Model.CoveragePercent, Is.EqualTo(33.3), "1 of 3");
    }

    /// <summary>
    /// datetime(6) has no offset, so EF hands back Kind Unspecified; the
    /// service must re-tag it Utc so Newtonsoft writes the trailing "Z"
    /// (same convention as PlanRegistrationHelper's ReconciledAt projection).
    /// A soft-deleted reconciled row must not count -- it is seeded with the
    /// LATEST stamp so a missing filter would show up as the wrong max.
    /// </summary>
    [Test]
    public async Task LastReconciledAt_IsMaxOverLiveReconciledRows_TaggedUtc()
    {
        await Seed(840, "2026-09-01", reconciledAt: new DateTime(2026, 9, 2, 8, 0, 0));
        await Seed(841, "2026-09-03", reconciledAt: new DateTime(2026, 9, 18, 12, 32, 0));

        // Reconcile + soft-delete in ONE save (no boundary exists for 842, so
        // the interceptor permits it) -- the same trick as
        // DayLockHelperTests.LockedThrough_IgnoresRemovedRows.
        var ghost = await Seed(842, "2026-09-10");
        ghost.Reconciled = true;
        ghost.ReconciledAt = new DateTime(2026, 9, 30, 23, 0, 0);
        await ghost.Delete(TimePlanningPnDbContext!);

        var result = await Service().GetSummaryAsync(Today);

        Assert.Multiple(() =>
        {
            Assert.That(result.Model.LastReconciledAt, Is.EqualTo(new DateTime(2026, 9, 18, 12, 32, 0)));
            Assert.That(result.Model.LastReconciledAt!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(result.Model.WorkersWithAnyReconciled, Is.EqualTo(2), "842's only reconciled row is removed");
        });
    }

    [Test]
    public async Task ParameterlessOverload_UsesUtcToday()
    {
        var result = await Service().GetSummaryAsync();

        var expected = PayrollPeriod.LastClosed(DateTime.UtcNow.Date, 19);
        Assert.That(result.Model.PeriodEnd, Is.EqualTo(expected.End.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
    }

    [Test]
    public async Task DatabaseFailure_ReturnsUnsuccessfulResultInsteadOfThrowing()
    {
        var broken = CreateTimePlanningPnDbContext();
        await broken.DisposeAsync();

        var result = await Service(broken).GetSummaryAsync(Today);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo(ReconciliationSummaryService.ErrorMessage));
            Assert.That(result.Model, Is.Null);
        });
    }
}
