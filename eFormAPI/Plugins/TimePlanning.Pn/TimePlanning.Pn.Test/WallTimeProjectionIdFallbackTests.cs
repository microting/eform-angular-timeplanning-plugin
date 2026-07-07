using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Models.Planning;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Services.TimePlanningPlanningService;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using PlanRegistrationEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration;
using SdkSite = Microting.eForm.Infrastructure.Data.Entities.Site;

namespace TimePlanning.Pn.Test;

/// <summary>
/// WALL-TIME-AT-REST projection contract for <c>UseOneMinuteIntervals=true</c>
/// sites, covering the flag-flip gap: rows written while the flag was FALSE
/// have interval ids (Start1Id=79 ↔ 06:30) but NULL exact-time stamps. The
/// flag-true projection branch currently forwards the stamp verbatim — i.e.
/// null — so the day silently loses its times in the app/web after a
/// false→true flip. Required: fall back to the id-derived wall time
/// (midnight + (id-1)*5 min), exactly what the flag-false branch and
/// EnsureTimestampsFromIds already compute. Both timestamps and ids are wall
/// time, so no timezone math is involved.
///
/// Also locks (green today): stored wall digits are served VERBATIM by both
/// projections — no conversion on the read path, ever.
/// </summary>
[TestFixture]
public class WallTimeProjectionIdFallbackTests : TestBaseSetup
{
    private static readonly DateTime Date = new DateTime(2026, 7, 7, 0, 0, 0);
    private static readonly DateTime WallStart = new DateTime(2026, 7, 7, 6, 30, 0);  // id 79
    private static readonly DateTime WallStop = new DateTime(2026, 7, 7, 16, 0, 0);   // id 193

    private async Task<AssignedSiteEntity> SeedFlagOnSite(int sdkSiteId, DateTime? start1, DateTime? stop1)
    {
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = sdkSiteId,
            UseOneMinuteIntervals = true,
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        await new PlanRegistrationEntity
        {
            SdkSitId = sdkSiteId,
            Date = Date,
            Start1Id = 79,   // ↔ 06:30 wall time
            Stop1Id = 193,   // ↔ 16:00 wall time
            Pause1Id = 0,
            Start1StartedAt = start1,
            Stop1StoppedAt = stop1,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext);

        return assignedSite;
    }

    private async Task<TimePlanningPlanningPrDayModel> ProjectViaUpdatePlanRegistrationsInPeriod(
        int sdkSiteId, AssignedSiteEntity assignedSite)
    {
        var options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        options.Value.Returns(new TimePlanningBaseSettings
        {
            AutoBreakCalculationActive = "0",
            DayOfPayment = 20,
            GpsEnabled = "0",
            SnapshotEnabled = "0"
        });

        var planningsInPeriod = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking()
            .Where(x => x.SdkSitId == sdkSiteId)
            .Select(x => new PlanRegistrationEntity { Id = x.Id, Date = x.Date })
            .ToListAsync();

        var siteModel = new TimePlanningPlanningModel
        {
            SiteId = sdkSiteId,
            SiteName = $"Test site {sdkSiteId}",
            UseOneMinuteIntervals = true,
            PlanningPrDayModels = new List<TimePlanningPlanningPrDayModel>()
        };

        var result = await PlanRegistrationHelper.UpdatePlanRegistrationsInPeriod(
            planningsInPeriod,
            siteModel,
            TimePlanningPnDbContext,
            assignedSite,
            Substitute.For<ILogger<TimePlanningPlanningService>>(),
            new SdkSite { Name = $"Test site {sdkSiteId}", MicrotingUid = sdkSiteId },
            Date.AddDays(-1),
            Date.AddDays(1),
            options);

        return result.PlanningPrDayModels.Single(x => x.Date.Date == Date.Date);
    }

    // ------------------------------------------------------------------
    // Null-stamp fallback (RED pre-fix: projections serve null)
    // ------------------------------------------------------------------

    [Test]
    public async Task UpdatePlanRegistrationsInPeriod_FlagOn_NullStamps_FallBackToIdDerivedWallTime()
    {
        var assignedSite = await SeedFlagOnSite(9824, start1: null, stop1: null);

        var prDay = await ProjectViaUpdatePlanRegistrationsInPeriod(9824, assignedSite);

        Assert.Multiple(() =>
        {
            Assert.That(prDay.Start1StartedAt, Is.EqualTo(WallStart),
                "Flag-true projection must fall back to the id-derived wall time " +
                "(Start1Id=79 → 06:30) when the exact stamp is null, so a false→true " +
                "flag flip is lossless; it currently forwards null");
            Assert.That(prDay.Stop1StoppedAt, Is.EqualTo(WallStop),
                "Stop1Id=193 → 16:00 fallback expected; currently null");
        });
    }

    [Test]
    public async Task ReadBySiteAndDate_FlagOn_NullStamps_FallBackToIdDerivedWallTime()
    {
        await SeedFlagOnSite(9825, start1: null, stop1: null);

        var result = await PlanRegistrationHelper.ReadBySiteAndDate(
            TimePlanningPnDbContext, 9825, Date, null);

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result.Start1StartedAt, Is.EqualTo(WallStart),
                "ReadBySiteAndDate must fall back to id-derived wall time (79 → 06:30) " +
                "for a flag-true site when the stamp is null; it currently forwards null");
            Assert.That(result.Stop1StoppedAt, Is.EqualTo(WallStop),
                "Stop1Id=193 → 16:00 fallback expected; currently null");
        });
    }

    // ------------------------------------------------------------------
    // Verbatim wall digits (GREEN today — locks no-read-conversion)
    // ------------------------------------------------------------------

    [Test]
    public async Task UpdatePlanRegistrationsInPeriod_FlagOn_StoredWallDigits_ServedVerbatim()
    {
        var assignedSite = await SeedFlagOnSite(9826, WallStart, WallStop);

        var prDay = await ProjectViaUpdatePlanRegistrationsInPeriod(9826, assignedSite);

        Assert.Multiple(() =>
        {
            Assert.That(prDay.Start1StartedAt, Is.EqualTo(WallStart),
                "Stored wall digits are the display truth — served verbatim, no conversion");
            Assert.That(prDay.Stop1StoppedAt, Is.EqualTo(WallStop));
        });
    }

    [Test]
    public async Task ReadBySiteAndDate_FlagOn_StoredWallDigits_ServedVerbatim()
    {
        await SeedFlagOnSite(9827, WallStart, WallStop);

        var result = await PlanRegistrationHelper.ReadBySiteAndDate(
            TimePlanningPnDbContext, 9827, Date, null);

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result.Start1StartedAt, Is.EqualTo(WallStart),
                "Stored wall digits are served verbatim — no conversion on the read path");
            Assert.That(result.Stop1StoppedAt, Is.EqualTo(WallStop));
        });
    }
}
