using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.Index;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.UpdateCreate;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using PlanRegistrationEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Grid save (CreateUpdate → UpdatePlanning) on a one-minute site.
///
/// B2: an office correction of a shift's ids on a one-minute row used to be
/// replaced, in the same save, by hours re-derived from the device stamps that
/// still held the old times. The stamps of exactly the corrected parts are now
/// dropped, so the hours come from the office's ids.
///
/// B4: the row's mode marker follows the mode AT ITS OWN DATE
/// (UseOneMinuteIntervalsFrom), not the site's current flag.
///
/// Both apply only to a row whose shift ids the office changed: the grid posts
/// every visible row, and an untouched one keeps its stamps and marker.
///
/// Ids: id n is (n - 1) * 5 minutes after midnight — 85 = 07:00, 97 = 08:00,
/// 193 = 16:00, 235 = 19:30.
/// </summary>
[TestFixture]
public class OfficeEditOneMinuteStaleStampsTests : TestBaseSetup
{
    private const int SiteUid = 7801;
    private static readonly DateTime OneMinuteFrom = new(2026, 1, 1);

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

        // One-minute site whose flag took effect on OneMinuteFrom. Weekday-plan
        // branch (not the Google-sheet one), and every row below is older than
        // one month, so UpdatePlanRegistration never rewrites PlanHours.
        await new AssignedSiteEntity
        {
            SiteId = SiteUid,
            UseOneMinuteIntervals = true,
            UseOneMinuteIntervalsFrom = OneMinuteFrom,
            UseGoogleSheetAsDefault = false,
            Resigned = false,
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext!);
    }

    /// <summary>
    /// A row as the device left it: ids 07:00-19:30 and stamps a few seconds
    /// off them, hours derived from the stamps.
    /// </summary>
    private async Task SeedDeviceRow(DateTime date, bool? marker,
        int pause1Id = 0, DateTime? pause1StartedAt = null, DateTime? pause1StoppedAt = null)
    {
        var start = date.AddHours(7).AddSeconds(13);
        var stop = date.AddHours(19).AddMinutes(30).AddSeconds(47);
        var pauseSeconds = pause1StartedAt.HasValue && pause1StoppedAt.HasValue
            ? (int)(pause1StoppedAt.Value - pause1StartedAt.Value).TotalSeconds
            : 0;
        var nettoSeconds = (int)(stop - start).TotalSeconds - pauseSeconds;
        await new PlanRegistrationEntity
        {
            SdkSitId = SiteUid,
            Date = date,
            Start1Id = 85,
            Stop1Id = 235,
            Pause1Id = pause1Id,
            Start1StartedAt = start,
            Stop1StoppedAt = stop,
            Pause1StartedAt = pause1StartedAt,
            Pause1StoppedAt = pause1StoppedAt,
            PlanHours = 8,
            PlanHoursInSeconds = 28800,
            NettoHours = nettoSeconds / 3600.0,
            NettoHoursInSeconds = nettoSeconds,
            RegisteredUnderOneMinuteIntervals = marker,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext!);
    }

    private static TimePlanningWorkingHoursUpdateCreateModel OfficeEdit(
        DateTime date, int start1, int stop1, int pause1 = 0, double nettoHours = 0,
        int? start2 = null, int? stop2 = null, int? pause2 = null) => new()
    {
        SiteId = SiteUid,
        Plannings = new List<TimePlanningWorkingHoursModel>
        {
            new()
            {
                Date = date,
                Shift1Start = start1,
                Shift1Stop = stop1,
                Shift1Pause = pause1,
                Shift2Start = start2,
                Shift2Stop = stop2,
                Shift2Pause = pause2,
                PlanHours = 8,
                NettoHours = nettoHours,
                FlexHours = 0,
                PaidOutFlex = "0",
                Message = 0,
                PlanText = "",
                CommentOffice = "",
                CommentOfficeAll = "",
                CommentWorker = ""
            }
        }
    };

    private async Task<PlanRegistrationEntity> Stored(DateTime date) =>
        await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .SingleAsync(x => x.SdkSitId == SiteUid && x.Date == date);

    /// <summary>
    /// A two-shift row as the device left it: both shifts have their own
    /// stale-by-a-few-seconds work stamps, no pauses.
    /// </summary>
    private async Task SeedTwoShiftDeviceRow(DateTime date,
        int start1Id, int stop1Id, DateTime start1At, DateTime stop1At,
        int start2Id, int stop2Id, DateTime start2At, DateTime stop2At)
    {
        var nettoSeconds = (int)(stop1At - start1At).TotalSeconds
                            + (int)(stop2At - start2At).TotalSeconds;
        await new PlanRegistrationEntity
        {
            SdkSitId = SiteUid,
            Date = date,
            Start1Id = start1Id,
            Stop1Id = stop1Id,
            Start1StartedAt = start1At,
            Stop1StoppedAt = stop1At,
            Start2Id = start2Id,
            Stop2Id = stop2Id,
            Start2StartedAt = start2At,
            Stop2StoppedAt = stop2At,
            PlanHours = 8,
            PlanHoursInSeconds = 28800,
            NettoHours = nettoSeconds / 3600.0,
            NettoHoursInSeconds = nettoSeconds,
            RegisteredUnderOneMinuteIntervals = true,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext!);
    }

    [Test]
    public async Task OfficeCorrectsStartAndStop_OnOneMinuteRowWithStaleStamps_HoursFollowTheOfficeIds()
    {
        var date = new DateTime(2026, 3, 2);
        await SeedDeviceRow(date, marker: true);

        var result = await _service.CreateUpdate(OfficeEdit(date, start1: 97, stop1: 193));

        Assert.That(result.Success, Is.True, result.Message);
        var row = await Stored(date);
        Assert.Multiple(() =>
        {
            Assert.That(row.NettoHoursInSeconds, Is.EqualTo(8 * 3600),
                "08:00-16:00 as the office entered it, not the device's 07:00:13-19:30:47");
            Assert.That(row.NettoHours, Is.EqualTo(8.0).Within(1e-9));
            Assert.That(row.Start1Id, Is.EqualTo(97));
            Assert.That(row.Stop1Id, Is.EqualTo(193));
            Assert.That(row.Start1StartedAt, Is.Null, "the corrected shift's stale start stamp is dropped");
            Assert.That(row.Stop1StoppedAt, Is.Null, "the corrected shift's stale stop stamp is dropped");
            Assert.That(row.RegisteredUnderOneMinuteIntervals, Is.True);
        });
    }

    [Test]
    public async Task OfficeCorrectsThePause_OnOneMinuteRowWithStalePauseStamps_PauseFollowsTheOfficeId()
    {
        var date = new DateTime(2026, 3, 3);
        // Device pause 12:00:05-12:41:10 (2465 s); Pause1Id 7 = 30 min.
        await SeedDeviceRow(date, marker: true, pause1Id: 7,
            pause1StartedAt: date.AddHours(12).AddSeconds(5),
            pause1StoppedAt: date.AddHours(12).AddMinutes(41).AddSeconds(10));

        // Office: 08:00-16:00 with a 15-minute pause (Pause id 4 = (4 - 1) * 5 min).
        var result = await _service.CreateUpdate(OfficeEdit(date, start1: 97, stop1: 193, pause1: 4));

        Assert.That(result.Success, Is.True, result.Message);
        var row = await Stored(date);
        Assert.Multiple(() =>
        {
            Assert.That(row.NettoHoursInSeconds, Is.EqualTo(8 * 3600 - 15 * 60));
            Assert.That(row.Pause1Id, Is.EqualTo(4));
            Assert.That(row.Pause1StartedAt, Is.Null);
            Assert.That(row.Pause1StoppedAt, Is.Null);
        });
    }

    [Test]
    public async Task OfficeSavesWithoutChangingIds_OnOneMinuteRow_KeepsDeviceStampsAndHours()
    {
        var date = new DateTime(2026, 3, 4);
        await SeedDeviceRow(date, marker: true);
        var before = await Stored(date);

        // Same ids as stored: only the unchanged-ids comment/plan save the page does on every row.
        var result = await _service.CreateUpdate(OfficeEdit(date, start1: 85, stop1: 235));

        Assert.That(result.Success, Is.True, result.Message);
        var row = await Stored(date);
        Assert.Multiple(() =>
        {
            Assert.That(row.Start1StartedAt, Is.EqualTo(before.Start1StartedAt));
            Assert.That(row.Stop1StoppedAt, Is.EqualTo(before.Stop1StoppedAt));
            Assert.That(row.NettoHoursInSeconds, Is.EqualTo(before.NettoHoursInSeconds),
                "an unchanged shift keeps its second-precision hours from the stamps");
        });
    }

    [Test]
    public async Task OfficeEditOfARowBeforeTheEffectiveDate_MarksItFiveMinute_AndLeavesItsStampsAlone()
    {
        var date = new DateTime(2025, 12, 1); // before OneMinuteFrom
        await SeedDeviceRow(date, marker: null);
        var before = await Stored(date);

        var result = await _service.CreateUpdate(OfficeEdit(date, start1: 97, stop1: 193, nettoHours: 8));

        Assert.That(result.Success, Is.True, result.Message);
        var row = await Stored(date);
        Assert.Multiple(() =>
        {
            Assert.That(row.RegisteredUnderOneMinuteIntervals, Is.False,
                "the day's mode is the mode at its own date, not the site's current flag");
            Assert.That(row.NettoHours, Is.EqualTo(8.0).Within(1e-9),
                "a five-minute grid row keeps the hours the page posted");
            Assert.That(row.Start1StartedAt, Is.EqualTo(before.Start1StartedAt),
                "stamp clearing is confined to one-minute rows");
            Assert.That(row.Stop1StoppedAt, Is.EqualTo(before.Stop1StoppedAt));
        });
    }

    /// <summary>
    /// Shift 1: ids 85 (07:00) - 145 (12:00), device stamps 07:00:05-12:00:10
    /// (18005 s), left UNCHANGED by the office. Shift 2: ids 157 (13:00) -
    /// 229 (19:00), device stamps 13:00:08-19:00:03 (21595 s), corrected by
    /// the office to 169 (14:00) - 217 (18:00). Only shift 2's stamps must be
    /// dropped; shift 1's are a different shift and must survive untouched.
    /// After clearing, shift 2 falls back to the legacy id math: (217 - 169)
    /// * 5 * 60 = 14400 s (exactly 14:00-18:00, no pause). Total netto =
    /// 18005 + 14400 = 32405 s.
    /// </summary>
    [Test]
    public async Task OfficeCorrectsShift2_OnOneMinuteRowWithStaleShift2Stamps_OnlyShift2StampsAreDroppedAndHoursFollowTheOfficeIds()
    {
        var date = new DateTime(2026, 3, 6);
        var start1At = date.AddHours(7).AddSeconds(5);
        var stop1At = date.AddHours(12).AddSeconds(10);
        var start2At = date.AddHours(13).AddSeconds(8);
        var stop2At = date.AddHours(19).AddSeconds(3);
        await SeedTwoShiftDeviceRow(date,
            start1Id: 85, stop1Id: 145, start1At: start1At, stop1At: stop1At,
            start2Id: 157, stop2Id: 229, start2At: start2At, stop2At: stop2At);
        var before = await Stored(date);

        // Shift 1 posted unchanged (85/145); shift 2 corrected to 14:00-18:00 (169/217).
        var result = await _service.CreateUpdate(
            OfficeEdit(date, start1: 85, stop1: 145, start2: 169, stop2: 217));

        Assert.That(result.Success, Is.True, result.Message);
        var row = await Stored(date);
        Assert.Multiple(() =>
        {
            Assert.That(row.Start2Id, Is.EqualTo(169));
            Assert.That(row.Stop2Id, Is.EqualTo(217));
            Assert.That(row.Start2StartedAt, Is.Null, "the corrected shift 2's stale start stamp is dropped");
            Assert.That(row.Stop2StoppedAt, Is.Null, "the corrected shift 2's stale stop stamp is dropped");
            Assert.That(row.Start1StartedAt, Is.EqualTo(before.Start1StartedAt),
                "shift 1 was not corrected, so its stamps survive");
            Assert.That(row.Stop1StoppedAt, Is.EqualTo(before.Stop1StoppedAt));
            Assert.That(row.NettoHoursInSeconds, Is.EqualTo(18005 + 14400),
                "shift 1 from its surviving stamps, shift 2 from the office's 14:00-18:00 ids");
        });
    }

    /// <summary>
    /// Same start/stop ids as stored (85/235, so B2's start/stop branch never
    /// fires) but a changed pause id: 7 (device, 12:00:05-12:41:10, 2465 s)
    /// -> office 4 (15 min). Work span from SeedDeviceRow is 07:00:13-19:30:47
    /// = 45034 s. After the pause stamps are dropped, the pause falls back to
    /// the legacy tick value of the OFFICE's id: (4 - 1) * 5 * 60 = 900 s.
    /// Netto = 45034 - 900 = 44134 s.
    /// </summary>
    [Test]
    public async Task OfficeChangesOnlyThePause_OnOneMinuteRowWithUnchangedStartStop_PauseStampsAreDroppedButWorkStampsSurvive()
    {
        var date = new DateTime(2026, 3, 7);
        await SeedDeviceRow(date, marker: true, pause1Id: 7,
            pause1StartedAt: date.AddHours(12).AddSeconds(5),
            pause1StoppedAt: date.AddHours(12).AddMinutes(41).AddSeconds(10));
        var before = await Stored(date);

        // Same start/stop as stored (85/235); only the pause id changes.
        var result = await _service.CreateUpdate(OfficeEdit(date, start1: 85, stop1: 235, pause1: 4));

        Assert.That(result.Success, Is.True, result.Message);
        var row = await Stored(date);
        Assert.Multiple(() =>
        {
            Assert.That(row.Pause1Id, Is.EqualTo(4));
            Assert.That(row.Pause1StartedAt, Is.Null, "the corrected pause's stale start stamp is dropped");
            Assert.That(row.Pause1StoppedAt, Is.Null, "the corrected pause's stale stop stamp is dropped");
            Assert.That(row.Start1StartedAt, Is.EqualTo(before.Start1StartedAt),
                "start/stop were not corrected, so the work stamps survive");
            Assert.That(row.Stop1StoppedAt, Is.EqualTo(before.Stop1StoppedAt));
            Assert.That(row.NettoHoursInSeconds, Is.EqualTo(45034 - 900),
                "work seconds from the surviving stamps, pause from the office's 15-minute id");
        });
    }

    /// <summary>
    /// The grid posts EVERY visible row. A row dated before
    /// UseOneMinuteIntervalsFrom (so its date-mode is five-minute) that still
    /// carries RegisteredUnderOneMinuteIntervals = true — e.g. set on purpose by
    /// the single-day editor — is posted back with its ids unchanged and only
    /// an office comment edited. No id changed, so the row is not
    /// re-registered: the marker stays true and the device stamps stay put.
    /// </summary>
    [Test]
    public async Task UntouchedPostedRow_KeepsItsMarker()
    {
        var date = new DateTime(2025, 12, 2); // before OneMinuteFrom
        await SeedDeviceRow(date, marker: true);
        var before = await Stored(date);

        // Same ids as stored (85/235, pause 0, shift 2 null → 0); only the comment changes.
        var edit = OfficeEdit(date, start1: 85, stop1: 235);
        edit.Plannings[0].CommentOffice = "office note";
        var result = await _service.CreateUpdate(edit);

        Assert.That(result.Success, Is.True, result.Message);
        var row = await Stored(date);
        Assert.Multiple(() =>
        {
            Assert.That(row.CommentOffice, Is.EqualTo("office note"), "the save itself went through");
            Assert.That(row.RegisteredUnderOneMinuteIntervals, Is.True,
                "an untouched posted row keeps its marker, even when its date-mode is five-minute");
            Assert.That(row.Start1Id, Is.EqualTo(85));
            Assert.That(row.Stop1Id, Is.EqualTo(235));
            Assert.That(row.Start1StartedAt, Is.EqualTo(before.Start1StartedAt));
            Assert.That(row.Stop1StoppedAt, Is.EqualTo(before.Stop1StoppedAt));
        });
    }

    /// <summary>
    /// Today's row: UpdatePlanning's entire id-rewrite block — including B2 —
    /// is gated on <c>planRegistration.Date != midnight</c>, so an office id
    /// change posted for today never reaches ClearStampsOfCorrectedShifts at
    /// all. Ids and stamps must come back exactly as stored.
    /// </summary>
    [Test]
    public async Task OfficeEditsToday_OnOneMinuteRow_LeavesIdsAndStampsUntouched()
    {
        var date = DateTime.Today;
        await SeedDeviceRow(date, marker: true);
        var before = await Stored(date);

        // Posted ids differ from stored (85/235), but today's row must ignore them.
        var result = await _service.CreateUpdate(OfficeEdit(date, start1: 97, stop1: 193));

        Assert.That(result.Success, Is.True, result.Message);
        var row = await Stored(date);
        Assert.Multiple(() =>
        {
            Assert.That(row.Start1Id, Is.EqualTo(before.Start1Id), "today's row ignores the posted ids entirely");
            Assert.That(row.Stop1Id, Is.EqualTo(before.Stop1Id));
            Assert.That(row.Start1StartedAt, Is.EqualTo(before.Start1StartedAt),
                "B2 does not apply to today's row");
            Assert.That(row.Stop1StoppedAt, Is.EqualTo(before.Stop1StoppedAt));
        });
    }
}
