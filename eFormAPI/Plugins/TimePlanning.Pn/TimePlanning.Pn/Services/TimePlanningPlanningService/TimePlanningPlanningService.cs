﻿/*
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
            var datesInPeriod = new List<DateTime>();
            var date = model.DateFrom;
            var midnightOfDateFrom = new DateTime(date.Value.Year, date.Value.Month, date.Value.Day, 0, 0, 0);
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
                    .Where(x => x.Date >= model.DateFrom)
                    .Where(x => x.Date <= model.DateTo)
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

                    await newPlanRegistration.Create(dbContext);
                }

                if (missingDates.Count > 0)
                {
                    planningsInPeriod = await dbContext.PlanRegistrations
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .Where(x => x.SdkSitId == dbAssignedSite.SiteId)
                        .Where(x => x.Date >= model.DateFrom)
                        .Where(x => x.Date <= model.DateTo)
                        .OrderBy(x => x.Date)
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
                    var midnight = new DateTime(planRegistration.Date.Year, planRegistration.Date.Month, planRegistration.Date.Day, 0, 0, 0);

                    var planningModel = new TimePlanningPlanningPrDayModel
                    {
                        Id = planRegistration.Id,
                        SiteName = site.Name,
                        Date = midnight,
                        PlanText = planRegistration.PlanText,
                        PlanHours = planRegistration.PlanHours,
                        Message = planRegistration.MessageId,
                        SiteId = dbAssignedSite.SiteId,
                        WeekDay = planRegistration.Date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)planRegistration.Date.DayOfWeek,
                        ActualHours = planRegistration.NettoHours,
                        Difference = planRegistration.NettoHours - planRegistration.PlanHours,
                        PlanHoursMatched = Math.Abs(planRegistration.NettoHours - planRegistration.PlanHours) <= 0.00,
                        WorkDayStarted = planRegistration.Start1Id != 0,
                        WorkDayEnded = planRegistration.Stop1Id != 0 || (planRegistration.Start2Id != 0 && planRegistration.Stop2Id != 0),
                        PlannedStartOfShift1 = planRegistration.PlannedStartOfShift1,
                        PlannedEndOfShift1 = planRegistration.PlannedEndOfShift1,
                        PlannedBreakOfShift1 = planRegistration.PlannedBreakOfShift1,
                        PlannedStartOfShift2 = planRegistration.PlannedStartOfShift2,
                        PlannedEndOfShift2 = planRegistration.PlannedEndOfShift2,
                        PlannedBreakOfShift2 = planRegistration.PlannedBreakOfShift2,
                        IsDoubleShift = planRegistration.Start2StartedAt != planRegistration.Stop2StoppedAt,
                        OnVacation = planRegistration.OnVacation,
                        Sick = planRegistration.Sick,
                        OtherAllowedAbsence = planRegistration.OtherAllowedAbsence,
                        AbsenceWithoutPermission = planRegistration.AbsenceWithoutPermission,
                        Start1StartedAt = (planRegistration.Start1Id == 0 ? null : midnight.AddMinutes(
                            (planRegistration.Start1Id * 5) - 5)),
                        Stop1StoppedAt = (planRegistration.Stop1Id == 0 ? null : midnight.AddMinutes(
                            (planRegistration.Stop1Id * 5) - 5)),
                        Start2StartedAt = (planRegistration.Start2Id == 0 ? null : midnight.AddMinutes(
                            (planRegistration.Start2Id * 5) - 5)),
                        Stop2StoppedAt = (planRegistration.Stop2Id == 0 ? null : midnight.AddMinutes(
                            (planRegistration.Stop2Id * 5) - 5)),
                        Break1Shift = planRegistration.Pause1Id,
                        Break2Shift = planRegistration.Pause2Id,
                        PauseMinutes = planRegistration.Pause1Id * 5 + planRegistration.Pause2Id * 5
                    };
                    try
                    {
                        if (dbAssignedSite.UseGoogleSheetAsDefault)
                        {
                            if (planRegistration.PlannedStartOfShift1 == 0 && !string.IsNullOrEmpty(planRegistration.PlanText) &&
                                planRegistration.PlanHours > 0)
                            {
                                // split the planText by this regex (.*)-(.*)\/(.*)
                                // the parts are in hours, so we need to multiply by 60 to get minutes and can be like 7.30 or 7:30 so it can be 7.5, 7:30, 7½ and they are all the same
                                // so we parse the first part and multiply by 60 and just add the second part
                                // the last part is the break in minutes and can be ¾ or ½
                                var regex = new Regex(@"(.*)-(.*)\/(.*)");
                                var match = regex.Match(planRegistration.PlanText);
                                if (match.Captures.Count == 0)
                                {
                                    regex = new Regex(@"(.*)-(.*)");
                                    match = regex.Match(planRegistration.PlanText);
                                }

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
                                var secondPartMinutes = secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                                var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                                planRegistration.PlannedStartOfShift1 = firstPartTotalMinutes;
                                planRegistration.PlannedEndOfShift1 = secondPartTotalMinutes;

                                if (match.Groups.Count == 4)
                                {
                                    var breakPart = match.Groups[3].Value;
                                    var breakPartMinutes = breakPart switch
                                    {
                                        "¾" => 45,
                                        "½" => 30,
                                        "1" => 60,
                                        _ => 0
                                    };

                                    planRegistration.PlannedBreakOfShift1 = breakPartMinutes;
                                }

                                await planRegistration.Update(dbContext).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            if (planRegistration.Date > DateTime.Now)
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

                                await planRegistration.Update(dbContext).ConfigureAwait(false);
                            }
                        }

                        planningModel.PlannedBreakOfShift1 = planRegistration.PlannedBreakOfShift1;
                        planningModel.PlannedStartOfShift1 = planRegistration.PlannedStartOfShift1;
                        planningModel.PlannedEndOfShift1 = planRegistration.PlannedEndOfShift1;
                        planningModel.PlanHoursMatched = Math.Abs(planRegistration.NettoHours - planRegistration.PlanHours) <= 0.00;

                    }
                    catch (Exception e)
                    {
                        logger.LogError($"Could not parse PlanText for planning with id: {planRegistration.Id} the PlanText was: {planRegistration.PlanText}");
                        SentrySdk.CaptureMessage($"Could not parse PlanText for planning with id: {planRegistration.Id} the PlanText was: {planRegistration.PlanText}");
                        //SentrySdk.CaptureException(e);
                        logger.LogError(e.Message);
                        logger.LogTrace(e.StackTrace);
                    }

                    siteModel.PlanningPrDayModels.Add(planningModel);
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
        var date = model.DateFrom;
        var midnightOfDateFrom = new DateTime(date!.Value.Year, date.Value.Month, date.Value.Day, 0, 0, 0);
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
            .Where(x => x.Date >= model.DateFrom)
            .Where(x => x.Date <= model.DateTo)
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

            await newPlanRegistration.Create(dbContext);
        }

        if (missingDates.Count > 0)
        {
            planningsInPeriod = await dbContext.PlanRegistrations
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.SdkSitId == dbAssignedSite.SiteId)
                .Where(x => x.Date >= model.DateFrom)
                .Where(x => x.Date <= model.DateTo)
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
                Difference = planRegistration.NettoHours - planRegistration.PlanHours,
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
                IsDoubleShift = planRegistration.Start2StartedAt != planRegistration.Stop2StoppedAt,
                OnVacation = planRegistration.OnVacation,
                Sick = planRegistration.Sick,
                OtherAllowedAbsence = planRegistration.OtherAllowedAbsence,
                AbsenceWithoutPermission = planRegistration.AbsenceWithoutPermission,
                Start1StartedAt = (planRegistration.Start1Id == 0
                    ? null
                    : midnight.AddMinutes(
                        (planRegistration.Start1Id * 5) - 5)),
                Stop1StoppedAt = (planRegistration.Stop1Id == 0
                    ? null
                    : midnight.AddMinutes(
                        (planRegistration.Stop1Id * 5) - 5)),
                Start2StartedAt = (planRegistration.Start2Id == 0
                    ? null
                    : midnight.AddMinutes(
                        (planRegistration.Start2Id * 5) - 5)),
                Stop2StoppedAt = (planRegistration.Stop2Id == 0
                    ? null
                    : midnight.AddMinutes(
                        (planRegistration.Stop2Id * 5) - 5)),
                Break1Shift = planRegistration.Pause1Id,
                Break2Shift = planRegistration.Pause2Id,
                PauseMinutes = planRegistration.Pause1Id * 5 + planRegistration.Pause2Id * 5
            };
            try
            {
                if (dbAssignedSite.UseGoogleSheetAsDefault)
                {
                    if (planRegistration.PlannedStartOfShift1 == 0 &&
                        !string.IsNullOrEmpty(planRegistration.PlanText) &&
                        planRegistration.PlanHours > 0)
                    {
                        // split the planText by this regex (.*)-(.*)\/(.*)
                        // the parts are in hours, so we need to multiply by 60 to get minutes and can be like 7.30 or 7:30 so it can be 7.5, 7:30, 7½ and they are all the same
                        // so we parse the first part and multiply by 60 and just add the second part
                        // the last part is the break in minutes and can be ¾ or ½
                        var regex = new Regex(@"(.*)-(.*)\/(.*)");
                        var match = regex.Match(planRegistration.PlanText);
                        if (match.Captures.Count == 0)
                        {
                            regex = new Regex(@"(.*)-(.*)");
                            match = regex.Match(planRegistration.PlanText);
                        }

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
                        var secondPartMinutes = secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                        var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                        planRegistration.PlannedStartOfShift1 = firstPartTotalMinutes;
                        planRegistration.PlannedEndOfShift1 = secondPartTotalMinutes;

                        if (match.Groups.Count == 4)
                        {
                            var breakPart = match.Groups[3].Value;
                            var breakPartMinutes = breakPart switch
                            {
                                "¾" => 45,
                                "½" => 30,
                                "1" => 60,
                                _ => 0
                            };

                            planRegistration.PlannedBreakOfShift1 = breakPartMinutes;
                        }

                        await planRegistration.Update(dbContext).ConfigureAwait(false);
                    }
                }
                else
                {
                    if (planRegistration.Date > DateTime.Now)
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

                planningModel.PlannedBreakOfShift1 = planRegistration.PlannedBreakOfShift1;
                planningModel.PlannedStartOfShift1 = planRegistration.PlannedStartOfShift1;
                planningModel.PlannedEndOfShift1 = planRegistration.PlannedEndOfShift1;
                planningModel.PlanHoursMatched = Math.Abs(planRegistration.NettoHours - planRegistration.PlanHours) <= 0.00;
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

            siteModel.PlanningPrDayModels.Add(planningModel);
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
                .Where(x => x.Id == id)
                .FirstOrDefault();

            if (planning == null)
            {
                return new OperationResult(
                    false,
                    localizationService.GetString("PlanningNotFound"));
            }

            planning.MessageId = model.Message;
            planning.PlanHours = model.PlanHours;

            var preTimePlanning =
                await dbContext.PlanRegistrations.AsNoTracking()
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.Date < planning.Date
                                && x.SdkSitId == planning.SdkSitId)
                    .OrderByDescending(x => x.Date)
                    .FirstOrDefaultAsync();


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
    //
    // public async Task<OperationResult> UpdateCreatePlanning(TimePlanningPlanningUpdateModel model)
    // {
    //     try
    //     {
    //         var planning = await dbContext.PlanRegistrations
    //             .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
    //             .Where(x => x.SdkSitId == model.SiteId)
    //             .Where(x => x.Date == model.Date)
    //             .FirstOrDefaultAsync();
    //         if (planning != null)
    //         {
    //             return await UpdatePlanning(planning, model);
    //         }
    //
    //         return await CreatePlanning(model, model.SiteId);
    //     }
    //     catch (Exception e)
    //     {
    //         SentrySdk.CaptureException(e);
    //         logger.LogError(e.Message);
    //         logger.LogTrace(e.StackTrace);
    //         return new OperationResult(
    //             false,
    //             localizationService.GetString("ErrorWhileUpdatePlanning"));
    //     }
    // }
    //
    // private async Task<OperationResult> CreatePlanning(TimePlanningPlanningUpdateModel model, int sdkSiteId)
    // {
    //     try
    //     {
    //         var planning = new PlanRegistration
    //         {
    //             PlanText = model.PlanText,
    //             SdkSitId = sdkSiteId,
    //             Date = model.Date,
    //             PlanHours = model.PlanHours,
    //             CreatedByUserId = userService.UserId,
    //             UpdatedByUserId = userService.UserId,
    //             MessageId = model.Message
    //         };
    //
    //         await planning.Create(dbContext);
    //
    //         return new OperationResult(
    //             true,
    //             localizationService.GetString("SuccessfullyCreatePlanning"));
    //     }
    //     catch (Exception e)
    //     {
    //         SentrySdk.CaptureException(e);
    //         logger.LogError(e.Message);
    //         logger.LogTrace(e.StackTrace);
    //         return new OperationResult(
    //             false,
    //             localizationService.GetString("ErrorWhileCreatePlanning"));
    //     }
    // }
    //
    // private async Task<OperationResult> UpdatePlanning(PlanRegistration planning,
    //     TimePlanningPlanningUpdateModel model)
    // {
    //     try
    //     {
    //         planning.MessageId = model.Message;
    //         planning.PlanText = model.PlanText;
    //         planning.PlanHours = model.PlanHours;
    //         planning.UpdatedByUserId = userService.UserId;
    //
    //         await planning.Update(dbContext);
    //
    //         return new OperationResult(
    //             true,
    //             localizationService.GetString("SuccessfullyUpdatePlanning"));
    //     }
    //     catch (Exception e)
    //     {
    //         SentrySdk.CaptureException(e);
    //         logger.LogError(e.Message);
    //         logger.LogTrace(e.StackTrace);
    //         return new OperationResult(
    //             false,
    //             localizationService.GetString("ErrorWhileUpdatePlanning"));
    //     }
    // }
}