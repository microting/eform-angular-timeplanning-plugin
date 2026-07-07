using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.UpdateCreate;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using PlanRegistrationEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration;
using RegistrationDeviceEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.RegistrationDevice;

namespace TimePlanning.Pn.Test;

/// <summary>
/// WALL-TIME-AT-REST hardening contract for the WorkingHours kiosk write path
/// (<c>UpdateWorkingHour(int? sdkSiteId, model, token)</c>), which today uses
/// plain <c>DateTime.Parse</c> on the incoming stamp strings. A Z-suffixed or
/// offset-carrying string therefore lands as SERVER-LOCAL digits — the stored
/// value silently depends on the host's timezone (UTC in CI/prod containers,
/// CET on dev machines). No current client sends Z here (kiosk sends naive
/// wall digits), but a future app cleanup or third-party client must not be
/// able to corrupt storage — so these sites route through the same normalizer
/// as the gRPC plannings path. The kiosk flow has no authenticated user, so
/// instant-carrying input normalizes into the documented default zone
/// (Europe/Copenhagen); naive digits stay verbatim.
///
/// NOTE on red evidence: the Z-input tests fail pre-fix on any host whose
/// local timezone is NOT Europe/Copenhagen (run with TZ=UTC to reproduce CI);
/// on a CET/CEST host they pass pre-fix by coincidence (server-local ==
/// intended zone). Post-fix they are deterministic on every host.
/// </summary>
[TestFixture]
public class WallTimeWorkingHoursWritePathTests : TestBaseSetup
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
        var options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        options.Value.Returns(new TimePlanningBaseSettings
        {
            AutoBreakCalculationActive = "0",
            DayOfPayment = 20,
            GpsEnabled = "0",
            SnapshotEnabled = "0"
        });

        // The kiosk path only touches userService.UserId and dbContext;
        // baseDbContext / coreHelper are never dereferenced on this branch
        // (same fixture shape as WorkingHoursGrpcKioskNonRoundMinutesTests).
        _service = new TimePlanningWorkingHoursService(
            Substitute.For<ILogger<TimePlanningWorkingHoursService>>(),
            TimePlanningPnDbContext!,
            userService,
            localizationService,
            baseDbContext: null!,
            options,
            coreService);
    }

    private async Task SeedKioskSite(int siteUid, string token)
    {
        await new AssignedSiteEntity
        {
            SiteId = siteUid,
            UseOneMinuteIntervals = true,
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);

        await new RegistrationDeviceEntity
        {
            Token = token,
            Name = "Kiosk Device",
            OtpCode = "12345",
            SoftwareVersion = "1.0.0",
            Manufacturer = "Test",
            Model = "Test",
            OsVersion = "1.0",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);
    }

    private static TimePlanningWorkingHoursUpdateModel BuildModel(
        DateTime date, string start1, string stop1)
    {
        return new TimePlanningWorkingHoursUpdateModel
        {
            Date = date,
            Shift1Start = 79,   // legacy 5-min slot ↔ 06:30 wall time
            Shift1Stop = 193,   // legacy 5-min slot ↔ 16:00 wall time
            Shift1Pause = 0,
            Start1StartedAt = start1,
            Stop1StoppedAt = stop1,
            CommentWorker = "",
            OsVersion = "1.0",
            Model = "Test",
            Manufacturer = "Test",
            SoftwareVersion = "1.0.0",
        };
    }

    [Test]
    public async Task KioskCreate_ZSuffixedUtcStamps_PersistWallDigits()
    {
        const string token = "kiosk-walltime-create-z";
        await SeedKioskSite(9821, token);

        var date = new DateTime(2026, 7, 7, 0, 0, 0);
        // 04:30Z / 14:00Z == the 06:30–16:00 CEST wall-time shift (prod oracle shape).
        var model = BuildModel(date, "2026-07-07T04:30:00Z", "2026-07-07T14:00:00Z");

        var result = await _service.UpdateWorkingHour(sdkSiteId: 9821, model, token);
        Assert.That(result.Success, Is.True, result.Message);

        var pr = await TimePlanningPnDbContext!.PlanRegistrations
            .AsNoTracking()
            .SingleAsync(x => x.SdkSitId == 9821 && x.Date == date);
        Assert.Multiple(() =>
        {
            Assert.That(pr.Start1StartedAt, Is.EqualTo(new DateTime(2026, 7, 7, 6, 30, 0)),
                "Z-suffixed input must persist as Europe/Copenhagen wall digits (06:30), " +
                "not UTC digits and not whatever the server's local timezone happens to be");
            Assert.That(pr.Stop1StoppedAt, Is.EqualTo(new DateTime(2026, 7, 7, 16, 0, 0)),
                "Z-suffixed input must persist as wall digits (16:00)");
        });
    }

    [Test]
    public async Task KioskUpdate_ExistingRow_ZSuffixedUtcStamps_PersistWallDigits()
    {
        const string token = "kiosk-walltime-update-z";
        await SeedKioskSite(9822, token);

        var date = new DateTime(2026, 7, 7, 0, 0, 0);
        await new PlanRegistrationEntity
        {
            SdkSitId = 9822,
            Date = date,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);

        var model = BuildModel(date, "2026-07-07T04:30:00Z", "2026-07-07T14:00:00Z");

        var result = await _service.UpdateWorkingHour(sdkSiteId: 9822, model, token);
        Assert.That(result.Success, Is.True, result.Message);

        var pr = await TimePlanningPnDbContext!.PlanRegistrations
            .AsNoTracking()
            .SingleAsync(x => x.SdkSitId == 9822 && x.Date == date);
        Assert.Multiple(() =>
        {
            Assert.That(pr.Start1StartedAt, Is.EqualTo(new DateTime(2026, 7, 7, 6, 30, 0)),
                "Update branch must apply the same UTC→wall normalization as create");
            Assert.That(pr.Stop1StoppedAt, Is.EqualTo(new DateTime(2026, 7, 7, 16, 0, 0)));
        });
    }

    /// <summary>
    /// REGRESSION LOCK (green today): naive wall digits — what the kiosk app
    /// actually sends — persist byte-identical.
    /// </summary>
    [Test]
    public async Task KioskCreate_NaiveWallDigits_PersistVerbatim()
    {
        const string token = "kiosk-walltime-create-naive";
        await SeedKioskSite(9823, token);

        var date = new DateTime(2026, 7, 7, 0, 0, 0);
        var model = BuildModel(date, "2026-07-07T06:30:00", "2026-07-07T16:00:00");

        var result = await _service.UpdateWorkingHour(sdkSiteId: 9823, model, token);
        Assert.That(result.Success, Is.True, result.Message);

        var pr = await TimePlanningPnDbContext!.PlanRegistrations
            .AsNoTracking()
            .SingleAsync(x => x.SdkSitId == 9823 && x.Date == date);
        Assert.Multiple(() =>
        {
            Assert.That(pr.Start1StartedAt, Is.EqualTo(new DateTime(2026, 7, 7, 6, 30, 0)),
                "Naive wall digits must persist verbatim — no shifting");
            Assert.That(pr.Stop1StoppedAt, Is.EqualTo(new DateTime(2026, 7, 7, 16, 0, 0)));
        });
    }
}
