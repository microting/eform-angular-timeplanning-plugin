using System;
using System.Collections.Generic;
using System.Linq;
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
using TimePlanning.Pn.Infrastructure.Models.Planning;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.Index;
using TimePlanning.Pn.Services.TimePlanningPlanningService;
using AssignedSite = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using Site = Microting.eForm.Infrastructure.Data.Entities.Site;

namespace TimePlanning.Pn.Infrastructure.Helpers;

public static class PlanRegistrationHelper
{
    public static PlanRegistration CalculatePauseAutoBreakCalculationActive(
        AssignedSite assignedSite, PlanRegistration planning)
    {
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


    public static async Task<TimePlanningPlanningModel> UpdatePlanRegistrationsInPeriod(
        List<PlanRegistration> planningsInPeriod,
        TimePlanningPlanningModel siteModel,
        TimePlanningPnDbContext dbContext,
        AssignedSite dbAssignedSite,
        ILogger<TimePlanningPlanningService> logger,
        Site site,
        DateTime midnightOfDateFrom,
        DateTime midnightOfDateTo,
        IPluginDbOptions<TimePlanningBaseSettings> options
        )
    {
        var tainted = false;
        var settingsDayOfPayment = options.Value.DayOfPayment == 0 ? 20 : options.Value.DayOfPayment;
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

            if (planRegistration.Start1Id > 289)
            {
                // FIXME: This is a workaround, it should be removed when the frontend is fixed.
                planRegistration.Start1Id /= 5 + 1;
                planRegistration.Start1StartedAt = planRegistration.Date.AddMinutes(planRegistration.Start1Id * 5);
            }

            if (planRegistration.Stop1Id > 289 )
            {
                // FIXME: This is a workaround, it should be removed when the frontend is fixed.
                planRegistration.Stop1Id /= 5 + 1;
                planRegistration.Stop1StoppedAt = planRegistration.Date.AddMinutes(planRegistration.Stop1Id * 5);
            }

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
                                var splitList = planRegistration.PlanText.Split(';');
                                var firsSplit = splitList[0];

                                var regex = new Regex(@"(.*)-(.*)\/(.*)");
                                var match = regex.Match(firsSplit);
                                if (match.Captures.Count == 0)
                                {
                                    regex = new Regex(@"(.*)-(.*)");
                                    match = regex.Match(firsSplit);

                                    if (match.Captures.Count == 1)
                                    {
                                        var firstPart = match.Groups[1].Value;
                                        var firstPartSplit =
                                            firstPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                        var firstPartHours = int.Parse(firstPartSplit[0]);
                                        var firstPartMinutes =
                                            firstPartSplit.Length > 1 ? int.Parse(firstPartSplit[1]) : 0;
                                        var firstPartTotalMinutes = firstPartHours * 60 + firstPartMinutes;
                                        var secondPart = match.Groups[2].Value;
                                        var secondPartSplit =
                                            secondPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                        var secondPartHours = int.Parse(secondPartSplit[0]);
                                        var secondPartMinutes =
                                            secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                                        var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                                        planRegistration.PlannedStartOfShift1 = firstPartTotalMinutes;
                                        planRegistration.PlannedEndOfShift1 = secondPartTotalMinutes;

                                        if (match.Groups.Count == 4)
                                        {
                                            var breakPart = match.Groups[3].Value.Replace(",", ".").Trim();

                                            var breakPartMinutes = BreakTimeCalculator(breakPart);

                                            planRegistration.PlannedBreakOfShift1 = breakPartMinutes;
                                        }
                                        else
                                        {
                                            planRegistration.PlannedBreakOfShift1 = 0;
                                        }
                                    }
                                }

                                if (match.Captures.Count == 1)
                                {
                                    var firstPart = match.Groups[1].Value;
                                    var firstPartSplit =
                                        firstPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                    var firstPartHours = int.Parse(firstPartSplit[0]);
                                    var firstPartMinutes = firstPartSplit.Length > 1 ? int.Parse(firstPartSplit[1]) : 0;
                                    var firstPartTotalMinutes = firstPartHours * 60 + firstPartMinutes;
                                    var secondPart = match.Groups[2].Value;
                                    var secondPartSplit =
                                        secondPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                    var secondPartHours = int.Parse(secondPartSplit[0]);
                                    var secondPartMinutes =
                                        secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                                    var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                                    planRegistration.PlannedStartOfShift1 = firstPartTotalMinutes;
                                    planRegistration.PlannedEndOfShift1 = secondPartTotalMinutes;

                                    if (match.Groups.Count == 4)
                                    {
                                        var breakPart = match.Groups[3].Value.Replace(",", ".").Trim();

                                        var breakPartMinutes = BreakTimeCalculator(breakPart);

                                        planRegistration.PlannedBreakOfShift1 = breakPartMinutes;
                                    }
                                    else
                                    {
                                        planRegistration.PlannedBreakOfShift1 = 0;
                                    }
                                }

                                if (splitList.Length > 1)
                                {
                                    var secondSplit = splitList[1];
                                    regex = new Regex(@"(.*)-(.*)\/(.*)");
                                    match = regex.Match(secondSplit);
                                    if (match.Captures.Count == 0)
                                    {
                                        regex = new Regex(@"(.*)-(.*)");
                                        match = regex.Match(secondSplit);

                                        if (match.Captures.Count == 1)
                                        {
                                            var firstPart = match.Groups[1].Value;
                                            var firstPartSplit =
                                                firstPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                            var firstPartHours = int.Parse(firstPartSplit[0]);
                                            var firstPartMinutes =
                                                firstPartSplit.Length > 1 ? int.Parse(firstPartSplit[1]) : 0;
                                            var firstPartTotalMinutes = firstPartHours * 60 + firstPartMinutes;
                                            var secondPart = match.Groups[2].Value;
                                            var secondPartSplit =
                                                secondPart.Split(['.', ':', '½'],
                                                    StringSplitOptions.RemoveEmptyEntries);
                                            var secondPartHours = int.Parse(secondPartSplit[0]);
                                            var secondPartMinutes =
                                                secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                                            var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                                            planRegistration.PlannedStartOfShift2 = firstPartTotalMinutes;
                                            planRegistration.PlannedEndOfShift2 = secondPartTotalMinutes;

                                            if (match.Groups.Count == 4)
                                            {
                                                var breakPart = match.Groups[3].Value.Replace(",", ".").Trim();

                                                var breakPartMinutes = BreakTimeCalculator(breakPart);

                                                planRegistration.PlannedBreakOfShift2 = breakPartMinutes;
                                            }
                                            else
                                            {
                                                planRegistration.PlannedBreakOfShift2 = 0;
                                            }
                                        }
                                    }

                                    if (match.Captures.Count == 1)
                                    {
                                        var firstPart = match.Groups[1].Value;
                                        var firstPartSplit =
                                            firstPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                        var firstPartHours = int.Parse(firstPartSplit[0]);
                                        var firstPartMinutes =
                                            firstPartSplit.Length > 1 ? int.Parse(firstPartSplit[1]) : 0;
                                        var firstPartTotalMinutes = firstPartHours * 60 + firstPartMinutes;
                                        var secondPart = match.Groups[2].Value;
                                        var secondPartSplit =
                                            secondPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                        var secondPartHours = int.Parse(secondPartSplit[0]);
                                        var secondPartMinutes =
                                            secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                                        var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                                        planRegistration.PlannedStartOfShift2 = firstPartTotalMinutes;
                                        planRegistration.PlannedEndOfShift2 = secondPartTotalMinutes;

                                        if (match.Groups.Count == 4)
                                        {
                                            var breakPart = match.Groups[3].Value.Replace(",", ".").Trim();

                                            var breakPartMinutes = BreakTimeCalculator(breakPart);

                                            planRegistration.PlannedBreakOfShift2 = breakPartMinutes;
                                        }
                                        else
                                        {
                                            planRegistration.PlannedBreakOfShift2 = 0;
                                        }
                                    }
                                }

                                if (splitList.Length > 2)
                                {
                                    var thirdSplit = splitList[2];
                                    regex = new Regex(@"(.*)-(.*)\/(.*)");
                                    match = regex.Match(thirdSplit);
                                    if (match.Captures.Count == 0)
                                    {
                                        regex = new Regex(@"(.*)-(.*)");
                                        match = regex.Match(thirdSplit);

                                        if (match.Captures.Count == 1)
                                        {
                                            var firstPart = match.Groups[1].Value;
                                            var firstPartSplit =
                                                firstPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                            var firstPartHours = int.Parse(firstPartSplit[0]);
                                            var firstPartMinutes =
                                                firstPartSplit.Length > 1 ? int.Parse(firstPartSplit[1]) : 0;
                                            var firstPartTotalMinutes = firstPartHours * 60 + firstPartMinutes;
                                            var secondPart = match.Groups[2].Value;
                                            var secondPartSplit =
                                                secondPart.Split(['.', ':', '½'],
                                                    StringSplitOptions.RemoveEmptyEntries);
                                            var secondPartHours = int.Parse(secondPartSplit[0]);
                                            var secondPartMinutes =
                                                secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                                            var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                                            planRegistration.PlannedStartOfShift3 = firstPartTotalMinutes;
                                            planRegistration.PlannedEndOfShift3 = secondPartTotalMinutes;

                                            if (match.Groups.Count == 4)
                                            {
                                                var breakPart = match.Groups[3].Value.Replace(",", ".").Trim();
                                                var breakPartMinutes = BreakTimeCalculator(breakPart);

                                                planRegistration.PlannedBreakOfShift3 = breakPartMinutes;
                                            }
                                            else
                                            {
                                                planRegistration.PlannedBreakOfShift3 = 0;
                                            }
                                        }
                                    }
                                }

                                if (splitList.Length > 3)
                                {
                                    var fourthSplit = splitList[3];
                                    regex = new Regex(@"(.*)-(.*)\/(.*)");
                                    match = regex.Match(fourthSplit);
                                    if (match.Captures.Count == 0)
                                    {
                                        regex = new Regex(@"(.*)-(.*)");
                                        match = regex.Match(fourthSplit);

                                        if (match.Captures.Count == 1)
                                        {
                                            var firstPart = match.Groups[1].Value;
                                            var firstPartSplit =
                                                firstPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                            var firstPartHours = int.Parse(firstPartSplit[0]);
                                            var firstPartMinutes =
                                                firstPartSplit.Length > 1 ? int.Parse(firstPartSplit[1]) : 0;
                                            var firstPartTotalMinutes = firstPartHours * 60 + firstPartMinutes;
                                            var secondPart = match.Groups[2].Value;
                                            var secondPartSplit =
                                                secondPart.Split(['.', ':', '½'],
                                                    StringSplitOptions.RemoveEmptyEntries);
                                            var secondPartHours = int.Parse(secondPartSplit[0]);
                                            var secondPartMinutes =
                                                secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                                            var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                                            planRegistration.PlannedStartOfShift4 = firstPartTotalMinutes;
                                            planRegistration.PlannedEndOfShift4 = secondPartTotalMinutes;

                                            if (match.Groups.Count == 4)
                                            {
                                                var breakPart = match.Groups[3].Value.Replace(",", ".").Trim();
                                                var breakPartMinutes = BreakTimeCalculator(breakPart);

                                                planRegistration.PlannedBreakOfShift4 = breakPartMinutes;
                                            }
                                            else
                                            {
                                                planRegistration.PlannedBreakOfShift4 = 0;
                                            }
                                        }
                                    }
                                }

                                if (splitList.Length > 4)
                                {
                                    var fifthSplit = splitList[4];
                                    regex = new Regex(@"(.*)-(.*)\/(.*)");
                                    match = regex.Match(fifthSplit);
                                    if (match.Captures.Count == 0)
                                    {
                                        regex = new Regex(@"(.*)-(.*)");
                                        match = regex.Match(fifthSplit);

                                        if (match.Captures.Count == 1)
                                        {
                                            var firstPart = match.Groups[1].Value;
                                            var firstPartSplit =
                                                firstPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                            var firstPartHours = int.Parse(firstPartSplit[0]);
                                            var firstPartMinutes =
                                                firstPartSplit.Length > 1 ? int.Parse(firstPartSplit[1]) : 0;
                                            var firstPartTotalMinutes = firstPartHours * 60 + firstPartMinutes;
                                            var secondPart = match.Groups[2].Value;
                                            var secondPartSplit =
                                                secondPart.Split(['.', ':', '½'],
                                                    StringSplitOptions.RemoveEmptyEntries);
                                            var secondPartHours = int.Parse(secondPartSplit[0]);
                                            var secondPartMinutes =
                                                secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                                            var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                                            planRegistration.PlannedStartOfShift5 = firstPartTotalMinutes;
                                            planRegistration.PlannedEndOfShift5 = secondPartTotalMinutes;

                                            if (match.Groups.Count == 4)
                                            {
                                                var breakPart = match.Groups[3].Value.Replace(",", ".").Trim();

                                                var breakPartMinutes = BreakTimeCalculator(breakPart);

                                                planRegistration.PlannedBreakOfShift5 = breakPartMinutes;
                                            }
                                            else
                                            {
                                                planRegistration.PlannedBreakOfShift5 = 0;
                                            }
                                        }
                                    }
                                }

                                var calculatedPlanHoursInMinutes = 0;
                                var originalPlanHours = planRegistration.PlanHours;
                                if (planRegistration.PlannedStartOfShift1 != 0 &&
                                    planRegistration.PlannedEndOfShift1 != 0)
                                {
                                    calculatedPlanHoursInMinutes += planRegistration.PlannedEndOfShift1 -
                                                                    planRegistration.PlannedStartOfShift1 -
                                                                    planRegistration.PlannedBreakOfShift1;
                                    planRegistration.PlanHours = calculatedPlanHoursInMinutes / 60.0;
                                }

                                if (planRegistration.PlannedStartOfShift2 != 0 &&
                                    planRegistration.PlannedEndOfShift2 != 0)
                                {
                                    calculatedPlanHoursInMinutes += planRegistration.PlannedEndOfShift2 -
                                                                    planRegistration.PlannedStartOfShift2 -
                                                                    planRegistration.PlannedBreakOfShift2;
                                    planRegistration.PlanHours = calculatedPlanHoursInMinutes / 60.0;
                                }

                                if (planRegistration.PlannedStartOfShift3 != 0 &&
                                    planRegistration.PlannedEndOfShift3 != 0)
                                {
                                    calculatedPlanHoursInMinutes += planRegistration.PlannedEndOfShift3 -
                                                                    planRegistration.PlannedStartOfShift3 -
                                                                    planRegistration.PlannedBreakOfShift3;
                                    planRegistration.PlanHours = calculatedPlanHoursInMinutes / 60.0;
                                }

                                if (planRegistration.PlannedStartOfShift4 != 0 &&
                                    planRegistration.PlannedEndOfShift4 != 0)
                                {
                                    calculatedPlanHoursInMinutes += planRegistration.PlannedEndOfShift4 -
                                                                    planRegistration.PlannedStartOfShift4 -
                                                                    planRegistration.PlannedBreakOfShift4;
                                    planRegistration.PlanHours = calculatedPlanHoursInMinutes / 60.0;
                                }

                                if (planRegistration.PlannedStartOfShift5 != 0 &&
                                    planRegistration.PlannedEndOfShift5 != 0)
                                {
                                    calculatedPlanHoursInMinutes += planRegistration.PlannedEndOfShift5 -
                                                                    planRegistration.PlannedStartOfShift5 -
                                                                    planRegistration.PlannedBreakOfShift5;
                                    planRegistration.PlanHours = calculatedPlanHoursInMinutes / 60.0;
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

                        if (preTimePlanning != null)
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

                        if (preTimePlanning != null)
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

            var planningModel = new TimePlanningPlanningPrDayModel
            {
                Id = planRegistration.Id,
                SiteName = site.Name,
                Date = midnight,
                PlanText = planRegistration.PlanText,
                PlanHours = planRegistration.PlanHours,
                Message = planRegistration.MessageId,
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
                Start1StartedAt = dbAssignedSite.UseOneMinuteIntervals
                    ? planRegistration.Start1StartedAt
                    : (planRegistration.Start1Id == 0
                        ? null
                        : midnight.AddMinutes(
                            (planRegistration.Start1Id * 5) - 5)),
                Stop1StoppedAt = dbAssignedSite.UseOneMinuteIntervals
                    ? planRegistration.Stop1StoppedAt
                    : (planRegistration.Stop1Id == 0
                        ? null
                        : midnight.AddMinutes(
                            (planRegistration.Stop1Id * 5) - 5)),
                Start2StartedAt = dbAssignedSite.UseOneMinuteIntervals
                    ? planRegistration.Start2StartedAt
                    : (planRegistration.Start2Id == 0
                        ? null
                        : midnight.AddMinutes(
                            (planRegistration.Start2Id * 5) - 5)),
                Stop2StoppedAt = dbAssignedSite.UseOneMinuteIntervals
                    ? planRegistration.Stop2StoppedAt
                    : (planRegistration.Stop2Id == 0
                        ? null
                        : midnight.AddMinutes(
                            (planRegistration.Stop2Id * 5) - 5)),
                Start3StartedAt = dbAssignedSite.UseOneMinuteIntervals
                    ? planRegistration.Start3StartedAt
                    : (planRegistration.Start3Id == 0
                        ? null
                        : midnight.AddMinutes(
                            (planRegistration.Start3Id * 5) - 5)),
                Stop3StoppedAt = dbAssignedSite.UseOneMinuteIntervals
                    ? planRegistration.Stop3StoppedAt
                    : (planRegistration.Stop3Id == 0
                        ? null
                        : midnight.AddMinutes(
                            (planRegistration.Stop3Id * 5) - 5)),
                Start4StartedAt = dbAssignedSite.UseOneMinuteIntervals
                    ? planRegistration.Start4StartedAt
                    : (planRegistration.Start4Id == 0
                        ? null
                        : midnight.AddMinutes(
                            (planRegistration.Start4Id * 5) - 5)),
                Stop4StoppedAt = dbAssignedSite.UseOneMinuteIntervals
                    ? planRegistration.Stop4StoppedAt
                    : (planRegistration.Stop4Id == 0
                        ? null
                        : midnight.AddMinutes(
                            (planRegistration.Stop4Id * 5) - 5)),
                Start5StartedAt = dbAssignedSite.UseOneMinuteIntervals
                    ? planRegistration.Start5StartedAt
                    : (planRegistration.Start5Id == 0
                        ? null
                        : midnight.AddMinutes(
                            (planRegistration.Start5Id * 5) - 5)),
                Stop5StoppedAt = dbAssignedSite.UseOneMinuteIntervals
                    ? planRegistration.Stop5StoppedAt
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

            planningModel.PauseMinutes += planRegistration.Pause1Id > 0
                ? (planRegistration.Pause1Id * 5) - 5
                : 0;
            planningModel.PauseMinutes += planRegistration.Pause2Id > 0
                ? (planRegistration.Pause2Id * 5) - 5
                : 0;
            planningModel.PauseMinutes += planRegistration.Pause3Id > 0
                ? (planRegistration.Pause3Id * 5) - 5
                : 0;
            planningModel.PauseMinutes += planRegistration.Pause4Id > 0
                ? (planRegistration.Pause4Id * 5) - 5
                : 0;
            planningModel.PauseMinutes += planRegistration.Pause5Id > 0
                ? (planRegistration.Pause5Id * 5) - 5
                : 0;

            // planningModel.PauseMinutes = planningModel.PauseMinutes > 0 ? planningModel.PauseMinutes - 5 : 0;

            planningModel.CommentOffice = planRegistration.CommentOffice;
            planningModel.WorkerComment = planRegistration.WorkerComment;
            planningModel.PlanHoursMatched = Math.Abs(planRegistration.NettoHours - planRegistration.PlanHours) <= 0.00;

            planningModel.IsDoubleShift = planningModel.Start2StartedAt != planningModel.Stop2StoppedAt;
            planningModel.NettoHoursOverride = planRegistration.NettoHoursOverride;
            planningModel.NettoHoursOverrideActive = planRegistration.NettoHoursOverrideActive;

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
                            var splitList = planRegistration.PlanText.Split(';');
                            var firsSplit = splitList[0];

                            var regex = new Regex(@"(.*)-(.*)\/(.*)");
                            var match = regex.Match(firsSplit);
                            if (match.Captures.Count == 0)
                            {
                                regex = new Regex(@"(.*)-(.*)");
                                match = regex.Match(firsSplit);

                                if (match.Captures.Count == 1)
                                {
                                    var firstPart = match.Groups[1].Value;
                                    var firstPartSplit =
                                        firstPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                    var firstPartHours = int.Parse(firstPartSplit[0]);
                                    var firstPartMinutes = firstPartSplit.Length > 1 ? int.Parse(firstPartSplit[1]) : 0;
                                    var firstPartTotalMinutes = firstPartHours * 60 + firstPartMinutes;
                                    var secondPart = match.Groups[2].Value;
                                    var secondPartSplit =
                                        secondPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                    var secondPartHours = int.Parse(secondPartSplit[0]);
                                    var secondPartMinutes =
                                        secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                                    var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                                    planRegistration.PlannedStartOfShift1 = firstPartTotalMinutes;
                                    planRegistration.PlannedEndOfShift1 = secondPartTotalMinutes;

                                    if (match.Groups.Count == 4)
                                    {
                                        var breakPart = match.Groups[3].Value.Replace(",", ".").Trim();

                                        var breakPartMinutes = BreakTimeCalculator(breakPart);

                                        planRegistration.PlannedBreakOfShift1 = breakPartMinutes;
                                    }
                                    else
                                    {
                                        planRegistration.PlannedBreakOfShift1 = 0;
                                    }
                                }
                            }

                            if (match.Captures.Count == 1)
                            {
                                var firstPart = match.Groups[1].Value;
                                var firstPartSplit =
                                    firstPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                var firstPartHours = int.Parse(firstPartSplit[0]);
                                var firstPartMinutes = firstPartSplit.Length > 1 ? int.Parse(firstPartSplit[1]) : 0;
                                var firstPartTotalMinutes = firstPartHours * 60 + firstPartMinutes;
                                var secondPart = match.Groups[2].Value;
                                var secondPartSplit =
                                    secondPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                var secondPartHours = int.Parse(secondPartSplit[0]);
                                var secondPartMinutes =
                                    secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                                var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                                planRegistration.PlannedStartOfShift1 = firstPartTotalMinutes;
                                planRegistration.PlannedEndOfShift1 = secondPartTotalMinutes;

                                if (match.Groups.Count == 4)
                                {
                                    var breakPart = match.Groups[3].Value.Replace(",", ".").Trim();

                                    var breakPartMinutes = BreakTimeCalculator(breakPart);

                                    planRegistration.PlannedBreakOfShift1 = breakPartMinutes;
                                }
                                else
                                {
                                    planRegistration.PlannedBreakOfShift1 = 0;
                                }
                            }

                            if (splitList.Length > 1)
                            {
                                var secondSplit = splitList[1];
                                regex = new Regex(@"(.*)-(.*)\/(.*)");
                                match = regex.Match(secondSplit);
                                if (match.Captures.Count == 0)
                                {
                                    regex = new Regex(@"(.*)-(.*)");
                                    match = regex.Match(secondSplit);

                                    if (match.Captures.Count == 1)
                                    {
                                        var firstPart = match.Groups[1].Value;
                                        var firstPartSplit =
                                            firstPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                        var firstPartHours = int.Parse(firstPartSplit[0]);
                                        var firstPartMinutes =
                                            firstPartSplit.Length > 1 ? int.Parse(firstPartSplit[1]) : 0;
                                        var firstPartTotalMinutes = firstPartHours * 60 + firstPartMinutes;
                                        var secondPart = match.Groups[2].Value;
                                        var secondPartSplit =
                                            secondPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                        var secondPartHours = int.Parse(secondPartSplit[0]);
                                        var secondPartMinutes =
                                            secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                                        var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                                        planRegistration.PlannedStartOfShift2 = firstPartTotalMinutes;
                                        planRegistration.PlannedEndOfShift2 = secondPartTotalMinutes;

                                        if (match.Groups.Count == 4)
                                        {
                                            var breakPart = match.Groups[3].Value.Replace(",", ".").Trim();

                                            var breakPartMinutes = BreakTimeCalculator(breakPart);

                                            planRegistration.PlannedBreakOfShift2 = breakPartMinutes;
                                        }
                                        else
                                        {
                                            planRegistration.PlannedBreakOfShift2 = 0;
                                        }
                                    }
                                }

                                if (match.Captures.Count == 1)
                                {
                                    var firstPart = match.Groups[1].Value;
                                    var firstPartSplit =
                                        firstPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                    var firstPartHours = int.Parse(firstPartSplit[0]);
                                    var firstPartMinutes = firstPartSplit.Length > 1 ? int.Parse(firstPartSplit[1]) : 0;
                                    var firstPartTotalMinutes = firstPartHours * 60 + firstPartMinutes;
                                    var secondPart = match.Groups[2].Value;
                                    var secondPartSplit =
                                        secondPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                    var secondPartHours = int.Parse(secondPartSplit[0]);
                                    var secondPartMinutes =
                                        secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                                    var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                                    planRegistration.PlannedStartOfShift2 = firstPartTotalMinutes;
                                    planRegistration.PlannedEndOfShift2 = secondPartTotalMinutes;

                                    if (match.Groups.Count == 4)
                                    {
                                        var breakPart = match.Groups[3].Value.Replace(",", ".").Trim();

                                        var breakPartMinutes = BreakTimeCalculator(breakPart);

                                        planRegistration.PlannedBreakOfShift2 = breakPartMinutes;
                                    }
                                    else
                                    {
                                        planRegistration.PlannedBreakOfShift2 = 0;
                                    }
                                }
                            }

                            if (splitList.Length > 2)
                            {
                                var thirdSplit = splitList[2];
                                regex = new Regex(@"(.*)-(.*)\/(.*)");
                                match = regex.Match(thirdSplit);
                                if (match.Captures.Count == 0)
                                {
                                    regex = new Regex(@"(.*)-(.*)");
                                    match = regex.Match(thirdSplit);

                                    if (match.Captures.Count == 1)
                                    {
                                        var firstPart = match.Groups[1].Value;
                                        var firstPartSplit =
                                            firstPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                        var firstPartHours = int.Parse(firstPartSplit[0]);
                                        var firstPartMinutes =
                                            firstPartSplit.Length > 1 ? int.Parse(firstPartSplit[1]) : 0;
                                        var firstPartTotalMinutes = firstPartHours * 60 + firstPartMinutes;
                                        var secondPart = match.Groups[2].Value;
                                        var secondPartSplit =
                                            secondPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                        var secondPartHours = int.Parse(secondPartSplit[0]);
                                        var secondPartMinutes =
                                            secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                                        var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                                        planRegistration.PlannedStartOfShift3 = firstPartTotalMinutes;
                                        planRegistration.PlannedEndOfShift3 = secondPartTotalMinutes;

                                        if (match.Groups.Count == 4)
                                        {
                                            var breakPart = match.Groups[3].Value.Replace(",", ".").Trim();
                                            var breakPartMinutes = BreakTimeCalculator(breakPart);

                                            planRegistration.PlannedBreakOfShift3 = breakPartMinutes;
                                        }
                                        else
                                        {
                                            planRegistration.PlannedBreakOfShift3 = 0;
                                        }
                                    }
                                }
                            }

                            if (splitList.Length > 3)
                            {
                                var fourthSplit = splitList[3];
                                regex = new Regex(@"(.*)-(.*)\/(.*)");
                                match = regex.Match(fourthSplit);
                                if (match.Captures.Count == 0)
                                {
                                    regex = new Regex(@"(.*)-(.*)");
                                    match = regex.Match(fourthSplit);

                                    if (match.Captures.Count == 1)
                                    {
                                        var firstPart = match.Groups[1].Value;
                                        var firstPartSplit =
                                            firstPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                        var firstPartHours = int.Parse(firstPartSplit[0]);
                                        var firstPartMinutes =
                                            firstPartSplit.Length > 1 ? int.Parse(firstPartSplit[1]) : 0;
                                        var firstPartTotalMinutes = firstPartHours * 60 + firstPartMinutes;
                                        var secondPart = match.Groups[2].Value;
                                        var secondPartSplit =
                                            secondPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                        var secondPartHours = int.Parse(secondPartSplit[0]);
                                        var secondPartMinutes =
                                            secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                                        var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                                        planRegistration.PlannedStartOfShift4 = firstPartTotalMinutes;
                                        planRegistration.PlannedEndOfShift4 = secondPartTotalMinutes;

                                        if (match.Groups.Count == 4)
                                        {
                                            var breakPart = match.Groups[3].Value.Replace(",", ".").Trim();
                                            var breakPartMinutes = BreakTimeCalculator(breakPart);

                                            planRegistration.PlannedBreakOfShift4 = breakPartMinutes;
                                        }
                                        else
                                        {
                                            planRegistration.PlannedBreakOfShift4 = 0;
                                        }
                                    }
                                }
                            }

                            if (splitList.Length > 4)
                            {
                                var fifthSplit = splitList[4];
                                regex = new Regex(@"(.*)-(.*)\/(.*)");
                                match = regex.Match(fifthSplit);
                                if (match.Captures.Count == 0)
                                {
                                    regex = new Regex(@"(.*)-(.*)");
                                    match = regex.Match(fifthSplit);

                                    if (match.Captures.Count == 1)
                                    {
                                        var firstPart = match.Groups[1].Value;
                                        var firstPartSplit =
                                            firstPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                        var firstPartHours = int.Parse(firstPartSplit[0]);
                                        var firstPartMinutes =
                                            firstPartSplit.Length > 1 ? int.Parse(firstPartSplit[1]) : 0;
                                        var firstPartTotalMinutes = firstPartHours * 60 + firstPartMinutes;
                                        var secondPart = match.Groups[2].Value;
                                        var secondPartSplit =
                                            secondPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                        var secondPartHours = int.Parse(secondPartSplit[0]);
                                        var secondPartMinutes =
                                            secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                                        var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                                        planRegistration.PlannedStartOfShift5 = firstPartTotalMinutes;
                                        planRegistration.PlannedEndOfShift5 = secondPartTotalMinutes;

                                        if (match.Groups.Count == 4)
                                        {
                                            var breakPart = match.Groups[3].Value.Replace(",", ".").Trim();

                                            var breakPartMinutes = BreakTimeCalculator(breakPart);

                                            planRegistration.PlannedBreakOfShift5 = breakPartMinutes;
                                        }
                                        else
                                        {
                                            planRegistration.PlannedBreakOfShift5 = 0;
                                        }
                                    }
                                }
                            }

                            var calculatedPlanHoursInMinutes = 0;
                            var originalPlanHours = planRegistration.PlanHours;
                            if (planRegistration.PlannedStartOfShift1 != 0 && planRegistration.PlannedEndOfShift1 != 0)
                            {
                                calculatedPlanHoursInMinutes += planRegistration.PlannedEndOfShift1 -
                                                                planRegistration.PlannedStartOfShift1 -
                                                                planRegistration.PlannedBreakOfShift1;
                                planRegistration.PlanHours = calculatedPlanHoursInMinutes / 60.0;
                            }

                            if (planRegistration.PlannedStartOfShift2 != 0 && planRegistration.PlannedEndOfShift2 != 0)
                            {
                                calculatedPlanHoursInMinutes += planRegistration.PlannedEndOfShift2 -
                                                                planRegistration.PlannedStartOfShift2 -
                                                                planRegistration.PlannedBreakOfShift2;
                                planRegistration.PlanHours = calculatedPlanHoursInMinutes / 60.0;
                            }

                            if (planRegistration.PlannedStartOfShift3 != 0 && planRegistration.PlannedEndOfShift3 != 0)
                            {
                                calculatedPlanHoursInMinutes += planRegistration.PlannedEndOfShift3 -
                                                                planRegistration.PlannedStartOfShift3 -
                                                                planRegistration.PlannedBreakOfShift3;
                                planRegistration.PlanHours = calculatedPlanHoursInMinutes / 60.0;
                            }

                            if (planRegistration.PlannedStartOfShift4 != 0 && planRegistration.PlannedEndOfShift4 != 0)
                            {
                                calculatedPlanHoursInMinutes += planRegistration.PlannedEndOfShift4 -
                                                                planRegistration.PlannedStartOfShift4 -
                                                                planRegistration.PlannedBreakOfShift4;
                                planRegistration.PlanHours = calculatedPlanHoursInMinutes / 60.0;
                            }

                            if (planRegistration.PlannedStartOfShift5 != 0 && planRegistration.PlannedEndOfShift5 != 0)
                            {
                                calculatedPlanHoursInMinutes += planRegistration.PlannedEndOfShift5 -
                                                                planRegistration.PlannedStartOfShift5 -
                                                                planRegistration.PlannedBreakOfShift5;
                                planRegistration.PlanHours = calculatedPlanHoursInMinutes / 60.0;
                            }

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

                    if (preTimePlanning != null)
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

                    if (preTimePlanning != null)
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
        if (token != null)
        {
            var registrationDevice = await dbContext.RegistrationDevices
                .Where(x => x.Token == token).FirstOrDefaultAsync();
            if (registrationDevice == null)
            {
                return null;
            }
        }

        // var today = DateTime.UtcNow;
        var midnight = dateTime;

        var planRegistration = await dbContext.PlanRegistrations
            .Where(x => x.Date == midnight)
            .Where(x => x.SdkSitId == sdkSiteId)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync();

        if (planRegistration == null)
        {
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

        return timePlanningWorkingHoursModel;
        // return new OperationDataResult<TimePlanningWorkingHoursModel>(true, "Plan registration found",
        //     timePlanningWorkingHoursModel);
    }

}