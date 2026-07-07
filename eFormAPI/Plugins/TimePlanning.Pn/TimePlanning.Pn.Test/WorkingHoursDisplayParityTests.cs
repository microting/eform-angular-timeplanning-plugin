using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.Index;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using PlanRegistrationEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration;
using SdkLanguage = Microting.eForm.Infrastructure.Data.Entities.Language;
using SdkSite = Microting.eForm.Infrastructure.Data.Entities.Site;
using SdkSiteWorker = Microting.eForm.Infrastructure.Data.Entities.SiteWorker;
using SdkWorker = Microting.eForm.Infrastructure.Data.Entities.Worker;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Stage 3 tick-exact parity on the working-hours surfaces
/// (<see cref="TimePlanningWorkingHoursService"/>): a row registered under
/// 5-minute mode must keep rendering AND totalling its tick times on every
/// surface — Index DTO stamps (web grid + pay-line feed), Excel day cells
/// (GetShiftTime / GetShiftTimeFraction with the PER-ROW mode), and time-band
/// pay lines (CalculatePayLinesForDay over the normalized DTO) — even after
/// the site flips UseOneMinuteIntervals to true. The per-row mode comes from
/// the AssignedSiteVersions audit trail (OneMinuteModeTimeline); the Index
/// normalization itself lives in ApplyModeAtRegistrationStamps.
/// </summary>
[TestFixture]
public class WorkingHoursDisplayParityTests : TestBaseSetup
{
    private TimePlanningWorkingHoursService _service = null!;

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

        var sdkDb = core.DbContextHelper.GetDbContext();
        var language = await sdkDb.Languages.FirstOrDefaultAsync(l => l.LanguageCode == "da");
        if (language == null)
        {
            language = new SdkLanguage { LanguageCode = "da", Name = "Danish" };
            await language.Create(sdkDb);
        }
        userService.GetCurrentUserLanguage().Returns(language);

        var options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        options.Value.Returns(new TimePlanningBaseSettings
        {
            AutoBreakCalculationActive = "0",
            DayOfPayment = 20,
            GpsEnabled = "0",
            SnapshotEnabled = "0"
        });

        _service = new TimePlanningWorkingHoursService(
            Substitute.For<ILogger<TimePlanningWorkingHoursService>>(),
            TimePlanningPnDbContext!,
            userService,
            localizationService,
            baseDbContext: null!,
            options,
            coreService);
    }

    // ------------------------------------------------------------------
    // 1. Pure helpers: per-row mode drives the Excel cell formatters.
    // ------------------------------------------------------------------

    /// <summary>
    /// GetShiftTime's flag argument is now the PER-ROW mode at registration:
    /// a tick row (false) formats from the Id even when an exact stamp is
    /// available; a one-minute row (true) formats the stamp.
    /// </summary>
    [Test]
    public void GetShiftTime_PerRowMode_TickRowFormatsFromId_OneMinuteRowFromStamp()
    {
        var plr = new PlanRegistrationEntity();
        var stamp = new DateTime(2026, 5, 20, 8, 7, 0); // off-grid on purpose

        Assert.Multiple(() =>
        {
            Assert.That(_service.GetShiftTime(plr, 98, stamp, useOneMinuteIntervals: false),
                Is.EqualTo("08:05"),
                "Tick row: Id 98 → 08:05 must win over the exact stamp 08:07.");
            Assert.That(_service.GetShiftTime(plr, 98, stamp, useOneMinuteIntervals: true),
                Is.EqualTo("08:07"),
                "One-minute row: the exact stamp must win.");
            // Tick edge kept intact: Id 289 = 24:00 (don't-wrap convention).
            Assert.That(_service.GetShiftTime(plr, 289, stamp, useOneMinuteIntervals: false),
                Is.EqualTo("24:00"));
        });
    }

    /// <summary>Same per-row rule for the Dagsoversigt day-fraction cells.</summary>
    [Test]
    public void GetShiftTimeFraction_PerRowMode_TickRowFromId_OneMinuteRowFromStamp()
    {
        var stamp = new DateTime(2026, 5, 20, 8, 7, 0);

        Assert.Multiple(() =>
        {
            Assert.That(_service.GetShiftTimeFraction(98, stamp, useOneMinuteIntervals: false)!.Value,
                Is.EqualTo((8 * 60 + 5) / 1440.0).Within(1e-9),
                "Tick row: fraction derives from Id 98 → 08:05.");
            Assert.That(_service.GetShiftTimeFraction(98, stamp, useOneMinuteIntervals: true)!.Value,
                Is.EqualTo((8 * 60 + 7) / 1440.0).Within(1e-9),
                "One-minute row: fraction derives from the exact stamp 08:07.");
        });
    }

    // ------------------------------------------------------------------
    // 2. DTO normalization: ApplyModeAtRegistrationStamps.
    // ------------------------------------------------------------------

    [Test]
    public void ApplyModeAtRegistrationStamps_TickRow_OverwritesStampsWithIdDerived()
    {
        var date = new DateTime(2026, 5, 20);
        var day = new TimePlanningWorkingHoursModel
        {
            Date = date,
            Shift1Start = 98, Shift1Stop = 193,
            Start1StartedAt = date.AddHours(8).AddMinutes(7),
            Stop1StoppedAt = date.AddHours(16).AddMinutes(3),
            // Shift 2 untouched (ids 0) but carrying a stray stamp: a tick row's
            // truth is its ids, so the stamp must clear (pre-flip it never
            // rendered nor summed either — id path showed blank).
            Start2StartedAt = date.AddHours(18)
        };

        TimePlanningWorkingHoursService.ApplyModeAtRegistrationStamps(day, rowIsOneMinute: false);

        Assert.Multiple(() =>
        {
            Assert.That(day.Start1StartedAt, Is.EqualTo(date.AddHours(8).AddMinutes(5)));
            Assert.That(day.Stop1StoppedAt, Is.EqualTo(date.AddHours(16)));
            Assert.That(day.Start2StartedAt, Is.Null);
            Assert.That(day.Stop2StoppedAt, Is.Null);
        });
    }

    [Test]
    public void ApplyModeAtRegistrationStamps_OneMinuteRow_StampsWin_NullsFallBackToIds()
    {
        var date = new DateTime(2026, 6, 10);
        var day = new TimePlanningWorkingHoursModel
        {
            Date = date,
            Shift1Start = 98, Shift1Stop = 193,
            Start1StartedAt = date.AddHours(8).AddMinutes(7), // exact stamp wins
            Stop1StoppedAt = null,                            // falls back to Id 193
            Shift2Start = 205, Shift2Stop = 0                 // 17:00 fallback / null
        };

        TimePlanningWorkingHoursService.ApplyModeAtRegistrationStamps(day, rowIsOneMinute: true);

        Assert.Multiple(() =>
        {
            Assert.That(day.Start1StartedAt, Is.EqualTo(date.AddHours(8).AddMinutes(7)));
            Assert.That(day.Stop1StoppedAt, Is.EqualTo(date.AddHours(16)));
            Assert.That(day.Start2StartedAt, Is.EqualTo(date.AddHours(17)));
            Assert.That(day.Stop2StoppedAt, Is.Null);
        });
    }

    // ------------------------------------------------------------------
    // 3. End-to-end: Index normalizes per-row; pay lines total tick seconds
    //    for pre-flip rows and exact seconds for post-flip rows.
    // ------------------------------------------------------------------

    [Test]
    public async Task Index_FlippedSite_PreFlipRowTickStamps_PostFlipRowExactStamps_AndPayLineSeconds()
    {
        const int siteUid = 917;
        var flipDate = new DateTime(2026, 6, 1);
        var preFlipDate = new DateTime(2026, 5, 20);  // Wednesday, before flip
        var postFlipDate = new DateTime(2026, 6, 10); // Wednesday, after flip

        await SeedSdkSite(siteUid);
        await SeedFlippedAssignedSite(siteUid, flipDate);
        foreach (var date in new[] { preFlipDate, postFlipDate })
        {
            await new PlanRegistrationEntity
            {
                SdkSitId = siteUid,
                Date = date,
                Start1Id = 98,   // 08:05
                Stop1Id = 193,   // 16:00
                Start1StartedAt = date.AddHours(8).AddMinutes(7),
                Stop1StoppedAt = date.AddHours(16).AddMinutes(3),
                PlanText = "",
                CommentOffice = "",
                CommentOfficeAll = "",
                WorkflowState = Constants.WorkflowStates.Created,
                CreatedByUserId = 1,
                UpdatedByUserId = 1
            }.Create(TimePlanningPnDbContext!);
        }

        var result = await _service.Index(new TimePlanningWorkingHoursRequestModel
        {
            SiteId = siteUid,
            DateFrom = preFlipDate,
            DateTo = postFlipDate
        });
        Assert.That(result.Success, Is.True, result.Message);

        var preFlipDay = result.Model.Single(x => x.Date == preFlipDate);
        var postFlipDay = result.Model.Single(x => x.Date == postFlipDate);

        Assert.Multiple(() =>
        {
            // Web grid / pay-line feed: pre-flip row carries TICK stamps...
            Assert.That(preFlipDay.Start1StartedAt, Is.EqualTo(preFlipDate.AddHours(8).AddMinutes(5)),
                "Pre-flip (tick) row: Index must surface the Id-derived 08:05, not the raw 08:07 stamp.");
            Assert.That(preFlipDay.Stop1StoppedAt, Is.EqualTo(preFlipDate.AddHours(16)),
                "Pre-flip (tick) row: Index must surface the Id-derived 16:00, not the raw 16:03 stamp.");
            // ...while the post-flip row keeps its exact stamps.
            Assert.That(postFlipDay.Start1StartedAt, Is.EqualTo(postFlipDate.AddHours(8).AddMinutes(7)));
            Assert.That(postFlipDay.Stop1StoppedAt, Is.EqualTo(postFlipDate.AddHours(16).AddMinutes(3)));
        });

        // Time-band pay lines consume the SAME normalized models (this mirrors
        // the export wiring at GenerateExcelDashboard): pre-flip totals must be
        // TICK seconds (08:05→16:00 = 28500 s), post-flip totals exact-stamp
        // seconds (08:07→16:03 = 28560 s) — no drift when the site flips.
        var payRuleSet = AllDayWednesdayBand();
        var preFlipLines = TimePlanningWorkingHoursService.CalculatePayLinesForDay(
            preFlipDay.Id ?? 0, preFlipDay.Date, preFlipDay, totalSeconds: 0, payRuleSet);
        var postFlipLines = TimePlanningWorkingHoursService.CalculatePayLinesForDay(
            postFlipDay.Id ?? 0, postFlipDay.Date, postFlipDay, totalSeconds: 0, payRuleSet);

        Assert.Multiple(() =>
        {
            Assert.That(preFlipLines.Sum(l => l.HoursInSeconds), Is.EqualTo((193 - 98) * 5 * 60),
                "Pre-flip (tick) row: pay-band attribution must total tick seconds (28500).");
            Assert.That(postFlipLines.Sum(l => l.HoursInSeconds), Is.EqualTo(28560),
                "Post-flip (one-minute) row: pay-band attribution must total exact stamp seconds.");
        });
    }

    /// <summary>
    /// Write-time mode marker beats the timeline on the working-hours surface
    /// too (Index DTO → web grid, Excel cells, pay lines): a pre-flip-DATED row
    /// whose marker is true (admin exact edit after the flip — the l1m
    /// scenario) surfaces its exact stamps; a post-flip-DATED row whose marker
    /// is false surfaces ticks.
    /// </summary>
    [Test]
    public async Task Index_WriteTimeMarker_WinsOverTimeline()
    {
        const int siteUid = 919;
        var flipDate = new DateTime(2026, 6, 1);
        var markerTrueDate = new DateTime(2026, 5, 20);  // pre-flip, marker=true
        var markerFalseDate = new DateTime(2026, 6, 10); // post-flip, marker=false

        await SeedSdkSite(siteUid);
        await SeedFlippedAssignedSite(siteUid, flipDate);
        foreach (var (date, marker) in new (DateTime, bool?)[]
                 {
                     (markerTrueDate, true),
                     (markerFalseDate, false)
                 })
        {
            await new PlanRegistrationEntity
            {
                SdkSitId = siteUid,
                Date = date,
                Start1Id = 98,   // 08:05
                Stop1Id = 193,   // 16:00
                Start1StartedAt = date.AddHours(8).AddMinutes(7),
                Stop1StoppedAt = date.AddHours(16).AddMinutes(3),
                RegisteredUnderOneMinuteIntervals = marker,
                PlanText = "",
                CommentOffice = "",
                CommentOfficeAll = "",
                WorkflowState = Constants.WorkflowStates.Created,
                CreatedByUserId = 1,
                UpdatedByUserId = 1
            }.Create(TimePlanningPnDbContext!);
        }

        var result = await _service.Index(new TimePlanningWorkingHoursRequestModel
        {
            SiteId = siteUid,
            DateFrom = markerTrueDate,
            DateTo = markerFalseDate
        });
        Assert.That(result.Success, Is.True, result.Message);

        var markerTrueDay = result.Model.Single(x => x.Date == markerTrueDate);
        var markerFalseDay = result.Model.Single(x => x.Date == markerFalseDate);

        Assert.Multiple(() =>
        {
            Assert.That(markerTrueDay.Start1StartedAt, Is.EqualTo(markerTrueDate.AddHours(8).AddMinutes(7)),
                "marker=true pre-flip row: Index must surface the exact 08:07 stamp.");
            Assert.That(markerTrueDay.Stop1StoppedAt, Is.EqualTo(markerTrueDate.AddHours(16).AddMinutes(3)),
                "marker=true pre-flip row: Index must surface the exact 16:03 stamp.");
            Assert.That(markerFalseDay.Start1StartedAt, Is.EqualTo(markerFalseDate.AddHours(8).AddMinutes(5)),
                "marker=false post-flip row: Index must surface the tick 08:05.");
            Assert.That(markerFalseDay.Stop1StoppedAt, Is.EqualTo(markerFalseDate.AddHours(16)),
                "marker=false post-flip row: Index must surface the tick 16:00.");
        });
    }

    // ------------------------------------------------------------------
    // Seed helpers
    // ------------------------------------------------------------------

    private async Task SeedSdkSite(int siteUid)
    {
        var core = await GetCore();
        var sdkDb = core.DbContextHelper.GetDbContext();

        var site = new SdkSite { Name = $"Site {siteUid}", MicrotingUid = siteUid };
        await site.Create(sdkDb);

        var worker = new SdkWorker
        {
            FirstName = "Test",
            LastName = "Worker",
            Email = $"test{siteUid}@example.com",
            MicrotingUid = 1000 + siteUid
        };
        await worker.Create(sdkDb);

        await new SdkSiteWorker
        {
            SiteId = site.Id,
            WorkerId = worker.Id,
            MicrotingUid = 2000 + siteUid
        }.Create(sdkDb);
    }

    /// <summary>
    /// AssignedSite flipped false→true saved mid-day on <paramref name="flipDate"/>,
    /// seeded through the raw DbSets so the AssignedSiteVersions audit trail's
    /// UpdatedAt save times (which the timeline reconstructs change points
    /// from) are exactly the test's chosen dates. Mirrors the prod quirk:
    /// version rows COPY the base entity's CreatedAt.
    /// </summary>
    private async Task SeedFlippedAssignedSite(int siteUid, DateTime flipDate)
    {
        var createdAt = flipDate.AddYears(-1);
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = siteUid,
            UseOneMinuteIntervals = true, // current flag (post-flip)
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
            CreatedAt = createdAt,
            UpdatedAt = flipDate,
            Version = 2
        };
        TimePlanningPnDbContext!.AssignedSites.Add(assignedSite);
        await TimePlanningPnDbContext.SaveChangesAsync();

        TimePlanningPnDbContext.AssignedSiteVersions.AddRange(
            new AssignedSiteVersion
            {
                AssignedSiteId = assignedSite.Id,
                SiteId = siteUid,
                UseOneMinuteIntervals = false,
                WorkflowState = Constants.WorkflowStates.Created,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                Version = 1
            },
            new AssignedSiteVersion
            {
                AssignedSiteId = assignedSite.Id,
                SiteId = siteUid,
                UseOneMinuteIntervals = true,
                WorkflowState = Constants.WorkflowStates.Created,
                CreatedAt = createdAt, // prod quirk: copy of the BASE CreatedAt
                UpdatedAt = flipDate.AddHours(14).AddMinutes(45),
                Version = 2
            });
        await TimePlanningPnDbContext.SaveChangesAsync();
    }

    private static PayRuleSet AllDayWednesdayBand() => new()
    {
        Name = "AllDayWednesday",
        DayRules = new List<PayDayRule>(),
        DayTypeRules = new List<PayDayTypeRule>
        {
            new PayDayTypeRule
            {
                DayType = DayType.Wednesday,
                DefaultPayCode = "WORK",
                Priority = 1,
                TimeBandRules = new List<PayTimeBandRule>
                {
                    new PayTimeBandRule
                    {
                        StartSecondOfDay = 0, EndSecondOfDay = 86400,
                        PayCode = "WORK", PayrollCode = "100", Priority = 1
                    }
                }
            }
        }
    };
}
