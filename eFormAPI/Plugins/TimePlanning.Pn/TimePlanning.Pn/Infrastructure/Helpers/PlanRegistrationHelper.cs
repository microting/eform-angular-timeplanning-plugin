using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eForm.Infrastructure.Models;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using Sentry;
using TimePlanning.Pn.Infrastructure.Models.Holiday;
using TimePlanning.Pn.Infrastructure.Models.Planning;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.Index;
using TimePlanning.Pn.Services.TimePlanningPlanningService;
using AssignedSite = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using Site = Microting.eForm.Infrastructure.Data.Entities.Site;

namespace TimePlanning.Pn.Infrastructure.Helpers;

public static class PlanRegistrationHelper
{
    private static DanishHolidayConfiguration _holidayConfiguration;
    private static readonly object _holidayConfigLock = new object();

    /// <summary>
    /// Loads the Danish holiday configuration from the JSON file.
    /// Caches the result for subsequent calls.
    /// </summary>
    private static DanishHolidayConfiguration LoadHolidayConfiguration()
    {
        if (_holidayConfiguration != null)
        {
            return _holidayConfiguration;
        }

        lock (_holidayConfigLock)
        {
            if (_holidayConfiguration != null)
            {
                return _holidayConfiguration;
            }

            try
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                // First, try to load as embedded resource
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = "TimePlanning.Pn.Resources.danish_holidays_2025_2030.json";

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            var json = reader.ReadToEnd();
                            var config = JsonSerializer.Deserialize<DanishHolidayConfiguration>(json, jsonOptions);
                            _holidayConfiguration = config ?? new DanishHolidayConfiguration
                            {
                                Holidays = new List<HolidayDefinition>()
                            };
                        }
                    }
                    else
                    {
                        // Fallback: try to load from file system
                        var assemblyLocation = assembly.Location;
                        var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
                        var resourcePath = Path.Combine(assemblyDirectory, "Resources", "danish_holidays_2025_2030.json");

                        if (File.Exists(resourcePath))
                        {
                            var json = File.ReadAllText(resourcePath);
                            var config = JsonSerializer.Deserialize<DanishHolidayConfiguration>(json, jsonOptions);
                            _holidayConfiguration = config ?? new DanishHolidayConfiguration
                            {
                                Holidays = new List<HolidayDefinition>()
                            };
                        }
                        else
                        {
                            SentrySdk.CaptureEvent(new SentryEvent
                            {
                                Message = $"Holiday configuration not found as embedded resource or at: {resourcePath}. Using empty holiday configuration as fallback.",
                                Level = SentryLevel.Warning
                            });
                            _holidayConfiguration = new DanishHolidayConfiguration
                            {
                                Holidays = new List<HolidayDefinition>()
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                _holidayConfiguration = new DanishHolidayConfiguration
                {
                    Holidays = new List<HolidayDefinition>()
                };
            }

            return _holidayConfiguration;
        }
    }

    public static PlanRegistration CalculatePauseAutoBreakCalculationActive(
        AssignedSite assignedSite, PlanRegistration planning)
    {
        // Phase 1 note (auto-break, write path):
        // -------------------------------------------------------------------
        // Auto-break pauses are COMPUTED from day-of-week rules (divider /
        // pr-divider / upper-limit), not OBSERVED. There is no real punch
        // marking when the worker started or stopped the pause -- the rule
        // simply says "deduct N minutes of pause for a shift of M minutes
        // worked on this weekday". So even when UseOneMinuteIntervals is
        // on, we do NOT fabricate Pause1StartedAt / Pause1StoppedAt
        // timestamps for an auto-computed pause: there is no real-world
        // signal to anchor them to. The legacy `Pause1Id` int (in 5-minute
        // multiples) stays the source of truth for auto-break, and Phase 2
        // will read it back when computing NettoHours. The DateTime pause
        // fields remain reserved for OBSERVED pauses (worker explicitly
        // started/stopped a break on the kiosk / mobile), which already
        // travel through the `Pause*StartedAt` write path in
        // `UpdateWorkingHour`.
        if (assignedSite.UseOneMinuteIntervals)
        {
            // No-op for auto-break specifically; fall through to the
            // existing 5-minute logic which sets Pause1Id. See note above.
        }
        if (assignedSite.AutoBreakCalculationActive)
        {
            var minutesActualAtWork =
                (planning.Stop1Id - planning.Start1Id
                    + planning.Stop2Id - planning.Start2Id
                    + planning.Stop3Id - planning.Start3Id
                    + planning.Stop4Id - planning.Start4Id
                    + planning.Stop5Id - planning.Start5Id) * 5;
            if (minutesActualAtWork > 0)
            {
                var dayOfWeek = planning.Date.DayOfWeek;
                var breakTime = 0;
                planning.Pause2Id = 0;
                planning.Pause3Id = 0;
                planning.Pause4Id = 0;
                planning.Pause5Id = 0;
                switch (dayOfWeek)
                {
                    case DayOfWeek.Monday:
                    {
                        if (assignedSite.MondayBreakMinutesDivider == 0)
                        {
                            if (!planning.PlanChangedByAdmin)
                            {
                                planning.Pause1Id = 0;
                            }
                            break;
                        }
                        var numberOfBreaks = minutesActualAtWork /
                                             assignedSite.MondayBreakMinutesDivider;
                        breakTime = numberOfBreaks *
                                    assignedSite.MondayBreakMinutesPrDivider;
                        planning.Pause1Id =
                            breakTime < assignedSite.MondayBreakMinutesUpperLimit
                                ? breakTime
                                : assignedSite.MondayBreakMinutesUpperLimit;
                        break;
                    }
                    case DayOfWeek.Tuesday:
                    {
                        if (assignedSite.TuesdayBreakMinutesDivider == 0)
                        {
                            if (!planning.PlanChangedByAdmin)
                            {
                                planning.Pause1Id = 0;
                            }
                            break;
                        }
                        var numberOfBreaks = minutesActualAtWork /
                                             assignedSite.TuesdayBreakMinutesDivider;
                        breakTime = numberOfBreaks *
                                    assignedSite.TuesdayBreakMinutesPrDivider;
                        planning.Pause1Id =
                            breakTime < assignedSite.TuesdayBreakMinutesUpperLimit
                                ? breakTime
                                : assignedSite.TuesdayBreakMinutesUpperLimit;
                        break;
                    }
                    case DayOfWeek.Wednesday:
                    {
                        if (assignedSite.WednesdayBreakMinutesDivider == 0)
                        {
                            if (!planning.PlanChangedByAdmin)
                            {
                                planning.Pause1Id = 0;
                            }
                            break;
                        }
                        var numberOfBreaks = minutesActualAtWork /
                                             assignedSite.WednesdayBreakMinutesDivider;
                        breakTime = numberOfBreaks *
                                    assignedSite.WednesdayBreakMinutesPrDivider;
                        planning.Pause1Id =
                            breakTime < assignedSite.WednesdayBreakMinutesUpperLimit
                                ? breakTime
                                : assignedSite.WednesdayBreakMinutesUpperLimit;
                        break;
                    }
                    case DayOfWeek.Thursday:
                    {
                        if (assignedSite.ThursdayBreakMinutesDivider == 0)
                        {
                            if (!planning.PlanChangedByAdmin)
                            {
                                planning.Pause1Id = 0;
                            }
                            break;
                        }
                        var numberOfBreaks = minutesActualAtWork /
                                             assignedSite.ThursdayBreakMinutesDivider;
                        breakTime = numberOfBreaks *
                                    assignedSite.ThursdayBreakMinutesPrDivider;
                        planning.Pause1Id =
                            breakTime < assignedSite.ThursdayBreakMinutesUpperLimit
                                ? breakTime
                                : assignedSite.ThursdayBreakMinutesUpperLimit;
                        break;
                    }
                    case DayOfWeek.Friday:
                    {
                        if (assignedSite.FridayBreakMinutesDivider == 0)
                        {
                            if (!planning.PlanChangedByAdmin)
                            {
                                planning.Pause1Id = 0;
                            }
                            break;
                        }
                        var numberOfBreaks = minutesActualAtWork /
                                             assignedSite.FridayBreakMinutesDivider;
                        breakTime = numberOfBreaks *
                                    assignedSite.FridayBreakMinutesPrDivider;
                        planning.Pause1Id =
                            breakTime < assignedSite.FridayBreakMinutesUpperLimit
                                ? breakTime
                                : assignedSite.FridayBreakMinutesUpperLimit;
                        break;
                    }
                    case DayOfWeek.Saturday:
                    {
                        if (assignedSite.SaturdayBreakMinutesDivider == 0)
                        {
                            if (!planning.PlanChangedByAdmin)
                            {
                                planning.Pause1Id = 0;
                            }
                            break;
                        }
                        var numberOfBreaks = minutesActualAtWork /
                                             assignedSite.SaturdayBreakMinutesDivider;
                        breakTime = numberOfBreaks *
                                    assignedSite.SaturdayBreakMinutesPrDivider;
                        planning.Pause1Id =
                            breakTime < assignedSite.SaturdayBreakMinutesUpperLimit
                                ? breakTime
                                : assignedSite.SaturdayBreakMinutesUpperLimit;
                        break;
                    }
                    case DayOfWeek.Sunday:
                    {
                        if (assignedSite.SundayBreakMinutesDivider == 0)
                        {
                            if (!planning.PlanChangedByAdmin)
                            {
                                planning.Pause1Id = 0;
                            }
                            break;
                        }
                        var numberOfBreaks = minutesActualAtWork /
                                             assignedSite.SundayBreakMinutesDivider;
                        breakTime = numberOfBreaks *
                                    assignedSite.SundayBreakMinutesPrDivider;
                        planning.Pause1Id =
                            breakTime < assignedSite.SundayBreakMinutesUpperLimit
                                ? breakTime
                                : assignedSite.SundayBreakMinutesUpperLimit;
                        break;
                    }
                }

                if (planning.Pause1Id > 0)
                {
                    planning.Pause1Id = planning.Pause1Id / 5 + 1;
                }
            }
        }

        return planning;
    }

    /// <summary>
    /// Recalculates PlanHours and PlanHoursInSeconds from the five planned
    /// shift slots.  This must be called after MoveShift / MoveContent so
    /// that the totals on both the source and target PlanRegistrations stay
    /// consistent.
    ///
    /// This method is only called post-handover, when shift data has been
    /// physically relocated between PlanRegistrations. The UseOnlyPlanHours
    /// guard is intentionally omitted because the manually-set PlanHours
    /// value would be stale after shifts have been moved. Likewise the
    /// MessageId guard is omitted for the same reason -- the old MessageId
    /// no longer reflects the new shift layout.
    /// </summary>
    public static void RecalculatePlanHoursFromShifts(PlanRegistration pr)
    {
        // Existing 5-min path (planned shifts stay minute-precision per plan)
        var totalMinutes = 0;

        if (pr.PlannedStartOfShift1 != 0 && pr.PlannedEndOfShift1 != 0)
            totalMinutes += pr.PlannedEndOfShift1 - pr.PlannedStartOfShift1 - pr.PlannedBreakOfShift1;

        if (pr.PlannedStartOfShift2 != 0 && pr.PlannedEndOfShift2 != 0)
            totalMinutes += pr.PlannedEndOfShift2 - pr.PlannedStartOfShift2 - pr.PlannedBreakOfShift2;

        if (pr.PlannedStartOfShift3 != 0 && pr.PlannedEndOfShift3 != 0)
            totalMinutes += pr.PlannedEndOfShift3 - pr.PlannedStartOfShift3 - pr.PlannedBreakOfShift3;

        if (pr.PlannedStartOfShift4 != 0 && pr.PlannedEndOfShift4 != 0)
            totalMinutes += pr.PlannedEndOfShift4 - pr.PlannedStartOfShift4 - pr.PlannedBreakOfShift4;

        if (pr.PlannedStartOfShift5 != 0 && pr.PlannedEndOfShift5 != 0)
            totalMinutes += pr.PlannedEndOfShift5 - pr.PlannedStartOfShift5 - pr.PlannedBreakOfShift5;

        pr.PlanHours = totalMinutes / 60.0;
        pr.PlanHoursInSeconds = totalMinutes * 60;
    }

    /// <summary>
    /// Phase 0 plumbing overload threading the UseOneMinuteIntervals flag.
    /// Per the rollout plan, planned-shift precision stays minute-only, so
    /// this overload simply delegates to the existing 1-arg method regardless
    /// of the flag. The parameter is kept for symmetry with other helpers and
    /// to future-proof if planned-shift precision ever changes.
    /// </summary>
    public static void RecalculatePlanHoursFromShifts(PlanRegistration pr, bool useOneMinuteIntervals)
    {
        if (useOneMinuteIntervals)
        {
            // TODO Phase 1+2 (if ever needed): planned-shift precision is currently
            // out of scope; fall through to the existing minute-precision path.
        }
        RecalculatePlanHoursFromShifts(pr);
    }

    /// <summary>
    /// Phase 2 — second-precision NettoHours computation.
    ///
    /// When <see cref="AssignedSite.UseOneMinuteIntervals"/> is on, this helper
    /// computes NettoHours from DateTime deltas (precise to the second) instead
    /// of the legacy <c>(StopId - StartId - (PauseId-1)) * 5</c> minute-tick math
    /// in the per-call sites. Mirrors the flag-off formula in seconds:
    ///
    /// <code>
    /// nettoSeconds = 0
    /// for each shift n in 1..5:
    ///     if (Start_n_StartedAt and Stop_n_StoppedAt are populated):
    ///         nettoSeconds += (Stop_n_StoppedAt - Start_n_StartedAt).TotalSeconds
    ///         if (Pause_n_StartedAt and Pause_n_StoppedAt are populated):
    ///             nettoSeconds -= (Pause_n_StoppedAt - Pause_n_StartedAt).TotalSeconds
    ///         else if (Pause_n_Id > 0):
    ///             nettoSeconds -= (Pause_n_Id - 1) * 5 * 60
    ///     else if (Stop_n_Id &gt;= Start_n_Id and Stop_n_Id != 0):
    ///         // legacy fallback for shifts that don't have DateTime stamps
    ///         nettoSeconds += (Stop_n_Id - Start_n_Id) * 5 * 60
    ///         nettoSeconds -= (Pause_n_Id &gt; 0 ? Pause_n_Id - 1 : 0) * 5 * 60
    /// </code>
    ///
    /// Returns the computed netto seconds. The caller writes both the
    /// <c>*InSeconds</c> primary and back-derives the legacy <c>double</c>
    /// hour field (<c>x = xInSeconds / 3600.0</c>) for read compatibility.
    /// </summary>
    public static long ComputeNettoSecondsFromDateTimeShifts(PlanRegistration pr)
    {
        long nettoSeconds = 0;

        // Helper: compute one shift's contribution. Prefer DateTime delta when
        // both stamps are populated; otherwise fall back to the legacy 5-min
        // tick math so mixed-precision rows (some shifts precise, some not)
        // still get a complete total. Pause is the canonical per-shift total
        // (ALL slots, not just the primary) — second-precision because this
        // method only runs on UseOneMinuteIntervals sites.
        long ShiftSeconds(int shift, DateTime? startAt, DateTime? stopAt, int startId, int stopId)
        {
            long workSeconds;
            if (startAt.HasValue && stopAt.HasValue && stopAt.Value > startAt.Value)
            {
                workSeconds = (long)(stopAt.Value - startAt.Value).TotalSeconds;
            }
            else if (stopId >= startId && stopId != 0)
            {
                workSeconds = (long)(stopId - startId) * 5 * 60;
            }
            else
            {
                return 0;
            }

            long pauseSeconds = ComputeShiftPauseSeconds(pr, shift, useOneMinuteIntervals: true);

            return workSeconds - pauseSeconds;
        }

        nettoSeconds += ShiftSeconds(1, pr.Start1StartedAt, pr.Stop1StoppedAt, pr.Start1Id, pr.Stop1Id);
        nettoSeconds += ShiftSeconds(2, pr.Start2StartedAt, pr.Stop2StoppedAt, pr.Start2Id, pr.Stop2Id);
        nettoSeconds += ShiftSeconds(3, pr.Start3StartedAt, pr.Stop3StoppedAt, pr.Start3Id, pr.Stop3Id);
        nettoSeconds += ShiftSeconds(4, pr.Start4StartedAt, pr.Stop4StoppedAt, pr.Start4Id, pr.Stop4Id);
        nettoSeconds += ShiftSeconds(5, pr.Start5StartedAt, pr.Stop5StoppedAt, pr.Start5Id, pr.Stop5Id);

        return Math.Max(0, nettoSeconds);
    }

    /// <summary>
    /// Aggregates total pause minutes for a PlanRegistration by summing the
    /// canonical per-shift pause (<see cref="ComputeShiftPauseSeconds"/>) across
    /// shifts 1-5 and rounding the total down to whole minutes.
    ///
    /// ComputeShiftPauseSeconds is the single source of truth: per shift it walks
    /// every populated pause slot (primary Pause{N} plus the multi-pause sub-slots)
    /// and applies the exact stamp delta when useOneMinuteIntervals is true, or the
    /// floor-to-5-minute clock-tick delta when it is false, falling back per shift to
    /// the legacy Pause{N}Id tick value only when that shift has no timestamped slots.
    /// </summary>
    public static int AggregatePauseMinutes(PlanRegistration pr, bool useOneMinuteIntervals)
    {
        // Sum the canonical per-shift pause across all 5 shifts. The canonical
        // method walks EVERY populated slot of each shift (primary + sub-slots),
        // applies the exact delta (flag on) or the floor-to-5-minute clock-tick
        // delta (flag off), and falls back per-shift to the legacy Pause{N}Id
        // tick value only when that shift has no timestamped slots.
        long totalSeconds = 0;
        for (var shift = 1; shift <= 5; shift++)
        {
            totalSeconds += ComputeShiftPauseSeconds(pr, shift, useOneMinuteIntervals);
        }

        return (int)(totalSeconds / 60); // round down to whole minutes
    }

    /// <summary>
    /// Phase 2 — write the second-precision NettoHours / Flex / SumFlex chain.
    ///
    /// Computes <c>NettoHoursInSeconds</c> from DateTime deltas (or legacy
    /// fallback) via <see cref="ComputeNettoSecondsFromDateTimeShifts"/>,
    /// derives <c>FlexInSeconds</c> from <c>PlanHoursInSeconds</c>, then
    /// derives <c>SumFlexEndInSeconds</c> from the running balance plus the
    /// computed flex minus paid-out flex. Back-derives the legacy
    /// <c>double</c> hour fields (<c>x = xInSeconds / 3600.0</c>) so existing
    /// read paths stay compatible.
    ///
    /// Mirrors the existing flag-off formula sign-for-sign:
    ///   Flex            = NettoHours - PlanHours          (or override)
    ///   SumFlexEnd      = SumFlexStart + NettoHours - PlanHours - PaiedOutFlex
    ///                       (when preTimePlanning exists)
    ///   SumFlexEnd      = NettoHours - PlanHours - PaiedOutFlex
    ///                       (when no preTimePlanning, SumFlexStart = 0)
    /// — but every operand is in seconds, so no precision is lost on the
    /// way through the int columns.
    ///
    /// Caller passes <paramref name="sumFlexStartInSeconds"/> from the previous
    /// day's <c>SumFlexEndInSeconds</c> (or 0 when there is no preceding row).
    /// When the override is active, the override (in hours) is converted to
    /// seconds via <c>* 3600</c> for the chain.
    /// </summary>
    /// <param name="pr">The plan registration to update in place.</param>
    /// <param name="sumFlexStartInSeconds">
    /// Running flex balance carried in from the previous day's
    /// <c>SumFlexEndInSeconds</c>; pass 0 when there is no preceding row.
    /// </param>
    /// <param name="hasPreTimePlanning">
    /// True when there is a preceding planning row (use the running balance);
    /// false when this is the first row (reset SumFlexStart to 0).
    /// </param>
    public static void ApplyNettoFlexChainSecondPrecision(PlanRegistration pr,
        int sumFlexStartInSeconds, bool hasPreTimePlanning)
    {
        var nettoSeconds = ComputeNettoSecondsFromDateTimeShifts(pr);
        pr.NettoHoursInSeconds = (int)nettoSeconds;
        pr.NettoHours = nettoSeconds / 3600.0;

        // Punch-clock / scheduled days populate the double PlanHours but leave
        // PlanHoursInSeconds at 0. Fall back to PlanHours * 3600 so flex is
        // computed against the real plan instead of treating it as 0.
        var planHoursSeconds = pr.PlanHoursInSeconds != 0
            ? pr.PlanHoursInSeconds
            : (int)Math.Round(pr.PlanHours * 3600);
        // Production writers populate only the double PaiedOutFlex and leave
        // PaiedOutFlexInSeconds at 0. Fall back to PaiedOutFlex * 3600 so a
        // paid-out flex is subtracted instead of being treated as 0.
        var paiedOutFlexSeconds = pr.PaiedOutFlexInSeconds != 0
            ? pr.PaiedOutFlexInSeconds
            : (int)Math.Round(pr.PaiedOutFlex * 3600);

        // Mirror the flag-off override semantics:
        //   Flex      = (override ? NettoHoursOverride : NettoHours) - PlanHours
        //   SumFlexEnd uses the same numerator.
        var effectiveNettoSecondsForFlex = pr.NettoHoursOverrideActive
            ? (long)(pr.NettoHoursOverride * 3600)
            : nettoSeconds;

        var flexSeconds = effectiveNettoSecondsForFlex - planHoursSeconds;
        pr.FlexInSeconds = (int)flexSeconds;
        pr.Flex = flexSeconds / 3600.0;

        if (hasPreTimePlanning)
        {
            pr.SumFlexStartInSeconds = sumFlexStartInSeconds;
            pr.SumFlexStart = sumFlexStartInSeconds / 3600.0;
            var sumFlexEndSeconds = (long)sumFlexStartInSeconds
                                    + effectiveNettoSecondsForFlex - planHoursSeconds
                                    - paiedOutFlexSeconds;
            pr.SumFlexEndInSeconds = (int)sumFlexEndSeconds;
            pr.SumFlexEnd = sumFlexEndSeconds / 3600.0;
        }
        else
        {
            pr.SumFlexStartInSeconds = 0;
            pr.SumFlexStart = 0;
            var sumFlexEndSeconds = effectiveNettoSecondsForFlex - planHoursSeconds - paiedOutFlexSeconds;
            pr.SumFlexEndInSeconds = (int)sumFlexEndSeconds;
            pr.SumFlexEnd = sumFlexEndSeconds / 3600.0;
        }
    }

    public static async Task<TimePlanningPlanningModel> UpdatePlanRegistrationsInPeriod(
        List<PlanRegistration> planningsInPeriod,
        TimePlanningPlanningModel siteModel,
        TimePlanningPnDbContext dbContext,
        AssignedSite dbAssignedSite,
        ILogger<TimePlanningPlanningService> logger,
        Site site,
        DateTime midnightOfDateFrom,
        DateTime midnightOfDateTo,
        IPluginDbOptions<TimePlanningBaseSettings> options,
        string? messageLanguage = null
        )
    {
        var tainted = false;
        var settingsDayOfPayment = options.Value.DayOfPayment == 0 ? 20 : options.Value.DayOfPayment;
        // Load the message catalog once (no N+1) so each day can resolve its
        // localized label without re-querying per row.
        var messagesById = await dbContext.Messages.AsNoTracking().ToDictionaryAsync(m => m.Id);
        // Stage 3 tick-exact parity: resolve the UseOneMinuteIntervals mode that
        // was in force when each row was REGISTERED (from AssignedSiteVersions —
        // one query, in-memory lookups) so the Start/Stop display projection
        // below renders tick rows from ids and one-minute rows from stamps,
        // regardless of the site's CURRENT flag. Write/calc forks in this method
        // intentionally keep using dbAssignedSite.UseOneMinuteIntervals.
        var oneMinuteTimeline = await OneMinuteModeTimeline.BuildAsync(dbContext, dbAssignedSite);
        var toDay = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0);
        // var dayOfPayment = toDay.Day >= settingsDayOfPayment
        //     ? new DateTime(DateTime.Now.Year, DateTime.Now.Month, settingsDayOfPayment, 0, 0, 0)
        //     : new DateTime(DateTime.Now.Year, DateTime.Now.Month - 1, settingsDayOfPayment, 0, 0, 0);
        var dayOfPayment = toDay.AddMonths(-1);
        foreach (var plan in planningsInPeriod)
        {
            var planRegistration = await dbContext.PlanRegistrations.AsTracking().FirstAsync(x => x.Id == plan.Id);
            var midnight = new DateTime(planRegistration.Date.Year, planRegistration.Date.Month,
                planRegistration.Date.Day, 0, 0, 0);
            // Mode at registration (see timeline build above) — display-only.
            var rowIsOneMinute = oneMinuteTimeline.WasOneMinuteAt(planRegistration.Date);

            if (planRegistration.Start1Id > 289)
            {
                // FIXME: This is a workaround, it should be removed when the frontend is fixed.
                planRegistration.Start1Id /= 5 + 1;
                // Phase 1: when UseOneMinuteIntervals is on AND a precise stamp is
                // already populated, preserve it instead of snapping back to the
                // 5-minute index. Existing flag-off behavior is byte-identical:
                // the int Id is corrected and StartedAt is backfilled from it.
                // When the flag is on but StartedAt is null, fall through to the
                // backfill so legacy rows without precise stamps still get one.
                if (dbAssignedSite.UseOneMinuteIntervals && planRegistration.Start1StartedAt.HasValue)
                {
                    // Phase 1: precise DateTime stamp wins; do NOT overwrite it
                    // with the 5-minute snap derived from Start1Id.
                }
                else
                {
                    planRegistration.Start1StartedAt = planRegistration.Date.AddMinutes(planRegistration.Start1Id * 5);
                }
            }

            if (planRegistration.Stop1Id > 289 )
            {
                // FIXME: This is a workaround, it should be removed when the frontend is fixed.
                planRegistration.Stop1Id /= 5 + 1;
                // Phase 1: same fork as Start1 above for the stop stamp.
                if (dbAssignedSite.UseOneMinuteIntervals && planRegistration.Stop1StoppedAt.HasValue)
                {
                    // Phase 1: precise DateTime stamp wins; do NOT overwrite it
                    // with the 5-minute snap derived from Stop1Id.
                }
                else
                {
                    planRegistration.Stop1StoppedAt = planRegistration.Date.AddMinutes(planRegistration.Stop1Id * 5);
                }
            }
            planRegistration.IsSaturday = midnight.DayOfWeek == DayOfWeek.Saturday;
            planRegistration.IsSunday = midnight.DayOfWeek == DayOfWeek.Sunday;
            await planRegistration.Update(dbContext).ConfigureAwait(false);

            if (!dbAssignedSite.Resigned)
            {
                try
                {
                    if (dbAssignedSite.UseGoogleSheetAsDefault)
                    {
                        if (planRegistration.Date > dayOfPayment && !planRegistration.PlanChangedByAdmin)
                        {
                            if (!string.IsNullOrEmpty(planRegistration.PlanText))
                            {
                                var originalPlanHours = planRegistration.PlanHours;
                                PlanTextHelper.ParsePlanText(planRegistration);

                                if (originalPlanHours != planRegistration.PlanHours || tainted)
                                {
                                    SentrySdk.CaptureEvent(
                                        new SentryEvent
                                        {
                                            Message = $"PlanRegistrationHelper.UpdatePlanRegistrationsInPeriod: " +
                                                      $"Plan hours changed from {originalPlanHours} to {planRegistration.PlanHours} " +
                                                      $"for plan registration with ID {planRegistration.Id} and date {planRegistration.Date}",
                                            Level = SentryLevel.Warning
                                        });
                                    tainted = true;

                                }
                            }
                            else
                            {
                                planRegistration.PlannedStartOfShift1 = 0;
                                planRegistration.PlannedEndOfShift1 = 0;
                                planRegistration.PlannedBreakOfShift1 = 0;
                                planRegistration.PlannedStartOfShift2 = 0;
                                planRegistration.PlannedEndOfShift2 = 0;
                                planRegistration.PlannedBreakOfShift2 = 0;
                                planRegistration.PlannedStartOfShift3 = 0;
                                planRegistration.PlannedEndOfShift3 = 0;
                                planRegistration.PlannedBreakOfShift3 = 0;
                                planRegistration.PlannedStartOfShift4 = 0;
                                planRegistration.PlannedEndOfShift4 = 0;
                                planRegistration.PlannedBreakOfShift4 = 0;
                                planRegistration.PlannedStartOfShift5 = 0;
                                planRegistration.PlannedEndOfShift5 = 0;
                                planRegistration.PlannedBreakOfShift5 = 0;
                            }
                        }

                        var preTimePlanning =
                            await dbContext.PlanRegistrations.AsNoTracking()
                                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                                .Where(x => x.Date < planRegistration.Date
                                            && x.SdkSitId == dbAssignedSite.SiteId)
                                .OrderByDescending(x => x.Date)
                                .FirstOrDefaultAsync();

                        // Phase 2: when UseOneMinuteIntervals is on, run the
                        // SumFlex chain in seconds (source of truth) and
                        // back-derive doubles. Flag-off path stays byte-identical.
                        if (dbAssignedSite.UseOneMinuteIntervals)
                        {
                            ApplyNettoFlexChainSecondPrecision(
                                planRegistration,
                                preTimePlanning?.SumFlexEndInSeconds ?? 0,
                                preTimePlanning != null);
                        }
                        else if (preTimePlanning != null)
                        {
                            planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                            if (planRegistration.NettoHoursOverrideActive)
                            {
                                planRegistration.SumFlexEnd =
                                    preTimePlanning.SumFlexEnd + planRegistration.NettoHoursOverride -
                                    planRegistration.PlanHours -
                                    planRegistration.PaiedOutFlex;
                                planRegistration.Flex =
                                    planRegistration.NettoHoursOverride - planRegistration.PlanHours;
                            }
                            else
                            {
                                planRegistration.SumFlexEnd =
                                    preTimePlanning.SumFlexEnd + planRegistration.NettoHours -
                                    planRegistration.PlanHours -
                                    planRegistration.PaiedOutFlex;
                                planRegistration.Flex = planRegistration.NettoHours - planRegistration.PlanHours;
                            }
                        }
                        else
                        {
                            if (planRegistration.NettoHoursOverrideActive)
                            {
                                planRegistration.SumFlexEnd =
                                    planRegistration.NettoHoursOverride - planRegistration.PlanHours -
                                    planRegistration.PaiedOutFlex;
                                planRegistration.SumFlexStart = 0;
                                planRegistration.Flex =
                                    planRegistration.NettoHoursOverride - planRegistration.PlanHours;
                            }
                            else
                            {
                                planRegistration.SumFlexEnd =
                                    planRegistration.NettoHours - planRegistration.PlanHours -
                                    planRegistration.PaiedOutFlex;
                                planRegistration.SumFlexStart = 0;
                                planRegistration.Flex = planRegistration.NettoHours - planRegistration.PlanHours;
                            }
                        }

                        await planRegistration.Update(dbContext).ConfigureAwait(false);
                    }
                    else
                    {
                        if (planRegistration.Date > dayOfPayment && !planRegistration.PlanChangedByAdmin)
                        {
                            var dayOfWeek = planRegistration.Date.DayOfWeek;
                            var originalPlanHours = planRegistration.PlanHours;
                            switch (dayOfWeek)
                            {
                                case DayOfWeek.Monday:
                                    planRegistration.PlanHours = dbAssignedSite.MondayPlanHours != 0
                                        ? (double)dbAssignedSite.MondayPlanHours / 60
                                        : 0;
                                    if (!dbAssignedSite.UseOnlyPlanHours)
                                    {
                                        planRegistration.PlannedStartOfShift1 = dbAssignedSite.StartMonday ?? 0;
                                        planRegistration.PlannedEndOfShift1 = dbAssignedSite.EndMonday ?? 0;
                                        planRegistration.PlannedBreakOfShift1 = dbAssignedSite.BreakMonday ?? 0;
                                        planRegistration.PlannedStartOfShift2 =
                                            dbAssignedSite.StartMonday2NdShift ?? 0;
                                        planRegistration.PlannedEndOfShift2 = dbAssignedSite.EndMonday2NdShift ?? 0;
                                        planRegistration.PlannedBreakOfShift2 =
                                            dbAssignedSite.BreakMonday2NdShift ?? 0;
                                        planRegistration.PlannedStartOfShift3 =
                                            dbAssignedSite.StartMonday3RdShift ?? 0;
                                        planRegistration.PlannedEndOfShift3 = dbAssignedSite.EndMonday3RdShift ?? 0;
                                        planRegistration.PlannedBreakOfShift3 =
                                            dbAssignedSite.BreakMonday3RdShift ?? 0;
                                        planRegistration.PlannedStartOfShift4 =
                                            dbAssignedSite.StartMonday4ThShift ?? 0;
                                        planRegistration.PlannedEndOfShift4 = dbAssignedSite.EndMonday4ThShift ?? 0;
                                        planRegistration.PlannedBreakOfShift4 =
                                            dbAssignedSite.BreakMonday4ThShift ?? 0;
                                        planRegistration.PlannedStartOfShift5 =
                                            dbAssignedSite.StartMonday5ThShift ?? 0;
                                        planRegistration.PlannedEndOfShift5 = dbAssignedSite.EndMonday5ThShift ?? 0;
                                        planRegistration.PlannedBreakOfShift5 =
                                            dbAssignedSite.BreakMonday5ThShift ?? 0;
                                    }

                                    break;
                                case DayOfWeek.Tuesday:
                                    planRegistration.PlanHours = dbAssignedSite.TuesdayPlanHours != 0
                                        ? (double)dbAssignedSite.TuesdayPlanHours / 60
                                        : 0;
                                    if (!dbAssignedSite.UseOnlyPlanHours)
                                    {
                                        planRegistration.PlannedStartOfShift1 = dbAssignedSite.StartTuesday ?? 0;
                                        planRegistration.PlannedEndOfShift1 = dbAssignedSite.EndTuesday ?? 0;
                                        planRegistration.PlannedBreakOfShift1 = dbAssignedSite.BreakTuesday ?? 0;
                                        planRegistration.PlannedStartOfShift2 =
                                            dbAssignedSite.StartTuesday2NdShift ?? 0;
                                        planRegistration.PlannedEndOfShift2 =
                                            dbAssignedSite.EndTuesday2NdShift ?? 0;
                                        planRegistration.PlannedBreakOfShift2 =
                                            dbAssignedSite.BreakTuesday2NdShift ?? 0;
                                        planRegistration.PlannedStartOfShift3 =
                                            dbAssignedSite.StartTuesday3RdShift ?? 0;
                                        planRegistration.PlannedEndOfShift3 =
                                            dbAssignedSite.EndTuesday3RdShift ?? 0;
                                        planRegistration.PlannedBreakOfShift3 =
                                            dbAssignedSite.BreakTuesday3RdShift ?? 0;
                                        planRegistration.PlannedStartOfShift4 =
                                            dbAssignedSite.StartTuesday4ThShift ?? 0;
                                        planRegistration.PlannedEndOfShift4 =
                                            dbAssignedSite.EndTuesday4ThShift ?? 0;
                                        planRegistration.PlannedBreakOfShift4 =
                                            dbAssignedSite.BreakTuesday4ThShift ?? 0;
                                        planRegistration.PlannedStartOfShift5 =
                                            dbAssignedSite.StartTuesday5ThShift ?? 0;
                                        planRegistration.PlannedEndOfShift5 =
                                            dbAssignedSite.EndTuesday5ThShift ?? 0;
                                        planRegistration.PlannedBreakOfShift5 =
                                            dbAssignedSite.BreakTuesday5ThShift ?? 0;
                                    }

                                    break;
                                case DayOfWeek.Wednesday:
                                    planRegistration.PlanHours = dbAssignedSite.WednesdayPlanHours != 0
                                        ? (double)dbAssignedSite.WednesdayPlanHours / 60
                                        : 0;
                                    if (!dbAssignedSite.UseOnlyPlanHours)
                                    {
                                        planRegistration.PlannedStartOfShift1 = dbAssignedSite.StartWednesday ?? 0;
                                        planRegistration.PlannedEndOfShift1 = dbAssignedSite.EndWednesday ?? 0;
                                        planRegistration.PlannedBreakOfShift1 = dbAssignedSite.BreakWednesday ?? 0;
                                        planRegistration.PlannedStartOfShift2 =
                                            dbAssignedSite.StartWednesday2NdShift ?? 0;
                                        planRegistration.PlannedEndOfShift2 =
                                            dbAssignedSite.EndWednesday2NdShift ?? 0;
                                        planRegistration.PlannedBreakOfShift2 =
                                            dbAssignedSite.BreakWednesday2NdShift ?? 0;
                                        planRegistration.PlannedStartOfShift3 =
                                            dbAssignedSite.StartWednesday3RdShift ?? 0;
                                        planRegistration.PlannedEndOfShift3 =
                                            dbAssignedSite.EndWednesday3RdShift ?? 0;
                                        planRegistration.PlannedBreakOfShift3 =
                                            dbAssignedSite.BreakWednesday3RdShift ?? 0;
                                        planRegistration.PlannedStartOfShift4 =
                                            dbAssignedSite.StartWednesday4ThShift ?? 0;
                                        planRegistration.PlannedEndOfShift4 =
                                            dbAssignedSite.EndWednesday4ThShift ?? 0;
                                        planRegistration.PlannedBreakOfShift4 =
                                            dbAssignedSite.BreakWednesday4ThShift ?? 0;
                                        planRegistration.PlannedStartOfShift5 =
                                            dbAssignedSite.StartWednesday5ThShift ?? 0;
                                        planRegistration.PlannedEndOfShift5 =
                                            dbAssignedSite.EndWednesday5ThShift ?? 0;
                                        planRegistration.PlannedBreakOfShift5 =
                                            dbAssignedSite.BreakWednesday5ThShift ?? 0;
                                    }

                                    break;
                                case DayOfWeek.Thursday:
                                    planRegistration.PlanHours = dbAssignedSite.ThursdayPlanHours != 0
                                        ? (double)dbAssignedSite.ThursdayPlanHours / 60
                                        : 0;
                                    if (!dbAssignedSite.UseOnlyPlanHours)
                                    {
                                        planRegistration.PlannedStartOfShift1 = dbAssignedSite.StartThursday ?? 0;
                                        planRegistration.PlannedEndOfShift1 = dbAssignedSite.EndThursday ?? 0;
                                        planRegistration.PlannedBreakOfShift1 = dbAssignedSite.BreakThursday ?? 0;
                                        planRegistration.PlannedStartOfShift2 =
                                            dbAssignedSite.StartThursday2NdShift ?? 0;
                                        planRegistration.PlannedEndOfShift2 =
                                            dbAssignedSite.EndThursday2NdShift ?? 0;
                                        planRegistration.PlannedBreakOfShift2 =
                                            dbAssignedSite.BreakThursday2NdShift ?? 0;
                                        planRegistration.PlannedStartOfShift3 =
                                            dbAssignedSite.StartThursday3RdShift ?? 0;
                                        planRegistration.PlannedEndOfShift3 =
                                            dbAssignedSite.EndThursday3RdShift ?? 0;
                                        planRegistration.PlannedBreakOfShift3 =
                                            dbAssignedSite.BreakThursday3RdShift ?? 0;
                                        planRegistration.PlannedStartOfShift4 =
                                            dbAssignedSite.StartThursday4ThShift ?? 0;
                                        planRegistration.PlannedEndOfShift4 =
                                            dbAssignedSite.EndThursday4ThShift ?? 0;
                                        planRegistration.PlannedBreakOfShift4 =
                                            dbAssignedSite.BreakThursday4ThShift ?? 0;
                                        planRegistration.PlannedStartOfShift5 =
                                            dbAssignedSite.StartThursday5ThShift ?? 0;
                                        planRegistration.PlannedEndOfShift5 =
                                            dbAssignedSite.EndThursday5ThShift ?? 0;
                                        planRegistration.PlannedBreakOfShift5 =
                                            dbAssignedSite.BreakThursday5ThShift ?? 0;
                                    }

                                    break;
                                case DayOfWeek.Friday:
                                    planRegistration.PlanHours = dbAssignedSite.FridayPlanHours != 0
                                        ? (double)dbAssignedSite.FridayPlanHours / 60
                                        : 0;
                                    if (!dbAssignedSite.UseOnlyPlanHours)
                                    {
                                        planRegistration.PlannedStartOfShift1 = dbAssignedSite.StartFriday ?? 0;
                                        planRegistration.PlannedEndOfShift1 = dbAssignedSite.EndFriday ?? 0;
                                        planRegistration.PlannedBreakOfShift1 = dbAssignedSite.BreakFriday ?? 0;
                                        planRegistration.PlannedStartOfShift2 =
                                            dbAssignedSite.StartFriday2NdShift ?? 0;
                                        planRegistration.PlannedEndOfShift2 = dbAssignedSite.EndFriday2NdShift ?? 0;
                                        planRegistration.PlannedBreakOfShift2 =
                                            dbAssignedSite.BreakFriday2NdShift ?? 0;
                                        planRegistration.PlannedStartOfShift3 =
                                            dbAssignedSite.StartFriday3RdShift ?? 0;
                                        planRegistration.PlannedEndOfShift3 = dbAssignedSite.EndFriday3RdShift ?? 0;
                                        planRegistration.PlannedBreakOfShift3 =
                                            dbAssignedSite.BreakFriday3RdShift ?? 0;
                                        planRegistration.PlannedStartOfShift4 =
                                            dbAssignedSite.StartFriday4ThShift ?? 0;
                                        planRegistration.PlannedEndOfShift4 = dbAssignedSite.EndFriday4ThShift ?? 0;
                                        planRegistration.PlannedBreakOfShift4 =
                                            dbAssignedSite.BreakFriday4ThShift ?? 0;
                                        planRegistration.PlannedStartOfShift5 =
                                            dbAssignedSite.StartFriday5ThShift ?? 0;
                                        planRegistration.PlannedEndOfShift5 = dbAssignedSite.EndFriday5ThShift ?? 0;
                                        planRegistration.PlannedBreakOfShift5 =
                                            dbAssignedSite.BreakFriday5ThShift ?? 0;
                                    }

                                    break;
                                case DayOfWeek.Saturday:
                                    planRegistration.PlanHours = dbAssignedSite.SaturdayPlanHours != 0
                                        ? (double)dbAssignedSite.SaturdayPlanHours / 60
                                        : 0;
                                    if (!dbAssignedSite.UseOnlyPlanHours)
                                    {
                                        planRegistration.PlannedStartOfShift1 = dbAssignedSite.StartSaturday ?? 0;
                                        planRegistration.PlannedEndOfShift1 = dbAssignedSite.EndSaturday ?? 0;
                                        planRegistration.PlannedBreakOfShift1 = dbAssignedSite.BreakSaturday ?? 0;
                                        planRegistration.PlannedStartOfShift2 =
                                            dbAssignedSite.StartSaturday2NdShift ?? 0;
                                        planRegistration.PlannedEndOfShift2 =
                                            dbAssignedSite.EndSaturday2NdShift ?? 0;
                                        planRegistration.PlannedBreakOfShift2 =
                                            dbAssignedSite.BreakSaturday2NdShift ?? 0;
                                        planRegistration.PlannedStartOfShift3 =
                                            dbAssignedSite.StartSaturday3RdShift ?? 0;
                                        planRegistration.PlannedEndOfShift3 =
                                            dbAssignedSite.EndSaturday3RdShift ?? 0;
                                        planRegistration.PlannedBreakOfShift3 =
                                            dbAssignedSite.BreakSaturday3RdShift ?? 0;
                                        planRegistration.PlannedStartOfShift4 =
                                            dbAssignedSite.StartSaturday4ThShift ?? 0;
                                        planRegistration.PlannedEndOfShift4 =
                                            dbAssignedSite.EndSaturday4ThShift ?? 0;
                                        planRegistration.PlannedBreakOfShift4 =
                                            dbAssignedSite.BreakSaturday4ThShift ?? 0;
                                        planRegistration.PlannedStartOfShift5 =
                                            dbAssignedSite.StartSaturday5ThShift ?? 0;
                                        planRegistration.PlannedEndOfShift5 =
                                            dbAssignedSite.EndSaturday5ThShift ?? 0;
                                        planRegistration.PlannedBreakOfShift5 =
                                            dbAssignedSite.BreakSaturday5ThShift ?? 0;
                                    }

                                    break;
                                case DayOfWeek.Sunday:
                                    planRegistration.PlanHours = dbAssignedSite.SundayPlanHours != 0
                                        ? (double)dbAssignedSite.SundayPlanHours / 60
                                        : 0;
                                    if (!dbAssignedSite.UseOnlyPlanHours)
                                    {
                                        planRegistration.PlannedStartOfShift1 = dbAssignedSite.StartSunday ?? 0;
                                        planRegistration.PlannedEndOfShift1 = dbAssignedSite.EndSunday ?? 0;
                                        planRegistration.PlannedBreakOfShift1 = dbAssignedSite.BreakSunday ?? 0;
                                        planRegistration.PlannedStartOfShift2 =
                                            dbAssignedSite.StartSunday2NdShift ?? 0;
                                        planRegistration.PlannedEndOfShift2 = dbAssignedSite.EndSunday2NdShift ?? 0;
                                        planRegistration.PlannedBreakOfShift2 =
                                            dbAssignedSite.BreakSunday2NdShift ?? 0;
                                        planRegistration.PlannedStartOfShift3 =
                                            dbAssignedSite.StartSunday3RdShift ?? 0;
                                        planRegistration.PlannedEndOfShift3 = dbAssignedSite.EndSunday3RdShift ?? 0;
                                        planRegistration.PlannedBreakOfShift3 =
                                            dbAssignedSite.BreakSunday3RdShift ?? 0;
                                        planRegistration.PlannedStartOfShift4 =
                                            dbAssignedSite.StartSunday4ThShift ?? 0;
                                        planRegistration.PlannedEndOfShift4 = dbAssignedSite.EndSunday4ThShift ?? 0;
                                        planRegistration.PlannedBreakOfShift4 =
                                            dbAssignedSite.BreakSunday4ThShift ?? 0;
                                        planRegistration.PlannedStartOfShift5 =
                                            dbAssignedSite.StartSunday5ThShift ?? 0;
                                        planRegistration.PlannedEndOfShift5 = dbAssignedSite.EndSunday5ThShift ?? 0;
                                        planRegistration.PlannedBreakOfShift5 =
                                            dbAssignedSite.BreakSunday5ThShift ?? 0;
                                    }

                                    break;
                            }

                            if (originalPlanHours != planRegistration.PlanHours || tainted)
                            {

                                SentrySdk.CaptureEvent(
                                    new SentryEvent
                                    {
                                        Message = $"PlanRegistrationHelper.UpdatePlanRegistrationsInPeriod: " +
                                                  $"Plan hours changed from {originalPlanHours} to {planRegistration.PlanHours} " +
                                                  $"for plan registration with ID {planRegistration.Id} and date {planRegistration.Date}",
                                        Level = SentryLevel.Warning
                                    });
                                tainted = true;
                            }

                            // Console.WriteLine($"The plannedHours are now: {planRegistration.PlanHours}");

                            await planRegistration.Update(dbContext).ConfigureAwait(false);
                        }

                        var preTimePlanning =
                            await dbContext.PlanRegistrations.AsNoTracking()
                                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                                .Where(x => x.Date < planRegistration.Date
                                            && x.SdkSitId == dbAssignedSite.SiteId)
                                .OrderByDescending(x => x.Date)
                                .FirstOrDefaultAsync();

                        // Phase 2: when UseOneMinuteIntervals is on, run the
                        // SumFlex chain in seconds (source of truth) and
                        // back-derive doubles. Flag-off path stays byte-identical.
                        if (dbAssignedSite.UseOneMinuteIntervals)
                        {
                            ApplyNettoFlexChainSecondPrecision(
                                planRegistration,
                                preTimePlanning?.SumFlexEndInSeconds ?? 0,
                                preTimePlanning != null);
                        }
                        else if (preTimePlanning != null)
                        {
                            if (planRegistration.NettoHoursOverrideActive)
                            {
                                planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                                planRegistration.SumFlexEnd =
                                    preTimePlanning.SumFlexEnd + planRegistration.NettoHoursOverride -
                                    planRegistration.PlanHours -
                                    planRegistration.PaiedOutFlex;
                                planRegistration.Flex = planRegistration.NettoHoursOverride - planRegistration.PlanHours;
                            } else
                            {
                                planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                                planRegistration.SumFlexEnd =
                                    preTimePlanning.SumFlexEnd + planRegistration.NettoHours -
                                    planRegistration.PlanHours -
                                    planRegistration.PaiedOutFlex;
                                planRegistration.Flex = planRegistration.NettoHours - planRegistration.PlanHours;
                            }
                        }
                        else
                        {
                            if (planRegistration.NettoHoursOverrideActive)
                            {
                                planRegistration.SumFlexEnd =
                                    planRegistration.NettoHoursOverride - planRegistration.PlanHours -
                                    planRegistration.PaiedOutFlex;
                                planRegistration.SumFlexStart = 0;
                                planRegistration.Flex = planRegistration.NettoHoursOverride - planRegistration.PlanHours;
                            } else
                            {
                                planRegistration.SumFlexEnd =
                                    planRegistration.NettoHours - planRegistration.PlanHours -
                                    planRegistration.PaiedOutFlex;
                                planRegistration.SumFlexStart = 0;
                                planRegistration.Flex = planRegistration.NettoHours - planRegistration.PlanHours;
                            }
                        }
                        await planRegistration.Update(dbContext).ConfigureAwait(false);
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(
                        $"Could not parse PlanText for planning with id: {planRegistration.Id} the PlanText was: {planRegistration.PlanText}");
                    SentrySdk.CaptureMessage(
                        $"Could not parse PlanText for planning with id: {planRegistration.Id} the PlanText was: {planRegistration.PlanText}");
                    //SentrySdk.CaptureException(e);
                    logger.LogError(e.Message);
                    logger.LogTrace(e.StackTrace);
                }
            }

            string? messageLabel = null;
            if (planRegistration.MessageId.HasValue
                && messagesById.TryGetValue(planRegistration.MessageId.Value, out var msg))
            {
                messageLabel = messageLanguage switch
                {
                    "da" => string.IsNullOrEmpty(msg.DaName) ? msg.Name : msg.DaName,
                    "de" => string.IsNullOrEmpty(msg.DeName) ? msg.Name : msg.DeName,
                    "en" => string.IsNullOrEmpty(msg.EnName) ? msg.Name : msg.EnName,
                    _    => msg.Name,
                };
            }

            var planningModel = new TimePlanningPlanningPrDayModel
            {
                Id = planRegistration.Id,
                SiteName = site.Name,
                Date = midnight,
                PlanText = planRegistration.PlanText,
                PlanHours = planRegistration.PlanHours,
                Message = planRegistration.MessageId,
                MessageLabel = messageLabel,
                SiteId = dbAssignedSite.SiteId,
                WeekDay =
                    planRegistration.Date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)planRegistration.Date.DayOfWeek,
                ActualHours = planRegistration.NettoHours,
                Difference = planRegistration.Flex,
                PlanHoursMatched = Math.Abs(planRegistration.NettoHours - planRegistration.PlanHours) <= 0.00,
                WorkDayStarted = planRegistration.Start1Id != 0,
                WorkDayEnded = planRegistration.Stop1Id != 0 ||
                               (planRegistration.Start2Id != 0 && planRegistration.Stop2Id != 0),
                PlannedStartOfShift1 = planRegistration.PlannedStartOfShift1,
                PlannedEndOfShift1 = planRegistration.PlannedEndOfShift1,
                PlannedBreakOfShift1 = planRegistration.PlannedBreakOfShift1,
                PlannedStartOfShift2 = planRegistration.PlannedStartOfShift2,
                PlannedEndOfShift2 = planRegistration.PlannedEndOfShift2,
                PlannedBreakOfShift2 = planRegistration.PlannedBreakOfShift2,
                PlannedStartOfShift3 = planRegistration.PlannedStartOfShift3,
                PlannedEndOfShift3 = planRegistration.PlannedEndOfShift3,
                PlannedBreakOfShift3 = planRegistration.PlannedBreakOfShift3,
                PlannedStartOfShift4 = planRegistration.PlannedStartOfShift4,
                PlannedEndOfShift4 = planRegistration.PlannedEndOfShift4,
                PlannedBreakOfShift4 = planRegistration.PlannedBreakOfShift4,
                PlannedStartOfShift5 = planRegistration.PlannedStartOfShift5,
                PlannedEndOfShift5 = planRegistration.PlannedEndOfShift5,
                PlannedBreakOfShift5 = planRegistration.PlannedBreakOfShift5,
                OnVacation = planRegistration.OnVacation,
                Sick = planRegistration.Sick,
                OtherAllowedAbsence = planRegistration.OtherAllowedAbsence,
                AbsenceWithoutPermission = planRegistration.AbsenceWithoutPermission,
                // Stage 3 tick-exact display parity, forked on the mode AT
                // REGISTRATION (rowIsOneMinute, from OneMinuteModeTimeline) —
                // not the site's current flag:
                //  - tick row (registered under 5-minute mode): ALWAYS the
                //    Id-derived tick time, even when an exact stamp exists —
                //    bit-identical to what the row showed before a site flip.
                //  - one-minute row: the exact stamp first, falling back to the
                //    Id-derived time when the stamp is NULL but the Id exists
                //    (mirrors the Excel export / EnsureTimestampsFromIds rule)
                //    so no row ever renders blank while an Id is present.
                Start1StartedAt = rowIsOneMinute
                    ? planRegistration.Start1StartedAt
                      ?? (planRegistration.Start1Id > 0
                          ? midnight.AddMinutes(
                              (planRegistration.Start1Id * 5) - 5)
                          : null)
                    : (planRegistration.Start1Id == 0
                        ? null
                        : midnight.AddMinutes(
                            (planRegistration.Start1Id * 5) - 5)),
                Stop1StoppedAt = rowIsOneMinute
                    ? planRegistration.Stop1StoppedAt
                      ?? (planRegistration.Stop1Id > 0
                          ? midnight.AddMinutes(
                              (planRegistration.Stop1Id * 5) - 5)
                          : null)
                    : (planRegistration.Stop1Id == 0
                        ? null
                        : midnight.AddMinutes(
                            (planRegistration.Stop1Id * 5) - 5)),
                Start2StartedAt = rowIsOneMinute
                    ? planRegistration.Start2StartedAt
                      ?? (planRegistration.Start2Id > 0
                          ? midnight.AddMinutes(
                              (planRegistration.Start2Id * 5) - 5)
                          : null)
                    : (planRegistration.Start2Id == 0
                        ? null
                        : midnight.AddMinutes(
                            (planRegistration.Start2Id * 5) - 5)),
                Stop2StoppedAt = rowIsOneMinute
                    ? planRegistration.Stop2StoppedAt
                      ?? (planRegistration.Stop2Id > 0
                          ? midnight.AddMinutes(
                              (planRegistration.Stop2Id * 5) - 5)
                          : null)
                    : (planRegistration.Stop2Id == 0
                        ? null
                        : midnight.AddMinutes(
                            (planRegistration.Stop2Id * 5) - 5)),
                Start3StartedAt = rowIsOneMinute
                    ? planRegistration.Start3StartedAt
                      ?? (planRegistration.Start3Id > 0
                          ? midnight.AddMinutes(
                              (planRegistration.Start3Id * 5) - 5)
                          : null)
                    : (planRegistration.Start3Id == 0
                        ? null
                        : midnight.AddMinutes(
                            (planRegistration.Start3Id * 5) - 5)),
                Stop3StoppedAt = rowIsOneMinute
                    ? planRegistration.Stop3StoppedAt
                      ?? (planRegistration.Stop3Id > 0
                          ? midnight.AddMinutes(
                              (planRegistration.Stop3Id * 5) - 5)
                          : null)
                    : (planRegistration.Stop3Id == 0
                        ? null
                        : midnight.AddMinutes(
                            (planRegistration.Stop3Id * 5) - 5)),
                Start4StartedAt = rowIsOneMinute
                    ? planRegistration.Start4StartedAt
                      ?? (planRegistration.Start4Id > 0
                          ? midnight.AddMinutes(
                              (planRegistration.Start4Id * 5) - 5)
                          : null)
                    : (planRegistration.Start4Id == 0
                        ? null
                        : midnight.AddMinutes(
                            (planRegistration.Start4Id * 5) - 5)),
                Stop4StoppedAt = rowIsOneMinute
                    ? planRegistration.Stop4StoppedAt
                      ?? (planRegistration.Stop4Id > 0
                          ? midnight.AddMinutes(
                              (planRegistration.Stop4Id * 5) - 5)
                          : null)
                    : (planRegistration.Stop4Id == 0
                        ? null
                        : midnight.AddMinutes(
                            (planRegistration.Stop4Id * 5) - 5)),
                Start5StartedAt = rowIsOneMinute
                    ? planRegistration.Start5StartedAt
                      ?? (planRegistration.Start5Id > 0
                          ? midnight.AddMinutes(
                              (planRegistration.Start5Id * 5) - 5)
                          : null)
                    : (planRegistration.Start5Id == 0
                        ? null
                        : midnight.AddMinutes(
                            (planRegistration.Start5Id * 5) - 5)),
                Stop5StoppedAt = rowIsOneMinute
                    ? planRegistration.Stop5StoppedAt
                      ?? (planRegistration.Stop5Id > 0
                          ? midnight.AddMinutes(
                              (planRegistration.Stop5Id * 5) - 5)
                          : null)
                    : (planRegistration.Stop5Id == 0
                        ? null
                        : midnight.AddMinutes(
                            (planRegistration.Stop5Id * 5) - 5)),
                Break1Shift = planRegistration.Pause1Id,
                Break2Shift = planRegistration.Pause2Id,
                Break3Shift = planRegistration.Pause3Id,
                Break4Shift = planRegistration.Pause4Id,
                Break5Shift = planRegistration.Pause5Id,
                Pause1Id = planRegistration.Pause1Id > 0 ? planRegistration.Pause1Id - 1 : 0,
                Pause2Id = planRegistration.Pause2Id > 0 ? planRegistration.Pause2Id - 1 : 0,
                Pause3Id = planRegistration.Pause3Id > 0 ? planRegistration.Pause3Id - 1 : 0,
                Pause4Id = planRegistration.Pause4Id > 0 ? planRegistration.Pause4Id - 1 : 0,
                Pause5Id = planRegistration.Pause5Id > 0 ? planRegistration.Pause5Id - 1 : 0,
                PauseMinutes = 0,
                CommentOffice = planRegistration.CommentOffice,
                WorkerComment = planRegistration.WorkerComment,
                SumFlexStart = planRegistration.SumFlexStart,
                SumFlexEnd = planRegistration.SumFlexEnd,
                PaidOutFlex = planRegistration.PaiedOutFlex,
                Pause1StartedAt = planRegistration.Pause1StartedAt,
                Pause1StoppedAt = planRegistration.Pause1StoppedAt,
                Pause2StartedAt = planRegistration.Pause2StartedAt,
                Pause2StoppedAt = planRegistration.Pause2StoppedAt,
                Pause10StartedAt = planRegistration.Pause10StartedAt,
                Pause10StoppedAt = planRegistration.Pause10StoppedAt,
                Pause11StartedAt = planRegistration.Pause11StartedAt,
                Pause11StoppedAt = planRegistration.Pause11StoppedAt,
                Pause12StartedAt = planRegistration.Pause12StartedAt,
                Pause12StoppedAt = planRegistration.Pause12StoppedAt,
                Pause13StartedAt = planRegistration.Pause13StartedAt,
                Pause13StoppedAt = planRegistration.Pause13StoppedAt,
                Pause14StartedAt = planRegistration.Pause14StartedAt,
                Pause14StoppedAt = planRegistration.Pause14StoppedAt,
                Pause15StartedAt = planRegistration.Pause15StartedAt,
                Pause15StoppedAt = planRegistration.Pause15StoppedAt,
                Pause16StartedAt = planRegistration.Pause16StartedAt,
                Pause16StoppedAt = planRegistration.Pause16StoppedAt,
                Pause17StartedAt = planRegistration.Pause17StartedAt,
                Pause17StoppedAt = planRegistration.Pause17StoppedAt,
                Pause18StartedAt = planRegistration.Pause18StartedAt,
                Pause18StoppedAt = planRegistration.Pause18StoppedAt,
                Pause19StartedAt = planRegistration.Pause19StartedAt,
                Pause19StoppedAt = planRegistration.Pause19StoppedAt,
                Pause20StartedAt = planRegistration.Pause20StartedAt,
                Pause20StoppedAt = planRegistration.Pause20StoppedAt,
                Pause21StartedAt = planRegistration.Pause21StartedAt,
                Pause21StoppedAt = planRegistration.Pause21StoppedAt,
                Pause22StartedAt = planRegistration.Pause22StartedAt,
                Pause22StoppedAt = planRegistration.Pause22StoppedAt,
                Pause23StartedAt = planRegistration.Pause23StartedAt,
                Pause23StoppedAt = planRegistration.Pause23StoppedAt,
                Pause24StartedAt = planRegistration.Pause24StartedAt,
                Pause24StoppedAt = planRegistration.Pause24StoppedAt,
                Pause25StartedAt = planRegistration.Pause25StartedAt,
                Pause25StoppedAt = planRegistration.Pause25StoppedAt,
                Pause26StartedAt = planRegistration.Pause26StartedAt,
                Pause26StoppedAt = planRegistration.Pause26StoppedAt,
                Pause27StartedAt = planRegistration.Pause27StartedAt,
                Pause27StoppedAt = planRegistration.Pause27StoppedAt,
                Pause28StartedAt = planRegistration.Pause28StartedAt,
                Pause28StoppedAt = planRegistration.Pause28StoppedAt,
                Pause29StartedAt = planRegistration.Pause29StartedAt,
                Pause29StoppedAt = planRegistration.Pause29StoppedAt,
                Pause100StartedAt = planRegistration.Pause100StartedAt,
                Pause100StoppedAt = planRegistration.Pause100StoppedAt,
                Pause101StartedAt = planRegistration.Pause101StartedAt,
                Pause101StoppedAt = planRegistration.Pause101StoppedAt,
                Pause102StartedAt = planRegistration.Pause102StartedAt,
                Pause102StoppedAt = planRegistration.Pause102StoppedAt,
                Pause200StartedAt = planRegistration.Pause200StartedAt,
                Pause200StoppedAt = planRegistration.Pause200StoppedAt,
                Pause201StartedAt = planRegistration.Pause201StartedAt,
                Pause201StoppedAt = planRegistration.Pause201StoppedAt,
                Pause202StartedAt = planRegistration.Pause202StartedAt,
                Pause202StoppedAt = planRegistration.Pause202StoppedAt,
                Pause3StartedAt = planRegistration.Pause3StartedAt,
                Pause3StoppedAt = planRegistration.Pause3StoppedAt,
                Pause4StartedAt = planRegistration.Pause4StartedAt,
                Pause4StoppedAt = planRegistration.Pause4StoppedAt,
                Pause5StartedAt = planRegistration.Pause5StartedAt,
                Pause5StoppedAt = planRegistration.Pause5StoppedAt
            };

            planningModel.PauseMinutes += AggregatePauseMinutes(planRegistration, dbAssignedSite.UseOneMinuteIntervals);

            // planningModel.PauseMinutes = planningModel.PauseMinutes > 0 ? planningModel.PauseMinutes - 5 : 0;

            planningModel.CommentOffice = planRegistration.CommentOffice;
            planningModel.WorkerComment = planRegistration.WorkerComment;
            planningModel.PlanHoursMatched = Math.Abs(planRegistration.NettoHours - planRegistration.PlanHours) <= 0.00;

            planningModel.IsDoubleShift = planningModel.Start2StartedAt != planningModel.Stop2StoppedAt;
            planningModel.NettoHoursOverride = planRegistration.NettoHoursOverride;
            planningModel.NettoHoursOverrideActive = planRegistration.NettoHoursOverrideActive;

            // Approach C READ projection: for any shift with a pause override,
            // present a single synthesized pause pair (sum = override) and empty
            // the sub-slots IN THE RESPONSE DTO, so the unchanged mobile app's
            // timestamp-summing display reflects the override. The DB row
            // (planRegistration) is read-only here — documentation preserved.
            // PauseMinutes above is already override-aware via AggregatePauseMinutes.
            ProjectPauseOverridesOntoDto(planningModel, planRegistration);

            planningsInPeriod = await dbContext.PlanRegistrations
                .AsNoTracking()
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.SdkSitId == dbAssignedSite.SiteId)
                .Where(x => x.Date >= midnightOfDateFrom)
                .Where(x => x.Date <= midnightOfDateTo)
                .Select(x => new PlanRegistration
                {
                    Id = x.Id,
                    Date = x.Date,
                    PlanHours = x.PlanHours,
                    NettoHours = x.NettoHours,
                    NettoHoursOverride = x.NettoHoursOverride,
                    NettoHoursOverrideActive = x.NettoHoursOverrideActive,
                })
                .OrderBy(x => x.Date)
                .ToListAsync().ConfigureAwait(false);

            var plannedTotalHours = planningsInPeriod.Sum(x => x.PlanHours);
            var nettoHoursTotal = planningsInPeriod.Where(x => x.NettoHoursOverrideActive == false).Sum(x => x.NettoHours);
            var nettoHoursOverrideTotal = planningsInPeriod.Where(x => x.NettoHoursOverrideActive).Sum(x => x.NettoHoursOverride);

            siteModel.PlannedHours = (int)plannedTotalHours;
            siteModel.PlannedMinutes = (int)((plannedTotalHours - siteModel.PlannedHours) * 60);
            siteModel.CurrentWorkedHours = (int)nettoHoursTotal + (int)nettoHoursOverrideTotal;
            siteModel.CurrentWorkedMinutes = (int)((nettoHoursTotal + nettoHoursOverrideTotal - siteModel.CurrentWorkedHours) * 60);
            siteModel.PercentageCompleted = (int)(nettoHoursTotal + nettoHoursOverrideTotal / plannedTotalHours * 100);

            siteModel.PlanningPrDayModels.Add(planningModel);
        }

        return siteModel;
    }

    public static async Task<PlanRegistration> UpdatePlanRegistration(
        PlanRegistration planRegistration,
        TimePlanningPnDbContext dbContext,
        AssignedSite dbAssignedSite,
        DateTime dayOfPayment
        )
    {
        if (dbAssignedSite.Resigned)
        {
            return planRegistration;
        }
        var tainted = false;
        // foreach (var plan in planningsInPeriod)
        // {
            // var planRegistration = await dbContext.PlanRegistrations.AsTracking().FirstAsync(x => x.Id == planRegistrationId);
            // var midnight = new DateTime(planRegistration.Date.Year, planRegistration.Date.Month,
            //     planRegistration.Date.Day, 0, 0, 0);
            var toDay = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0);
            // var dayOfPayment = toDay.Day >= settingsDayOfPayment
                // ? new DateTime(DateTime.Now.Year, DateTime.Now.Month, settingsDayOfPayment, 0, 0, 0)
                // : new DateTime(DateTime.Now.Year, DateTime.Now.Month - 1, settingsDayOfPayment, 0, 0, 0);

            try
            {
                if (dbAssignedSite.UseGoogleSheetAsDefault)
                {
                    if (planRegistration.Date > dayOfPayment && !planRegistration.PlanChangedByAdmin)
                    {
                    if (!string.IsNullOrEmpty(planRegistration.PlanText))
                    {
                            var originalPlanHours = planRegistration.PlanHours;
                            PlanTextHelper.ParsePlanText(planRegistration);

                            if (originalPlanHours != planRegistration.PlanHours || tainted)
                            {
                                tainted = true;

                            }
                        }
                        else
                        {
                            planRegistration.PlannedStartOfShift1 = 0;
                            planRegistration.PlannedEndOfShift1 = 0;
                            planRegistration.PlannedBreakOfShift1 = 0;
                            planRegistration.PlannedStartOfShift2 = 0;
                            planRegistration.PlannedEndOfShift2 = 0;
                            planRegistration.PlannedBreakOfShift2 = 0;
                            planRegistration.PlannedStartOfShift3 = 0;
                            planRegistration.PlannedEndOfShift3 = 0;
                            planRegistration.PlannedBreakOfShift3 = 0;
                            planRegistration.PlannedStartOfShift4 = 0;
                            planRegistration.PlannedEndOfShift4 = 0;
                            planRegistration.PlannedBreakOfShift4 = 0;
                            planRegistration.PlannedStartOfShift5 = 0;
                            planRegistration.PlannedEndOfShift5 = 0;
                            planRegistration.PlannedBreakOfShift5 = 0;
                        }
                    }

                    var preTimePlanning =
                        await dbContext.PlanRegistrations.AsNoTracking()
                            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                            .Where(x => x.Date < planRegistration.Date
                                        && x.SdkSitId == dbAssignedSite.SiteId)
                            .OrderByDescending(x => x.Date)
                            .FirstOrDefaultAsync();

                    // Phase 2: when UseOneMinuteIntervals is on, run the
                    // SumFlex chain in seconds (source of truth) and
                    // back-derive doubles. Flag-off path stays byte-identical.
                    if (dbAssignedSite.UseOneMinuteIntervals)
                    {
                        ApplyNettoFlexChainSecondPrecision(
                            planRegistration,
                            preTimePlanning?.SumFlexEndInSeconds ?? 0,
                            preTimePlanning != null);
                    }
                    else if (preTimePlanning != null)
                    {
                        if (planRegistration.NettoHoursOverrideActive)
                        {
                            planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                            planRegistration.SumFlexEnd =
                                preTimePlanning.SumFlexEnd + planRegistration.NettoHoursOverride -
                                planRegistration.PlanHours -
                                planRegistration.PaiedOutFlex;
                            planRegistration.Flex = planRegistration.NettoHoursOverride - planRegistration.PlanHours;
                        }
                        else
                        {
                            planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                            planRegistration.SumFlexEnd =
                                preTimePlanning.SumFlexEnd + planRegistration.NettoHours -
                                planRegistration.PlanHours -
                                planRegistration.PaiedOutFlex;
                            planRegistration.Flex = planRegistration.NettoHours - planRegistration.PlanHours;
                        }
                    }
                    else
                    {
                        if (planRegistration.NettoHoursOverrideActive)
                        {
                            planRegistration.SumFlexEnd =
                                planRegistration.NettoHoursOverride - planRegistration.PlanHours -
                                planRegistration.PaiedOutFlex;
                            planRegistration.SumFlexStart = 0;
                            planRegistration.Flex = planRegistration.NettoHoursOverride - planRegistration.PlanHours;
                        }
                        else
                        {
                            planRegistration.SumFlexEnd =
                                planRegistration.NettoHours - planRegistration.PlanHours -
                                planRegistration.PaiedOutFlex;
                            planRegistration.SumFlexStart = 0;
                            planRegistration.Flex = planRegistration.NettoHours - planRegistration.PlanHours;
                        }
                    }

                    await planRegistration.Update(dbContext).ConfigureAwait(false);
                }
                else
                {
                    if (planRegistration.Date > dayOfPayment && !planRegistration.PlanChangedByAdmin)
                    {
                        var dayOfWeek = planRegistration.Date.DayOfWeek;
                        var originalPlanHours = planRegistration.PlanHours;
                        switch (dayOfWeek)
                        {
                            case DayOfWeek.Monday:
                                planRegistration.PlanHours = dbAssignedSite.MondayPlanHours != 0
                                    ? (double)dbAssignedSite.MondayPlanHours / 60
                                    : 0;
                                if (!dbAssignedSite.UseOnlyPlanHours)
                                {
                                    planRegistration.PlannedStartOfShift1 = dbAssignedSite.StartMonday ?? 0;
                                    planRegistration.PlannedEndOfShift1 = dbAssignedSite.EndMonday ?? 0;
                                    planRegistration.PlannedBreakOfShift1 = dbAssignedSite.BreakMonday ?? 0;
                                    planRegistration.PlannedStartOfShift2 =
                                        dbAssignedSite.StartMonday2NdShift ?? 0;
                                    planRegistration.PlannedEndOfShift2 = dbAssignedSite.EndMonday2NdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift2 =
                                        dbAssignedSite.BreakMonday2NdShift ?? 0;
                                    planRegistration.PlannedStartOfShift3 =
                                        dbAssignedSite.StartMonday3RdShift ?? 0;
                                    planRegistration.PlannedEndOfShift3 = dbAssignedSite.EndMonday3RdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift3 =
                                        dbAssignedSite.BreakMonday3RdShift ?? 0;
                                    planRegistration.PlannedStartOfShift4 =
                                        dbAssignedSite.StartMonday4ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift4 = dbAssignedSite.EndMonday4ThShift ?? 0;
                                    planRegistration.PlannedBreakOfShift4 =
                                        dbAssignedSite.BreakMonday4ThShift ?? 0;
                                    planRegistration.PlannedStartOfShift5 =
                                        dbAssignedSite.StartMonday5ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift5 = dbAssignedSite.EndMonday5ThShift ?? 0;
                                    planRegistration.PlannedBreakOfShift5 =
                                        dbAssignedSite.BreakMonday5ThShift ?? 0;
                                }

                                break;
                            case DayOfWeek.Tuesday:
                                planRegistration.PlanHours = dbAssignedSite.TuesdayPlanHours != 0
                                    ? (double)dbAssignedSite.TuesdayPlanHours / 60
                                    : 0;
                                if (!dbAssignedSite.UseOnlyPlanHours)
                                {
                                    planRegistration.PlannedStartOfShift1 = dbAssignedSite.StartTuesday ?? 0;
                                    planRegistration.PlannedEndOfShift1 = dbAssignedSite.EndTuesday ?? 0;
                                    planRegistration.PlannedBreakOfShift1 = dbAssignedSite.BreakTuesday ?? 0;
                                    planRegistration.PlannedStartOfShift2 =
                                        dbAssignedSite.StartTuesday2NdShift ?? 0;
                                    planRegistration.PlannedEndOfShift2 =
                                        dbAssignedSite.EndTuesday2NdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift2 =
                                        dbAssignedSite.BreakTuesday2NdShift ?? 0;
                                    planRegistration.PlannedStartOfShift3 =
                                        dbAssignedSite.StartTuesday3RdShift ?? 0;
                                    planRegistration.PlannedEndOfShift3 =
                                        dbAssignedSite.EndTuesday3RdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift3 =
                                        dbAssignedSite.BreakTuesday3RdShift ?? 0;
                                    planRegistration.PlannedStartOfShift4 =
                                        dbAssignedSite.StartTuesday4ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift4 =
                                        dbAssignedSite.EndTuesday4ThShift ?? 0;
                                    planRegistration.PlannedBreakOfShift4 =
                                        dbAssignedSite.BreakTuesday4ThShift ?? 0;
                                    planRegistration.PlannedStartOfShift5 =
                                        dbAssignedSite.StartTuesday5ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift5 =
                                        dbAssignedSite.EndTuesday5ThShift ?? 0;
                                    planRegistration.PlannedBreakOfShift5 =
                                        dbAssignedSite.BreakTuesday5ThShift ?? 0;
                                }

                                break;
                            case DayOfWeek.Wednesday:
                                planRegistration.PlanHours = dbAssignedSite.WednesdayPlanHours != 0
                                    ? (double)dbAssignedSite.WednesdayPlanHours / 60
                                    : 0;
                                if (!dbAssignedSite.UseOnlyPlanHours)
                                {
                                    planRegistration.PlannedStartOfShift1 = dbAssignedSite.StartWednesday ?? 0;
                                    planRegistration.PlannedEndOfShift1 = dbAssignedSite.EndWednesday ?? 0;
                                    planRegistration.PlannedBreakOfShift1 = dbAssignedSite.BreakWednesday ?? 0;
                                    planRegistration.PlannedStartOfShift2 =
                                        dbAssignedSite.StartWednesday2NdShift ?? 0;
                                    planRegistration.PlannedEndOfShift2 =
                                        dbAssignedSite.EndWednesday2NdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift2 =
                                        dbAssignedSite.BreakWednesday2NdShift ?? 0;
                                    planRegistration.PlannedStartOfShift3 =
                                        dbAssignedSite.StartWednesday3RdShift ?? 0;
                                    planRegistration.PlannedEndOfShift3 =
                                        dbAssignedSite.EndWednesday3RdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift3 =
                                        dbAssignedSite.BreakWednesday3RdShift ?? 0;
                                    planRegistration.PlannedStartOfShift4 =
                                        dbAssignedSite.StartWednesday4ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift4 =
                                        dbAssignedSite.EndWednesday4ThShift ?? 0;
                                    planRegistration.PlannedBreakOfShift4 =
                                        dbAssignedSite.BreakWednesday4ThShift ?? 0;
                                    planRegistration.PlannedStartOfShift5 =
                                        dbAssignedSite.StartWednesday5ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift5 =
                                        dbAssignedSite.EndWednesday5ThShift ?? 0;
                                    planRegistration.PlannedBreakOfShift5 =
                                        dbAssignedSite.BreakWednesday5ThShift ?? 0;
                                }

                                break;
                            case DayOfWeek.Thursday:
                                planRegistration.PlanHours = dbAssignedSite.ThursdayPlanHours != 0
                                    ? (double)dbAssignedSite.ThursdayPlanHours / 60
                                    : 0;
                                if (!dbAssignedSite.UseOnlyPlanHours)
                                {
                                    planRegistration.PlannedStartOfShift1 = dbAssignedSite.StartThursday ?? 0;
                                    planRegistration.PlannedEndOfShift1 = dbAssignedSite.EndThursday ?? 0;
                                    planRegistration.PlannedBreakOfShift1 = dbAssignedSite.BreakThursday ?? 0;
                                    planRegistration.PlannedStartOfShift2 =
                                        dbAssignedSite.StartThursday2NdShift ?? 0;
                                    planRegistration.PlannedEndOfShift2 =
                                        dbAssignedSite.EndThursday2NdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift2 =
                                        dbAssignedSite.BreakThursday2NdShift ?? 0;
                                    planRegistration.PlannedStartOfShift3 =
                                        dbAssignedSite.StartThursday3RdShift ?? 0;
                                    planRegistration.PlannedEndOfShift3 =
                                        dbAssignedSite.EndThursday3RdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift3 =
                                        dbAssignedSite.BreakThursday3RdShift ?? 0;
                                    planRegistration.PlannedStartOfShift4 =
                                        dbAssignedSite.StartThursday4ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift4 =
                                        dbAssignedSite.EndThursday4ThShift ?? 0;
                                    planRegistration.PlannedBreakOfShift4 =
                                        dbAssignedSite.BreakThursday4ThShift ?? 0;
                                    planRegistration.PlannedStartOfShift5 =
                                        dbAssignedSite.StartThursday5ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift5 =
                                        dbAssignedSite.EndThursday5ThShift ?? 0;
                                    planRegistration.PlannedBreakOfShift5 =
                                        dbAssignedSite.BreakThursday5ThShift ?? 0;
                                }

                                break;
                            case DayOfWeek.Friday:
                                planRegistration.PlanHours = dbAssignedSite.FridayPlanHours != 0
                                    ? (double)dbAssignedSite.FridayPlanHours / 60
                                    : 0;
                                if (!dbAssignedSite.UseOnlyPlanHours)
                                {
                                    planRegistration.PlannedStartOfShift1 = dbAssignedSite.StartFriday ?? 0;
                                    planRegistration.PlannedEndOfShift1 = dbAssignedSite.EndFriday ?? 0;
                                    planRegistration.PlannedBreakOfShift1 = dbAssignedSite.BreakFriday ?? 0;
                                    planRegistration.PlannedStartOfShift2 =
                                        dbAssignedSite.StartFriday2NdShift ?? 0;
                                    planRegistration.PlannedEndOfShift2 = dbAssignedSite.EndFriday2NdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift2 =
                                        dbAssignedSite.BreakFriday2NdShift ?? 0;
                                    planRegistration.PlannedStartOfShift3 =
                                        dbAssignedSite.StartFriday3RdShift ?? 0;
                                    planRegistration.PlannedEndOfShift3 = dbAssignedSite.EndFriday3RdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift3 =
                                        dbAssignedSite.BreakFriday3RdShift ?? 0;
                                    planRegistration.PlannedStartOfShift4 =
                                        dbAssignedSite.StartFriday4ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift4 = dbAssignedSite.EndFriday4ThShift ?? 0;
                                    planRegistration.PlannedBreakOfShift4 =
                                        dbAssignedSite.BreakFriday4ThShift ?? 0;
                                    planRegistration.PlannedStartOfShift5 =
                                        dbAssignedSite.StartFriday5ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift5 = dbAssignedSite.EndFriday5ThShift ?? 0;
                                    planRegistration.PlannedBreakOfShift5 =
                                        dbAssignedSite.BreakFriday5ThShift ?? 0;
                                }

                                break;
                            case DayOfWeek.Saturday:
                                planRegistration.PlanHours = dbAssignedSite.SaturdayPlanHours != 0
                                    ? (double)dbAssignedSite.SaturdayPlanHours / 60
                                    : 0;
                                if (!dbAssignedSite.UseOnlyPlanHours)
                                {
                                    planRegistration.PlannedStartOfShift1 = dbAssignedSite.StartSaturday ?? 0;
                                    planRegistration.PlannedEndOfShift1 = dbAssignedSite.EndSaturday ?? 0;
                                    planRegistration.PlannedBreakOfShift1 = dbAssignedSite.BreakSaturday ?? 0;
                                    planRegistration.PlannedStartOfShift2 =
                                        dbAssignedSite.StartSaturday2NdShift ?? 0;
                                    planRegistration.PlannedEndOfShift2 =
                                        dbAssignedSite.EndSaturday2NdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift2 =
                                        dbAssignedSite.BreakSaturday2NdShift ?? 0;
                                    planRegistration.PlannedStartOfShift3 =
                                        dbAssignedSite.StartSaturday3RdShift ?? 0;
                                    planRegistration.PlannedEndOfShift3 =
                                        dbAssignedSite.EndSaturday3RdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift3 =
                                        dbAssignedSite.BreakSaturday3RdShift ?? 0;
                                    planRegistration.PlannedStartOfShift4 =
                                        dbAssignedSite.StartSaturday4ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift4 =
                                        dbAssignedSite.EndSaturday4ThShift ?? 0;
                                    planRegistration.PlannedBreakOfShift4 =
                                        dbAssignedSite.BreakSaturday4ThShift ?? 0;
                                    planRegistration.PlannedStartOfShift5 =
                                        dbAssignedSite.StartSaturday5ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift5 =
                                        dbAssignedSite.EndSaturday5ThShift ?? 0;
                                    planRegistration.PlannedBreakOfShift5 =
                                        dbAssignedSite.BreakSaturday5ThShift ?? 0;
                                }

                                break;
                            case DayOfWeek.Sunday:
                                planRegistration.PlanHours = dbAssignedSite.SundayPlanHours != 0
                                    ? (double)dbAssignedSite.SundayPlanHours / 60
                                    : 0;
                                if (!dbAssignedSite.UseOnlyPlanHours)
                                {
                                    planRegistration.PlannedStartOfShift1 = dbAssignedSite.StartSunday ?? 0;
                                    planRegistration.PlannedEndOfShift1 = dbAssignedSite.EndSunday ?? 0;
                                    planRegistration.PlannedBreakOfShift1 = dbAssignedSite.BreakSunday ?? 0;
                                    planRegistration.PlannedStartOfShift2 =
                                        dbAssignedSite.StartSunday2NdShift ?? 0;
                                    planRegistration.PlannedEndOfShift2 = dbAssignedSite.EndSunday2NdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift2 =
                                        dbAssignedSite.BreakSunday2NdShift ?? 0;
                                    planRegistration.PlannedStartOfShift3 =
                                        dbAssignedSite.StartSunday3RdShift ?? 0;
                                    planRegistration.PlannedEndOfShift3 = dbAssignedSite.EndSunday3RdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift3 =
                                        dbAssignedSite.BreakSunday3RdShift ?? 0;
                                    planRegistration.PlannedStartOfShift4 =
                                        dbAssignedSite.StartSunday4ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift4 = dbAssignedSite.EndSunday4ThShift ?? 0;
                                    planRegistration.PlannedBreakOfShift4 =
                                        dbAssignedSite.BreakSunday4ThShift ?? 0;
                                    planRegistration.PlannedStartOfShift5 =
                                        dbAssignedSite.StartSunday5ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift5 = dbAssignedSite.EndSunday5ThShift ?? 0;
                                    planRegistration.PlannedBreakOfShift5 =
                                        dbAssignedSite.BreakSunday5ThShift ?? 0;
                                }

                                break;
                        }

                        if (originalPlanHours != planRegistration.PlanHours || tainted)
                        {
                            tainted = true;

                        }
                    }

                    var preTimePlanning =
                        await dbContext.PlanRegistrations.AsNoTracking()
                            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                            .Where(x => x.Date < planRegistration.Date
                                        && x.SdkSitId == dbAssignedSite.SiteId)
                            .OrderByDescending(x => x.Date)
                            .FirstOrDefaultAsync();

                    // Phase 2: when UseOneMinuteIntervals is on, run the
                    // SumFlex chain in seconds (source of truth) and
                    // back-derive doubles. Flag-off path stays byte-identical.
                    if (dbAssignedSite.UseOneMinuteIntervals)
                    {
                        ApplyNettoFlexChainSecondPrecision(
                            planRegistration,
                            preTimePlanning?.SumFlexEndInSeconds ?? 0,
                            preTimePlanning != null);
                    }
                    else if (preTimePlanning != null)
                    {
                        if (planRegistration.NettoHoursOverrideActive)
                        {planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                            planRegistration.SumFlexEnd =
                                preTimePlanning.SumFlexEnd + planRegistration.NettoHoursOverride -
                                planRegistration.PlanHours -
                                planRegistration.PaiedOutFlex;
                            planRegistration.Flex = planRegistration.NettoHoursOverride - planRegistration.PlanHours;
                        } else
                        {
                            planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                            planRegistration.SumFlexEnd =
                                preTimePlanning.SumFlexEnd + planRegistration.NettoHours -
                                planRegistration.PlanHours -
                                planRegistration.PaiedOutFlex;
                            planRegistration.Flex = planRegistration.NettoHours - planRegistration.PlanHours;
                        }
                    }
                    else
                    {
                        if (planRegistration.NettoHoursOverrideActive)
                        {
                            planRegistration.SumFlexEnd =
                                planRegistration.NettoHoursOverride - planRegistration.PlanHours -
                                planRegistration.PaiedOutFlex;
                            planRegistration.SumFlexStart = 0;
                            planRegistration.Flex = planRegistration.NettoHoursOverride - planRegistration.PlanHours;
                        }
                        else
                        {
                            planRegistration.SumFlexEnd =
                                planRegistration.NettoHours - planRegistration.PlanHours -
                                planRegistration.PaiedOutFlex;
                            planRegistration.SumFlexStart = 0;
                            planRegistration.Flex = planRegistration.NettoHours - planRegistration.PlanHours;
                        }
                    }

                    // Console.WriteLine($"The plannedHours are now: {planRegistration.PlanHours}");

                    await planRegistration.Update(dbContext).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                SentrySdk.CaptureMessage(
                    $"Could not parse PlanText for planning with id: {planRegistration.Id} the PlanText was: {planRegistration.PlanText}");
            }
        // }
        return planRegistration;
    }


    private static int BreakTimeCalculator(string breakPart)
    {
        return breakPart switch
        {
            "0.1" => 5,
            ".1" => 5,
            "0.15" => 10,
            ".15" => 10,
            "0.25" => 15,
            ".25" => 15,
            "0.3" => 20,
            ".3" => 20,
            "0.4" => 25,
            ".4" => 25,
            "0.5" => 30,
            ".5" => 30,
            "0.6" => 35,
            ".6" => 35,
            "0.7" => 40,
            ".7" => 40,
            "0.75" => 45,
            ".75" => 45,
            "0.8" => 50,
            ".8" => 50,
            "0.9" => 55,
            ".9" => 55,
            "¾" => 45,
            "½" => 30,
            "1" => 60,
            "1.0" => 60,
            "1.25" => 75,
            "1.5" => 90,
            "1.75" => 105,
            "2" => 120,
            "2.0" => 120,
            "2.25" => 135,
            "2.5" => 150,
            "2.75" => 165,
            "3" => 180,
            "3.0" => 180,
            "3.25" => 195,
            "3.5" => 210,
            "3.75" => 225,
            "4" => 240,
            "4.0" => 240,
            "4.25" => 255,
            "4.5" => 270,
            "4.75" => 285,
            _ => 0
        };
    }

    public static async Task<TimePlanningWorkingHoursModel> ReadBySiteAndDate(
        TimePlanningPnDbContext dbContext, int sdkSiteId, DateTime dateTime,
        string token)
    {
        Console.WriteLine($"[DEBUG-GRPC-READ] ReadBySiteAndDate: sdkSiteId={sdkSiteId}, dateTime={dateTime:yyyy-MM-dd HH:mm:ss}, dateTime.Kind={dateTime.Kind}, token={(token == null ? "NULL" : token[..Math.Min(8, token.Length)] + "...")}");

        if (token != null)
        {
            var registrationDevice = await dbContext.RegistrationDevices
                .AsNoTracking()
                .Where(x => x.Token == token).FirstOrDefaultAsync();
            if (registrationDevice == null)
            {
                Console.WriteLine($"[DEBUG-GRPC-READ] ReadBySiteAndDate: EARLY RETURN null -- token not found in RegistrationDevices");
                return null;
            }
            Console.WriteLine($"[DEBUG-GRPC-READ] ReadBySiteAndDate: registrationDevice found, Id={registrationDevice.Id}");
        }

        // var today = DateTime.UtcNow;
        var midnight = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, 0, 0, 0);
        Console.WriteLine($"[DEBUG-GRPC-READ] ReadBySiteAndDate: Querying PlanRegistrations WHERE Date={midnight:yyyy-MM-dd HH:mm:ss} AND SdkSitId={sdkSiteId} AND WorkflowState != Removed");

        var planRegistration = await dbContext.PlanRegistrations
            .AsNoTracking()
            .Where(x => x.Date == midnight)
            .Where(x => x.SdkSitId == sdkSiteId)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync();

        if (planRegistration == null)
        {
            Console.WriteLine($"[DEBUG-GRPC-READ] ReadBySiteAndDate: planRegistration NOT FOUND in DB -- returning EMPTY in-memory model (all zeros)");
            var newTimePlanningWorkingHoursModel = new TimePlanningWorkingHoursModel
            {
                SdkSiteId = sdkSiteId,
                Date = midnight,
                PlanText = "",
                PlanHours = 0,
                Shift1Start = 0,
                Shift1Stop = 0,
                Shift1Pause = 0,
                Shift2Start = 0,
                Shift2Stop = 0,
                Shift2Pause = 0,
                NettoHours = 0,
                FlexHours = 0,
                SumFlexStart = 0,
                SumFlexEnd = 0,
                PaidOutFlex = "0",
                Message = 0,
                CommentWorker = "",
                CommentOffice = "",
                CommentOfficeAll = "",
                Shift1PauseNumber = 0,
                Shift2PauseNumber = 0,
            };

            return newTimePlanningWorkingHoursModel;

            // return new OperationDataResult<TimePlanningWorkingHoursModel>(false, "Plan registration not found",
            //     null);
        }

        Console.WriteLine($"[DEBUG-GRPC-READ] ReadBySiteAndDate: planRegistration FOUND IN DB -- Id={planRegistration.Id}, Date={planRegistration.Date:yyyy-MM-dd HH:mm:ss}, SdkSitId={planRegistration.SdkSitId}, WorkflowState={planRegistration.WorkflowState}, Start1Id={planRegistration.Start1Id}, Stop1Id={planRegistration.Stop1Id}, Pause1Id={planRegistration.Pause1Id}, Start1StartedAt={planRegistration.Start1StartedAt}, Stop1StoppedAt={planRegistration.Stop1StoppedAt}, NettoHours={planRegistration.NettoHours}");

        var timePlanningWorkingHoursModel = new TimePlanningWorkingHoursModel
        {
            SdkSiteId = sdkSiteId,
            Date = planRegistration.Date,
            PlanText = planRegistration.PlanText,
            PlanHours = planRegistration.PlanHours,
            Shift1Start = planRegistration.Start1Id,
            Shift1Stop = planRegistration.Stop1Id,
            Shift1Pause = planRegistration.Pause1Id,
            Shift2Start = planRegistration.Start2Id,
            Shift2Stop = planRegistration.Stop2Id,
            Shift2Pause = planRegistration.Pause2Id,
            Shift3Start = planRegistration.Start3Id,
            Shift3Stop = planRegistration.Stop3Id,
            Shift3Pause = planRegistration.Pause3Id,
            Shift4Start = planRegistration.Start4Id,
            Shift4Stop = planRegistration.Stop4Id,
            Shift4Pause = planRegistration.Pause4Id,
            Shift5Start = planRegistration.Start5Id,
            Shift5Stop = planRegistration.Stop5Id,
            Shift5Pause = planRegistration.Pause5Id,
            NettoHours = planRegistration.NettoHours,
            FlexHours = planRegistration.Flex,
            SumFlexStart = planRegistration.SumFlexStart,
            SumFlexEnd = planRegistration.SumFlexEnd,
            PaidOutFlex = planRegistration.PaiedOutFlex.ToString(),
            Message = planRegistration.MessageId,
            CommentWorker = planRegistration.WorkerComment,
            CommentOffice = planRegistration.CommentOffice,
            CommentOfficeAll = planRegistration.CommentOfficeAll,
            Start1StartedAt = planRegistration.Start1StartedAt,
            Stop1StoppedAt = planRegistration.Stop1StoppedAt,
            Pause1StartedAt = planRegistration.Pause1StartedAt,
            Pause1StoppedAt = planRegistration.Pause1StoppedAt,
            Start2StartedAt = planRegistration.Start2StartedAt,
            Stop2StoppedAt = planRegistration.Stop2StoppedAt,
            Pause2StartedAt = planRegistration.Pause2StartedAt,
            Pause2StoppedAt = planRegistration.Pause2StoppedAt,
            Pause10StartedAt = planRegistration.Pause10StartedAt,
            Pause10StoppedAt = planRegistration.Pause10StoppedAt,
            Pause11StartedAt = planRegistration.Pause11StartedAt,
            Pause11StoppedAt = planRegistration.Pause11StoppedAt,
            Pause12StartedAt = planRegistration.Pause12StartedAt,
            Pause12StoppedAt = planRegistration.Pause12StoppedAt,
            Pause13StartedAt = planRegistration.Pause13StartedAt,
            Pause13StoppedAt = planRegistration.Pause13StoppedAt,
            Pause14StartedAt = planRegistration.Pause14StartedAt,
            Pause14StoppedAt = planRegistration.Pause14StoppedAt,
            Pause15StartedAt = planRegistration.Pause15StartedAt,
            Pause15StoppedAt = planRegistration.Pause15StoppedAt,
            Pause16StartedAt = planRegistration.Pause16StartedAt,
            Pause16StoppedAt = planRegistration.Pause16StoppedAt,
            Pause17StartedAt = planRegistration.Pause17StartedAt,
            Pause17StoppedAt = planRegistration.Pause17StoppedAt,
            Pause18StartedAt = planRegistration.Pause18StartedAt,
            Pause18StoppedAt = planRegistration.Pause18StoppedAt,
            Pause19StartedAt = planRegistration.Pause19StartedAt,
            Pause19StoppedAt = planRegistration.Pause19StoppedAt,
            Pause100StartedAt = planRegistration.Pause100StartedAt,
            Pause100StoppedAt = planRegistration.Pause100StoppedAt,
            Pause101StartedAt = planRegistration.Pause101StartedAt,
            Pause101StoppedAt = planRegistration.Pause101StoppedAt,
            Pause102StartedAt = planRegistration.Pause102StartedAt,
            Pause102StoppedAt = planRegistration.Pause102StoppedAt,
            Pause20StartedAt = planRegistration.Pause20StartedAt,
            Pause20StoppedAt = planRegistration.Pause20StoppedAt,
            Pause21StartedAt = planRegistration.Pause21StartedAt,
            Pause21StoppedAt = planRegistration.Pause21StoppedAt,
            Pause22StartedAt = planRegistration.Pause22StartedAt,
            Pause22StoppedAt = planRegistration.Pause22StoppedAt,
            Pause23StartedAt = planRegistration.Pause23StartedAt,
            Pause23StoppedAt = planRegistration.Pause23StoppedAt,
            Pause24StartedAt = planRegistration.Pause24StartedAt,
            Pause24StoppedAt = planRegistration.Pause24StoppedAt,
            Pause25StartedAt = planRegistration.Pause25StartedAt,
            Pause25StoppedAt = planRegistration.Pause25StoppedAt,
            Pause26StartedAt = planRegistration.Pause26StartedAt,
            Pause26StoppedAt = planRegistration.Pause26StoppedAt,
            Pause27StartedAt = planRegistration.Pause27StartedAt,
            Pause27StoppedAt = planRegistration.Pause27StoppedAt,
            Pause28StartedAt = planRegistration.Pause28StartedAt,
            Pause28StoppedAt = planRegistration.Pause28StoppedAt,
            Pause29StartedAt = planRegistration.Pause29StartedAt,
            Pause29StoppedAt = planRegistration.Pause29StoppedAt,
            Pause200StartedAt = planRegistration.Pause200StartedAt,
            Pause200StoppedAt = planRegistration.Pause200StoppedAt,
            Pause201StartedAt = planRegistration.Pause201StartedAt,
            Pause201StoppedAt = planRegistration.Pause201StoppedAt,
            Pause202StartedAt = planRegistration.Pause202StartedAt,
            Pause202StoppedAt = planRegistration.Pause202StoppedAt,
            Start3StartedAt = planRegistration.Start3StartedAt,
            Stop3StoppedAt = planRegistration.Stop3StoppedAt,
            Pause3StartedAt = planRegistration.Pause3StartedAt,
            Pause3StoppedAt = planRegistration.Pause3StoppedAt,
            Start4StartedAt = planRegistration.Start4StartedAt,
            Stop4StoppedAt = planRegistration.Stop4StoppedAt,
            Pause4StartedAt = planRegistration.Pause4StartedAt,
            Pause4StoppedAt = planRegistration.Pause4StoppedAt,
            Start5StartedAt = planRegistration.Start5StartedAt,
            Stop5StoppedAt = planRegistration.Stop5StoppedAt,
            Pause5StartedAt = planRegistration.Pause5StartedAt,
            Pause5StoppedAt = planRegistration.Pause5StoppedAt,
            Shift1PauseNumber = planRegistration.Shift1PauseNumber,
            Shift2PauseNumber = planRegistration.Shift2PauseNumber
        };

        // Approach C READ projection (today path): when a shift carries a pause
        // override, present a single synthesized pause pair and empty the
        // sub-slots IN THE RESPONSE MODEL so the unchanged app's timestamp-summing
        // display reflects the override. The DB row is read-only here.
        ProjectPauseOverridesOntoWorkingHours(timePlanningWorkingHoursModel, planRegistration);

        return timePlanningWorkingHoursModel;
        // return new OperationDataResult<TimePlanningWorkingHoursModel>(true, "Plan registration found",
        //     timePlanningWorkingHoursModel);
    }

    /// <summary>
    /// Extract work intervals from PlanRegistration (Start/Stop pairs).
    /// Returns intervals as (StartTime, EndTime) tuples.
    /// Ignores incomplete or invalid intervals (null or negative duration).
    /// </summary>
    private static IEnumerable<(DateTime Start, DateTime End)> GetWorkIntervals(PlanRegistration pr)
    {
        var intervals = new (DateTime?, DateTime?)[]
        {
            (pr.Start1StartedAt, pr.Stop1StoppedAt),
            (pr.Start2StartedAt, pr.Stop2StoppedAt),
            (pr.Start3StartedAt, pr.Stop3StoppedAt),
            (pr.Start4StartedAt, pr.Stop4StoppedAt),
            (pr.Start5StartedAt, pr.Stop5StoppedAt)
        };

        foreach (var (start, end) in intervals)
        {
            if (start.HasValue && end.HasValue && start.Value < end.Value)
            {
                yield return (start.Value, end.Value);
            }
        }
    }

    /// <summary>
    /// Single source of truth for every pause stamp pair on a PlanRegistration.
    /// Enumerates Pause1-5, Pause10-19, Pause20-29, Pause100-102, Pause200-202
    /// — 31 pairs total. Order mirrors the frontend's
    /// workday-entity-dialog.component.ts:getPauseTimestampPairs so the
    /// backend↔frontend mapping is easy to audit.
    ///
    /// Returns raw nullable pairs so callers can distinguish "no stamp"
    /// from "stamp with zero/negative duration"; both
    /// <see cref="GetPauseIntervals"/> and
    /// <see cref="AggregatePauseMinutes"/> consume this enumerator.
    /// </summary>
    private static IEnumerable<(DateTime? StartedAt, DateTime? StoppedAt)> EnumeratePauseStampPairs(PlanRegistration pr)
    {
        // Delegate to the per-shift enumerator so there is ONE authoritative
        // list of the 31 pause columns. The pairs are regrouped by shift
        // (shift 1: Pause1,10-19,100-102; shift 2: Pause2,20-29,200-202;
        // shifts 3-5: Pause3/4/5) rather than the old flat 1-5/10-29/100-102/
        // 200-202 order, but the only consumer (GetPauseIntervals →
        // CalculateTotalSeconds) sums durations and is order-independent.
        for (var shift = 1; shift <= 5; shift++)
        {
            foreach (var pair in EnumerateShiftPauseStampPairs(pr, shift))
            {
                yield return pair;
            }
        }
    }

    /// <summary>
    /// Enumerates the pause stamp pairs that belong to ONE shift.
    /// A shift can carry pauses in several slot columns:
    ///   shift 1 → Pause1 (primary), Pause10..Pause19, Pause100..Pause102
    ///   shift 2 → Pause2 (primary), Pause20..Pause29, Pause200..Pause202
    ///   shift 3 → Pause3 (single slot)
    ///   shift 4 → Pause4 (single slot)
    ///   shift 5 → Pause5 (single slot)
    /// The primary slot is always yielded first so callers that need the
    /// "primary only" semantics (e.g. legacy-fallback) can take the first pair.
    /// </summary>
    private static IEnumerable<(DateTime? StartedAt, DateTime? StoppedAt)> EnumerateShiftPauseStampPairs(PlanRegistration pr, int shift)
    {
        switch (shift)
        {
            case 1:
                yield return (pr.Pause1StartedAt, pr.Pause1StoppedAt);
                yield return (pr.Pause10StartedAt, pr.Pause10StoppedAt);
                yield return (pr.Pause11StartedAt, pr.Pause11StoppedAt);
                yield return (pr.Pause12StartedAt, pr.Pause12StoppedAt);
                yield return (pr.Pause13StartedAt, pr.Pause13StoppedAt);
                yield return (pr.Pause14StartedAt, pr.Pause14StoppedAt);
                yield return (pr.Pause15StartedAt, pr.Pause15StoppedAt);
                yield return (pr.Pause16StartedAt, pr.Pause16StoppedAt);
                yield return (pr.Pause17StartedAt, pr.Pause17StoppedAt);
                yield return (pr.Pause18StartedAt, pr.Pause18StoppedAt);
                yield return (pr.Pause19StartedAt, pr.Pause19StoppedAt);
                yield return (pr.Pause100StartedAt, pr.Pause100StoppedAt);
                yield return (pr.Pause101StartedAt, pr.Pause101StoppedAt);
                yield return (pr.Pause102StartedAt, pr.Pause102StoppedAt);
                break;
            case 2:
                yield return (pr.Pause2StartedAt, pr.Pause2StoppedAt);
                yield return (pr.Pause20StartedAt, pr.Pause20StoppedAt);
                yield return (pr.Pause21StartedAt, pr.Pause21StoppedAt);
                yield return (pr.Pause22StartedAt, pr.Pause22StoppedAt);
                yield return (pr.Pause23StartedAt, pr.Pause23StoppedAt);
                yield return (pr.Pause24StartedAt, pr.Pause24StoppedAt);
                yield return (pr.Pause25StartedAt, pr.Pause25StoppedAt);
                yield return (pr.Pause26StartedAt, pr.Pause26StoppedAt);
                yield return (pr.Pause27StartedAt, pr.Pause27StoppedAt);
                yield return (pr.Pause28StartedAt, pr.Pause28StoppedAt);
                yield return (pr.Pause29StartedAt, pr.Pause29StoppedAt);
                yield return (pr.Pause200StartedAt, pr.Pause200StoppedAt);
                yield return (pr.Pause201StartedAt, pr.Pause201StoppedAt);
                yield return (pr.Pause202StartedAt, pr.Pause202StoppedAt);
                break;
            case 3:
                yield return (pr.Pause3StartedAt, pr.Pause3StoppedAt);
                break;
            case 4:
                yield return (pr.Pause4StartedAt, pr.Pause4StoppedAt);
                break;
            case 5:
                yield return (pr.Pause5StartedAt, pr.Pause5StoppedAt);
                break;
        }
    }

    /// <summary>
    /// The legacy 5-minute-tick integer pause field for a shift's primary slot.
    /// Pause{N}Id stores break in 5-minute ticks plus a +1 sentinel
    /// (Pause1Id = 1 means 0 min, Pause1Id = 4 means 15 min, etc.).
    /// </summary>
    private static int PrimaryPauseId(PlanRegistration pr, int shift) => shift switch
    {
        1 => pr.Pause1Id,
        2 => pr.Pause2Id,
        3 => pr.Pause3Id,
        4 => pr.Pause4Id,
        5 => pr.Pause5Id,
        _ => 0
    };

    private static readonly long FiveMinuteTicks = TimeSpan.FromMinutes(5).Ticks;

    /// <summary>
    /// Floors a DateTime down to its absolute 5-minute grid boundary on the
    /// timeline (NOT relative to the day) so the result is over-midnight safe.
    /// </summary>
    private static DateTime FloorTo5Min(DateTime dt)
        => new DateTime(dt.Ticks - (dt.Ticks % FiveMinuteTicks), dt.Kind);

    /// <summary>
    /// Canonical per-shift pause total in SECONDS — the single source of truth
    /// for every netto and display pause computation.
    ///
    /// Sums the contribution of EVERY populated pause slot that belongs to the
    /// shift (primary Pause{N} plus its sub-slots, see
    /// <see cref="EnumerateShiftPauseStampPairs"/>), where each slot contributes:
    ///   • <paramref name="useOneMinuteIntervals"/> == true  → the exact
    ///     (StoppedAt - StartedAt) delta in seconds (full precision).
    ///   • <paramref name="useOneMinuteIntervals"/> == false → the clock-tick
    ///     delta: floor BOTH endpoints to the absolute 5-minute grid and
    ///     difference them — floor(stop) - floor(start), a whole number of
    ///     5-minute units. A pause that stays inside one 5-min cell contributes
    ///     0; it adds 5 min for each 5-minute boundary it crosses.
    ///
    /// Fallback: when the shift has NO slot with both timestamps present (e.g.
    /// legacy admin-entered rows that only carry the integer field), falls back
    /// to the legacy 5-minute-tick value of the shift's primary slot only:
    /// (Pause{N}Id > 0 ? Pause{N}Id - 1 : 0) * 5 * 60 seconds.
    /// </summary>
    public static int ComputeShiftPauseSeconds(PlanRegistration r, int shift, bool useOneMinuteIntervals)
    {
        // Admin/manual pause override takes precedence: when set, it is the
        // authoritative total pause MINUTES for the shift. The recorded
        // Pause{N}StartedAt/StoppedAt sub-slots are preserved untouched in the DB
        // (documentation of what the worker actually did) but are not summed here.
        var overrideMinutes = GetShiftPauseOverrideMinutes(r, shift);
        if (overrideMinutes.HasValue)
        {
            return overrideMinutes.Value * 60;
        }

        long totalSeconds = 0;
        var hasTimestampedSlot = false;

        foreach (var (startedAt, stoppedAt) in EnumerateShiftPauseStampPairs(r, shift))
        {
            // A slot only counts as "measured" — and thus suppresses the
            // legacy-tick fallback — when BOTH endpoints are present, i.e. it is
            // a complete, measurable interval. A deliberately zero-duration
            // (start == stop) or invalid (stop < start) but COMPLETE pause still
            // counts: the worker stamped a real (if zero) pause, so the intended
            // contribution is 0 and the legacy field must not resurface.
            // An orphaned slot (only one endpoint — e.g. kiosk crash or partial
            // edit) is NOT a complete slot, so it does not suppress the fallback;
            // the row correctly falls back to the legacy Pause{N}Id tick value.
            if (startedAt.HasValue && stoppedAt.HasValue)
            {
                hasTimestampedSlot = true;
            }

            if (!startedAt.HasValue || !stoppedAt.HasValue || stoppedAt.Value <= startedAt.Value)
            {
                continue;
            }

            if (useOneMinuteIntervals)
            {
                totalSeconds += (long)(stoppedAt.Value - startedAt.Value).TotalSeconds;
            }
            else
            {
                var tickDelta = FloorTo5Min(stoppedAt.Value) - FloorTo5Min(startedAt.Value);
                totalSeconds += (long)tickDelta.TotalSeconds;
            }
        }

        if (!hasTimestampedSlot)
        {
            var pauseId = PrimaryPauseId(r, shift);
            return pauseId > 0 ? (pauseId - 1) * 5 * 60 : 0;
        }

        return (int)totalSeconds;
    }

    /// <summary>
    /// Read the per-shift admin/manual pause override (in minutes) from the
    /// registration. null = no override (compute pause from recorded slots);
    /// non-null = authoritative total pause minutes for that shift.
    /// </summary>
    public static int? GetShiftPauseOverrideMinutes(PlanRegistration r, int shift)
    {
        return shift switch
        {
            1 => r.Pause1OverrideMinutes,
            2 => r.Pause2OverrideMinutes,
            3 => r.Pause3OverrideMinutes,
            4 => r.Pause4OverrideMinutes,
            5 => r.Pause5OverrideMinutes,
            _ => null
        };
    }

    /// <summary>
    /// Set the per-shift admin/manual pause override (in minutes) on the
    /// registration. null reverts to compute-from-slots.
    /// </summary>
    public static void SetShiftPauseOverrideMinutes(PlanRegistration r, int shift, int? minutes)
    {
        switch (shift)
        {
            case 1: r.Pause1OverrideMinutes = minutes; break;
            case 2: r.Pause2OverrideMinutes = minutes; break;
            case 3: r.Pause3OverrideMinutes = minutes; break;
            case 4: r.Pause4OverrideMinutes = minutes; break;
            case 5: r.Pause5OverrideMinutes = minutes; break;
        }
    }

    /// <summary>
    /// READ projection (Approach C): when a shift carries a pause override,
    /// rewrite the OUTGOING DTO so the (unchanged) mobile app, which displays the
    /// pause by summing the served Pause{N}StartedAt/StoppedAt timestamps, shows
    /// the override duration. For each overridden shift the DTO gets a single
    /// synthesized pause pair (anchored at the shift start, or midnight + the
    /// 5-min-grid Start{N}Id when no start timestamp exists) and ALL of that
    /// shift's sub-slot pause timestamps are emptied. The override minutes are
    /// also surfaced on the DTO for the web dialog.
    ///
    /// CRITICAL: this mutates the response DTO only. The DB entity
    /// (<paramref name="source"/>) is read-only here and never written.
    /// </summary>
    public static void ProjectPauseOverridesOntoDto(
        TimePlanningPlanningPrDayModel model,
        PlanRegistration source)
    {
        for (var shift = 1; shift <= 5; shift++)
        {
            var overrideMinutes = GetShiftPauseOverrideMinutes(source, shift);
            // Surface the raw override on the DTO regardless (web dialog read).
            SetDtoPauseOverrideMinutes(model, shift, overrideMinutes);

            if (!overrideMinutes.HasValue)
            {
                continue;
            }

            var (pauseStart, pauseStop) = ComputeOverridePausePair(source, shift, overrideMinutes.Value);

            EmptyShiftPauseStampsOnDto(model, shift);
            SetDtoPrimaryPause(model, shift, pauseStart, pauseStop);

            // Edit round-trip consistency (FIX 1b): reflect the override in the
            // legacy coarse fields the client round-trips so a re-save without a
            // pause change does NOT re-trigger the write-path inference. The read
            // path mirrors the entity as Break{N}Shift = Pause{N}Id (raw coarse
            // tick) and the DTO's Pause{N}Id = tick - 1; the write change-detection
            // compares model.Break{N}Shift against the pre-edit entity Pause{N}Id.
            // So the displayed/edited break tick must equal the override's coarse
            // tick: (overrideMinutes / 5) + 1.
            var coarseTick = (overrideMinutes.Value / 5) + 1;
            SetDtoBreakShift(model, shift, coarseTick);
            SetDtoPauseId(model, shift, coarseTick > 0 ? coarseTick - 1 : 0);
        }
    }

    /// <summary>
    /// READ projection for the TODAY (working-hours) path — same Approach C
    /// contract as the history overload, but onto the
    /// <see cref="TimePlanningWorkingHoursModel"/> the gRPC ReadWorkingHours
    /// response is mapped from. DB entity (<paramref name="source"/>) is
    /// read-only.
    /// </summary>
    public static void ProjectPauseOverridesOntoWorkingHours(
        TimePlanningWorkingHoursModel model,
        PlanRegistration source)
    {
        for (var shift = 1; shift <= 5; shift++)
        {
            var overrideMinutes = GetShiftPauseOverrideMinutes(source, shift);
            if (!overrideMinutes.HasValue)
            {
                continue;
            }

            var (pauseStart, pauseStop) = ComputeOverridePausePair(source, shift, overrideMinutes.Value);

            EmptyShiftPauseStampsOnWorkingHours(model, shift);
            SetWorkingHoursPrimaryPause(model, shift, pauseStart, pauseStop);

            // Edit round-trip consistency (FIX 1b): the working-hours model has no
            // Break{N}Shift / Pause{N}Id; its legacy coarse pause field is
            // Shift{N}Pause (read path sets it from Pause{N}Id). Reflect the
            // override there as the coarse tick so a re-save round-trips stably.
            SetWorkingHoursShiftPause(model, shift, (overrideMinutes.Value / 5) + 1);
        }
    }

    /// <summary>
    /// Synthesized single pause pair for an overridden shift (FIX simplifier): the
    /// pause is anchored at the shift's start timestamp, or — when no start stamp
    /// exists — at midnight + the 5-min-grid Start{N}Id offset (else midnight), and
    /// runs for the override's whole minutes. Shared by both READ projection
    /// overloads so the anchor math stays in one place.
    /// </summary>
    private static (DateTime Start, DateTime Stop) ComputeOverridePausePair(
        PlanRegistration source, int shift, int overrideMinutes)
    {
        var midnight = source.Date.Date;
        var (shiftStartStamp, startId) = GetShiftStartAnchor(source, shift);
        var pauseStart = shiftStartStamp
                         ?? (startId > 0 ? midnight.AddMinutes((startId - 1) * 5) : midnight);
        var pauseStop = pauseStart.AddMinutes(overrideMinutes);
        return (pauseStart, pauseStop);
    }

    private static void SetWorkingHoursShiftPause(TimePlanningWorkingHoursModel m, int shift, int coarseTick)
    {
        switch (shift)
        {
            case 1: m.Shift1Pause = coarseTick; break;
            case 2: m.Shift2Pause = coarseTick; break;
            case 3: m.Shift3Pause = coarseTick; break;
            case 4: m.Shift4Pause = coarseTick; break;
            case 5: m.Shift5Pause = coarseTick; break;
        }
    }

    private static void SetWorkingHoursPrimaryPause(TimePlanningWorkingHoursModel m, int shift, DateTime start, DateTime stop)
    {
        switch (shift)
        {
            case 1: m.Pause1StartedAt = start; m.Pause1StoppedAt = stop; break;
            case 2: m.Pause2StartedAt = start; m.Pause2StoppedAt = stop; break;
            case 3: m.Pause3StartedAt = start; m.Pause3StoppedAt = stop; break;
            case 4: m.Pause4StartedAt = start; m.Pause4StoppedAt = stop; break;
            case 5: m.Pause5StartedAt = start; m.Pause5StoppedAt = stop; break;
        }
    }

    private static void EmptyShiftPauseStampsOnWorkingHours(TimePlanningWorkingHoursModel m, int shift)
    {
        switch (shift)
        {
            case 1:
                m.Pause10StartedAt = null; m.Pause10StoppedAt = null;
                m.Pause11StartedAt = null; m.Pause11StoppedAt = null;
                m.Pause12StartedAt = null; m.Pause12StoppedAt = null;
                m.Pause13StartedAt = null; m.Pause13StoppedAt = null;
                m.Pause14StartedAt = null; m.Pause14StoppedAt = null;
                m.Pause15StartedAt = null; m.Pause15StoppedAt = null;
                m.Pause16StartedAt = null; m.Pause16StoppedAt = null;
                m.Pause17StartedAt = null; m.Pause17StoppedAt = null;
                m.Pause18StartedAt = null; m.Pause18StoppedAt = null;
                m.Pause19StartedAt = null; m.Pause19StoppedAt = null;
                m.Pause100StartedAt = null; m.Pause100StoppedAt = null;
                m.Pause101StartedAt = null; m.Pause101StoppedAt = null;
                m.Pause102StartedAt = null; m.Pause102StoppedAt = null;
                break;
            case 2:
                m.Pause20StartedAt = null; m.Pause20StoppedAt = null;
                m.Pause21StartedAt = null; m.Pause21StoppedAt = null;
                m.Pause22StartedAt = null; m.Pause22StoppedAt = null;
                m.Pause23StartedAt = null; m.Pause23StoppedAt = null;
                m.Pause24StartedAt = null; m.Pause24StoppedAt = null;
                m.Pause25StartedAt = null; m.Pause25StoppedAt = null;
                m.Pause26StartedAt = null; m.Pause26StoppedAt = null;
                m.Pause27StartedAt = null; m.Pause27StoppedAt = null;
                m.Pause28StartedAt = null; m.Pause28StoppedAt = null;
                m.Pause29StartedAt = null; m.Pause29StoppedAt = null;
                m.Pause200StartedAt = null; m.Pause200StoppedAt = null;
                m.Pause201StartedAt = null; m.Pause201StoppedAt = null;
                m.Pause202StartedAt = null; m.Pause202StoppedAt = null;
                break;
        }
    }

    private static (DateTime? Stamp, int StartId) GetShiftStartAnchor(PlanRegistration r, int shift) => shift switch
    {
        1 => (r.Start1StartedAt, r.Start1Id),
        2 => (r.Start2StartedAt, r.Start2Id),
        3 => (r.Start3StartedAt, r.Start3Id),
        4 => (r.Start4StartedAt, r.Start4Id),
        5 => (r.Start5StartedAt, r.Start5Id),
        _ => (null, 0)
    };

    private static void SetDtoPauseOverrideMinutes(TimePlanningPlanningPrDayModel m, int shift, int? minutes)
    {
        switch (shift)
        {
            case 1: m.Pause1OverrideMinutes = minutes; break;
            case 2: m.Pause2OverrideMinutes = minutes; break;
            case 3: m.Pause3OverrideMinutes = minutes; break;
            case 4: m.Pause4OverrideMinutes = minutes; break;
            case 5: m.Pause5OverrideMinutes = minutes; break;
        }
    }

    private static void SetDtoBreakShift(TimePlanningPlanningPrDayModel m, int shift, int coarseTick)
    {
        switch (shift)
        {
            case 1: m.Break1Shift = coarseTick; break;
            case 2: m.Break2Shift = coarseTick; break;
            case 3: m.Break3Shift = coarseTick; break;
            case 4: m.Break4Shift = coarseTick; break;
            case 5: m.Break5Shift = coarseTick; break;
        }
    }

    private static void SetDtoPauseId(TimePlanningPlanningPrDayModel m, int shift, int pauseId)
    {
        switch (shift)
        {
            case 1: m.Pause1Id = pauseId; break;
            case 2: m.Pause2Id = pauseId; break;
            case 3: m.Pause3Id = pauseId; break;
            case 4: m.Pause4Id = pauseId; break;
            case 5: m.Pause5Id = pauseId; break;
        }
    }

    private static void SetDtoPrimaryPause(TimePlanningPlanningPrDayModel m, int shift, DateTime start, DateTime stop)
    {
        switch (shift)
        {
            case 1: m.Pause1StartedAt = start; m.Pause1StoppedAt = stop; break;
            case 2: m.Pause2StartedAt = start; m.Pause2StoppedAt = stop; break;
            case 3: m.Pause3StartedAt = start; m.Pause3StoppedAt = stop; break;
            case 4: m.Pause4StartedAt = start; m.Pause4StoppedAt = stop; break;
            case 5: m.Pause5StartedAt = start; m.Pause5StoppedAt = stop; break;
        }
    }

    private static void EmptyShiftPauseStampsOnDto(TimePlanningPlanningPrDayModel m, int shift)
    {
        switch (shift)
        {
            case 1:
                m.Pause10StartedAt = null; m.Pause10StoppedAt = null;
                m.Pause11StartedAt = null; m.Pause11StoppedAt = null;
                m.Pause12StartedAt = null; m.Pause12StoppedAt = null;
                m.Pause13StartedAt = null; m.Pause13StoppedAt = null;
                m.Pause14StartedAt = null; m.Pause14StoppedAt = null;
                m.Pause15StartedAt = null; m.Pause15StoppedAt = null;
                m.Pause16StartedAt = null; m.Pause16StoppedAt = null;
                m.Pause17StartedAt = null; m.Pause17StoppedAt = null;
                m.Pause18StartedAt = null; m.Pause18StoppedAt = null;
                m.Pause19StartedAt = null; m.Pause19StoppedAt = null;
                m.Pause100StartedAt = null; m.Pause100StoppedAt = null;
                m.Pause101StartedAt = null; m.Pause101StoppedAt = null;
                m.Pause102StartedAt = null; m.Pause102StoppedAt = null;
                break;
            case 2:
                m.Pause20StartedAt = null; m.Pause20StoppedAt = null;
                m.Pause21StartedAt = null; m.Pause21StoppedAt = null;
                m.Pause22StartedAt = null; m.Pause22StoppedAt = null;
                m.Pause23StartedAt = null; m.Pause23StoppedAt = null;
                m.Pause24StartedAt = null; m.Pause24StoppedAt = null;
                m.Pause25StartedAt = null; m.Pause25StoppedAt = null;
                m.Pause26StartedAt = null; m.Pause26StoppedAt = null;
                m.Pause27StartedAt = null; m.Pause27StoppedAt = null;
                m.Pause28StartedAt = null; m.Pause28StoppedAt = null;
                m.Pause29StartedAt = null; m.Pause29StoppedAt = null;
                m.Pause200StartedAt = null; m.Pause200StoppedAt = null;
                m.Pause201StartedAt = null; m.Pause201StoppedAt = null;
                m.Pause202StartedAt = null; m.Pause202StoppedAt = null;
                break;
            // Shifts 3-5 have no sub-slots; the primary pause pair is rewritten
            // by SetDtoPrimaryPause.
        }
    }

    /// <summary>
    /// Extract pause intervals from PlanRegistration.
    /// Consumes <see cref="EnumeratePauseStampPairs"/> and filters out incomplete
    /// or invalid intervals (null endpoints or non-positive duration).
    /// </summary>
    private static IEnumerable<(DateTime Start, DateTime End)> GetPauseIntervals(PlanRegistration pr)
    {
        foreach (var (start, end) in EnumeratePauseStampPairs(pr))
        {
            if (start.HasValue && end.HasValue && start.Value < end.Value)
            {
                yield return (start.Value, end.Value);
            }
        }
    }

    /// <summary>
    /// Calculate total seconds for a collection of time intervals.
    /// </summary>
    private static long CalculateTotalSeconds(IEnumerable<(DateTime Start, DateTime End)> intervals)
    {
        return intervals.Sum(interval => (long)(interval.End - interval.Start).TotalSeconds);
    }

    /// <summary>
    /// Classify the day and return the day code.
    /// Returns: SUNDAY, SATURDAY, HOLIDAY, GRUNDLOVSDAG, or WEEKDAY
    /// Priority: GRUNDLOVSDAG > HOLIDAY > SUNDAY > SATURDAY > WEEKDAY
    /// </summary>
    private static string GetDayCode(DateTime date)
    {
        // Check if it's Grundlovsdag (June 5th) - highest priority
        if (date.Month == 6 && date.Day == 5)
        {
            return "GRUNDLOVSDAG";
        }

        // Check against holiday configuration for official holidays
        if (IsOfficialHoliday(date))
        {
            return "HOLIDAY";
        }

        var dayOfWeek = date.DayOfWeek;

        if (dayOfWeek == DayOfWeek.Sunday)
        {
            return "SUNDAY";
        }

        if (dayOfWeek == DayOfWeek.Saturday)
        {
            return "SATURDAY";
        }

        return "WEEKDAY";
    }

    /// <summary>
    /// Check if a date is an official Danish holiday.
    /// Loads holiday data from the danish_holidays_2025_2030.json configuration file.
    /// </summary>
    public static bool IsOfficialHoliday(DateTime date)
    {
        var config = LoadHolidayConfiguration();

        // Normalize the date to midnight for comparison
        var midnight = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0);

        // Check if the date exists in our holiday configuration
        return config.Holidays?.Any(h => h.ParsedDate == midnight) ?? false;
    }

    /// <summary>
    /// Check if a date is Grundlovsdag (Constitution Day - June 5th).
    /// </summary>
    public static bool IsGrundlovsdag(DateTime date)
    {
        return date.Month == 6 && date.Day == 5;
    }

    /// <summary>
    /// Compute and update all seconds-based time tracking fields on a PlanRegistration.
    /// This calculates work time, pause time, and effective net hours based on actual timestamps.
    /// This method does NOT persist changes - caller must save the PlanRegistration.
    /// </summary>
    /// <param name="planRegistration">The plan registration to update</param>
    public static void ComputeTimeTrackingFields(PlanRegistration planRegistration)
    {
        // Calculate work intervals and total work seconds
        var workIntervals = GetWorkIntervals(planRegistration);
        var totalWorkSeconds = CalculateTotalSeconds(workIntervals);

        // Calculate total pause seconds via the canonical per-shift method so
        // ALL populated pause slots (primary Pause{N} plus the multi-pause
        // sub-slots Pause10/11/.., Pause20/21/..) are deducted, not just the
        // legacy primary Pause{N}Id. This is the 5-minute (flag-off) grid path:
        // each timestamped slot contributes its floor-to-5min clock-tick delta,
        // falling back per-shift to the legacy Pause{N}Id tick value only when a
        // shift has no timestamped pause slots.
        long totalPauseSeconds = 0;
        for (var shift = 1; shift <= 5; shift++)
        {
            totalPauseSeconds += ComputeShiftPauseSeconds(planRegistration, shift, useOneMinuteIntervals: false);
        }

        // Net work seconds = total work - total pause (cannot be negative)
        var netWorkSeconds = Math.Max(0, totalWorkSeconds - totalPauseSeconds);

        // Set NettoHoursInSeconds and NettoHours (as double in hours)
        planRegistration.NettoHoursInSeconds = (int)netWorkSeconds;
        planRegistration.NettoHours = netWorkSeconds / 3600.0;

        // Calculate effective net hours (considering override if active)
        if (planRegistration.NettoHoursOverrideActive)
        {
            // If override is active, use the override value
            planRegistration.EffectiveNetHoursInSeconds = (int)(planRegistration.NettoHoursOverride * 3600);
        }
        else
        {
            // Otherwise, effective = actual net
            planRegistration.EffectiveNetHoursInSeconds = (int)netWorkSeconds;
        }

        // Set day classification flags
        var midnight = new DateTime(planRegistration.Date.Year, planRegistration.Date.Month,
            planRegistration.Date.Day, 0, 0, 0);
        planRegistration.IsSaturday = midnight.DayOfWeek == DayOfWeek.Saturday;
        planRegistration.IsSunday = midnight.DayOfWeek == DayOfWeek.Sunday;

        // Set DayCode for pay line generation
        var dayCode = GetDayCode(midnight);
        // Note: DayCode field may or may not exist on PlanRegistration in current version
        // If it doesn't exist, this will need to be stored separately or added to the entity

        // Note: Break policy splitting (paid/unpaid) would be done here if break policy rules exist
        // For now, we'll leave that for future implementation when BreakPolicy entities are fully defined
    }

    /// <summary>
    /// Mark a PlanRegistration as having been calculated by the rule engine.
    /// Sets RuleEngineCalculated flag and timestamp.
    /// </summary>
    /// <param name="planRegistration">The plan registration to mark</param>
    public static void MarkAsRuleEngineCalculated(PlanRegistration planRegistration)
    {
        planRegistration.RuleEngineCalculated = true;
        planRegistration.RuleEngineCalculatedAt = DateTime.UtcNow;
    }

}