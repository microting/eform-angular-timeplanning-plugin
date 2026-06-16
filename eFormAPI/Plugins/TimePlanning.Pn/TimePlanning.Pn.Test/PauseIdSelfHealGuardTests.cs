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
using RegistrationDeviceEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.RegistrationDevice;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Integration coverage for the on-save pauseNId self-heal guard
/// (<c>TimePlanningWorkingHoursService.SelfHealCorruptPauseIds</c>) exercised
/// end-to-end through the kiosk gRPC write path
/// (<c>UpdateWorkingHour(int? sdkSiteId, model, token)</c>) against a real
/// Testcontainers MariaDB.
///
/// The kiosk and personal save paths share the identical guard call placed
/// immediately before the inline 5-minute-tick netto math, so the kiosk path is
/// used to drive every scenario: it requires only <c>userService.UserId</c> and
/// the plugin <c>dbContext</c> (no SDK core / JWT), which the integration harness
/// provides. A corrupt <c>Shift1Pause</c> (absolute stop-tick) plus truthful
/// <c>Pause1StartedAt/StoppedAt</c> must be persisted as the timestamp-derived
/// duration id, healing netto in the same save.
/// </summary>
[TestFixture]
public class PauseIdSelfHealGuardTests : TestBaseSetup
{
    [SetUp]
    public async Task SetUpTest()
    {
        await base.Setup();
    }

    private TimePlanningWorkingHoursService BuildService(bool? pauseIdSelfHealEnabled)
    {
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
            SnapshotEnabled = "0",
            PauseIdSelfHealEnabled = pauseIdSelfHealEnabled,
        });

        // The kiosk path only references userService.UserId and dbContext from the
        // constructor graph; baseDbContext / coreHelper are never dereferenced here.
        return new TimePlanningWorkingHoursService(
            Substitute.For<ILogger<TimePlanningWorkingHoursService>>(),
            TimePlanningPnDbContext!,
            userService,
            localizationService,
            baseDbContext: null!,
            options,
            coreService);
    }

    private async Task SeedSiteAsync(int siteId, bool useOneMinuteIntervals)
    {
        await new AssignedSiteEntity
        {
            SiteId = siteId,
            UseOneMinuteIntervals = useOneMinuteIntervals,
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);
    }

    private async Task SeedDeviceAsync(string token, string otp)
    {
        await new RegistrationDeviceEntity
        {
            Token = token,
            Name = "Kiosk Device",
            OtpCode = otp,
            SoftwareVersion = "1.0.0",
            Manufacturer = "Test",
            Model = "Test",
            OsVersion = "1.0",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);
    }

    // A worked shift of 96 5-minute ticks (108 -> 204, i.e. 09:00 -> 17:00).
    // The inline netto math is (Stop1Id - Start1Id) - (Pause1Id - 1), then * 5.
    // With the corrupt id (116) netto is grossly negative; with the corrected id
    // (6) netto is sane and positive.
    private static TimePlanningWorkingHoursUpdateModel BuildModel(
        DateTime date, int pauseId, int pauseMinutes)
    {
        var pauseStart = date.AddHours(12);
        var pauseStop = pauseStart.AddMinutes(pauseMinutes);
        return new TimePlanningWorkingHoursUpdateModel
        {
            Date = date,
            Shift1Start = 108,
            Shift1Stop = 204,
            Shift1Pause = pauseId,
            Start1StartedAt = $"{date:yyyy-MM-dd}T09:00:00",
            Stop1StoppedAt = $"{date:yyyy-MM-dd}T17:00:00",
            Pause1StartedAt = pauseStart.ToString("yyyy-MM-ddTHH:mm:ss"),
            Pause1StoppedAt = pauseStop.ToString("yyyy-MM-ddTHH:mm:ss"),
            CommentWorker = "",
            OsVersion = "1.0",
            Model = "Test",
            Manufacturer = "Test",
            SoftwareVersion = "1.0.0",
        };
    }

    private async Task<Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration>
        PersistedRowAsync(int siteId, DateTime date)
    {
        return await TimePlanningPnDbContext!.PlanRegistrations
            .AsNoTracking()
            .Where(x => x.SdkSitId == siteId && x.Date == date)
            .OrderByDescending(x => x.Id)
            .FirstAsync();
    }

    [Test]
    public async Task Corrupt_5MinSite_IsHealedOnSave()
    {
        const int siteId = 9601;
        var token = "selfheal-corrupt";
        var date = new DateTime(2026, 6, 16, 0, 0, 0);
        await SeedSiteAsync(siteId, useOneMinuteIntervals: false);
        await SeedDeviceAsync(token, "10001");

        // 28-min real pause, corrupt absolute-tick id 116 -> corrected to 6.
        var model = BuildModel(date, pauseId: 116, pauseMinutes: 28);
        var service = BuildService(pauseIdSelfHealEnabled: null);

        var result = await service.UpdateWorkingHour(sdkSiteId: siteId, model, token);
        Assert.That(result.Success, Is.True, result.Message);

        var pr = await PersistedRowAsync(siteId, date);
        Assert.Multiple(() =>
        {
            Assert.That(pr.Pause1Id, Is.EqualTo(6), "corrupt absolute-tick id must be healed to (28/5)+1");
            Assert.That(pr.NettoHours, Is.GreaterThan(0), "netto must be sane (positive) after healing");
        });
    }

    [Test]
    public async Task Correct_Value_Unchanged()
    {
        const int siteId = 9602;
        var token = "selfheal-correct";
        var date = new DateTime(2026, 6, 16, 0, 0, 0);
        await SeedSiteAsync(siteId, useOneMinuteIntervals: false);
        await SeedDeviceAsync(token, "10002");

        // 30-min pause, already-correct id 7 = (30/5)+1.
        var model = BuildModel(date, pauseId: 7, pauseMinutes: 30);
        var service = BuildService(pauseIdSelfHealEnabled: null);

        var result = await service.UpdateWorkingHour(sdkSiteId: siteId, model, token);
        Assert.That(result.Success, Is.True, result.Message);

        var pr = await PersistedRowAsync(siteId, date);
        Assert.That(pr.Pause1Id, Is.EqualTo(7), "correct id must be left unchanged");
    }

    [Test]
    public async Task OffByOne_Unchanged()
    {
        const int siteId = 9603;
        var token = "selfheal-offbyone";
        var date = new DateTime(2026, 6, 16, 0, 0, 0);
        await SeedSiteAsync(siteId, useOneMinuteIntervals: false);
        await SeedDeviceAsync(token, "10003");

        // 30-min pause, id 6 = 30/5 (missing +1) understates -> not corrected.
        var model = BuildModel(date, pauseId: 6, pauseMinutes: 30);
        var service = BuildService(pauseIdSelfHealEnabled: null);

        var result = await service.UpdateWorkingHour(sdkSiteId: siteId, model, token);
        Assert.That(result.Success, Is.True, result.Message);

        var pr = await PersistedRowAsync(siteId, date);
        Assert.That(pr.Pause1Id, Is.EqualTo(6), "off-by-one must be left unchanged");
    }

    [Test]
    public async Task OneMinuteSite_Unchanged()
    {
        const int siteId = 9604;
        var token = "selfheal-1min";
        var date = new DateTime(2026, 6, 16, 0, 0, 0);
        await SeedSiteAsync(siteId, useOneMinuteIntervals: true);
        await SeedDeviceAsync(token, "10004");

        // 1-minute sites are never affected; the guard must skip them.
        var model = BuildModel(date, pauseId: 116, pauseMinutes: 28);
        var service = BuildService(pauseIdSelfHealEnabled: null);

        var result = await service.UpdateWorkingHour(sdkSiteId: siteId, model, token);
        Assert.That(result.Success, Is.True, result.Message);

        var pr = await PersistedRowAsync(siteId, date);
        Assert.That(pr.Pause1Id, Is.EqualTo(116), "guard must skip 1-minute sites");
    }

    [Test]
    public async Task FlagDisabled_NotHealed()
    {
        const int siteId = 9605;
        var token = "selfheal-flagoff";
        var date = new DateTime(2026, 6, 16, 0, 0, 0);
        await SeedSiteAsync(siteId, useOneMinuteIntervals: false);
        await SeedDeviceAsync(token, "10005");

        var model = BuildModel(date, pauseId: 116, pauseMinutes: 28);
        // Kill switch off -> the corrupt id must be persisted as-is.
        var service = BuildService(pauseIdSelfHealEnabled: false);

        var result = await service.UpdateWorkingHour(sdkSiteId: siteId, model, token);
        Assert.That(result.Success, Is.True, result.Message);

        var pr = await PersistedRowAsync(siteId, date);
        Assert.That(pr.Pause1Id, Is.EqualTo(116), "flag off -> guard must not heal");
    }

    [Test]
    public async Task Kiosk_Path_IsHealed()
    {
        const int siteId = 9606;
        var token = "selfheal-kiosk";
        var date = new DateTime(2026, 6, 17, 0, 0, 0);
        await SeedSiteAsync(siteId, useOneMinuteIntervals: false);
        await SeedDeviceAsync(token, "10006");

        // Explicit kiosk-path heal of a corrupt absolute-tick id.
        var model = BuildModel(date, pauseId: 116, pauseMinutes: 28);
        var service = BuildService(pauseIdSelfHealEnabled: null);

        var result = await service.UpdateWorkingHour(sdkSiteId: siteId, model, token);
        Assert.That(result.Success, Is.True, result.Message);

        var pr = await PersistedRowAsync(siteId, date);
        Assert.Multiple(() =>
        {
            Assert.That(pr.Pause1Id, Is.EqualTo(6), "kiosk save must heal the corrupt id");
            Assert.That(pr.NettoHours, Is.GreaterThan(0), "kiosk netto must be sane after healing");
        });
    }
}
