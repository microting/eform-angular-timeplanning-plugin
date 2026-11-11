using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using Microting.TimePlanningBase.Infrastructure.Helpers;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Interceptors;
using TimePlanning.Pn.Infrastructure.Models.Planning;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Services.PushNotificationService;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningPlanningService;
using TimePlanning.Pn.Services.TimePlanningSettingService;
using static TimePlanning.Pn.Test.PlanPatternTestSeed;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using AssignedSiteModel = TimePlanning.Pn.Infrastructure.Models.Settings.AssignedSite;
using SdkSite = Microting.eForm.Infrastructure.Data.Entities.Site;

#nullable enable
namespace TimePlanning.Pn.Test;

/// <summary>
/// #5489 guarded addition 1: the three plugin places that write the weekday
/// template into PlanRegistration rows take the plan from the worker's active
/// PlanPattern instead, when the pattern governs the row's date.
///
/// The invariant tests pin today's behaviour: a worker without a pattern gets
/// exactly the template values in all three places.
/// </summary>
[TestFixture]
public class PlanPatternRowGenerationTests : TestBaseSetup
{
    private const int SiteId = 5489;

    private static DateTime Today => DateTime.UtcNow.Date;

    // ---------------------------------------------------------------- invariant

    [Test]
    public async Task Invariant_NoPattern_LazyCreation_WritesTemplate()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        var row = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(2));

        await RunPeriodUpdate(site, Today.AddDays(1), Today.AddDays(3));

        var stored = await ReadRowAsync(TimePlanningPnDbContext!, row.Id);
        Assert.That(Shift1(stored), Is.EqualTo((TemplateStart, TemplateEnd, TemplateBreak, TemplateHours)));
        Assert.That(stored.PlanText, Is.EqualTo(""), "The template path never writes PlanText");
    }

    [Test]
    public async Task Invariant_NoPattern_UpdatePlanRegistration_WritesTemplate()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        var row = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(2));

        await RunSingleUpdate(site, row.Id);

        var stored = await ReadRowAsync(TimePlanningPnDbContext!, row.Id);
        Assert.That(Shift1(stored), Is.EqualTo((TemplateStart, TemplateEnd, TemplateBreak, TemplateHours)));
        Assert.That(stored.PlanText, Is.EqualTo(""));
    }

    [Test]
    public async Task Invariant_NoPattern_UpdateAssignedSite_WritesTemplate()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        var row = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(2));

        await RunUpdateAssignedSite(site);

        var stored = await ReadRowAsync(TimePlanningPnDbContext!, row.Id);
        Assert.That(Shift1(stored), Is.EqualTo((TemplateStart, TemplateEnd, TemplateBreak, TemplateHours)));
        Assert.That(stored.PlanText, Is.EqualTo(""));
    }

    // ------------------------------------------- 1. UpdatePlanRegistrationsInPeriod

    [Test]
    public async Task LazyCreation_Pattern_WritesPatternPlan()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        await CreatePatternAsync(TimePlanningPnDbContext!, site, Today.AddDays(-30));
        var row = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(2));

        await RunPeriodUpdate(site, Today.AddDays(1), Today.AddDays(3));

        var stored = await ReadRowAsync(TimePlanningPnDbContext!, row.Id);
        Assert.That(stored.PlanText, Is.EqualTo(PatternText));
        Assert.That(Shift1(stored), Is.EqualTo((8 * 60, 16 * 60, 30, 7.5)));
    }

    [Test]
    public async Task LazyCreation_PatternEmptyDay_ClearsPlan()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        await CreatePatternAsync(TimePlanningPnDbContext!, site, Today.AddDays(-30), planTextForWeek: _ => "");
        var row = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(2),
            r => { r.PlanHours = 6; r.PlannedStartOfShift1 = 60; r.PlannedEndOfShift1 = 420; });

        await RunPeriodUpdate(site, Today.AddDays(1), Today.AddDays(3));

        var stored = await ReadRowAsync(TimePlanningPnDbContext!, row.Id);
        Assert.That(stored.PlanText, Is.EqualTo(""));
        Assert.That(Shift1(stored), Is.EqualTo((0, 0, 0, 0.0)));
    }

    [Test]
    public async Task LazyCreation_DateBeforeValidFrom_WritesTemplate()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        await CreatePatternAsync(TimePlanningPnDbContext!, site, Today.AddDays(10));
        var row = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(2));

        await RunPeriodUpdate(site, Today.AddDays(1), Today.AddDays(3));

        var stored = await ReadRowAsync(TimePlanningPnDbContext!, row.Id);
        Assert.That(Shift1(stored), Is.EqualTo((TemplateStart, TemplateEnd, TemplateBreak, TemplateHours)));
        Assert.That(stored.PlanText, Is.EqualTo(""));
    }

    [Test]
    public async Task LazyCreation_CorruptPattern_FallsBackToTemplate()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        var pattern = await CreatePatternAsync(TimePlanningPnDbContext!, site, Today.AddDays(-30));
        pattern.RotationWeeks = 9; // outside 1..8: the resolver throws
        await pattern.Update(TimePlanningPnDbContext!);
        var row = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(2));

        await RunPeriodUpdate(site, Today.AddDays(1), Today.AddDays(3));

        var stored = await ReadRowAsync(TimePlanningPnDbContext!, row.Id);
        Assert.That(Shift1(stored), Is.EqualTo((TemplateStart, TemplateEnd, TemplateBreak, TemplateHours)));
    }

    [Test]
    public async Task LazyCreation_PlanChangedByAdmin_Untouched()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        await CreatePatternAsync(TimePlanningPnDbContext!, site, Today.AddDays(-30));
        var row = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(2), r =>
        {
            r.PlanChangedByAdmin = true;
            r.PlanText = "10:00-12:00";
            r.PlanHours = 2;
            r.PlannedStartOfShift1 = 600;
            r.PlannedEndOfShift1 = 720;
        });

        await RunPeriodUpdate(site, Today.AddDays(1), Today.AddDays(3));

        var stored = await ReadRowAsync(TimePlanningPnDbContext!, row.Id);
        Assert.That(stored.PlanText, Is.EqualTo("10:00-12:00"));
        Assert.That(Shift1(stored), Is.EqualTo((600, 720, 0, 2.0)));
    }

    [Test]
    public async Task LazyCreation_PaidDay_Untouched()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        await CreatePatternAsync(TimePlanningPnDbContext!, site, Today.AddDays(-90));
        var paidDate = Today.AddDays(-45); // before dayOfPayment (today - 1 month)
        var row = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, paidDate, r => r.PlanHours = 3);

        await RunPeriodUpdate(site, paidDate.AddDays(-1), paidDate.AddDays(1));

        var stored = await ReadRowAsync(TimePlanningPnDbContext!, row.Id);
        Assert.That(stored.PlanText, Is.EqualTo(""));
        Assert.That(Shift1(stored), Is.EqualTo((0, 0, 0, 3.0)));
    }

    [Test]
    public async Task LazyCreation_LockedDay_Untouched()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        await CreatePatternAsync(TimePlanningPnDbContext!, site, Today.AddDays(-30));
        var row = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(-5), r => r.PlanHours = 3);
        await LockThroughAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(-3));

        await RunPeriodUpdate(site, Today.AddDays(-6), Today.AddDays(-4));

        var stored = await ReadRowAsync(TimePlanningPnDbContext!, row.Id);
        Assert.That(stored.PlanText, Is.EqualTo(""));
        Assert.That(Shift1(stored), Is.EqualTo((0, 0, 0, 3.0)));
    }

    [Test]
    public async Task LazyCreation_AbsenceDay_TakesPatternHours_KeepsAbsence()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        await CreatePatternAsync(TimePlanningPnDbContext!, site, Today.AddDays(-30));
        var message = await TimePlanningPnDbContext!.Messages.FirstAsync();
        var row = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(2), r =>
        {
            r.OnVacation = true;
            r.MessageId = message.Id;
        });

        await RunPeriodUpdate(site, Today.AddDays(1), Today.AddDays(3));

        var stored = await ReadRowAsync(TimePlanningPnDbContext!, row.Id);
        Assert.That(stored.PlanText, Is.EqualTo(PatternText));
        Assert.That(stored.PlanHours, Is.EqualTo(7.5));
        Assert.That(stored.OnVacation, Is.True);
        Assert.That(stored.MessageId, Is.EqualTo(message.Id));
    }

    [Test]
    public async Task LazyCreation_RemovedPattern_WritesTemplate()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        var pattern = await CreatePatternAsync(TimePlanningPnDbContext!, site, Today.AddDays(-30));
        await pattern.Delete(TimePlanningPnDbContext!);
        var row = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(2));

        await RunPeriodUpdate(site, Today.AddDays(1), Today.AddDays(3));

        var stored = await ReadRowAsync(TimePlanningPnDbContext!, row.Id);
        Assert.That(Shift1(stored), Is.EqualTo((TemplateStart, TemplateEnd, TemplateBreak, TemplateHours)));
    }

    // -------------------------------------------------- 2. UpdatePlanRegistration

    [Test]
    public async Task UpdatePlanRegistration_Pattern_WritesPatternPlan()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        await CreatePatternAsync(TimePlanningPnDbContext!, site, Today.AddDays(-30));
        var row = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(2));

        await RunSingleUpdate(site, row.Id);

        var stored = await ReadRowAsync(TimePlanningPnDbContext!, row.Id);
        Assert.That(stored.PlanText, Is.EqualTo(PatternText));
        Assert.That(Shift1(stored), Is.EqualTo((8 * 60, 16 * 60, 30, 7.5)));
    }

    [Test]
    public async Task UpdatePlanRegistration_PlanChangedByAdmin_Untouched()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        await CreatePatternAsync(TimePlanningPnDbContext!, site, Today.AddDays(-30));
        var row = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(2), r =>
        {
            r.PlanChangedByAdmin = true;
            r.PlanText = "10:00-12:00";
            r.PlanHours = 2;
        });

        await RunSingleUpdate(site, row.Id);

        var stored = await ReadRowAsync(TimePlanningPnDbContext!, row.Id);
        Assert.That(stored.PlanText, Is.EqualTo("10:00-12:00"));
        Assert.That(stored.PlanHours, Is.EqualTo(2));
    }

    [Test]
    public async Task UpdatePlanRegistration_PaidDay_Untouched()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        await CreatePatternAsync(TimePlanningPnDbContext!, site, Today.AddDays(-90));
        var row = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(-45), r => r.PlanHours = 3);

        await RunSingleUpdate(site, row.Id);

        var stored = await ReadRowAsync(TimePlanningPnDbContext!, row.Id);
        Assert.That(stored.PlanText, Is.EqualTo(""));
        Assert.That(stored.PlanHours, Is.EqualTo(3));
    }

    [Test]
    public async Task UpdatePlanRegistration_LockedDay_StillRefusedWithDayLockedException()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        await CreatePatternAsync(TimePlanningPnDbContext!, site, Today.AddDays(-30));
        var row = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(-5), r => r.PlanHours = 3);
        await LockThroughAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(-3));

        await Assert.ThrowsAsync<DayLockedException>(() => RunSingleUpdate(site, row.Id));

        await using var fresh = CreateTimePlanningPnDbContext();
        var stored = await ReadRowAsync(fresh, row.Id);
        Assert.That(stored.PlanText, Is.EqualTo(""));
        Assert.That(stored.PlanHours, Is.EqualTo(3));
    }

    [Test]
    public async Task UpdatePlanRegistration_AbsenceDay_TakesPatternHours_KeepsAbsence()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        await CreatePatternAsync(TimePlanningPnDbContext!, site, Today.AddDays(-30));
        var row = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(2), r => r.Sick = true);

        await RunSingleUpdate(site, row.Id);

        var stored = await ReadRowAsync(TimePlanningPnDbContext!, row.Id);
        Assert.That(stored.PlanText, Is.EqualTo(PatternText));
        Assert.That(stored.PlanHours, Is.EqualTo(7.5));
        Assert.That(stored.Sick, Is.True);
    }

    // ----------------------------------------------------- 3. UpdateAssignedSite

    [Test]
    public async Task UpdateAssignedSite_Pattern_WritesPatternPlanFromToday()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        // Two-week rotation anchored on this week's Monday: week 0 long, week 1 short.
        var anchor = MondayOnOrBefore(Today);
        await CreatePatternAsync(TimePlanningPnDbContext!, site, Today.AddDays(-30), rotationWeeks: 2,
            planTextForWeek: w => w == 0 ? PatternText : PatternTextShort, anchor: anchor);
        var past = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(-2), r => r.PlanHours = 3);

        await RunUpdateAssignedSite(site);

        var rows = await LiveRowsAsync(TimePlanningPnDbContext!, SiteId);
        var thisWeek = rows.Single(r => r.Date == anchor.AddDays(Math.Max(0, (Today - anchor).Days)));
        var nextWeek = rows.Single(r => r.Date == thisWeek.Date.AddDays(7));
        Assert.That(thisWeek.PlanText, Is.EqualTo(PatternText));
        Assert.That(thisWeek.PlanHours, Is.EqualTo(7.5));
        Assert.That(nextWeek.PlanText, Is.EqualTo(PatternTextShort));
        Assert.That(Shift1(nextWeek), Is.EqualTo((9 * 60, 13 * 60, 0, 4.0)));

        var storedPast = rows.Single(r => r.Id == past.Id);
        Assert.That(storedPast.PlanText, Is.EqualTo(""), "UpdateAssignedSite never writes before today");
        Assert.That(storedPast.PlanHours, Is.EqualTo(3));
    }

    [Test]
    public async Task UpdateAssignedSite_PlanChangedByAdminAndAbsence_KeepTheirExistingSkip()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        await CreatePatternAsync(TimePlanningPnDbContext!, site, Today.AddDays(-30));
        var manual = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(1), r =>
        {
            r.PlanChangedByAdmin = true;
            r.PlanText = "10:00-12:00";
            r.PlanHours = 2;
        });
        var absence = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(2), r =>
        {
            r.OnVacation = true;
            r.PlanHours = 5;
        });

        await RunUpdateAssignedSite(site);

        var storedManual = await ReadRowAsync(TimePlanningPnDbContext!, manual.Id);
        var storedAbsence = await ReadRowAsync(TimePlanningPnDbContext!, absence.Id);
        Assert.That(storedManual.PlanText, Is.EqualTo("10:00-12:00"));
        Assert.That(storedManual.PlanHours, Is.EqualTo(2));
        Assert.That(storedAbsence.PlanText, Is.EqualTo(""));
        Assert.That(storedAbsence.PlanHours, Is.EqualTo(5));
        Assert.That(storedAbsence.OnVacation, Is.True);
    }

    [Test]
    public async Task UpdateAssignedSite_PatternedWorker_StaysOffTheSheetWhatever_TheRequestSays()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        await CreatePatternAsync(TimePlanningPnDbContext!, site, Today);

        await RunUpdateAssignedSite(site, m => m.UseGoogleSheetAsDefault = true);

        var stored = await TimePlanningPnDbContext!.AssignedSites.AsNoTracking().SingleAsync(x => x.Id == site.Id);
        Assert.That(stored.UseGoogleSheetAsDefault, Is.False);
    }

    [Test]
    public async Task UpdateAssignedSite_UnpatternedWorker_TakesTheRequestedSheetFlag()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        var removed = await CreatePatternAsync(TimePlanningPnDbContext!, site, Today);
        await removed.Delete(TimePlanningPnDbContext!);

        await RunUpdateAssignedSite(site, m => m.UseGoogleSheetAsDefault = true);

        var stored = await TimePlanningPnDbContext!.AssignedSites.AsNoTracking().SingleAsync(x => x.Id == site.Id);
        Assert.That(stored.UseGoogleSheetAsDefault, Is.True);
    }

    // ------------------------------------------- accepted content handover

    [Test]
    public async Task PatternBranch_AcceptedHandoverDay_IsKeptInAllThreePlaces()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        await CreatePatternAsync(TimePlanningPnDbContext!, site, Today.AddDays(-30));
        // AcceptAsync stamps ContentHandoverRequestId on both rows of an
        // accepted handover and nowhere else.
        void HandedOver(PlanRegistration r)
        {
            r.ContentHandoverRequestId = 4711;
            r.PlanText = "10:00-12:00";
            r.PlanHours = 2;
            r.PlannedStartOfShift1 = 600;
            r.PlannedEndOfShift1 = 720;
        }
        var lazy = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(1), HandedOver);
        var single = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(2), HandedOver);
        var settings = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(3), HandedOver);

        await RunPeriodUpdate(site, Today.AddDays(1), Today.AddDays(1));
        await RunSingleUpdate(site, single.Id);
        await RunUpdateAssignedSite(site);

        foreach (var id in new[] { lazy.Id, single.Id, settings.Id })
        {
            var stored = await ReadRowAsync(TimePlanningPnDbContext!, id);
            Assert.That(stored.PlanText, Is.EqualTo("10:00-12:00"), $"row {id}");
            Assert.That(Shift1(stored), Is.EqualTo((600, 720, 0, 2.0)), $"row {id}");
        }
    }

    [Test]
    public async Task UpdatePlanRegistration_UsesThePatternTheCallerLoaded()
    {
        var site = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        await CreatePatternAsync(TimePlanningPnDbContext!, site, Today.AddDays(-30));
        var row = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, Today.AddDays(2));

        // The caller loaded "no pattern" (e.g. before one was saved): the
        // helper must use that rather than query again.
        var tracked = await TimePlanningPnDbContext!.PlanRegistrations.AsTracking().SingleAsync(x => x.Id == row.Id);
        var timeline = await OneMinuteModeTimeline.BuildAsync(TimePlanningPnDbContext, site);
        await PlanRegistrationHelper.UpdatePlanRegistration(
            tracked, TimePlanningPnDbContext, site, DateTime.Now.AddMonths(-1), timeline, null);

        var stored = await ReadRowAsync(TimePlanningPnDbContext, row.Id);
        Assert.That(Shift1(stored), Is.EqualTo((TemplateStart, TemplateEnd, TemplateBreak, TemplateHours)));
    }

    // ------------------------------------------------- Google Sheet import

    [Test]
    public async Task SheetImport_SkipsPatternedWorkers_AndImportsEveryoneElseAsToday()
    {
        const int plainSiteId = SiteId + 1;
        const int deletedOffSiteId = SiteId + 2;
        const int deletedOnSiteId = SiteId + 3;
        var patterned = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, SiteId);
        await CreatePatternAsync(TimePlanningPnDbContext!, patterned, Today.AddDays(-30));
        // Off the sheet but never patterned: the import has always written
        // such a worker's rows, and still must.
        await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, plainSiteId, useGoogleSheet: false);
        // Pattern deleted, still off the sheet: handed to the template, so skipped.
        var deletedOff = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, deletedOffSiteId, useGoogleSheet: false);
        await (await CreatePatternAsync(TimePlanningPnDbContext!, deletedOff, Today)).Delete(TimePlanningPnDbContext!);
        // Pattern deleted, then put back on the sheet by an admin: imported again.
        var deletedOn = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, deletedOnSiteId, useGoogleSheet: true);
        await (await CreatePatternAsync(TimePlanningPnDbContext!, deletedOn, Today)).Delete(TimePlanningPnDbContext!);

        var day = Today.AddDays(3);
        void PatternPlan(PlanRegistration r)
        {
            r.PlanText = PatternText;
            PlanRegistrationPlanText.ParseInto(r);
        }
        var patternRow = await CreateRowAsync(TimePlanningPnDbContext!, SiteId, day, PatternPlan);
        var plainRow = await CreateRowAsync(TimePlanningPnDbContext!, plainSiteId, day);
        var deletedOffRow = await CreateRowAsync(TimePlanningPnDbContext!, deletedOffSiteId, day, PatternPlan);
        var deletedOnRow = await CreateRowAsync(TimePlanningPnDbContext!, deletedOnSiteId, day, PatternPlan);

        var sdkDb = (await GetCore()).DbContextHelper.GetDbContext();
        await new SdkSite { Name = "Pat Tern", MicrotingUid = SiteId }.Create(sdkDb);
        await new SdkSite { Name = "Plain Worker", MicrotingUid = plainSiteId }.Create(sdkDb);
        await new SdkSite { Name = "Deleted Off", MicrotingUid = deletedOffSiteId }.Create(sdkDb);
        await new SdkSite { Name = "Deleted On", MicrotingUid = deletedOnSiteId }.Create(sdkDb);
        var dateCell = day.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture);
        IList<IList<object>> sheet = new List<IList<object>>
        {
            new List<object> { "Dato", "Uge", "Ugedag",
                "Pat Tern - timer", "Pat Tern - tekst", "Plain Worker - timer", "Plain Worker - tekst",
                "Deleted Off - timer", "Deleted Off - tekst", "Deleted On - timer", "Deleted On - tekst" },
            new List<object> { dateCell, "", "", "5", "10:00-15:00", "5", "10:00-15:00",
                "5", "10:00-15:00", "5", "10:00-15:00" }
        };

        await GoogleSheetHelper.ApplyPlanTimerSheet(sdkDb, TimePlanningPnDbContext!, sheet, Substitute.For<ILogger>());

        var storedPattern = await ReadRowAsync(TimePlanningPnDbContext!, patternRow.Id);
        var storedPlain = await ReadRowAsync(TimePlanningPnDbContext!, plainRow.Id);
        var storedDeletedOff = await ReadRowAsync(TimePlanningPnDbContext!, deletedOffRow.Id);
        var storedDeletedOn = await ReadRowAsync(TimePlanningPnDbContext!, deletedOnRow.Id);
        Assert.That(storedPattern.PlanText, Is.EqualTo(PatternText), "active pattern: skipped");
        Assert.That(Shift1(storedPattern), Is.EqualTo((8 * 60, 16 * 60, 30, 7.5)));
        Assert.That(storedPlain.PlanText, Is.EqualTo("10:00-15:00"), "never patterned: imported as today");
        Assert.That(storedPlain.PlanHours, Is.EqualTo(5));
        Assert.That(storedDeletedOff.PlanText, Is.EqualTo(PatternText), "deleted pattern, off the sheet: skipped");
        Assert.That(storedDeletedOn.PlanText, Is.EqualTo("10:00-15:00"), "deleted pattern, back on the sheet: imported");
        Assert.That(storedDeletedOn.PlanHours, Is.EqualTo(5));
    }

    // ------------------------------------------------------------------ helpers

    private async Task RunPeriodUpdate(AssignedSiteEntity site, DateTime from, DateTime to)
    {
        var options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        options.Value.Returns(new TimePlanningBaseSettings
        {
            AutoBreakCalculationActive = "0",
            DayOfPayment = 20,
            GpsEnabled = "0",
            SnapshotEnabled = "0"
        });

        var planningsInPeriod = await TimePlanningPnDbContext!.PlanRegistrations
            .AsNoTracking()
            .Where(x => x.SdkSitId == site.SiteId && x.Date >= from && x.Date <= to)
            .OrderBy(x => x.Date)
            .Select(x => new PlanRegistration { Id = x.Id, Date = x.Date })
            .ToListAsync();

        await PlanRegistrationHelper.UpdatePlanRegistrationsInPeriod(
            planningsInPeriod,
            new TimePlanningPlanningModel
            {
                SiteId = site.SiteId,
                SiteName = "Pattern worker",
                PlanningPrDayModels = new List<TimePlanningPlanningPrDayModel>()
            },
            TimePlanningPnDbContext,
            site,
            Substitute.For<ILogger<TimePlanningPlanningService>>(),
            new SdkSite { Name = "Pattern worker", MicrotingUid = site.SiteId },
            from,
            to,
            options);
    }

    private async Task RunSingleUpdate(AssignedSiteEntity site, int rowId)
    {
        var row = await TimePlanningPnDbContext!.PlanRegistrations.AsTracking().SingleAsync(x => x.Id == rowId);
        var timeline = await OneMinuteModeTimeline.BuildAsync(TimePlanningPnDbContext, site);
        var planPattern = await PlanPatternPlanner.LoadActivePatternAsync(TimePlanningPnDbContext, site.Id);
        await PlanRegistrationHelper.UpdatePlanRegistration(
            row, TimePlanningPnDbContext, site, DateTime.Now.AddMonths(-1), timeline, planPattern);
    }

    private async Task RunUpdateAssignedSite(AssignedSiteEntity site, Action<AssignedSiteModel>? configure = null)
    {
        var userService = Substitute.For<IUserService>();
        userService.UserId.Returns(1);
        await SeedAdminCallerAsync(userService);
        var localization = Substitute.For<ITimePlanningLocalizationService>();
        localization.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());
        var options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        options.Value.Returns(new TimePlanningBaseSettings { DayOfPayment = 20, GpsEnabled = "0", SnapshotEnabled = "0" });

        var service = new TimeSettingService(
            options,
            TimePlanningPnDbContext!,
            Substitute.For<ILogger<TimeSettingService>>(),
            userService,
            localization,
            SeededBaseDbContext!,
            Substitute.For<IEFormCoreService>(),
            Substitute.For<IPushNotificationService>());

        // The dialog posts the worker's current values back; the template is
        // what the worker already has.
        AssignedSiteModel model = await TimePlanningPnDbContext!.AssignedSites.AsNoTracking()
            .SingleAsync(x => x.Id == site.Id);
        // The entity-to-model conversion does not carry the plan hours; the
        // dialog does.
        model.MondayPlanHours = site.MondayPlanHours;
        model.TuesdayPlanHours = site.TuesdayPlanHours;
        model.WednesdayPlanHours = site.WednesdayPlanHours;
        model.ThursdayPlanHours = site.ThursdayPlanHours;
        model.FridayPlanHours = site.FridayPlanHours;
        model.SaturdayPlanHours = site.SaturdayPlanHours;
        model.SundayPlanHours = site.SundayPlanHours;
        configure?.Invoke(model);
        var result = await service.UpdateAssignedSite(model);
        Assert.That(result.Success, Is.True, result.Message);
        TimePlanningPnDbContext.ChangeTracker.Clear();
    }
}
