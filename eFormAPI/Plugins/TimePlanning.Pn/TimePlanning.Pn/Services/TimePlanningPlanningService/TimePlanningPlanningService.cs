/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System.Text.RegularExpressions;
using Microting.EformAngularFrontendBase.Infrastructure.Data;
using Sentry;

namespace TimePlanning.Pn.Services.TimePlanningPlanningService;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Models.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using TimePlanningLocalizationService;

public class TimePlanningPlanningService(
    ILogger<TimePlanningPlanningService> logger,
    TimePlanningPnDbContext dbContext,
    IUserService userService,
    ITimePlanningLocalizationService localizationService,
    BaseDbContext baseDbContext,
    IEFormCoreService core)
    : ITimePlanningPlanningService
{
    public async Task<OperationDataResult<List<TimePlanningPlanningModel>>> Index(
        TimePlanningPlanningRequestModel model)
    {
        try
        {
            var sdkCore = await core.GetCore();
            var sdkDbContext = sdkCore.DbContextHelper.GetDbContext();
            var result = new List<TimePlanningPlanningModel>();
            var assignedSites =
                await dbContext.AssignedSites.Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .ToListAsync().ConfigureAwait(false);
            var midnightOfDateFrom = new DateTime(model.DateFrom!.Value.Year, model.DateFrom.Value.Month, model.DateFrom.Value.Day, 0, 0, 0);
            var midnightOfDateTo = new DateTime(model.DateTo!.Value.Year, model.DateTo.Value.Month, model.DateTo.Value.Day, 23, 59, 59);
            var todayMidnight = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0);
            var datesInPeriod = new List<DateTime>();
            var date = model.DateFrom;
            while (date <= model.DateTo)
            {
                datesInPeriod.Add(date.Value);
                date = date.Value.AddDays(1);
            }
            foreach (var dbAssignedSite in assignedSites)
            {
                var site = await sdkDbContext.Sites
                    .Where(x => x.MicrotingUid == dbAssignedSite.SiteId)
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .FirstOrDefaultAsync().ConfigureAwait(false);
                if (site == null)
                {
                    continue;
                }
                var siteModel = new TimePlanningPlanningModel
                {
                    SiteId = dbAssignedSite.SiteId,
                    SiteName = site.Name,
                    PlanningPrDayModels = new List<TimePlanningPlanningPrDayModel>()
                };

                // do a lookup in the baseDbContext.Users where the concat string of FirstName and LastName toLowerCase() is equal to the site.Name toLowerCase()
                // if we find a user, we take the user.EmailSha256 and set the siteModel.AvatarUrl to the gravatar url with the sha256
                var user = await baseDbContext.Users
                    .Where(x => (x.FirstName + " " + x.LastName).Replace(" ", "").ToLower() == site.Name.Replace(" ", "").ToLower())
                    .FirstOrDefaultAsync().ConfigureAwait(false);
                if (user != null)
                {
                    siteModel.AvatarUrl = user.ProfilePictureSnapshot != null
                        ? $"api/images/login-page-images?fileName={user.ProfilePictureSnapshot}"
                        : $"https://www.gravatar.com/avatar/{user.EmailSha256}?s=32&d=identicon";
                }

                var planningsInPeriod = await dbContext.PlanRegistrations
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.SdkSitId == dbAssignedSite.SiteId)
                    .Where(x => x.Date >= midnightOfDateFrom)
                    .Where(x => x.Date <= midnightOfDateTo)
                    .OrderBy(x => x.Date)
                    .ToListAsync().ConfigureAwait(false);

                var datesInPlannings = planningsInPeriod.Select(x => x.Date).ToList();
                var missingDates = new List<DateTime>();

                foreach (var dateTime in datesInPeriod)
                {
                    if (!datesInPlannings.Contains(dateTime))
                    {
                        missingDates.Add(dateTime);
                    }
                }

                foreach (var missingDate in missingDates)
                {
                    var newPlanRegistration = new PlanRegistration
                    {
                        Date = missingDate,
                        SdkSitId = dbAssignedSite.SiteId,
                        CreatedByUserId = userService.UserId,
                        UpdatedByUserId = userService.UserId
                    };

                    if (missingDate.Date <= todayMidnight)
                    {
                        var preTimePlanning =
                            await dbContext.PlanRegistrations.AsNoTracking()
                                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                                .Where(x => x.Date < missingDate
                                            && x.SdkSitId == dbAssignedSite.SiteId)
                                .OrderByDescending(x => x.Date)
                                .FirstOrDefaultAsync();

                        if (preTimePlanning != null)
                        {
                            newPlanRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                            newPlanRegistration.SumFlexEnd =
                                preTimePlanning.SumFlexEnd + newPlanRegistration.NettoHours -
                                newPlanRegistration.PlanHours -
                                newPlanRegistration.PaiedOutFlex;
                            newPlanRegistration.Flex = newPlanRegistration.NettoHours - newPlanRegistration.PlanHours;
                        }
                        else
                        {
                            newPlanRegistration.SumFlexEnd =
                                newPlanRegistration.NettoHours - newPlanRegistration.PlanHours -
                                newPlanRegistration.PaiedOutFlex;
                            newPlanRegistration.SumFlexStart = 0;
                            newPlanRegistration.Flex = newPlanRegistration.NettoHours - newPlanRegistration.PlanHours;
                        }
                    }

                    await newPlanRegistration.Create(dbContext);
                }

                if (missingDates.Count > 0)
                {
                    planningsInPeriod = await dbContext.PlanRegistrations
                        .AsNoTracking()
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .Where(x => x.SdkSitId == dbAssignedSite.SiteId)
                        .Where(x => x.Date >= midnightOfDateFrom)
                        .Where(x => x.Date <= midnightOfDateTo)
                        .OrderBy(x => x.Date)
                        .ToListAsync().ConfigureAwait(false);
                }

                foreach (var plan in planningsInPeriod)
                {
                    var planRegistration = await dbContext.PlanRegistrations.FirstAsync(x => x.Id == plan.Id);
                    var midnight = new DateTime(planRegistration.Date.Year, planRegistration.Date.Month, planRegistration.Date.Day, 0, 0, 0);

                    try
                    {
                        if (dbAssignedSite.UseGoogleSheetAsDefault)
                        {
                            if (!string.IsNullOrEmpty(planRegistration.PlanText))
                            {
                                if (planRegistration.Date > DateTime.Now && !planRegistration.PlanChangedByAdmin)
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
                                                var breakPartMinutes = breakPart switch
                                                {
                                                    "0.5" => 30,
                                                    ".5" => 30,
                                                    ".75" => 45,
                                                    "0.75" => 45,
                                                    "¾" => 45,
                                                    "½" => 30,
                                                    "1" => 60,
                                                    _ => 0
                                                };

                                                planRegistration.PlannedBreakOfShift1 = breakPartMinutes;
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
                                            var breakPartMinutes = breakPart switch
                                            {
                                                "0.5" => 30,
                                                ".5" => 30,
                                                ".75" => 45,
                                                "0.75" => 45,
                                                "¾" => 45,
                                                "½" => 30,
                                                "1" => 60,
                                                _ => 0
                                            };

                                            planRegistration.PlannedBreakOfShift1 = breakPartMinutes;
                                        }
                                    }

                                    if (splitList.Length == 2)
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
                                                    var breakPartMinutes = breakPart switch
                                                    {
                                                        "0.5" => 30,
                                                        ".5" => 30,
                                                        ".75" => 45,
                                                        "0.75" => 45,
                                                        "¾" => 45,
                                                        "½" => 30,
                                                        "1" => 60,
                                                        _ => 0
                                                    };

                                                    planRegistration.PlannedBreakOfShift2 = breakPartMinutes;
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
                                                var breakPartMinutes = breakPart switch
                                                {
                                                    "0.5" => 30,
                                                    ".5" => 30,
                                                    ".75" => 45,
                                                    "0.75" => 45,
                                                    "¾" => 45,
                                                    "½" => 30,
                                                    "1" => 60,
                                                    _ => 0
                                                };

                                                planRegistration.PlannedBreakOfShift2 = breakPartMinutes;
                                            }
                                        }
                                    }

                                    await planRegistration.Update(dbContext).ConfigureAwait(false);
                                }
                            }
                        }
                        else
                        {
                            if (planRegistration.Date > DateTime.Now && !planRegistration.PlanChangedByAdmin)
                            {
                                var dayOfWeek = planRegistration.Date.DayOfWeek;
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
                                Console.WriteLine($"The plannedHours are now: {planRegistration.PlanHours}");

                                await planRegistration.Update(dbContext).ConfigureAwait(false);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogError($"Could not parse PlanText for planning with id: {planRegistration.Id} the PlanText was: {planRegistration.PlanText}");
                        SentrySdk.CaptureMessage($"Could not parse PlanText for planning with id: {planRegistration.Id} the PlanText was: {planRegistration.PlanText}");
                        //SentrySdk.CaptureException(e);
                        logger.LogError(e.Message);
                        logger.LogTrace(e.StackTrace);
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
                        Break1Shift = planRegistration.Pause1Id,
                        Break2Shift = planRegistration.Pause2Id,
                        Pause1Id = planRegistration.Pause1Id > 0 ? planRegistration.Pause1Id - 1 : 0,
                        Pause2Id = planRegistration.Pause2Id > 0 ? planRegistration.Pause2Id - 1 : 0,
                        Pause3Id = planRegistration.Pause3Id > 0 ? planRegistration.Pause3Id - 1 : 0,
                        Pause4Id = planRegistration.Pause4Id > 0 ? planRegistration.Pause4Id - 1 : 0,
                        Pause5Id = planRegistration.Pause5Id > 0 ? planRegistration.Pause5Id - 1 : 0,
                        PauseMinutes = planRegistration.Pause1Id > 0 ? planRegistration.Pause1Id * 5 - 5 +
                                                                       (planRegistration.Pause2Id > 0
                                                                           ? planRegistration.Pause2Id * 5 - 5
                                                                           : 0) : planRegistration.Pause2Id > 0 ? planRegistration.Pause2Id * 5 - 5 : 0,
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
                    };

                    planningModel.CommentOffice = planRegistration.CommentOffice;
                    planningModel.WorkerComment = planRegistration.WorkerComment;
                    planningModel.PlannedBreakOfShift1 = planRegistration.PlannedBreakOfShift1;
                    planningModel.PlannedStartOfShift1 = planRegistration.PlannedStartOfShift1;
                    planningModel.PlannedEndOfShift1 = planRegistration.PlannedEndOfShift1;
                    planningModel.PlanHoursMatched = Math.Abs(planRegistration.NettoHours - planRegistration.PlanHours) <= 0.00;

                    planningModel.IsDoubleShift = planningModel.Start2StartedAt != planningModel.Stop2StoppedAt;

                    planningsInPeriod = await dbContext.PlanRegistrations
                        .AsNoTracking()
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .Where(x => x.SdkSitId == dbAssignedSite.SiteId)
                        .Where(x => x.Date >= midnightOfDateFrom)
                        .Where(x => x.Date <= midnightOfDateTo)
                        .OrderBy(x => x.Date)
                        .ToListAsync().ConfigureAwait(false);

                    var plannedTotalHours = planningsInPeriod.Sum(x => x.PlanHours);
                    var nettoHoursTotal = planningsInPeriod.Sum(x => x.NettoHours);

                    siteModel.PlannedHours = (int)plannedTotalHours;
                    siteModel.PlannedMinutes = (int)((plannedTotalHours - siteModel.PlannedHours) * 60);
                    siteModel.CurrentWorkedHours = (int)nettoHoursTotal;
                    siteModel.CurrentWorkedMinutes = (int)((nettoHoursTotal - siteModel.CurrentWorkedHours) * 60);
                    siteModel.PercentageCompleted = (int)(nettoHoursTotal / plannedTotalHours * 100);

                    siteModel.PlanningPrDayModels.Add(planningModel);

                    foreach (var entity in planningsInPeriod)
                    {
                        dbContext.Entry(entity).State = EntityState.Detached;
                    }
                }

                result.Add(siteModel);
            }

            return new OperationDataResult<List<TimePlanningPlanningModel>>(
                true,
                result);
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
            return new OperationDataResult<List<TimePlanningPlanningModel>>(
                false,
                localizationService.GetString("ErrorWhileObtainingPlannings"));
        }
    }

    public async Task<OperationDataResult<TimePlanningPlanningModel>> IndexByCurrentUserNam(
        TimePlanningPlanningRequestModel model)
    {
        var sdkCore = await core.GetCore();
        var sdkDbContext = sdkCore.DbContextHelper.GetDbContext();
        var currentUserAsync = await userService.GetCurrentUserAsync();
        var currentUser = baseDbContext.Users
            .Single(x => x.Id == currentUserAsync.Id);
        var fullName = currentUser.FirstName.Trim() + " " + currentUser.LastName.Trim();
        var sdkSite = await sdkDbContext.Sites.SingleOrDefaultAsync(x =>
            x.Name.Replace(" ", "") == fullName.Replace(" ", "") &&
            x.WorkflowState != Constants.WorkflowStates.Removed);

        if (sdkSite == null)
        {
            return new OperationDataResult<TimePlanningPlanningModel>(
                false,
                localizationService.GetString("SiteNotFound"));
        }

        var dbAssignedSite = await dbContext.AssignedSites
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync(x => x.SiteId == sdkSite.MicrotingUid);

        var datesInPeriod = new List<DateTime>();
        var midnightOfDateFrom = new DateTime(model.DateFrom!.Value.Year, model.DateFrom.Value.Month, model.DateFrom.Value.Day, 0, 0, 0);
        var midnightOfDateTo = new DateTime(model.DateTo!.Value.Year, model.DateTo.Value.Month, model.DateTo.Value.Day, 23, 59, 59);
        var todayMidnight = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0);
        var date = model.DateFrom;
        while (date <= model.DateTo)
        {
            datesInPeriod.Add(date.Value);
            date = date.Value.AddDays(1);
        }

        var siteModel = new TimePlanningPlanningModel
        {
            SiteId = (int)sdkSite.MicrotingUid!,
            SiteName = sdkSite.Name,
            PlanningPrDayModels = new List<TimePlanningPlanningPrDayModel>()
        };

        var user = await baseDbContext.Users
            .Where(x => (x.FirstName + " " + x.LastName).Replace(" ", "").ToLower() == sdkSite.Name.Replace(" ", "").ToLower())
            .FirstOrDefaultAsync().ConfigureAwait(false);
        if (user != null)
        {
            siteModel.AvatarUrl = user.ProfilePictureSnapshot != null
                ? $"api/images/login-page-images?fileName={user.ProfilePictureSnapshot}"
                : $"https://www.gravatar.com/avatar/{user.EmailSha256}?s=32&d=identicon";
        }

        var planningsInPeriod = await dbContext.PlanRegistrations
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .Where(x => x.SdkSitId == dbAssignedSite.SiteId)
            .Where(x => x.Date >= midnightOfDateFrom)
            .Where(x => x.Date <= midnightOfDateTo)
            .OrderByDescending(x => x.Date)
            .ToListAsync().ConfigureAwait(false);

        var datesInPlannings = planningsInPeriod.Select(x => x.Date).ToList();
        var missingDates = new List<DateTime>();

        foreach (var dateTime in datesInPeriod)
        {
            if (!datesInPlannings.Contains(dateTime))
            {
                missingDates.Add(dateTime);
            }
        }

        foreach (var missingDate in missingDates)
        {
            var newPlanRegistration = new PlanRegistration
            {
                Date = missingDate,
                SdkSitId = dbAssignedSite.SiteId,
                CreatedByUserId = userService.UserId,
                UpdatedByUserId = userService.UserId
            };

            if (missingDate.Date <= todayMidnight)
            {
                var preTimePlanning =
                    await dbContext.PlanRegistrations.AsNoTracking()
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .Where(x => x.Date < missingDate
                                    && x.SdkSitId == dbAssignedSite.SiteId)
                        .OrderByDescending(x => x.Date)
                        .FirstOrDefaultAsync();

                if (preTimePlanning != null)
                {
                    newPlanRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                    newPlanRegistration.SumFlexEnd = preTimePlanning.SumFlexEnd + newPlanRegistration.NettoHours -
                                                     newPlanRegistration.PlanHours -
                                                     newPlanRegistration.PaiedOutFlex;
                    newPlanRegistration.Flex = newPlanRegistration.NettoHours - newPlanRegistration.PlanHours;
                }
                else
                {
                    newPlanRegistration.SumFlexEnd = newPlanRegistration.NettoHours - newPlanRegistration.PlanHours -
                                                     newPlanRegistration.PaiedOutFlex;
                    newPlanRegistration.SumFlexStart = 0;
                    newPlanRegistration.Flex = newPlanRegistration.NettoHours - newPlanRegistration.PlanHours;
                }
            }

            await newPlanRegistration.Create(dbContext);
        }

        if (missingDates.Count > 0)
        {
            planningsInPeriod = await dbContext.PlanRegistrations
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.SdkSitId == dbAssignedSite.SiteId)
                .Where(x => x.Date >= midnightOfDateFrom)
                .Where(x => x.Date <= midnightOfDateTo)
                .OrderByDescending(x => x.Date)
                .ToListAsync().ConfigureAwait(false);
        }

        var plannedTotalHours = planningsInPeriod.Sum(x => x.PlanHours);
        var nettoHoursTotal = planningsInPeriod.Sum(x => x.NettoHours);

        siteModel.PlannedHours = (int)plannedTotalHours;
        siteModel.PlannedMinutes = (int)((plannedTotalHours - siteModel.PlannedHours) * 60);
        siteModel.CurrentWorkedHours = (int)nettoHoursTotal;
        siteModel.CurrentWorkedMinutes = (int)((nettoHoursTotal - siteModel.CurrentWorkedHours) * 60);
        siteModel.PercentageCompleted = (int)(nettoHoursTotal / plannedTotalHours * 100);

        foreach (var planRegistration in planningsInPeriod)
        {
            var midnight = new DateTime(planRegistration.Date.Year, planRegistration.Date.Month,
                planRegistration.Date.Day, 0, 0, 0);

            try
            {
                if (dbAssignedSite.UseGoogleSheetAsDefault)
                {
                    if (!string.IsNullOrEmpty(planRegistration.PlanText))
                    {
                        if (planRegistration.Date > DateTime.Now && !planRegistration.PlanChangedByAdmin)
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
                                        var breakPartMinutes = breakPart switch
                                        {
                                            "0.5" => 30,
                                            ".5" => 30,
                                            ".75" => 45,
                                            "0.75" => 45,
                                            "¾" => 45,
                                            "½" => 30,
                                            "1" => 60,
                                            _ => 0
                                        };

                                        planRegistration.PlannedBreakOfShift1 = breakPartMinutes;
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
                                    var breakPartMinutes = breakPart switch
                                    {
                                        "0.5" => 30,
                                        ".5" => 30,
                                        ".75" => 45,
                                        "0.75" => 45,
                                        "¾" => 45,
                                        "½" => 30,
                                        "1" => 60,
                                        _ => 0
                                    };

                                    planRegistration.PlannedBreakOfShift1 = breakPartMinutes;
                                }
                            }

                            if (splitList.Length == 2)
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
                                            var breakPartMinutes = breakPart switch
                                            {
                                                "0.5" => 30,
                                                ".5" => 30,
                                                ".75" => 45,
                                                "0.75" => 45,
                                                "¾" => 45,
                                                "½" => 30,
                                                "1" => 60,
                                                _ => 0
                                            };

                                            planRegistration.PlannedBreakOfShift2 = breakPartMinutes;
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
                                        var breakPartMinutes = breakPart switch
                                        {
                                            "0.5" => 30,
                                            ".5" => 30,
                                            ".75" => 45,
                                            "0.75" => 45,
                                            "¾" => 45,
                                            "½" => 30,
                                            "1" => 60,
                                            _ => 0
                                        };

                                        planRegistration.PlannedBreakOfShift2 = breakPartMinutes;
                                    }
                                }
                            }

                            await planRegistration.Update(dbContext).ConfigureAwait(false);
                        }
                    }
                }
                else
                {
                    if (planRegistration.Date > DateTime.Now && !planRegistration.PlanChangedByAdmin)
                    {
                        var dayOfWeek = planRegistration.Date.DayOfWeek;
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
                                    planRegistration.PlannedStartOfShift2 = dbAssignedSite.StartMonday2NdShift ?? 0;
                                    planRegistration.PlannedEndOfShift2 = dbAssignedSite.EndMonday2NdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift2 = dbAssignedSite.BreakMonday2NdShift ?? 0;
                                    planRegistration.PlannedStartOfShift3 = dbAssignedSite.StartMonday3RdShift ?? 0;
                                    planRegistration.PlannedEndOfShift3 = dbAssignedSite.EndMonday3RdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift3 = dbAssignedSite.BreakMonday3RdShift ?? 0;
                                    planRegistration.PlannedStartOfShift4 = dbAssignedSite.StartMonday4ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift4 = dbAssignedSite.EndMonday4ThShift ?? 0;
                                    planRegistration.PlannedBreakOfShift4 = dbAssignedSite.BreakMonday4ThShift ?? 0;
                                    planRegistration.PlannedStartOfShift5 = dbAssignedSite.StartMonday5ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift5 = dbAssignedSite.EndMonday5ThShift ?? 0;
                                    planRegistration.PlannedBreakOfShift5 = dbAssignedSite.BreakMonday5ThShift ?? 0;
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
                                    planRegistration.PlannedEndOfShift2 = dbAssignedSite.EndTuesday2NdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift2 =
                                        dbAssignedSite.BreakTuesday2NdShift ?? 0;
                                    planRegistration.PlannedStartOfShift3 =
                                        dbAssignedSite.StartTuesday3RdShift ?? 0;
                                    planRegistration.PlannedEndOfShift3 = dbAssignedSite.EndTuesday3RdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift3 =
                                        dbAssignedSite.BreakTuesday3RdShift ?? 0;
                                    planRegistration.PlannedStartOfShift4 =
                                        dbAssignedSite.StartTuesday4ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift4 = dbAssignedSite.EndTuesday4ThShift ?? 0;
                                    planRegistration.PlannedBreakOfShift4 =
                                        dbAssignedSite.BreakTuesday4ThShift ?? 0;
                                    planRegistration.PlannedStartOfShift5 =
                                        dbAssignedSite.StartTuesday5ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift5 = dbAssignedSite.EndTuesday5ThShift ?? 0;
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
                                    planRegistration.PlannedEndOfShift2 = dbAssignedSite.EndWednesday2NdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift2 =
                                        dbAssignedSite.BreakWednesday2NdShift ?? 0;
                                    planRegistration.PlannedStartOfShift3 =
                                        dbAssignedSite.StartWednesday3RdShift ?? 0;
                                    planRegistration.PlannedEndOfShift3 = dbAssignedSite.EndWednesday3RdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift3 =
                                        dbAssignedSite.BreakWednesday3RdShift ?? 0;
                                    planRegistration.PlannedStartOfShift4 =
                                        dbAssignedSite.StartWednesday4ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift4 = dbAssignedSite.EndWednesday4ThShift ?? 0;
                                    planRegistration.PlannedBreakOfShift4 =
                                        dbAssignedSite.BreakWednesday4ThShift ?? 0;
                                    planRegistration.PlannedStartOfShift5 =
                                        dbAssignedSite.StartWednesday5ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift5 = dbAssignedSite.EndWednesday5ThShift ?? 0;
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
                                    planRegistration.PlannedEndOfShift2 = dbAssignedSite.EndThursday2NdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift2 =
                                        dbAssignedSite.BreakThursday2NdShift ?? 0;
                                    planRegistration.PlannedStartOfShift3 =
                                        dbAssignedSite.StartThursday3RdShift ?? 0;
                                    planRegistration.PlannedEndOfShift3 = dbAssignedSite.EndThursday3RdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift3 =
                                        dbAssignedSite.BreakThursday3RdShift ?? 0;
                                    planRegistration.PlannedStartOfShift4 =
                                        dbAssignedSite.StartThursday4ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift4 = dbAssignedSite.EndThursday4ThShift ?? 0;
                                    planRegistration.PlannedBreakOfShift4 =
                                        dbAssignedSite.BreakThursday4ThShift ?? 0;
                                    planRegistration.PlannedStartOfShift5 =
                                        dbAssignedSite.StartThursday5ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift5 = dbAssignedSite.EndThursday5ThShift ?? 0;
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
                                    planRegistration.PlannedStartOfShift2 = dbAssignedSite.StartFriday2NdShift ?? 0;
                                    planRegistration.PlannedEndOfShift2 = dbAssignedSite.EndFriday2NdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift2 = dbAssignedSite.BreakFriday2NdShift ?? 0;
                                    planRegistration.PlannedStartOfShift3 = dbAssignedSite.StartFriday3RdShift ?? 0;
                                    planRegistration.PlannedEndOfShift3 = dbAssignedSite.EndFriday3RdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift3 = dbAssignedSite.BreakFriday3RdShift ?? 0;
                                    planRegistration.PlannedStartOfShift4 = dbAssignedSite.StartFriday4ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift4 = dbAssignedSite.EndFriday4ThShift ?? 0;
                                    planRegistration.PlannedBreakOfShift4 = dbAssignedSite.BreakFriday4ThShift ?? 0;
                                    planRegistration.PlannedStartOfShift5 = dbAssignedSite.StartFriday5ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift5 = dbAssignedSite.EndFriday5ThShift ?? 0;
                                    planRegistration.PlannedBreakOfShift5 = dbAssignedSite.BreakFriday5ThShift ?? 0;
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
                                    planRegistration.PlannedEndOfShift2 = dbAssignedSite.EndSaturday2NdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift2 =
                                        dbAssignedSite.BreakSaturday2NdShift ?? 0;
                                    planRegistration.PlannedStartOfShift3 =
                                        dbAssignedSite.StartSaturday3RdShift ?? 0;
                                    planRegistration.PlannedEndOfShift3 = dbAssignedSite.EndSaturday3RdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift3 =
                                        dbAssignedSite.BreakSaturday3RdShift ?? 0;
                                    planRegistration.PlannedStartOfShift4 =
                                        dbAssignedSite.StartSaturday4ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift4 = dbAssignedSite.EndSaturday4ThShift ?? 0;
                                    planRegistration.PlannedBreakOfShift4 =
                                        dbAssignedSite.BreakSaturday4ThShift ?? 0;
                                    planRegistration.PlannedStartOfShift5 =
                                        dbAssignedSite.StartSaturday5ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift5 = dbAssignedSite.EndSaturday5ThShift ?? 0;
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
                                    planRegistration.PlannedStartOfShift2 = dbAssignedSite.StartSunday2NdShift ?? 0;
                                    planRegistration.PlannedEndOfShift2 = dbAssignedSite.EndSunday2NdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift2 = dbAssignedSite.BreakSunday2NdShift ?? 0;
                                    planRegistration.PlannedStartOfShift3 = dbAssignedSite.StartSunday3RdShift ?? 0;
                                    planRegistration.PlannedEndOfShift3 = dbAssignedSite.EndSunday3RdShift ?? 0;
                                    planRegistration.PlannedBreakOfShift3 = dbAssignedSite.BreakSunday3RdShift ?? 0;
                                    planRegistration.PlannedStartOfShift4 = dbAssignedSite.StartSunday4ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift4 = dbAssignedSite.EndSunday4ThShift ?? 0;
                                    planRegistration.PlannedBreakOfShift4 = dbAssignedSite.BreakSunday4ThShift ?? 0;
                                    planRegistration.PlannedStartOfShift5 = dbAssignedSite.StartSunday5ThShift ?? 0;
                                    planRegistration.PlannedEndOfShift5 = dbAssignedSite.EndSunday5ThShift ?? 0;
                                    planRegistration.PlannedBreakOfShift5 = dbAssignedSite.BreakSunday5ThShift ?? 0;
                                }

                                break;
                        }

                        await planRegistration.Update(dbContext).ConfigureAwait(false);
                    }
                }

                var planningModel = new TimePlanningPlanningPrDayModel
                {
                    Id = planRegistration.Id,
                    SiteName = sdkSite.Name,
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
                    Break1Shift = planRegistration.Pause1Id,
                    Break2Shift = planRegistration.Pause2Id,
                    Pause1Id = planRegistration.Pause1Id,
                    Pause2Id = planRegistration.Pause2Id,
                    Pause3Id = planRegistration.Pause3Id,
                    Pause4Id = planRegistration.Pause4Id,
                    Pause5Id = planRegistration.Pause5Id,
                    PauseMinutes = planRegistration.Pause1Id > 0 ? planRegistration.Pause1Id * 5 - 5 +
                                                                   (planRegistration.Pause2Id > 0
                                                                       ? planRegistration.Pause2Id * 5 - 5
                                                                       : 0) : planRegistration.Pause2Id > 0 ? planRegistration.Pause2Id * 5 - 5 : 0,
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
                };

                planningModel.IsDoubleShift = planningModel.Start2StartedAt != planningModel.Stop2StoppedAt;


                planningModel.CommentOffice = planRegistration.CommentOffice;
                planningModel.WorkerComment = planRegistration.WorkerComment;
                planningModel.PlannedBreakOfShift1 = planRegistration.PlannedBreakOfShift1;
                planningModel.PlannedStartOfShift1 = planRegistration.PlannedStartOfShift1;
                planningModel.PlannedEndOfShift1 = planRegistration.PlannedEndOfShift1;
                planningModel.PlanHoursMatched = Math.Abs(planRegistration.NettoHours - planRegistration.PlanHours) <= 0.00;


                siteModel.PlanningPrDayModels.Add(planningModel);
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


            foreach (var entity in planningsInPeriod)
            {
                dbContext.Entry(entity).State = EntityState.Detached;
            }
        }

        siteModel.PlanningPrDayModels = model.IsSortDsc
            ? siteModel.PlanningPrDayModels.OrderByDescending(x => x.Date).ToList()
            : siteModel.PlanningPrDayModels.OrderBy(x => x.Date).ToList();

        return new OperationDataResult<TimePlanningPlanningModel>(
            true,
            siteModel);

    }

    public async Task<OperationResult> Update(int id, TimePlanningPlanningPrDayModel model)
    {
        try
        {
            var planning = dbContext.PlanRegistrations
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefault(x => x.Id == id);

            if (planning == null)
            {
                return new OperationResult(
                    false,
                    localizationService.GetString("PlanningNotFound"));
            }

            var assignedSite = await dbContext.AssignedSites
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstAsync(x => x.SiteId == planning.SdkSitId);

            planning.PlannedStartOfShift1 = model.PlannedStartOfShift1;
            planning.PlannedBreakOfShift1 = model.PlannedBreakOfShift1;
            planning.PlannedEndOfShift1 = model.PlannedEndOfShift1;
            planning.PlannedStartOfShift2 = model.PlannedStartOfShift2;
            planning.PlannedBreakOfShift2 = model.PlannedBreakOfShift2;
            planning.PlannedEndOfShift2 = model.PlannedEndOfShift2;
            planning.CommentOffice = model.CommentOffice;

            if (!planning.PlanChangedByAdmin)
            {
                var entry = dbContext.Entry(planning);
                planning.PlanChangedByAdmin = entry.State == EntityState.Modified;
            }

            if (!assignedSite.UseDetailedPauseEditing)
            {
                planning.Pause1Id = model.Pause1Id ?? planning.Pause1Id;
                planning.Pause2Id = model.Pause2Id ?? planning.Pause2Id;
            }
            else
            {
                planning.Shift1PauseNumber = 0;
                planning.Pause1StartedAt = model.Pause1StartedAt;
                planning.Pause1StoppedAt = model.Pause1StoppedAt;
                if (planning.Pause1StartedAt != null && planning.Pause1StoppedAt != null)
                {
                    planning.Shift1PauseNumber = (int)((DateTime)planning.Pause1StoppedAt - (DateTime)planning.Pause1StartedAt).TotalMinutes;
                }
                planning.Pause2StartedAt = model.Pause2StartedAt;
                planning.Pause2StoppedAt = model.Pause2StoppedAt;
                if (planning.Pause2StartedAt != null && planning.Pause2StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause2StoppedAt - (DateTime)planning.Pause2StartedAt).TotalMinutes;
                }
                planning.Pause10StartedAt = model.Pause10StartedAt;
                planning.Pause10StoppedAt = model.Pause10StoppedAt;
                if (planning.Pause10StartedAt != null && planning.Pause10StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause10StoppedAt - (DateTime)planning.Pause10StartedAt).TotalMinutes;
                }

                planning.Pause11StartedAt = model.Pause11StartedAt;
                planning.Pause11StoppedAt = model.Pause11StoppedAt;
                if (planning.Pause11StartedAt != null && planning.Pause11StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause11StoppedAt - (DateTime)planning.Pause11StartedAt).TotalMinutes;
                }
                planning.Pause12StartedAt = model.Pause12StartedAt;
                planning.Pause12StoppedAt = model.Pause12StoppedAt;
                if (planning.Pause12StartedAt != null && planning.Pause12StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause12StoppedAt - (DateTime)planning.Pause12StartedAt).TotalMinutes;
                }
                planning.Pause13StartedAt = model.Pause13StartedAt;
                planning.Pause13StoppedAt = model.Pause13StoppedAt;
                if (planning.Pause13StartedAt != null && planning.Pause13StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause13StoppedAt - (DateTime)planning.Pause13StartedAt).TotalMinutes;
                }
                planning.Pause14StartedAt = model.Pause14StartedAt;
                planning.Pause14StoppedAt = model.Pause14StoppedAt;
                if (planning.Pause14StartedAt != null && planning.Pause14StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause14StoppedAt - (DateTime)planning.Pause14StartedAt).TotalMinutes;
                }
                planning.Pause15StartedAt = model.Pause15StartedAt;
                planning.Pause15StoppedAt = model.Pause15StoppedAt;
                if (planning.Pause15StartedAt != null && planning.Pause15StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause15StoppedAt - (DateTime)planning.Pause15StartedAt).TotalMinutes;
                }
                planning.Pause16StartedAt = model.Pause16StartedAt;
                planning.Pause16StoppedAt = model.Pause16StoppedAt;
                if (planning.Pause16StartedAt != null && planning.Pause16StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause16StoppedAt - (DateTime)planning.Pause16StartedAt).TotalMinutes;
                }
                planning.Pause17StartedAt = model.Pause17StartedAt;
                planning.Pause17StoppedAt = model.Pause17StoppedAt;
                if (planning.Pause17StartedAt != null && planning.Pause17StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause17StoppedAt - (DateTime)planning.Pause17StartedAt).TotalMinutes;
                }
                planning.Pause18StartedAt = model.Pause18StartedAt;
                planning.Pause18StoppedAt = model.Pause18StoppedAt;
                if (planning.Pause18StartedAt != null && planning.Pause18StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause18StoppedAt - (DateTime)planning.Pause18StartedAt).TotalMinutes;
                }
                planning.Pause19StartedAt = model.Pause19StartedAt;
                planning.Pause19StoppedAt = model.Pause19StoppedAt;
                if (planning.Pause19StartedAt != null && planning.Pause19StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause19StoppedAt - (DateTime)planning.Pause19StartedAt).TotalMinutes;
                }
                planning.Pause100StartedAt = model.Pause100StartedAt;
                planning.Pause100StoppedAt = model.Pause100StoppedAt;
                if (planning.Pause100StartedAt != null && planning.Pause100StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause100StoppedAt - (DateTime)planning.Pause100StartedAt).TotalMinutes;
                }
                planning.Pause101StartedAt = model.Pause101StartedAt;
                planning.Pause101StoppedAt = model.Pause101StoppedAt;
                if (planning.Pause101StartedAt != null && planning.Pause101StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause101StoppedAt - (DateTime)planning.Pause101StartedAt).TotalMinutes;
                }
                planning.Pause102StartedAt = model.Pause102StartedAt;
                planning.Pause102StoppedAt = model.Pause102StoppedAt;
                if (planning.Pause102StartedAt != null && planning.Pause102StoppedAt != null)
                {
                    planning.Shift1PauseNumber += (int)((DateTime)planning.Pause102StoppedAt - (DateTime)planning.Pause102StartedAt).TotalMinutes;
                }

                planning.Pause1Id = planning.Shift1PauseNumber / 5;

                planning.Shift2PauseNumber = 0;
                planning.Pause20StartedAt = model.Pause20StartedAt;
                planning.Pause20StoppedAt = model.Pause20StoppedAt;
                if (planning.Pause20StartedAt != null && planning.Pause20StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause20StoppedAt - (DateTime)planning.Pause20StartedAt).TotalMinutes;
                }
                planning.Pause21StartedAt = model.Pause21StartedAt;
                planning.Pause21StoppedAt = model.Pause21StoppedAt;
                if (planning.Pause21StartedAt != null && planning.Pause21StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause21StoppedAt - (DateTime)planning.Pause21StartedAt).TotalMinutes;
                }
                planning.Pause22StartedAt = model.Pause22StartedAt;
                planning.Pause22StoppedAt = model.Pause22StoppedAt;
                if (planning.Pause22StartedAt != null && planning.Pause22StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause22StoppedAt - (DateTime)planning.Pause22StartedAt).TotalMinutes;
                }
                planning.Pause23StartedAt = model.Pause23StartedAt;
                planning.Pause23StoppedAt = model.Pause23StoppedAt;
                if (planning.Pause23StartedAt != null && planning.Pause23StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause23StoppedAt - (DateTime)planning.Pause23StartedAt).TotalMinutes;
                }
                planning.Pause24StartedAt = model.Pause24StartedAt;
                planning.Pause24StoppedAt = model.Pause24StoppedAt;
                if (planning.Pause24StartedAt != null && planning.Pause24StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause24StoppedAt - (DateTime)planning.Pause24StartedAt).TotalMinutes;
                }
                planning.Pause25StartedAt = model.Pause25StartedAt;
                planning.Pause25StoppedAt = model.Pause25StoppedAt;
                if (planning.Pause25StartedAt != null && planning.Pause25StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause25StoppedAt - (DateTime)planning.Pause25StartedAt).TotalMinutes;
                }
                planning.Pause26StartedAt = model.Pause26StartedAt;
                planning.Pause26StoppedAt = model.Pause26StoppedAt;
                if (planning.Pause26StartedAt != null && planning.Pause26StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause26StoppedAt - (DateTime)planning.Pause26StartedAt).TotalMinutes;
                }
                planning.Pause27StartedAt = model.Pause27StartedAt;
                planning.Pause27StoppedAt = model.Pause27StoppedAt;
                if (planning.Pause27StartedAt != null && planning.Pause27StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause27StoppedAt - (DateTime)planning.Pause27StartedAt).TotalMinutes;
                }
                planning.Pause28StartedAt = model.Pause28StartedAt;
                planning.Pause28StoppedAt = model.Pause28StoppedAt;
                if (planning.Pause28StartedAt != null && planning.Pause28StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause28StoppedAt - (DateTime)planning.Pause28StartedAt).TotalMinutes;
                }
                planning.Pause29StartedAt = model.Pause29StartedAt;
                planning.Pause29StoppedAt = model.Pause29StoppedAt;
                if (planning.Pause29StartedAt != null && planning.Pause29StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause29StoppedAt - (DateTime)planning.Pause29StartedAt).TotalMinutes;
                }
                planning.Pause200StartedAt = model.Pause200StartedAt;
                planning.Pause200StoppedAt = model.Pause200StoppedAt;
                if (planning.Pause200StartedAt != null && planning.Pause200StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause200StoppedAt - (DateTime)planning.Pause200StartedAt).TotalMinutes;
                }
                planning.Pause201StartedAt = model.Pause201StartedAt;
                planning.Pause201StoppedAt = model.Pause201StoppedAt;
                if (planning.Pause201StartedAt != null && planning.Pause201StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause201StoppedAt - (DateTime)planning.Pause201StartedAt).TotalMinutes;
                }
                planning.Pause202StartedAt = model.Pause202StartedAt;
                planning.Pause202StoppedAt = model.Pause202StoppedAt;
                if (planning.Pause202StartedAt != null && planning.Pause202StoppedAt != null)
                {
                    planning.Shift2PauseNumber += (int)((DateTime)planning.Pause202StoppedAt - (DateTime)planning.Pause202StartedAt).TotalMinutes;
                }
                planning.Pause2Id = planning.Shift2PauseNumber / 5;
                // we need to calculate the pause id based on the start and stop times from all the pauses above
            }
            planning.Start1Id = model.Start1Id ?? 0;
            planning.Stop1Id = model.Stop1Id ?? 0;
            planning.Start2Id = model.Start2Id ?? 0;
            planning.Stop2Id = model.Stop2Id ?? 0;
            planning.MessageId = model.Message;
            planning.PaiedOutFlex = model.PaidOutFlex;

            if (!assignedSite.UseOnlyPlanHours)
            {
                double minutesPlanned = 0;
                if (planning.PlannedStartOfShift1 != 0 && planning.PlannedEndOfShift1 != 0)
                {
                    minutesPlanned += planning.PlannedEndOfShift1 - planning.PlannedStartOfShift1 - planning.PlannedBreakOfShift1;
                }
                if (planning.PlannedStartOfShift2 != 0 && planning.PlannedEndOfShift2 != 0)
                {
                    minutesPlanned += planning.PlannedEndOfShift2 - planning.PlannedStartOfShift2 - planning.PlannedBreakOfShift2;
                }
                if (planning.PlannedStartOfShift3 != 0 && planning.PlannedEndOfShift3 != 0)
                {
                    minutesPlanned += planning.PlannedEndOfShift3 - planning.PlannedStartOfShift3 - planning.PlannedBreakOfShift3;
                }
                if (planning.PlannedStartOfShift4 != 0 && planning.PlannedEndOfShift4 != 0)
                {
                    minutesPlanned += planning.PlannedEndOfShift4 - planning.PlannedStartOfShift4 - planning.PlannedBreakOfShift4;
                }
                if (planning.PlannedStartOfShift5 != 0 && planning.PlannedEndOfShift5 != 0)
                {
                    minutesPlanned += planning.PlannedEndOfShift5 - planning.PlannedStartOfShift5 - planning.PlannedBreakOfShift5;
                }
                if (planning.MessageId == null)
                {
                    planning.PlanHours = minutesPlanned != 0 ? minutesPlanned / 60 : 0;
                }
                else
                {
                    planning.PlanHours = model.PlanHours;
                }
            } else {
                planning.PlanHours = model.PlanHours;
            }

            var minutesMultiplier = 5;
            double nettoMinutes = 0;

            if (planning.Stop1Id >= planning.Start1Id && planning.Stop1Id != 0)
            {
                nettoMinutes = planning.Stop1Id - planning.Start1Id;
                nettoMinutes -= planning.Pause1Id > 0 ? planning.Pause1Id - 1 : 0;
            }

            if (planning.Stop2Id >= planning.Start2Id && planning.Stop2Id != 0)
            {
                nettoMinutes = nettoMinutes + planning.Stop2Id - planning.Start2Id;
                nettoMinutes -= planning.Pause2Id > 0 ? planning.Pause2Id - 1 : 0;
            }

            nettoMinutes *= minutesMultiplier;

            double hours = nettoMinutes / 60;
            planning.NettoHours = hours;

            var preTimePlanning =
                await dbContext.PlanRegistrations.AsNoTracking()
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.Date < planning.Date
                                && x.SdkSitId == planning.SdkSitId)
                    .OrderByDescending(x => x.Date)
                    .FirstOrDefaultAsync();

            planning.SumFlexStart = preTimePlanning.SumFlexEnd;
            planning.SumFlexEnd = preTimePlanning.SumFlexEnd + planning.NettoHours -
                                  planning.PlanHours -
                                  planning.PaiedOutFlex;
            planning.Flex = planning.NettoHours - planning.PlanHours;
            await planning.Update(dbContext).ConfigureAwait(false);

            var planningsAfterThisPlanning = dbContext.PlanRegistrations
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.SdkSitId == planning.SdkSitId)
                .Where(x => x.Date > planning.Date)
                .OrderBy(x => x.Date)
                .ToList();

            foreach (var planningAfterThisPlanning in planningsAfterThisPlanning)
            {
                var preTimePlanningAfterThisPlanning =
                    await dbContext.PlanRegistrations.AsNoTracking()
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .Where(x => x.Date < planningAfterThisPlanning.Date
                                    && x.SdkSitId == planningAfterThisPlanning.SdkSitId)
                        .OrderByDescending(x => x.Date)
                        .FirstOrDefaultAsync();

                planningAfterThisPlanning.SumFlexStart = preTimePlanningAfterThisPlanning.SumFlexEnd;
                planningAfterThisPlanning.SumFlexEnd = preTimePlanningAfterThisPlanning.SumFlexEnd +
                                                       planningAfterThisPlanning.NettoHours -
                                                       planningAfterThisPlanning.PlanHours -
                                                       planningAfterThisPlanning.PaiedOutFlex;
                planningAfterThisPlanning.Flex = planningAfterThisPlanning.NettoHours - planningAfterThisPlanning.PlanHours;
                await planningAfterThisPlanning.Update(dbContext).ConfigureAwait(false);
            }

            return new OperationResult(
                true,
                localizationService.GetString("SuccessfullyUpdatedPlanning"));
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
            return new OperationResult(
                false,
                localizationService.GetString("ErrorWhileUpdatingPlanning"));
        }
    }

    public async Task<OperationResult> UpdateByCurrentUserNam(
        TimePlanningPlanningPrDayModel model)
    {
        try
        {
            var sdkCore = await core.GetCore();
            var sdkDbContext = sdkCore.DbContextHelper.GetDbContext();
            var currentUserAsync = await userService.GetCurrentUserAsync();
            var currentUser = baseDbContext.Users
                .Single(x => x.Id == currentUserAsync.Id);
            var fullName = currentUser.FirstName.Trim() + " " + currentUser.LastName.Trim();
            var sdkSite = await sdkDbContext.Sites.SingleOrDefaultAsync(x =>
                x.Name.Replace(" ", "") == fullName.Replace(" ", "") &&
                x.WorkflowState != Constants.WorkflowStates.Removed);

            if (sdkSite == null)
            {
                return new OperationDataResult<TimePlanningPlanningModel>(
                    false,
                    localizationService.GetString("SiteNotFound"));
            }

            var assignedSite = await dbContext.AssignedSites
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefaultAsync(x => x.SiteId == sdkSite.MicrotingUid);

            if (assignedSite == null)
            {
                return new OperationDataResult<TimePlanningPlanningModel>(
                    false,
                    localizationService.GetString("AssignedSiteNotFound"));
            }

            var planning = await dbContext.PlanRegistrations
                .Where(x => x.Id == model.Id)
                .FirstOrDefaultAsync().ConfigureAwait(false);

            if (planning == null)
            {
                return new OperationDataResult<TimePlanningPlanningModel>(
                    false,
                    localizationService.GetString("PlanningNotFound"));
            }

            if (!assignedSite.UseDetailedPauseEditing)
            {
                planning.Pause1Id = model.Pause1Id ?? planning.Pause1Id;
                planning.Pause2Id = model.Pause2Id ?? planning.Pause2Id;
            }
            else
            {
                planning.Shift1PauseNumber = 0;
                planning.Pause1StartedAt = model.Pause1StartedAt;
                planning.Pause1StoppedAt = model.Pause1StoppedAt;
                if (planning.Pause1StartedAt != null && planning.Pause1StoppedAt != null)
                {
                    planning.Shift1PauseNumber =
                        (int)((DateTime)planning.Pause1StoppedAt - (DateTime)planning.Pause1StartedAt).TotalMinutes;
                }

                planning.Pause2StartedAt = model.Pause2StartedAt;
                planning.Pause2StoppedAt = model.Pause2StoppedAt;
                if (planning.Pause2StartedAt != null && planning.Pause2StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause2StoppedAt - (DateTime)planning.Pause2StartedAt).TotalMinutes;
                }

                planning.Pause10StartedAt = model.Pause10StartedAt;
                planning.Pause10StoppedAt = model.Pause10StoppedAt;
                if (planning.Pause10StartedAt != null && planning.Pause10StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause10StoppedAt - (DateTime)planning.Pause10StartedAt).TotalMinutes;
                }

                planning.Pause11StartedAt = model.Pause11StartedAt;
                planning.Pause11StoppedAt = model.Pause11StoppedAt;
                if (planning.Pause11StartedAt != null && planning.Pause11StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause11StoppedAt - (DateTime)planning.Pause11StartedAt).TotalMinutes;
                }

                planning.Pause12StartedAt = model.Pause12StartedAt;
                planning.Pause12StoppedAt = model.Pause12StoppedAt;
                if (planning.Pause12StartedAt != null && planning.Pause12StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause12StoppedAt - (DateTime)planning.Pause12StartedAt).TotalMinutes;
                }

                planning.Pause13StartedAt = model.Pause13StartedAt;
                planning.Pause13StoppedAt = model.Pause13StoppedAt;
                if (planning.Pause13StartedAt != null && planning.Pause13StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause13StoppedAt - (DateTime)planning.Pause13StartedAt).TotalMinutes;
                }

                planning.Pause14StartedAt = model.Pause14StartedAt;
                planning.Pause14StoppedAt = model.Pause14StoppedAt;
                if (planning.Pause14StartedAt != null && planning.Pause14StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause14StoppedAt - (DateTime)planning.Pause14StartedAt).TotalMinutes;
                }

                planning.Pause15StartedAt = model.Pause15StartedAt;
                planning.Pause15StoppedAt = model.Pause15StoppedAt;
                if (planning.Pause15StartedAt != null && planning.Pause15StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause15StoppedAt - (DateTime)planning.Pause15StartedAt).TotalMinutes;
                }

                planning.Pause16StartedAt = model.Pause16StartedAt;
                planning.Pause16StoppedAt = model.Pause16StoppedAt;
                if (planning.Pause16StartedAt != null && planning.Pause16StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause16StoppedAt - (DateTime)planning.Pause16StartedAt).TotalMinutes;
                }

                planning.Pause17StartedAt = model.Pause17StartedAt;
                planning.Pause17StoppedAt = model.Pause17StoppedAt;
                if (planning.Pause17StartedAt != null && planning.Pause17StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause17StoppedAt - (DateTime)planning.Pause17StartedAt).TotalMinutes;
                }

                planning.Pause18StartedAt = model.Pause18StartedAt;
                planning.Pause18StoppedAt = model.Pause18StoppedAt;
                if (planning.Pause18StartedAt != null && planning.Pause18StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause18StoppedAt - (DateTime)planning.Pause18StartedAt).TotalMinutes;
                }

                planning.Pause19StartedAt = model.Pause19StartedAt;
                planning.Pause19StoppedAt = model.Pause19StoppedAt;
                if (planning.Pause19StartedAt != null && planning.Pause19StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause19StoppedAt - (DateTime)planning.Pause19StartedAt).TotalMinutes;
                }

                planning.Pause100StartedAt = model.Pause100StartedAt;
                planning.Pause100StoppedAt = model.Pause100StoppedAt;
                if (planning.Pause100StartedAt != null && planning.Pause100StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause100StoppedAt - (DateTime)planning.Pause100StartedAt).TotalMinutes;
                }

                planning.Pause101StartedAt = model.Pause101StartedAt;
                planning.Pause101StoppedAt = model.Pause101StoppedAt;
                if (planning.Pause101StartedAt != null && planning.Pause101StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause101StoppedAt - (DateTime)planning.Pause101StartedAt).TotalMinutes;
                }

                planning.Pause102StartedAt = model.Pause102StartedAt;
                planning.Pause102StoppedAt = model.Pause102StoppedAt;
                if (planning.Pause102StartedAt != null && planning.Pause102StoppedAt != null)
                {
                    planning.Shift1PauseNumber +=
                        (int)((DateTime)planning.Pause102StoppedAt - (DateTime)planning.Pause102StartedAt).TotalMinutes;
                }

                planning.Pause1Id = planning.Shift1PauseNumber / 5;

                planning.Shift2PauseNumber = 0;
                planning.Pause20StartedAt = model.Pause20StartedAt;
                planning.Pause20StoppedAt = model.Pause20StoppedAt;
                if (planning.Pause20StartedAt != null && planning.Pause20StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause20StoppedAt - (DateTime)planning.Pause20StartedAt).TotalMinutes;
                }

                planning.Pause21StartedAt = model.Pause21StartedAt;
                planning.Pause21StoppedAt = model.Pause21StoppedAt;
                if (planning.Pause21StartedAt != null && planning.Pause21StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause21StoppedAt - (DateTime)planning.Pause21StartedAt).TotalMinutes;
                }

                planning.Pause22StartedAt = model.Pause22StartedAt;
                planning.Pause22StoppedAt = model.Pause22StoppedAt;
                if (planning.Pause22StartedAt != null && planning.Pause22StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause22StoppedAt - (DateTime)planning.Pause22StartedAt).TotalMinutes;
                }

                planning.Pause23StartedAt = model.Pause23StartedAt;
                planning.Pause23StoppedAt = model.Pause23StoppedAt;
                if (planning.Pause23StartedAt != null && planning.Pause23StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause23StoppedAt - (DateTime)planning.Pause23StartedAt).TotalMinutes;
                }

                planning.Pause24StartedAt = model.Pause24StartedAt;
                planning.Pause24StoppedAt = model.Pause24StoppedAt;
                if (planning.Pause24StartedAt != null && planning.Pause24StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause24StoppedAt - (DateTime)planning.Pause24StartedAt).TotalMinutes;
                }

                planning.Pause25StartedAt = model.Pause25StartedAt;
                planning.Pause25StoppedAt = model.Pause25StoppedAt;
                if (planning.Pause25StartedAt != null && planning.Pause25StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause25StoppedAt - (DateTime)planning.Pause25StartedAt).TotalMinutes;
                }

                planning.Pause26StartedAt = model.Pause26StartedAt;
                planning.Pause26StoppedAt = model.Pause26StoppedAt;
                if (planning.Pause26StartedAt != null && planning.Pause26StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause26StoppedAt - (DateTime)planning.Pause26StartedAt).TotalMinutes;
                }

                planning.Pause27StartedAt = model.Pause27StartedAt;
                planning.Pause27StoppedAt = model.Pause27StoppedAt;
                if (planning.Pause27StartedAt != null && planning.Pause27StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause27StoppedAt - (DateTime)planning.Pause27StartedAt).TotalMinutes;
                }

                planning.Pause28StartedAt = model.Pause28StartedAt;
                planning.Pause28StoppedAt = model.Pause28StoppedAt;
                if (planning.Pause28StartedAt != null && planning.Pause28StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause28StoppedAt - (DateTime)planning.Pause28StartedAt).TotalMinutes;
                }

                planning.Pause29StartedAt = model.Pause29StartedAt;
                planning.Pause29StoppedAt = model.Pause29StoppedAt;
                if (planning.Pause29StartedAt != null && planning.Pause29StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause29StoppedAt - (DateTime)planning.Pause29StartedAt).TotalMinutes;
                }

                planning.Pause200StartedAt = model.Pause200StartedAt;
                planning.Pause200StoppedAt = model.Pause200StoppedAt;
                if (planning.Pause200StartedAt != null && planning.Pause200StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause200StoppedAt - (DateTime)planning.Pause200StartedAt).TotalMinutes;
                }

                planning.Pause201StartedAt = model.Pause201StartedAt;
                planning.Pause201StoppedAt = model.Pause201StoppedAt;
                if (planning.Pause201StartedAt != null && planning.Pause201StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause201StoppedAt - (DateTime)planning.Pause201StartedAt).TotalMinutes;
                }

                planning.Pause202StartedAt = model.Pause202StartedAt;
                planning.Pause202StoppedAt = model.Pause202StoppedAt;
                if (planning.Pause202StartedAt != null && planning.Pause202StoppedAt != null)
                {
                    planning.Shift2PauseNumber +=
                        (int)((DateTime)planning.Pause202StoppedAt - (DateTime)planning.Pause202StartedAt).TotalMinutes;
                }

                planning.Pause2Id = planning.Shift2PauseNumber / 5;
                // we need to calculate the pause id based on the start and stop times from all the pauses above
            }

            if (assignedSite.UseOneMinuteIntervals)
            {
                planning.Start1StartedAt = model.Start1StartedAt;

                planning.Start1Id = planning.Start1StartedAt != null
                    ? planning.Start1StartedAt.Value.Hour * 12
                      + planning.Start1StartedAt.Value.Minute / 5 + 1
                    : 0;
                planning.Stop1StoppedAt = model.Stop1StoppedAt;
                planning.Stop1Id = planning.Stop1StoppedAt != null
                    ? planning.Stop1StoppedAt.Value.Hour * 12
                      + planning.Stop1StoppedAt.Value.Minute / 5 + 1
                    : 0;
                planning.Start2StartedAt = model.Start2StartedAt;
                planning.Start2Id = planning.Start2StartedAt != null
                    ? planning.Start2StartedAt.Value.Hour * 12
                      + planning.Start2StartedAt.Value.Minute / 5 + 1
                    : 0;
                planning.Stop2StoppedAt = model.Stop2StoppedAt;
                planning.Stop2Id = planning.Stop2StoppedAt != null
                    ? planning.Stop2StoppedAt.Value.Hour * 12
                      + planning.Stop2StoppedAt.Value.Minute / 5 + 1
                    : 0;
            }

            planning.Start1Id = model.Start1Id ?? 0;
            planning.Stop1Id = model.Stop1Id ?? 0;
            planning.Start2Id = model.Start2Id ?? 0;
            planning.Stop2Id = model.Stop2Id ?? 0;
            planning.WorkerComment = model.WorkerComment;

            var minutesMultiplier = 5;
            double nettoMinutes = 0;

            if (planning.Stop1Id >= planning.Start1Id && planning.Stop1Id != 0)
            {
                nettoMinutes = planning.Stop1Id - planning.Start1Id;
                nettoMinutes -= planning.Pause1Id > 0 ? planning.Pause1Id - 1 : 0;
            }

            if (planning.Stop2Id >= planning.Start2Id && planning.Stop2Id != 0)
            {
                nettoMinutes = nettoMinutes + planning.Stop2Id - planning.Start2Id;
                nettoMinutes -= planning.Pause2Id > 0 ? planning.Pause2Id - 1 : 0;
            }

            nettoMinutes *= minutesMultiplier;

            double hours = nettoMinutes / 60;
            planning.NettoHours = hours;

            var preTimePlanning =
                await dbContext.PlanRegistrations.AsNoTracking()
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.Date < planning.Date
                                && x.SdkSitId == planning.SdkSitId)
                    .OrderByDescending(x => x.Date)
                    .FirstOrDefaultAsync();

            planning.SumFlexStart = preTimePlanning.SumFlexEnd;
            planning.SumFlexEnd = preTimePlanning.SumFlexEnd + planning.NettoHours -
                                  planning.PlanHours -
                                  planning.PaiedOutFlex;
            planning.Flex = planning.NettoHours - planning.PlanHours;
            await planning.Update(dbContext).ConfigureAwait(false);

            var planningsAfterThisPlanning = dbContext.PlanRegistrations
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.SdkSitId == planning.SdkSitId)
                .Where(x => x.Date > planning.Date)
                .OrderBy(x => x.Date)
                .ToList();

            foreach (var planningAfterThisPlanning in planningsAfterThisPlanning)
            {
                var preTimePlanningAfterThisPlanning =
                    await dbContext.PlanRegistrations.AsNoTracking()
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .Where(x => x.Date < planningAfterThisPlanning.Date
                                    && x.SdkSitId == planningAfterThisPlanning.SdkSitId)
                        .OrderByDescending(x => x.Date)
                        .FirstOrDefaultAsync();

                planningAfterThisPlanning.SumFlexStart = preTimePlanningAfterThisPlanning.SumFlexEnd;
                planningAfterThisPlanning.SumFlexEnd = preTimePlanningAfterThisPlanning.SumFlexEnd +
                                                       planningAfterThisPlanning.NettoHours -
                                                       planningAfterThisPlanning.PlanHours -
                                                       planningAfterThisPlanning.PaiedOutFlex;
                planningAfterThisPlanning.Flex =
                    planningAfterThisPlanning.NettoHours - planningAfterThisPlanning.PlanHours;
                await planningAfterThisPlanning.Update(dbContext).ConfigureAwait(false);
            }

            return new OperationResult(
                true,
                localizationService.GetString("SuccessfullyUpdatedPlanning"));
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
            return new OperationResult(
                false,
                localizationService.GetString("ErrorWhileUpdatingPlanning"));
        }
    }
}