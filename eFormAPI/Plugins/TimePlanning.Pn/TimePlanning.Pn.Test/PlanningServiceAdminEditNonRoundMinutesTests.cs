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

    // #5571: PlanChangedByAdmin marks a deliberate plan override, so the
    // settings template stops overwriting the day. Only a change to the plan
    // itself (PlanHours, planned start/end/break of shifts 1-5) may set it;
    // a comment, a netto override or an identical re-save must not.

    /// <summary>
    /// A five-minute site on the weekday-plan branch with one row planned
    /// 08:00-16:00 with a 30 min break (7.5 h).
    /// </summary>
    private async Task<PlanRegistrationEntity> SeedPlannedRow(int siteUid, bool planChangedByAdmin = false,
        double planHours = 7.5)
    {
        await new AssignedSiteEntity
        {
            SiteId = siteUid,
            UseOneMinuteIntervals = false,
            UseOnlyPlanHours = false,
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);

        var planning = new PlanRegistrationEntity
        {
            SdkSitId = siteUid,
            Date = new DateTime(2026, 5, 18, 0, 0, 0),
            PlanHours = planHours,
            PlannedStartOfShift1 = 480,
            PlannedEndOfShift1 = 960,
            PlannedBreakOfShift1 = 30,
            PlanChangedByAdmin = planChangedByAdmin,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        };
        await planning.Create(TimePlanningPnDbContext!);
        return planning;
    }

    /// <summary>The save that the web client sends when the plan is left as seeded.</summary>
    private static TimePlanningPlanningPrDayModel UnchangedPlanSave(PlanRegistrationEntity planning) => new()
    {
        Id = planning.Id,
        Date = planning.Date,
        PlanHours = planning.PlanHours,
        PlannedStartOfShift1 = 480,
        PlannedEndOfShift1 = 960,
        PlannedBreakOfShift1 = 30,
        CommentOffice = "",
    };

    private async Task<bool> StoredPlanChangedByAdmin(int id) =>
        (await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .SingleAsync(x => x.Id == id)).PlanChangedByAdmin;

    [Test]
    public async Task AdminUpdate_CommentOnly_DoesNotMarkPlanChangedByAdmin()
    {
        var planning = await SeedPlannedRow(9611);
        var model = UnchangedPlanSave(planning);
        model.CommentOffice = "Called in late";

        var result = await _service.Update(planning.Id, model);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(await StoredPlanChangedByAdmin(planning.Id), Is.False);
    }

    [Test]
    public async Task AdminUpdate_NettoOverrideOnly_DoesNotMarkPlanChangedByAdmin()
    {
        var planning = await SeedPlannedRow(9612);
        var model = UnchangedPlanSave(planning);
        model.NettoHoursOverride = 5;
        model.NettoHoursOverrideActive = true;

        var result = await _service.Update(planning.Id, model);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(await StoredPlanChangedByAdmin(planning.Id), Is.False);
    }

    [Test]
    public async Task AdminUpdate_IdenticalPlan_DoesNotMarkPlanChangedByAdmin()
    {
        var planning = await SeedPlannedRow(9613);

        var result = await _service.Update(planning.Id, UnchangedPlanSave(planning));

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(await StoredPlanChangedByAdmin(planning.Id), Is.False);
    }

    [Test]
    public async Task AdminUpdate_PlanChange_MarksPlanChangedByAdmin()
    {
        var planning = await SeedPlannedRow(9614);
        var model = UnchangedPlanSave(planning);
        model.PlannedEndOfShift1 = 1020; // 16:00 -> 17:00

        var result = await _service.Update(planning.Id, model);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(await StoredPlanChangedByAdmin(planning.Id), Is.True);
    }

    [Test]
    public async Task AdminUpdate_AlreadyMarked_StaysMarkedOnACommentOnlySave()
    {
        var planning = await SeedPlannedRow(9615, planChangedByAdmin: true);
        var model = UnchangedPlanSave(planning);
        model.CommentOffice = "Called in late";

        var result = await _service.Update(planning.Id, model);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(await StoredPlanChangedByAdmin(planning.Id), Is.True);
    }

    [Test]
    public async Task AdminUpdate_CommentOnly_PlanHoursDisagreeingWithShifts_DoesNotMarkPlanChangedByAdmin()
    {
        // A template/sheet row: 7.4 h planned, shifts worth 7.5 h. The save
        // recomputes PlanHours from the shifts, but the admin sent 7.4 back
        // unchanged, so this is not a plan edit.
        var planning = await SeedPlannedRow(9616, planHours: 7.4);
        var model = UnchangedPlanSave(planning);
        model.CommentOffice = "Called in late";

        var result = await _service.Update(planning.Id, model);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(await StoredPlanChangedByAdmin(planning.Id), Is.False);
    }

    [Test]
    public async Task AdminUpdate_ActualTimesOnly_DialogResendsShiftHours_DoesNotMarkPlanChangedByAdmin()
    {
        // The web dialog recomputes planHours = shifts / 60 whenever an actual
        // time is edited, so a 7.4 h template row (shifts worth 7.5 h) comes
        // back with 7.5. In shift mode the server derives PlanHours from the
        // unchanged shifts anyway, so this is not a plan edit.
        var planning = await SeedPlannedRow(9617, planHours: 7.4);
        var model = UnchangedPlanSave(planning);
        model.PlanHours = 7.5;
        model.Start1Id = 97;  // 08:00
        model.Stop1Id = 193;  // 16:00

        var result = await _service.Update(planning.Id, model);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(await StoredPlanChangedByAdmin(planning.Id), Is.False);
    }
}
