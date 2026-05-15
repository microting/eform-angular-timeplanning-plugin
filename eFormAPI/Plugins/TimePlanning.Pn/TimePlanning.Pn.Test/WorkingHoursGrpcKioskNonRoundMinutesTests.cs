using System;
using System.Linq;
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
/// End-to-end integration coverage for the kiosk gRPC write path
/// (<c>TimePlanningWorkingHoursService.UpdateWorkingHour(int? sdkSiteId, model, token)</c>):
/// when an <c>AssignedSite</c> has <c>UseOneMinuteIntervals=true</c>, a payload with
/// non-5-min Start/Stop stamps must persist exact <c>Start1StartedAt</c>/<c>Stop1StoppedAt</c>
/// values to the database. Locks the 08:04→10:10 user-reported example.
/// </summary>
[TestFixture]
public class WorkingHoursGrpcKioskNonRoundMinutesTests : TestBaseSetup
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

        // The kiosk path only references `userService.UserId` and `dbContext` from
        // the constructor graph; baseDbContext / coreHelper are not touched on this
        // branch. Passing null! is safe because primary-constructor parameters are
        // captured by the compiler as private fields and never dereferenced here.
        _service = new TimePlanningWorkingHoursService(
            Substitute.For<ILogger<TimePlanningWorkingHoursService>>(),
            TimePlanningPnDbContext!,
            userService,
            localizationService,
            baseDbContext: null!,
            options,
            coreService);
    }

    [Test]
    public async Task KioskCreate_FlagOn_NonRoundMinutes_PersistsExactStamps()
    {
        var token = "kiosk-test-token-08-04";
        await new AssignedSiteEntity
        {
            SiteId = 9501,
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

        var date = new DateTime(2026, 5, 15, 0, 0, 0);
        var model = new TimePlanningWorkingHoursUpdateModel
        {
            Date = date,
            Shift1Start = 97,
            Shift1Stop = 122,
            Shift1Pause = 0,
            Start1StartedAt = "2026-05-15T08:04:00",
            Stop1StoppedAt  = "2026-05-15T10:10:00",
            CommentWorker = "",
            OsVersion = "1.0",
            Model = "Test",
            Manufacturer = "Test",
            SoftwareVersion = "1.0.0",
        };

        var result = await _service.UpdateWorkingHour(sdkSiteId: 9501, model, token);

        Assert.That(result.Success, Is.True, result.Message);

        var pr = await TimePlanningPnDbContext!.PlanRegistrations
            .AsNoTracking()
            .SingleAsync(x => x.SdkSitId == 9501 && x.Date == date);
        Assert.Multiple(() =>
        {
            Assert.That(pr.Start1StartedAt, Is.EqualTo(new DateTime(2026, 5, 15, 8, 4, 0)),
                "Kiosk create must persist exact non-5-min Start1StartedAt");
            Assert.That(pr.Stop1StoppedAt, Is.EqualTo(new DateTime(2026, 5, 15, 10, 10, 0)),
                "Kiosk create must persist exact non-5-min Stop1StoppedAt");
            Assert.That(pr.Start1Id, Is.EqualTo(97));
            Assert.That(pr.Stop1Id, Is.EqualTo(122));
        });
    }

    [Test]
    public async Task KioskUpdate_FlagOn_NonRoundMinutes_OverwritesExistingRow()
    {
        var token = "kiosk-test-token-update";
        var date = new DateTime(2026, 5, 16, 0, 0, 0);

        await new AssignedSiteEntity
        {
            SiteId = 9502,
            UseOneMinuteIntervals = true,
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);

        await new RegistrationDeviceEntity
        {
            Token = token,
            Name = "Kiosk Update Device",
            OtpCode = "22222",
            SoftwareVersion = "1.0.0",
            Manufacturer = "Test",
            Model = "Test",
            OsVersion = "1.0",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);

        await new PlanRegistrationEntity
        {
            SdkSitId = 9502,
            Date = date,
            Start1Id = 96,
            Stop1Id = 120,
            Pause1Id = 0,
            Start1StartedAt = null,
            Stop1StoppedAt = null,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);

        var model = new TimePlanningWorkingHoursUpdateModel
        {
            Date = date,
            Shift1Start = 97,
            Shift1Stop = 122,
            Shift1Pause = 0,
            Start1StartedAt = "2026-05-16T08:04:00",
            Stop1StoppedAt  = "2026-05-16T10:10:00",
            CommentWorker = "",
            OsVersion = "1.0",
            Model = "Test",
            Manufacturer = "Test",
            SoftwareVersion = "1.0.0",
        };

        var result = await _service.UpdateWorkingHour(sdkSiteId: 9502, model, token);
        Assert.That(result.Success, Is.True, result.Message);

        var pr = await TimePlanningPnDbContext!.PlanRegistrations
            .AsNoTracking()
            .Where(x => x.SdkSitId == 9502 && x.Date == date)
            .OrderByDescending(x => x.Id)
            .FirstAsync();
        Assert.That(pr.Start1StartedAt, Is.EqualTo(new DateTime(2026, 5, 16, 8, 4, 0)));
        Assert.That(pr.Stop1StoppedAt,  Is.EqualTo(new DateTime(2026, 5, 16, 10, 10, 0)));
    }
}
