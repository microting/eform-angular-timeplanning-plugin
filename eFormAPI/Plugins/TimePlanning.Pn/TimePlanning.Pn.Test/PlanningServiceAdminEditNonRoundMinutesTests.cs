using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Models.Planning;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningPlanningService;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using PlanRegistrationEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration;

namespace TimePlanning.Pn.Test;

/// <summary>
/// End-to-end integration coverage for the admin web edit path
/// (<c>TimePlanningPlanningService.Update(int id, TimePlanningPlanningPrDayModel)</c>):
/// when an admin saves a row with off-grid actual stamps (e.g. 08:04 / 10:10), the
/// service must persist <c>Start1StartedAt</c>/<c>Stop1StoppedAt</c> exactly without
/// 5-min snapping. Counterpart round-minute test verifies the legacy path stays
/// byte-identical for the same workflow.
/// </summary>
[TestFixture]
public class PlanningServiceAdminEditNonRoundMinutesTests : TestBaseSetup
{
    private ITimePlanningPlanningService _service = null!;

    [SetUp]
    public async Task SetUpTest()
    {
        await base.Setup();

        var userService = Substitute.For<IUserService>();
        userService.UserId.Returns(1);
        userService.GetCurrentUserAsync().Returns(new EformUser { Id = 1 });

        var localizationService = Substitute.For<ITimePlanningLocalizationService>();
        localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

        var coreService = Substitute.For<IEFormCoreService>();
        var core = await GetCore();
        coreService.GetCore().Returns(core);

        var dbContextHelper = Substitute.For<ITimePlanningDbContextHelper>();
        dbContextHelper.GetDbContext().Returns(TimePlanningPnDbContext);

        var options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        options.Value.Returns(new TimePlanningBaseSettings
        {
            AutoBreakCalculationActive = "0",
            DayOfPayment = 20,
            GpsEnabled = "0",
            SnapshotEnabled = "0"
        });

        _service = new TimePlanningPlanningService(
            Substitute.For<ILogger<TimePlanningPlanningService>>(),
            options,
            TimePlanningPnDbContext!,
            dbContextHelper,
            userService,
            localizationService,
            null,
            coreService);
    }

    [Test]
    public async Task AdminUpdate_FlagOn_NonRoundMinutes_PersistsExactStamps()
    {
        // Site flagged for 1-min precision + a seeded PlanRegistration row.
        await new AssignedSiteEntity
        {
            SiteId = 9601,
            UseOneMinuteIntervals = true,
            AllowEditOfRegistrations = true,
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);

        var date = new DateTime(2026, 5, 15, 0, 0, 0);
        var planning = new PlanRegistrationEntity
        {
            SdkSitId = 9601,
            Date = date,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        };
        await planning.Create(TimePlanningPnDbContext!);

        var model = new TimePlanningPlanningPrDayModel
        {
            Id = planning.Id,
            Date = date,
            CommentOffice = "",
            Start1Id = 97, // legacy slot index — should ride alongside
            Stop1Id  = 122,
            Pause1Id = 0,
            Start1StartedAt = new DateTime(2026, 5, 15, 8, 4, 0),
            Stop1StoppedAt  = new DateTime(2026, 5, 15, 10, 10, 0),
        };

        var result = await _service.Update(planning.Id, model);
        Assert.That(result.Success, Is.True, result.Message);

        var reloaded = await TimePlanningPnDbContext!.PlanRegistrations
            .AsNoTracking()
            .SingleAsync(x => x.Id == planning.Id);
        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Start1StartedAt, Is.EqualTo(new DateTime(2026, 5, 15, 8, 4, 0)),
                "Admin edit must persist exact non-5-min Start1StartedAt");
            Assert.That(reloaded.Stop1StoppedAt, Is.EqualTo(new DateTime(2026, 5, 15, 10, 10, 0)),
                "Admin edit must persist exact non-5-min Stop1StoppedAt");
        });
    }

    [Test]
    public async Task AdminUpdate_RoundMinutes_PersistsExactStamps()
    {
        // 5-min-aligned counterpart: legacy slot-aligned stamps must also round-trip
        // without rounding/snapping drift.
        await new AssignedSiteEntity
        {
            SiteId = 9602,
            UseOneMinuteIntervals = false,
            AllowEditOfRegistrations = true,
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);

        var date = new DateTime(2026, 5, 16, 0, 0, 0);
        var planning = new PlanRegistrationEntity
        {
            SdkSitId = 9602,
            Date = date,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        };
        await planning.Create(TimePlanningPnDbContext!);

        var model = new TimePlanningPlanningPrDayModel
        {
            Id = planning.Id,
            Date = date,
            CommentOffice = "",
            Start1Id = 97,
            Stop1Id  = 121,
            Pause1Id = 0,
            Start1StartedAt = new DateTime(2026, 5, 16, 8, 0, 0),
            Stop1StoppedAt  = new DateTime(2026, 5, 16, 10, 0, 0),
        };

        var result = await _service.Update(planning.Id, model);
        Assert.That(result.Success, Is.True, result.Message);

        var reloaded = await TimePlanningPnDbContext!.PlanRegistrations
            .AsNoTracking()
            .SingleAsync(x => x.Id == planning.Id);
        Assert.That(reloaded.Start1StartedAt, Is.EqualTo(new DateTime(2026, 5, 16, 8, 0, 0)));
        Assert.That(reloaded.Stop1StoppedAt,  Is.EqualTo(new DateTime(2026, 5, 16, 10, 0, 0)));
    }
}
