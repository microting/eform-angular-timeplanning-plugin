using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Constants;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;

#nullable enable
namespace TimePlanning.Pn.Test;

/// <summary>
/// Seeds shared by the plan-pattern fixtures (#5489). Not a test class.
///
/// The weekday template and the pattern use deliberately different shifts so
/// every assertion can tell which of the two wrote a row:
/// template = 7:00-15:00 with a 30-minute break (7.5 h),
/// pattern  = <see cref="PatternText"/>, 8:00-16:00 with a 30-minute break (7.5 h),
/// or <see cref="PatternTextShort"/>, 9:00-13:00 with no break (4 h).
/// </summary>
internal static class PlanPatternTestSeed
{
    public const string PatternText = "8:00-16:00/0.5";
    public const string PatternTextShort = "9:00-13:00";

    public const int TemplateStart = 7 * 60;
    public const int TemplateEnd = 15 * 60;
    public const int TemplateBreak = 30;
    public const double TemplateHours = 7.5;

    public static async Task<AssignedSiteEntity> CreateTemplateWorkerAsync(
        TimePlanningPnDbContext db, int siteId, bool useGoogleSheet = false)
    {
        var site = new AssignedSiteEntity
        {
            SiteId = siteId,
            UseGoogleSheetAsDefault = useGoogleSheet,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
            MondayPlanHours = 450, TuesdayPlanHours = 450, WednesdayPlanHours = 450,
            ThursdayPlanHours = 450, FridayPlanHours = 450, SaturdayPlanHours = 450,
            SundayPlanHours = 450,
            StartMonday = TemplateStart, EndMonday = TemplateEnd, BreakMonday = TemplateBreak,
            StartTuesday = TemplateStart, EndTuesday = TemplateEnd, BreakTuesday = TemplateBreak,
            StartWednesday = TemplateStart, EndWednesday = TemplateEnd, BreakWednesday = TemplateBreak,
            StartThursday = TemplateStart, EndThursday = TemplateEnd, BreakThursday = TemplateBreak,
            StartFriday = TemplateStart, EndFriday = TemplateEnd, BreakFriday = TemplateBreak,
            StartSaturday = TemplateStart, EndSaturday = TemplateEnd, BreakSaturday = TemplateBreak,
            StartSunday = TemplateStart, EndSunday = TemplateEnd, BreakSunday = TemplateBreak,
        };
        await site.Create(db);
        return site;
    }

    /// <summary>The Monday on or before <paramref name="date"/>.</summary>
    public static DateTime MondayOnOrBefore(DateTime date) =>
        date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    /// <summary>
    /// Creates an active pattern directly in the database, every day of every
    /// week set to <paramref name="planTextForWeek"/>(weekIndex).
    /// </summary>
    public static async Task<PlanPattern> CreatePatternAsync(
        TimePlanningPnDbContext db, AssignedSiteEntity site, DateTime validFrom,
        int rotationWeeks = 1, Func<int, string>? planTextForWeek = null, DateTime? anchor = null)
    {
        planTextForWeek ??= _ => PatternText;
        var pattern = new PlanPattern
        {
            AssignedSiteId = site.Id,
            RotationWeeks = rotationWeeks,
            AnchorDate = anchor ?? MondayOnOrBefore(DateTime.UtcNow.Date.AddDays(-60)),
            ValidFrom = validFrom.Date,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await pattern.Create(db);
        for (var week = 0; week < Math.Max(rotationWeeks, 1); week++)
        {
            for (var day = 0; day < 7; day++)
            {
                await new PlanPatternDay
                {
                    PlanPatternId = pattern.Id,
                    WeekIndex = week,
                    DayOfWeek = day,
                    PlanText = planTextForWeek(week),
                    CreatedByUserId = 1,
                    UpdatedByUserId = 1
                }.Create(db);
            }
        }

        return pattern;
    }

    public static async Task<PlanRegistration> CreateRowAsync(
        TimePlanningPnDbContext db, int siteId, DateTime date, Action<PlanRegistration>? configure = null)
    {
        var row = new PlanRegistration
        {
            SdkSitId = siteId,
            Date = date.Date,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        configure?.Invoke(row);
        await row.Create(db);
        return row;
    }

    /// <summary>Makes every day up to and including <paramref name="date"/> locked for the worker.</summary>
    public static Task LockThroughAsync(TimePlanningPnDbContext db, int siteId, DateTime date) =>
        CreateRowAsync(db, siteId, date, r =>
        {
            r.Reconciled = true;
            r.ReconciledAt = DateTime.UtcNow;
        });

    public static Task<PlanRegistration> ReadRowAsync(TimePlanningPnDbContext db, int id) =>
        db.PlanRegistrations.AsNoTracking().SingleAsync(x => x.Id == id);

    public static Task<List<PlanRegistration>> LiveRowsAsync(TimePlanningPnDbContext db, int siteId) =>
        db.PlanRegistrations.AsNoTracking()
            .Where(x => x.SdkSitId == siteId && x.WorkflowState != Constants.WorkflowStates.Removed)
            .OrderBy(x => x.Date)
            .ToListAsync();

    public static (int Start, int End, int Break, double Hours) Shift1(PlanRegistration r) =>
        (r.PlannedStartOfShift1, r.PlannedEndOfShift1, r.PlannedBreakOfShift1, r.PlanHours);
}
