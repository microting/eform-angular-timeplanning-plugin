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

#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using DocumentFormat.OpenXml;
using Microsoft.AspNetCore.Http;
using TimePlanning.Pn.Resources;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using Microting.eForm.Infrastructure.Data.Entities;
using Microting.EformAngularFrontendBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using Sentry;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Data.Seed.Data;
using Microting.TimePlanningBase.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.UpdateCreate;

namespace TimePlanning.Pn.Services.TimePlanningWorkingHoursService;

using System;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Models.Settings;
using Infrastructure.Models.WorkingHours.Index;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.TimePlanningBase.Infrastructure.Data;
using TimePlanningLocalizationService;

/// <summary>
/// TimePlanningWorkingHoursService
/// </summary>
public class TimePlanningWorkingHoursService(
    ILogger<TimePlanningWorkingHoursService> logger,
    TimePlanningPnDbContext dbContext,
    IUserService userService,
    ITimePlanningLocalizationService localizationService,
    BaseDbContext baseDbContext,
    IPluginDbOptions<TimePlanningBaseSettings> options,
    IEFormCoreService coreHelper)
    : ITimePlanningWorkingHoursService
{
    public async Task<OperationDataResult<List<TimePlanningWorkingHoursModel>>> Index(
        TimePlanningWorkingHoursRequestModel model)
    {
        try
        {
            model.DateFrom = new DateTime(model.DateFrom.Year, model.DateFrom.Month, model.DateFrom.Day, 0, 0, 0);
            model.DateTo = new DateTime(model.DateTo.Year, model.DateTo.Month, model.DateTo.Day, 0, 0, 0);
            var core = await coreHelper.GetCore();
            await using var sdkDbContext = core.DbContextHelper.GetDbContext();
            var maxDaysEditable = options.Value.MaxDaysEditable;
            var language = await userService.GetCurrentUserLanguage();
            var ci = new CultureInfo(language.LanguageCode);
            List<(DateTime, string)> tupleValueList = new();
            var site = await sdkDbContext.Sites
                .AsNoTracking()
                .Where(x => x.MicrotingUid == model.SiteId)
                .Select(x => new
                {
                    x.Id,
                    x.Name
                })
                .FirstAsync();

            // Phase 2: look up the assigned site so we can fork the SumFlex
            // recompute chain to use *InSeconds as the source of truth when
            // UseOneMinuteIntervals is on.
            var assignedSite = await dbContext.AssignedSites
                .AsNoTracking()
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefaultAsync(x => x.SiteId == model.SiteId);
            var useOneMinuteIntervals = assignedSite?.UseOneMinuteIntervals ?? false;

            var timePlanningRequest = dbContext.PlanRegistrations
                .AsNoTracking()
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.SdkSitId == model.SiteId);

            // two dates may be displayed instead of one if the same date is selected.
            if (model.DateFrom == model.DateTo)
            {
                timePlanningRequest = timePlanningRequest
                    .Where(x => x.Date == model.DateFrom);
            }
            else
            {
                timePlanningRequest = timePlanningRequest
                    .Where(x => x.Date >= model.DateFrom && x.Date <= model.DateTo);
            }

            var dateTime = DateTime.Now;
            var midnight = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, 0, 0, 0);

            var timePlannings = await timePlanningRequest
                .Select(x => new TimePlanningWorkingHoursModel
                {
                    Id = x.Id,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                    WorkerName = site.Name,
                    WeekDay = (int)x.Date.DayOfWeek,
                    Date = x.Date,
                    PlanText = x.PlanText,
                    PlanHours = x.PlanHours,
                    Shift1Start = x.Start1Id,
                    Shift1Stop = x.Stop1Id,
                    Shift1Pause = x.Pause1Id,
                    Shift2Start = x.Start2Id,
                    Shift2Stop = x.Stop2Id,
                    Shift2Pause = x.Pause2Id,
                    Shift3Start = x.Start3Id,
                    Shift3Stop = x.Stop3Id,
                    Shift3Pause = x.Pause3Id,
                    Shift4Start = x.Start4Id,
                    Shift4Stop = x.Stop4Id,
                    Shift4Pause = x.Pause4Id,
                    Shift5Start = x.Start5Id,
                    Shift5Stop = x.Stop5Id,
                    Shift5Pause = x.Pause5Id,
                    // Real wall-clock timestamps used for accurate pay-line time-band attribution.
                    // The Shift{N}Start/Stop above are legacy 5-minute slot indices kept for
                    // backwards compatibility; payroll calculations consume these *StartedAt/*StoppedAt fields.
                    Start1StartedAt = x.Start1StartedAt,
                    Stop1StoppedAt = x.Stop1StoppedAt,
                    Start2StartedAt = x.Start2StartedAt,
                    Stop2StoppedAt = x.Stop2StoppedAt,
                    Start3StartedAt = x.Start3StartedAt,
                    Stop3StoppedAt = x.Stop3StoppedAt,
                    Start4StartedAt = x.Start4StartedAt,
                    Stop4StoppedAt = x.Stop4StoppedAt,
                    Start5StartedAt = x.Start5StartedAt,
                    Stop5StoppedAt = x.Stop5StoppedAt,
                    NettoHours = Math.Round(x.NettoHours, 2),
                    FlexHours = Math.Round(x.Flex, 2),
                    SumFlexStart = Math.Round(x.SumFlexStart, 2),
                    PaidOutFlex = x.PaiedOutFlex.ToString().Replace(",", "."),
                    // Phase 2: carry the second-precision siblings through to the
                    // model so the SumFlex chain below can fork to seconds-math
                    // when UseOneMinuteIntervals is on.
                    NettoHoursInSeconds = x.NettoHoursInSeconds,
                    FlexInSeconds = x.FlexInSeconds,
                    SumFlexStartInSeconds = x.SumFlexStartInSeconds,
                    SumFlexEndInSeconds = x.SumFlexEndInSeconds,
                    PaiedOutFlexInSeconds = x.PaiedOutFlexInSeconds,
                    Message = x.MessageId,
                    CommentWorker = x.WorkerComment.Replace("\r", "<br />"),
                    CommentOffice = x.CommentOffice.Replace("\r", "<br />"),
                    // CommentOfficeAll = x.CommentOfficeAll,
                    IsLocked = (x.Date < DateTime.Now.AddDays(-(int)(maxDaysEditable ?? 0)) || x.Date == midnight),
                    IsWeekend = x.Date.DayOfWeek == DayOfWeek.Saturday || x.Date.DayOfWeek == DayOfWeek.Sunday,
                    NettoHoursOverride = x.NettoHoursOverride,
                    NettoHoursOverrideActive = x.NettoHoursOverrideActive,
                    IsSaturday = x.Date.DayOfWeek == DayOfWeek.Saturday,
                    IsSunday = x.Date.DayOfWeek == DayOfWeek.Sunday
                })
                .ToListAsync();

            // Populate the canonical per-shift total pause minutes (all slots, not
            // just the primary Pause{N}Id) for the Excel export. ComputeShiftPauseSeconds
            // is not EF-translatable, so materialize the pause columns for these same
            // rows in ONE batched query (keyed by Id) and compute in memory — no N+1.
            if (timePlannings.Count > 0)
            {
                var pauseRows = await timePlanningRequest
                    .Select(x => new PlanRegistration
                    {
                        Id = x.Id,
                        Pause1Id = x.Pause1Id,
                        Pause2Id = x.Pause2Id,
                        Pause1StartedAt = x.Pause1StartedAt,
                        Pause1StoppedAt = x.Pause1StoppedAt,
                        Pause10StartedAt = x.Pause10StartedAt,
                        Pause10StoppedAt = x.Pause10StoppedAt,
                        Pause11StartedAt = x.Pause11StartedAt,
                        Pause11StoppedAt = x.Pause11StoppedAt,
                        Pause12StartedAt = x.Pause12StartedAt,
                        Pause12StoppedAt = x.Pause12StoppedAt,
                        Pause13StartedAt = x.Pause13StartedAt,
                        Pause13StoppedAt = x.Pause13StoppedAt,
                        Pause14StartedAt = x.Pause14StartedAt,
                        Pause14StoppedAt = x.Pause14StoppedAt,
                        Pause15StartedAt = x.Pause15StartedAt,
                        Pause15StoppedAt = x.Pause15StoppedAt,
                        Pause16StartedAt = x.Pause16StartedAt,
                        Pause16StoppedAt = x.Pause16StoppedAt,
                        Pause17StartedAt = x.Pause17StartedAt,
                        Pause17StoppedAt = x.Pause17StoppedAt,
                        Pause18StartedAt = x.Pause18StartedAt,
                        Pause18StoppedAt = x.Pause18StoppedAt,
                        Pause19StartedAt = x.Pause19StartedAt,
                        Pause19StoppedAt = x.Pause19StoppedAt,
                        Pause100StartedAt = x.Pause100StartedAt,
                        Pause100StoppedAt = x.Pause100StoppedAt,
                        Pause101StartedAt = x.Pause101StartedAt,
                        Pause101StoppedAt = x.Pause101StoppedAt,
                        Pause102StartedAt = x.Pause102StartedAt,
                        Pause102StoppedAt = x.Pause102StoppedAt,
                        Pause2StartedAt = x.Pause2StartedAt,
                        Pause2StoppedAt = x.Pause2StoppedAt,
                        Pause20StartedAt = x.Pause20StartedAt,
                        Pause20StoppedAt = x.Pause20StoppedAt,
                        Pause21StartedAt = x.Pause21StartedAt,
                        Pause21StoppedAt = x.Pause21StoppedAt,
                        Pause22StartedAt = x.Pause22StartedAt,
                        Pause22StoppedAt = x.Pause22StoppedAt,
                        Pause23StartedAt = x.Pause23StartedAt,
                        Pause23StoppedAt = x.Pause23StoppedAt,
                        Pause24StartedAt = x.Pause24StartedAt,
                        Pause24StoppedAt = x.Pause24StoppedAt,
                        Pause25StartedAt = x.Pause25StartedAt,
                        Pause25StoppedAt = x.Pause25StoppedAt,
                        Pause26StartedAt = x.Pause26StartedAt,
                        Pause26StoppedAt = x.Pause26StoppedAt,
                        Pause27StartedAt = x.Pause27StartedAt,
                        Pause27StoppedAt = x.Pause27StoppedAt,
                        Pause28StartedAt = x.Pause28StartedAt,
                        Pause28StoppedAt = x.Pause28StoppedAt,
                        Pause29StartedAt = x.Pause29StartedAt,
                        Pause29StoppedAt = x.Pause29StoppedAt,
                        Pause200StartedAt = x.Pause200StartedAt,
                        Pause200StoppedAt = x.Pause200StoppedAt,
                        Pause201StartedAt = x.Pause201StartedAt,
                        Pause201StoppedAt = x.Pause201StoppedAt,
                        Pause202StartedAt = x.Pause202StartedAt,
                        Pause202StoppedAt = x.Pause202StoppedAt,
                    })
                    .ToListAsync();

                var pauseRowsById = pauseRows.ToDictionary(x => x.Id);
                foreach (var tp in timePlannings)
                {
                    if (tp.Id is not { } id || !pauseRowsById.TryGetValue(id, out var pauseRow))
                    {
                        continue;
                    }

                    tp.Shift1PauseMinutes =
                        PlanRegistrationHelper.ComputeShiftPauseSeconds(pauseRow, 1, useOneMinuteIntervals) / 60;
                    tp.Shift2PauseMinutes =
                        PlanRegistrationHelper.ComputeShiftPauseSeconds(pauseRow, 2, useOneMinuteIntervals) / 60;
                }
            }

            var totalDays = (int)(model.DateTo - model.DateFrom).TotalDays + 1;

            var lastPlanning = dbContext.PlanRegistrations
                .AsNoTracking()
                .Where(x => x.Date < model.DateFrom)
                .Where(x => x.SdkSitId == model.SiteId).OrderBy(x => x.Date).LastOrDefault();

            if (lastPlanning != null)
            {
                // lastPlanning.Date = new DateTime(lastPlanning.Date.Year, lastPlanning.Date.Month, lastPlanning.Date.Day, 0, 0, 0);


                try
                {
                    var prePlanning = new TimePlanningWorkingHoursModel
                    {
                        Id = 0,
                        CreatedAt = lastPlanning.CreatedAt,
                        UpdatedAt = lastPlanning.UpdatedAt,
                        WorkerName = site.Name,
                        WeekDay = lastPlanning != null
                            ? (int)lastPlanning.Date.DayOfWeek
                            : (int)model.DateFrom.AddDays(-1).DayOfWeek,
                        Date = lastPlanning?.Date ?? model.DateFrom.AddDays(-1),
                        PlanText = lastPlanning?.PlanText,
                        PlanHours = lastPlanning?.PlanHours ?? 0,
                        Shift1Start = lastPlanning?.Start1Id,
                        Shift1Stop = lastPlanning?.Stop1Id,
                        Shift1Pause = lastPlanning?.Pause1Id,
                        Shift2Start = lastPlanning?.Start2Id,
                        Shift2Stop = lastPlanning?.Stop2Id,
                        Shift2Pause = lastPlanning?.Pause2Id,
                        // Canonical all-slots pause totals (not the legacy single Pause{N}Id)
                        // for the carried-over previous-day row. lastPlanning is a fully
                        // materialized PlanRegistration already in scope, so this reuses
                        // ComputeShiftPauseSeconds with no extra query (no N+1).
                        Shift1PauseMinutes = lastPlanning != null
                            ? PlanRegistrationHelper.ComputeShiftPauseSeconds(lastPlanning, 1, useOneMinuteIntervals) / 60
                            : 0,
                        Shift2PauseMinutes = lastPlanning != null
                            ? PlanRegistrationHelper.ComputeShiftPauseSeconds(lastPlanning, 2, useOneMinuteIntervals) / 60
                            : 0,
                        Shift3Start = lastPlanning?.Start3Id,
                        Shift3Stop = lastPlanning?.Stop3Id,
                        Shift3Pause = lastPlanning?.Pause3Id,
                        Shift4Start = lastPlanning?.Start4Id,
                        Shift4Stop = lastPlanning?.Stop4Id,
                        Shift4Pause = lastPlanning?.Pause4Id,
                        Shift5Start = lastPlanning?.Start5Id,
                        Shift5Stop = lastPlanning?.Stop5Id,
                        Shift5Pause = lastPlanning?.Pause5Id,
                        // Real wall-clock timestamps (source of truth for pay-line calculations)
                        Start1StartedAt = lastPlanning?.Start1StartedAt,
                        Stop1StoppedAt = lastPlanning?.Stop1StoppedAt,
                        Start2StartedAt = lastPlanning?.Start2StartedAt,
                        Stop2StoppedAt = lastPlanning?.Stop2StoppedAt,
                        Start3StartedAt = lastPlanning?.Start3StartedAt,
                        Stop3StoppedAt = lastPlanning?.Stop3StoppedAt,
                        Start4StartedAt = lastPlanning?.Start4StartedAt,
                        Stop4StoppedAt = lastPlanning?.Stop4StoppedAt,
                        Start5StartedAt = lastPlanning?.Start5StartedAt,
                        Stop5StoppedAt = lastPlanning?.Stop5StoppedAt,
                        NettoHours = Math.Round(lastPlanning?.NettoHours ?? 0, 2),
                        FlexHours = Math.Round(lastPlanning?.Flex ?? 0, 2),
                        SumFlexStart = lastPlanning?.SumFlexStart ?? 0,
                        PaidOutFlex = lastPlanning?.PaiedOutFlex.ToString().Replace(",", ".") ?? "0",
                        // Phase 2: second-precision siblings for the SumFlex chain.
                        NettoHoursInSeconds = lastPlanning?.NettoHoursInSeconds ?? 0,
                        FlexInSeconds = lastPlanning?.FlexInSeconds ?? 0,
                        SumFlexStartInSeconds = lastPlanning?.SumFlexStartInSeconds ?? 0,
                        SumFlexEndInSeconds = lastPlanning?.SumFlexEndInSeconds ?? 0,
                        PaiedOutFlexInSeconds = lastPlanning?.PaiedOutFlexInSeconds ?? 0,
                        Message = lastPlanning?.MessageId,
                        CommentWorker = lastPlanning?.WorkerComment?.Replace("\r", "<br />"),
                        CommentOffice = lastPlanning?.CommentOffice?.Replace("\r", "<br />"),
                        IsLocked = true,
                        IsWeekend = lastPlanning != null
                            ? lastPlanning.Date.DayOfWeek == DayOfWeek.Saturday ||
                              lastPlanning.Date.DayOfWeek == DayOfWeek.Sunday
                            : model.DateFrom.AddDays(-1).DayOfWeek == DayOfWeek.Saturday ||
                              model.DateFrom.AddDays(-1).DayOfWeek == DayOfWeek.Sunday
                    };

                    timePlannings.Add(prePlanning);

                }
                catch (Exception e)
                {
                    SentrySdk.CaptureException(e);
                    logger.LogError(e.Message);
                    logger.LogTrace(e.StackTrace);
                }
            }

            if (timePlannings.Count - 1 < totalDays)
            {
                var timePlanningForAdd = new List<TimePlanningWorkingHoursModel>();
                for (var i = 0; i < totalDays; i++)
                {
                    if (timePlannings.All(x => x.Date != model.DateFrom.AddDays(i)))
                    {
                        timePlanningForAdd.Add(new TimePlanningWorkingHoursModel
                        {
                            Date = model.DateFrom.AddDays(i),
                            WeekDay = (int)model.DateFrom.AddDays(i).DayOfWeek,
                            IsLocked = model.DateFrom.AddDays(i) < DateTime.Now.AddDays(-(int)(maxDaysEditable ?? 0)) ||
                                       model.DateFrom.AddDays(i) == midnight,
                            IsWeekend = model.DateFrom.AddDays(i).DayOfWeek == DayOfWeek.Saturday
                                        || model.DateFrom.AddDays(i).DayOfWeek == DayOfWeek.Sunday
                            //WorkerId = model.WorkerId,
                        });
                    }
                }

                timePlannings.AddRange(timePlanningForAdd);
            }

            timePlannings = timePlannings.OrderBy(x => x.Date).ToList();

            var j = 0;
            double sumFlexEnd = 0;
            // Phase 2: parallel running balance in seconds for flag-on chain.
            int sumFlexEndInSeconds = 0;
            //double SumFlexStart = 0;
            foreach (var timePlanningWorkingHoursModel in timePlannings)
            {
                if (j == 0)
                {
                    if (useOneMinuteIntervals)
                    {
                        // Phase 2: chain in seconds; back-derive doubles via /3600.0.
                        timePlanningWorkingHoursModel.SumFlexStartInSeconds =
                            timePlanningWorkingHoursModel.SumFlexStartInSeconds;
                        timePlanningWorkingHoursModel.SumFlexStart =
                            timePlanningWorkingHoursModel.SumFlexStartInSeconds / 3600.0;
                        timePlanningWorkingHoursModel.SumFlexEndInSeconds =
                            timePlanningWorkingHoursModel.SumFlexStartInSeconds
                            + timePlanningWorkingHoursModel.FlexInSeconds
                            - timePlanningWorkingHoursModel.PaiedOutFlexInSeconds;
                        timePlanningWorkingHoursModel.SumFlexEnd =
                            timePlanningWorkingHoursModel.SumFlexEndInSeconds / 3600.0;
                        sumFlexEndInSeconds = timePlanningWorkingHoursModel.SumFlexEndInSeconds;
                        sumFlexEnd = timePlanningWorkingHoursModel.SumFlexEnd;
                    }
                    else
                    {
                        timePlanningWorkingHoursModel.SumFlexStart =
                            Math.Round(timePlanningWorkingHoursModel.SumFlexStart, 2);
                        timePlanningWorkingHoursModel.SumFlexEnd = Math.Round(
                            timePlanningWorkingHoursModel.SumFlexStart + timePlanningWorkingHoursModel.FlexHours -
                            (string.IsNullOrEmpty(timePlanningWorkingHoursModel.PaidOutFlex)
                                ? 0
                                : double.Parse(timePlanningWorkingHoursModel.PaidOutFlex.Replace(",", "."),
                                    CultureInfo.InvariantCulture)), 2);
                        sumFlexEnd = timePlanningWorkingHoursModel.SumFlexEnd;
                    }
                }
                else
                {
                    if (useOneMinuteIntervals)
                    {
                        timePlanningWorkingHoursModel.SumFlexStartInSeconds = sumFlexEndInSeconds;
                        timePlanningWorkingHoursModel.SumFlexStart = sumFlexEndInSeconds / 3600.0;
                        try
                        {
                            timePlanningWorkingHoursModel.SumFlexEndInSeconds =
                                timePlanningWorkingHoursModel.SumFlexStartInSeconds
                                + timePlanningWorkingHoursModel.FlexInSeconds
                                - timePlanningWorkingHoursModel.PaiedOutFlexInSeconds;
                            timePlanningWorkingHoursModel.SumFlexEnd =
                                timePlanningWorkingHoursModel.SumFlexEndInSeconds / 3600.0;
                        }
                        catch (Exception e)
                        {
                            SentrySdk.CaptureException(e);
                            logger.LogError(e.Message);
                            logger.LogTrace(e.StackTrace);
                        }

                        sumFlexEndInSeconds = timePlanningWorkingHoursModel.SumFlexEndInSeconds;
                        sumFlexEnd = timePlanningWorkingHoursModel.SumFlexEnd;
                    }
                    else
                    {
                        timePlanningWorkingHoursModel.SumFlexStart = sumFlexEnd;
                        try
                        {
                            timePlanningWorkingHoursModel.SumFlexEnd = Math.Round(
                                timePlanningWorkingHoursModel.SumFlexStart + timePlanningWorkingHoursModel.FlexHours -
                                (string.IsNullOrEmpty(timePlanningWorkingHoursModel.PaidOutFlex)
                                    ? 0
                                    : double.Parse(timePlanningWorkingHoursModel.PaidOutFlex.Replace(",", "."),
                                        CultureInfo.InvariantCulture)), 2);
                        }
                        catch (Exception e)
                        {
                            SentrySdk.CaptureException(e);
                            logger.LogError(e.Message);
                            logger.LogTrace(e.StackTrace);
                        }

                        sumFlexEnd = timePlanningWorkingHoursModel.SumFlexEnd;
                    }
                }

                j++;
            }

            return new OperationDataResult<List<TimePlanningWorkingHoursModel>>(
                true,
                timePlannings);
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
            return new OperationDataResult<List<TimePlanningWorkingHoursModel>>(
                false,
                localizationService.GetString("ErrorWhileObtainingPlannings"));
        }
    }

    public async Task<OperationResult> CreateUpdate(TimePlanningWorkingHoursUpdateCreateModel model)
    {
        try
        {
            var planRegistrations = await dbContext.PlanRegistrations
                .Where(x => x.SdkSitId == model.SiteId)
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .ToListAsync();
            var first = true;
            foreach (var planning in model.Plannings)
            {
                planning.Date = new DateTime(planning.Date.Year, planning.Date.Month, planning.Date.Day, 0, 0, 0);
                var planRegistration = planRegistrations.FirstOrDefault(x => x.Date == planning.Date);
                if (planRegistration != null)
                {
                    await UpdatePlanning(first, planRegistration, planning, model.SiteId);
                }
                else
                {
                    if (!first)
                    {
                        await CreatePlanning(first, planning, model.SiteId, model.SiteId, planning.CommentWorker);
                    }
                }

                first = false;
            }

            // Check if there are any plannings after the last planning in the model
            var lastPlanning = model.Plannings
                .OrderByDescending(x => x.Date)
                .FirstOrDefault();
            if (lastPlanning != null)
            {
                lastPlanning.Date = new DateTime(lastPlanning.Date.Year, lastPlanning.Date.Month, lastPlanning.Date.Day,
                    0, 0, 0);
                var planRegistrationsAfterLastPlanning = planRegistrations
                    .Where(x => x.Date > lastPlanning.Date)
                    .Where(x => x.Date < DateTime.Now.AddDays(180))
                    .ToList();
                foreach (var planRegistration in planRegistrationsAfterLastPlanning)
                {
                    var preTimePlanning =
                        await dbContext.PlanRegistrations.AsNoTracking()
                            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                            .Where(x => x.Date < planRegistration.Date
                                        && x.SdkSitId == planRegistration.SdkSitId)
                            .OrderByDescending(x => x.Date)
                            .FirstOrDefaultAsync();

                    if (preTimePlanning != null)
                    {
                        planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                        planRegistration.SumFlexEnd = preTimePlanning.SumFlexEnd + planRegistration.NettoHours -
                                                      planRegistration.PlanHours -
                                                      planRegistration.PaiedOutFlex;
                        planRegistration.Flex = planRegistration.NettoHours - planRegistration.PlanHours;
                    }
                    else
                    {
                        planRegistration.SumFlexEnd = planRegistration.NettoHours - planRegistration.PlanHours -
                                                      planRegistration.PaiedOutFlex;
                        planRegistration.SumFlexStart = 0;
                        planRegistration.Flex = planRegistration.NettoHours - planRegistration.PlanHours;
                    }

                    await planRegistration.Update(dbContext);
                }
            }


            return new OperationResult(
                true,
                localizationService.GetString("SuccessfullyCreateOrUpdatePlanning"));
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
            return new OperationDataResult<List<TimePlanningWorkingHoursModel>>(
                false,
                localizationService.GetString("ErrorWhileCreateUpdatePlannings"));
        }
    }

    private async Task CreatePlanning(bool first, TimePlanningWorkingHoursModel model, int sdkSiteId,
        int microtingUid, string commentWorker)
    {
        var dateTime = DateTime.Now;
        var midnight = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, 0, 0, 0);

        if (model.Date != midnight)
        {
            try
            {
                var planRegistration = new PlanRegistration
                {
                    MessageId = model.Message == 0 ? null : model.Message,
                    PlanText = model.PlanText,
                    SdkSitId = sdkSiteId,
                    Date = model.Date,
                    PlanHours = model.PlanHours,
                    CreatedByUserId = userService.UserId,
                    UpdatedByUserId = userService.UserId,
                    CommentOffice = model.CommentOffice,
                    CommentOfficeAll = model.CommentOfficeAll,
                    NettoHours = model.NettoHours,
                    PaiedOutFlex = double.Parse(model.PaidOutFlex.Replace(",", "."), CultureInfo.InvariantCulture),
                    Pause1Id = model.Shift1Pause ?? 0,
                    Pause2Id = model.Shift2Pause ?? 0,
                    Start1Id = model.Shift1Start ?? 0,
                    Start2Id = model.Shift2Start ?? 0,
                    Stop1Id = model.Shift1Stop ?? 0,
                    Stop2Id = model.Shift2Stop ?? 0,
                    Flex = model.FlexHours,
                    StatusCaseId = 0
                };

                var preTimePlanning =
                    await dbContext.PlanRegistrations.AsNoTracking().Where(x => x.Date < planRegistration.Date
                            && x.SdkSitId == planRegistration.SdkSitId).OrderByDescending(x => x.Date)
                        .FirstOrDefaultAsync();
                if (preTimePlanning != null)
                {
                    planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                    planRegistration.SumFlexEnd = preTimePlanning.SumFlexEnd + planRegistration.Flex -
                                                  planRegistration.PaiedOutFlex;
                }
                else
                {
                    planRegistration.SumFlexEnd = planRegistration.Flex - planRegistration.PaiedOutFlex;
                    planRegistration.SumFlexStart = 0;
                }

                await planRegistration.Create(dbContext);
            }
            catch (Exception e)
            {
                SentrySdk.CaptureException(e);
                logger.LogError(e.Message);
                logger.LogTrace(e.StackTrace);
            }
        }
    }

    private async Task UpdatePlanning(bool first, PlanRegistration planRegistration,
        TimePlanningWorkingHoursModel model,
        int microtingUid)
    {
        var dateTime = DateTime.Now;
        var midnight = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, 0, 0, 0);

        if (planRegistration.Date != midnight)
        {
            planRegistration.MessageId = model.Message == 10 ? null : model.Message;
            planRegistration.PlanText = model.PlanText;
            planRegistration.Date = model.Date;
            planRegistration.PlanHours = model.PlanHours;
            planRegistration.UpdatedByUserId = userService.UserId;
            planRegistration.CommentOffice = model.CommentOffice;
            planRegistration.CommentOfficeAll = model.CommentOfficeAll;
            planRegistration.NettoHours = model.NettoHours;
            planRegistration.PaiedOutFlex = string.IsNullOrEmpty(model.PaidOutFlex)
                ? 0
                : double.Parse(model.PaidOutFlex.Replace(",", "."), CultureInfo.InvariantCulture);
            planRegistration.Pause1Id = model.Shift1Pause ?? 0;
            planRegistration.Pause2Id = model.Shift2Pause ?? 0;
            planRegistration.Start1Id = model.Shift1Start ?? 0;
            planRegistration.Start2Id = model.Shift2Start ?? 0;
            planRegistration.Stop1Id = model.Shift1Stop ?? 0;
            planRegistration.Stop2Id = model.Shift2Stop ?? 0;
            planRegistration.Flex = model.FlexHours;
        }

        // var preTimePlanning =
        //     await dbContext.PlanRegistrations.AsNoTracking()
        //         .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
        //         .Where(x => x.Date < planRegistration.Date
        //                     && x.SdkSitId == planRegistration.SdkSitId)
        //         .OrderByDescending(x => x.Date)
        //         .FirstOrDefaultAsync();
        // if (preTimePlanning != null)
        // {
        //     planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
        //     planRegistration.SumFlexEnd = preTimePlanning.SumFlexEnd + planRegistration.Flex -
        //                                   planRegistration.PaiedOutFlex;
        // }
        // else
        // {
        //     planRegistration.SumFlexEnd = planRegistration.Flex - planRegistration.PaiedOutFlex;
        //     planRegistration.SumFlexStart = 0;
        // }

        var assignedSite = await dbContext.AssignedSites
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync(x => x.SiteId == microtingUid);
        planRegistration = await PlanRegistrationHelper
            .UpdatePlanRegistration(planRegistration, dbContext, assignedSite, DateTime.Now.AddMonths(-1));

        await planRegistration.Update(dbContext);
    }

    public async Task<OperationDataResult<TimePlanningWorkingHourSimpleModel>> ReadSimple(DateTime dateTime, string? softwareVersion, string? model, string? manufacturer, string? osVersion)
    {
        var currentUserAsync = await userService.GetCurrentUserAsync();
        if (currentUserAsync == null)
        {
            return new OperationDataResult<TimePlanningWorkingHourSimpleModel>(false,
                localizationService.GetString("UserNotFound"), null!);
        }
        var currentUser = baseDbContext.Users
            .Single(x => x.Id == currentUserAsync.Id);
        var userEmail = (currentUser.Email ?? "").Trim().ToLower();
        var core = await coreHelper.GetCore();
        var sdkContext = core.DbContextHelper.GetDbContext();
        var sdkWorker = await sdkContext.Workers.FirstOrDefaultAsync(x =>
            x.Email.ToLower() == userEmail &&
            x.WorkflowState != Constants.WorkflowStates.Removed);
        var sdkSiteWorker = sdkWorker == null ? null : await sdkContext.SiteWorkers.FirstOrDefaultAsync(x =>
            x.WorkerId == sdkWorker.Id &&
            x.WorkflowState != Constants.WorkflowStates.Removed);
        var sdkSite = sdkSiteWorker == null ? null : await sdkContext.Sites.FirstOrDefaultAsync(x =>
            x.Id == sdkSiteWorker.SiteId &&
            x.WorkflowState != Constants.WorkflowStates.Removed);

        if (sdkSite == null)
        {
            return new OperationDataResult<TimePlanningWorkingHourSimpleModel>(false, "Site not found", null!);
        }

        var midnight = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, 0, 0, 0);

        var planRegistration = await dbContext.PlanRegistrations
            .Where(x => x.Date == midnight)
            .Where(x => x.SdkSitId == sdkSite.MicrotingUid)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync();

        if (planRegistration == null)
        {
            var preTimePlanning = await dbContext.PlanRegistrations.AsNoTracking()
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.Date < midnight
                            && x.SdkSitId == sdkSite.MicrotingUid)
                .OrderByDescending(x => x.Date)
                .FirstOrDefaultAsync();
            if (preTimePlanning != null)
            {
                var newTimePlanningWorkingHoursModel = new TimePlanningWorkingHourSimpleModel
                {
                    Date = midnight.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    YesterDay = midnight.AddDays(-1).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    Worker = sdkSite.Name,
                    PlanText = "",
                    PlanHours = 0,
                    NettoHours = 0,
                    FlexHours = 0,
                    SumFlexStart = Math.Round(preTimePlanning.SumFlexEnd, 2).ToString(CultureInfo.InvariantCulture),
                    SumFlexEnd = Math.Round(preTimePlanning.SumFlexEnd, 2).ToString(CultureInfo.InvariantCulture),
                    PaidOutFlex = 0,
                    CommentWorker = "",
                    CommentOffice = "",
                };

                return new OperationDataResult<TimePlanningWorkingHourSimpleModel>(true,
                    localizationService.GetString("PlanRegistrationLoaded"),
                    newTimePlanningWorkingHoursModel);
            }
            else
            {
                var newTimePlanningWorkingHoursModel = new TimePlanningWorkingHourSimpleModel
                {
                    Date = midnight.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    YesterDay = midnight.AddDays(-1).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    Worker = sdkSite.Name,
                    PlanText = "",
                    PlanHours = 0,
                    NettoHours = 0,
                    FlexHours = 0,
                    SumFlexStart = "0",
                    SumFlexEnd = "0",
                    PaidOutFlex = 0,
                    CommentWorker = "",
                    CommentOffice = "",
                };

                return new OperationDataResult<TimePlanningWorkingHourSimpleModel>(true,
                    localizationService.GetString("PlanRegistrationLoaded"),
                    newTimePlanningWorkingHoursModel);
            }
        }

        var timePlanningWorkingHoursModel = new TimePlanningWorkingHourSimpleModel
        {
            Date = midnight.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
            YesterDay = midnight.AddDays(-1).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
            Worker = sdkSite.Name,
            PlanText = planRegistration.PlanText,
            PlanHours = planRegistration.PlanHours,
            NettoHours = RoundToTwoDecimalPlaces(planRegistration.NettoHours),
            FlexHours = RoundToTwoDecimalPlaces(planRegistration.Flex),
            SumFlexStart = planRegistration.SumFlexStart == 0
                ? "0"
                : RoundToTwoDecimalPlaces(planRegistration.SumFlexStart).ToString(CultureInfo.InvariantCulture),
            SumFlexEnd = planRegistration.SumFlexEnd == 0
                ? "0"
                : RoundToTwoDecimalPlaces(planRegistration.SumFlexEnd).ToString(CultureInfo.InvariantCulture),
            PaidOutFlex = planRegistration.PaiedOutFlex,
            CommentWorker = planRegistration.WorkerComment,
            CommentOffice = planRegistration.CommentOffice,
            Start1StartedAt = RoundDownToNearestFiveMinutesAndFormat(midnight, planRegistration.Start1Id),
            Stop1StoppedAt = RoundDownToNearestFiveMinutesAndFormat(midnight, planRegistration.Stop1Id),
            Pause1TotalTime = RoundDownToNearestFiveMinutesAndFormat(midnight, planRegistration.Pause1Id),
            Start2StartedAt = RoundDownToNearestFiveMinutesAndFormat(midnight, planRegistration.Start2Id),
            Stop2StoppedAt = RoundDownToNearestFiveMinutesAndFormat(midnight, planRegistration.Stop2Id),
            Pause2TotalTime = RoundDownToNearestFiveMinutesAndFormat(midnight, planRegistration.Pause2Id)
        };

        if (model != null)
        {
            currentUser.TimeRegistrationModel = model;
            currentUser.TimeRegistrationManufacturer = manufacturer;
            currentUser.TimeRegistrationSoftwareVersion = softwareVersion;
            currentUser.TimeRegistrationOsVersion = osVersion;
            await baseDbContext.SaveChangesAsync();
        }

        return new OperationDataResult<TimePlanningWorkingHourSimpleModel>(true,
            localizationService.GetString("PlanRegistrationLoaded"),
            timePlanningWorkingHoursModel);
    }

    public async Task<OperationDataResult<TimePlanningHoursSummaryModel>> CalculateHoursSummary(DateTime startDate,
        DateTime endDate, string? softwareVersion, string? model, string? manufacturer, string? osVersion)
    {
        try
        {
            // Adjust startDate to 00:00:00 and endDate to 23:59:59
            startDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0);
            endDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59);

            var core = await coreHelper.GetCore();
            var sdkContext = core.DbContextHelper.GetDbContext();
            var currentUserAsync = await userService.GetCurrentUserAsync();
            if (currentUserAsync == null)
            {
                return new OperationDataResult<TimePlanningHoursSummaryModel>(false,
                    localizationService.GetString("UserNotFound"), null!);
            }
            var currentUser = baseDbContext.Users
                .Single(x => x.Id == currentUserAsync.Id);
            var userEmail = (currentUser.Email ?? "").Trim().ToLower();
            var sdkWorker = await sdkContext.Workers.FirstOrDefaultAsync(x =>
                x.Email.ToLower() == userEmail &&
                x.WorkflowState != Constants.WorkflowStates.Removed);
            var sdkSiteWorker = sdkWorker == null ? null : await sdkContext.SiteWorkers.FirstOrDefaultAsync(x =>
                x.WorkerId == sdkWorker.Id &&
                x.WorkflowState != Constants.WorkflowStates.Removed);
            var sdkSite = sdkSiteWorker == null ? null : await sdkContext.Sites.FirstOrDefaultAsync(x =>
                x.Id == sdkSiteWorker.SiteId &&
                x.WorkflowState != Constants.WorkflowStates.Removed);

            if (sdkSite == null)
            {
                return new OperationDataResult<TimePlanningHoursSummaryModel>(false, "Site not found", null!);
            }

            var planRegistrations = await dbContext.PlanRegistrations
                .Where(x => x.Date >= startDate && x.Date <= endDate)
                .Where(x => x.SdkSitId == sdkSite.MicrotingUid)
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .ToListAsync();

            var totalPlanHours = planRegistrations.Sum(x => x.PlanHours);
            var totalNettoHours = planRegistrations.Sum(x => x.NettoHours);
            var totalPaidOutFlex = planRegistrations.Sum(x => x.PaiedOutFlex);
            // The flex balance shown on the period status hero card is a point-in-time
            // read of the last day's stored SumFlexEnd, not a recomputation of
            // sum(NettoHours) - sum(PlanHours). The latter ignores opening balance
            // and mid-period payouts.
            var lastDay = planRegistrations
                .OrderByDescending(x => x.Date)
                .FirstOrDefault();
            var endOfPeriodFlex = lastDay?.SumFlexEnd ?? 0;

            var summary = new TimePlanningHoursSummaryModel
            {
                TotalPlanHours = totalPlanHours,
                TotalNettoHours = totalNettoHours,
                TotalPaidOutFlex = totalPaidOutFlex,
                Difference = endOfPeriodFlex
            };

            if (model != null)
            {
                currentUser.TimeRegistrationModel = model;
                currentUser.TimeRegistrationManufacturer = manufacturer;
                currentUser.TimeRegistrationSoftwareVersion = softwareVersion;
                currentUser.TimeRegistrationOsVersion = osVersion;
                await baseDbContext.SaveChangesAsync();
            }

            return new OperationDataResult<TimePlanningHoursSummaryModel>(true, summary);
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
            return new OperationDataResult<TimePlanningHoursSummaryModel>(false,
                "Error while calculating hours summary");
        }
    }


    private static double RoundToTwoDecimalPlaces(double value)
    {
        double roundedValue = Math.Round(value, 2);
        return roundedValue == -0 ? 0 : roundedValue;
    }

    private static string? RoundDownToNearestFiveMinutesAndFormat(DateTime date, int minutesToAdd)
    {
        if (minutesToAdd == 0)
        {
            return null;
        }

        var roundedDateTime = date.AddMinutes((minutesToAdd - 1) * 5);
        return roundedDateTime.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Phase 0 plumbing overload threading the UseOneMinuteIntervals flag.
    /// When true, future phases will switch to second-precision formatting
    /// from a DateTime stamp (HH:mm:ss). For Phase 0 this delegates to the
    /// existing 5-minute path so behavior is byte-identical.
    /// </summary>
    private static string? RoundDownToNearestFiveMinutesAndFormat(DateTime date, int minutesToAdd, bool useOneMinuteIntervals)
    {
        if (useOneMinuteIntervals)
        {
            // TODO Phase 4: format from DateTime stamp with second precision.
            // For Phase 0, fall through to existing 5-minute logic to preserve behavior.
        }
        return RoundDownToNearestFiveMinutesAndFormat(date, minutesToAdd);
    }

    public async Task<OperationDataResult<TimePlanningWorkingHoursModel>> Read(int sdkSiteId, DateTime dateTime,
        string token)
    {
        Console.WriteLine($"[DEBUG-GRPC-READ] Read(sdkSiteId={sdkSiteId}, dateTime={dateTime:yyyy-MM-dd}, token={token[..Math.Min(8, token.Length)]}) called");
        var result = await PlanRegistrationHelper.ReadBySiteAndDate(dbContext, sdkSiteId, dateTime, token);
        if (result == null)
        {
            Console.WriteLine($"[DEBUG-GRPC-READ] ReadBySiteAndDate returned null for sdkSiteId={sdkSiteId}, dateTime={dateTime:yyyy-MM-dd}");
            return new OperationDataResult<TimePlanningWorkingHoursModel>(false,
                localizationService.GetString("PlanRegistrationNotFound"), null!);
        }
        Console.WriteLine($"[DEBUG-GRPC-READ] ReadBySiteAndDate returned model: Id={result.Id}, SdkSiteId={result.SdkSiteId}, Date={result.Date:yyyy-MM-dd}, Start1={result.Shift1Start}, Stop1={result.Shift1Stop}, Start1StartedAt={result.Start1StartedAt}, Stop1StoppedAt={result.Stop1StoppedAt}");
        return new OperationDataResult<TimePlanningWorkingHoursModel>(true, "Plan registration found",
            result);
    }

    public async Task<OperationDataResult<TimePlanningWorkingHoursModel>> ReadFullByCurrentUser(
        DateTime dateTime,
        string? softwareVersion, string? deviceModel, string? manufacturer, string? osVersion)
    {
        Console.WriteLine($"[DEBUG-GRPC-READ] ReadFullByCurrentUser called: dateTime={dateTime:yyyy-MM-dd}");
        var currentUserAsync = await userService.GetCurrentUserAsync();
        if (currentUserAsync == null)
        {
            Console.WriteLine($"[DEBUG-GRPC-READ] EARLY RETURN: GetCurrentUserAsync returned null (JWT missing or invalid)");
            return new OperationDataResult<TimePlanningWorkingHoursModel>(false,
                localizationService.GetString("UserNotFound"), null!);
        }
        var currentUser = baseDbContext.Users
            .Single(x => x.Id == currentUserAsync.Id);
        Console.WriteLine($"[DEBUG-GRPC-READ] Current user: Id={currentUserAsync.Id}, email={currentUser.Email}");

        if (deviceModel != null)
        {
            currentUser.TimeRegistrationModel = deviceModel;
            currentUser.TimeRegistrationManufacturer = manufacturer;
            currentUser.TimeRegistrationSoftwareVersion = softwareVersion;
            currentUser.TimeRegistrationOsVersion = osVersion;
            await baseDbContext.SaveChangesAsync();
        }

        var userEmail = (currentUser.Email ?? "").Trim().ToLower();
        var core = await coreHelper.GetCore();
        var sdkContext = core.DbContextHelper.GetDbContext();
        var sdkWorker = await sdkContext.Workers.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Email.ToLower() == userEmail &&
            x.WorkflowState != Constants.WorkflowStates.Removed);
        Console.WriteLine($"[DEBUG-GRPC-READ] sdkWorker found: {sdkWorker != null}, Id={sdkWorker?.Id}");
        var sdkSiteWorker = sdkWorker == null ? null : await sdkContext.SiteWorkers.AsNoTracking().FirstOrDefaultAsync(x =>
            x.WorkerId == sdkWorker.Id &&
            x.WorkflowState != Constants.WorkflowStates.Removed);
        Console.WriteLine($"[DEBUG-GRPC-READ] sdkSiteWorker found: {sdkSiteWorker != null}, SiteId={sdkSiteWorker?.SiteId}");
        var sdkSite = sdkSiteWorker == null ? null : await sdkContext.Sites.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == sdkSiteWorker.SiteId &&
            x.WorkflowState != Constants.WorkflowStates.Removed);
        Console.WriteLine($"[DEBUG-GRPC-READ] sdkSite found: {sdkSite != null}, MicrotingUid={sdkSite?.MicrotingUid}");

        if (sdkSite == null)
        {
            Console.WriteLine($"[DEBUG-GRPC-READ] EARLY RETURN: sdkSite is null");
            return new OperationDataResult<TimePlanningWorkingHoursModel>(false,
                localizationService.GetString("SiteNotFound"), null!);
        }

        Console.WriteLine($"[DEBUG-GRPC-READ] Calling ReadBySiteAndDate: sdkSiteId={sdkSite.MicrotingUid!.Value}, dateTime={dateTime:yyyy-MM-dd}, token=null");
        var result = await PlanRegistrationHelper.ReadBySiteAndDate(dbContext, sdkSite.MicrotingUid!.Value, dateTime, null);
        if (result == null)
        {
            Console.WriteLine($"[DEBUG-GRPC-READ] ReadBySiteAndDate returned null");
            return new OperationDataResult<TimePlanningWorkingHoursModel>(false,
                localizationService.GetString("PlanRegistrationNotFound"), null!);
        }
        Console.WriteLine($"[DEBUG-GRPC-READ] ReadBySiteAndDate returned: Id={result.Id}, SdkSiteId={result.SdkSiteId}, Date={result.Date:yyyy-MM-dd}, Start1={result.Shift1Start}, Stop1={result.Shift1Stop}, Start1StartedAt={result.Start1StartedAt}, Stop1StoppedAt={result.Stop1StoppedAt}");
        return new OperationDataResult<TimePlanningWorkingHoursModel>(true, "Plan registration found",
            result);
    }

    /// <summary>
    /// Temporary, default-on, server-side guard: corrects a corrupt incoming
    /// pauseNId (the absolute-stop-tick bug on 5-minute sites) from the truthful
    /// pause timestamps before the inline netto math, so the row self-heals
    /// regardless of the client's app version. Shares the detect+correct rule
    /// with the batch repair via <see cref="PauseIdCorrection"/>. Skips 1-minute
    /// sites and obeys the <c>PauseIdSelfHealEnabled</c> kill switch (default on).
    /// </summary>
    private void SelfHealCorruptPauseIds(
        PlanRegistration pr,
        Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite? site)
    {
        if (site is null || site.UseOneMinuteIntervals) return;       // 5-minute sites only
        if (!(options.Value.PauseIdSelfHealEnabled ?? true)) return;  // kill switch, default on

        void Heal(DateTime? start, DateTime? stop, int current, int slot, Action<int> assign)
        {
            var corrected = PauseIdCorrection.CorrectedPauseId(start, stop, current);
            if (corrected is null) return;
            assign(corrected.Value);
            var msg = $"[PauseIdSelfHeal] site {pr.SdkSitId} {pr.Date:yyyy-MM-dd} "
                    + $"pause{slot}: {current} -> {corrected.Value}";
            Console.WriteLine(msg);
            SentrySdk.CaptureMessage(msg, SentryLevel.Warning);
        }

        Heal(pr.Pause1StartedAt, pr.Pause1StoppedAt, pr.Pause1Id, 1, v => pr.Pause1Id = v);
        Heal(pr.Pause2StartedAt, pr.Pause2StoppedAt, pr.Pause2Id, 2, v => pr.Pause2Id = v);
        Heal(pr.Pause3StartedAt, pr.Pause3StoppedAt, pr.Pause3Id, 3, v => pr.Pause3Id = v);
        Heal(pr.Pause4StartedAt, pr.Pause4StoppedAt, pr.Pause4Id, 4, v => pr.Pause4Id = v);
        Heal(pr.Pause5StartedAt, pr.Pause5StoppedAt, pr.Pause5Id, 5, v => pr.Pause5Id = v);
    }

    /// <summary>
    /// Flag-OFF netto computation (5-minute sites): work span in 5-minute ticks
    /// per shift minus the canonical all-slots shift pause (floor-to-5min clock
    /// tick), summed across shifts 1..5. Returns netto MINUTES. Mirrors the
    /// per-call inline blocks in the personal/kiosk create/update paths.
    /// </summary>
    private static double ComputeFlagOffNettoMinutes(PlanRegistration pr)
    {
        const int minutesMultiplier = 5;
        double nettoMinutes = 0;

        if (pr.Stop1Id >= pr.Start1Id && pr.Stop1Id != 0)
        {
            nettoMinutes += (pr.Stop1Id - pr.Start1Id) * minutesMultiplier;
            nettoMinutes -= PlanRegistrationHelper.ComputeShiftPauseSeconds(pr, 1, useOneMinuteIntervals: false) / 60.0;
        }

        if (pr.Stop2Id >= pr.Start2Id && pr.Stop2Id != 0)
        {
            nettoMinutes += (pr.Stop2Id - pr.Start2Id) * minutesMultiplier;
            nettoMinutes -= PlanRegistrationHelper.ComputeShiftPauseSeconds(pr, 2, useOneMinuteIntervals: false) / 60.0;
        }

        if (pr.Stop3Id >= pr.Start3Id && pr.Stop3Id != 0)
        {
            nettoMinutes += (pr.Stop3Id - pr.Start3Id) * minutesMultiplier;
            nettoMinutes -= PlanRegistrationHelper.ComputeShiftPauseSeconds(pr, 3, useOneMinuteIntervals: false) / 60.0;
        }

        if (pr.Stop4Id >= pr.Start4Id && pr.Stop4Id != 0)
        {
            nettoMinutes += (pr.Stop4Id - pr.Start4Id) * minutesMultiplier;
            nettoMinutes -= PlanRegistrationHelper.ComputeShiftPauseSeconds(pr, 4, useOneMinuteIntervals: false) / 60.0;
        }

        if (pr.Stop5Id >= pr.Start5Id && pr.Stop5Id != 0)
        {
            nettoMinutes += (pr.Stop5Id - pr.Start5Id) * minutesMultiplier;
            nettoMinutes -= PlanRegistrationHelper.ComputeShiftPauseSeconds(pr, 5, useOneMinuteIntervals: false) / 60.0;
        }

        return nettoMinutes;
    }

    /// <summary>
    /// Resolves the current user's timezone for wall-time normalization of
    /// incoming timestamp strings (see <see cref="WallTimeNormalizer"/>).
    /// Falls back to Europe/Copenhagen when the user has no zone stored or
    /// resolution fails.
    /// </summary>
    private async Task<TimeZoneInfo> GetCurrentUserTimeZoneOrDefaultAsync()
    {
        try
        {
            return await userService.GetCurrentUserTimeZoneInfo() ?? WallTimeNormalizer.DefaultTimeZone;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Could not resolve current user timezone, falling back to {DefaultTimeZoneId}: {Message}",
                WallTimeNormalizer.DefaultTimeZoneId, ex.Message);
            return WallTimeNormalizer.DefaultTimeZone;
        }
    }

    public async Task<OperationResult> UpdateWorkingHour(TimePlanningWorkingHoursUpdateModel model)
    {
        Console.WriteLine($"[DEBUG-GRPC-UPDATE] === UpdateWorkingHour (PERSONAL mode, 1-param) entered ===");
        Console.WriteLine($"[DEBUG-GRPC-UPDATE] model.Date={model.Date:yyyy-MM-dd}, Shift1Start={model.Shift1Start}, Shift1Stop={model.Shift1Stop}, Shift1Pause={model.Shift1Pause}");
        Console.WriteLine($"[DEBUG-GRPC-UPDATE] model.Start1StartedAt={model.Start1StartedAt}, model.Stop1StoppedAt={model.Stop1StoppedAt}");

        var sdkCore = await coreHelper.GetCore();
        var sdkDbContext = sdkCore.DbContextHelper.GetDbContext();
        var currentUserAsync = await userService.GetCurrentUserAsync();
        if (currentUserAsync == null)
        {
            Console.WriteLine($"[DEBUG-GRPC-UPDATE] EARLY RETURN: GetCurrentUserAsync returned null (JWT missing or invalid)");
            return new OperationResult(false, localizationService.GetString("UserNotFound"));
        }

        // Wall-time-at-rest hardening: any Z/offset-carrying stamp below is
        // normalized into this zone before persisting (naive digits — what the
        // app actually sends here today — pass through verbatim).
        var userTimeZone = await GetCurrentUserTimeZoneOrDefaultAsync();

        var currentUser = baseDbContext.Users
            .Single(x => x.Id == currentUserAsync.Id);
        var userEmail = (currentUser.Email ?? "").Trim().ToLower();
        Console.WriteLine($"[DEBUG-GRPC-UPDATE] Current user: Id={currentUserAsync.Id}, email={userEmail}");

        var sdkWorker = await sdkDbContext.Workers.FirstOrDefaultAsync(x =>
            x.Email.ToLower() == userEmail &&
            x.WorkflowState != Constants.WorkflowStates.Removed);
        Console.WriteLine($"[DEBUG-GRPC-UPDATE] sdkWorker found: {sdkWorker != null}, Id={sdkWorker?.Id}");

        var sdkSiteWorker = sdkWorker == null ? null : await sdkDbContext.SiteWorkers.FirstOrDefaultAsync(x =>
            x.WorkerId == sdkWorker.Id &&
            x.WorkflowState != Constants.WorkflowStates.Removed);
        Console.WriteLine($"[DEBUG-GRPC-UPDATE] sdkSiteWorker found: {sdkSiteWorker != null}, SiteId={sdkSiteWorker?.SiteId}");

        var sdkSite = sdkSiteWorker == null ? null : await sdkDbContext.Sites.FirstOrDefaultAsync(x =>
            x.Id == sdkSiteWorker.SiteId &&
            x.WorkflowState != Constants.WorkflowStates.Removed);
        Console.WriteLine($"[DEBUG-GRPC-UPDATE] sdkSite found: {sdkSite != null}, MicrotingUid={sdkSite?.MicrotingUid}");

        if (sdkSite == null)
        {
            Console.WriteLine($"[DEBUG-GRPC-UPDATE] EARLY RETURN: sdkSite is null, returning SiteNotFound");
            return new OperationResult(
                false,
                localizationService.GetString("SiteNotFound"));
        }

        var assignedSite = await dbContext.AssignedSites
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync(x => x.SiteId == sdkSite.MicrotingUid);
        Console.WriteLine($"[DEBUG-GRPC-UPDATE] assignedSite found: {assignedSite != null}, AllowEdit={assignedSite?.AllowEditOfRegistrations}, DaysBackEnabled={assignedSite?.DaysBackInTimeAllowedEditingEnabled}");

        // Guard: when both editing flags are disabled, the worker is not allowed
        // to mutate their own registrations from the mobile app — except for
        // today's registration, which is the punchclock-equivalent flow.
        var today = DateTime.Now.Date;
        if (assignedSite != null
            && !assignedSite.AllowEditOfRegistrations
            && !assignedSite.DaysBackInTimeAllowedEditingEnabled
            && model.Date.Date != today)
        {
            return new OperationResult(
                false,
                localizationService.GetString("EditingNotAllowedForWorker"));
        }

        var todayAtMidnight = model.Date;
        Console.WriteLine($"[DEBUG-GRPC-UPDATE] Querying PlanRegistrations: Date={todayAtMidnight:yyyy-MM-dd}, SdkSitId={sdkSite.MicrotingUid}");

        var planRegistration = await dbContext.PlanRegistrations
            .Where(x => x.Date == todayAtMidnight)
            .Where(x => x.SdkSitId == sdkSite.MicrotingUid)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync();
        Console.WriteLine($"[DEBUG-GRPC-UPDATE] DB lookup result: planRegistration found={planRegistration != null}, Id={planRegistration?.Id}, WorkflowState={planRegistration?.WorkflowState}");

        if (planRegistration == null)
        {
            Console.WriteLine($"[DEBUG-GRPC-UPDATE] planRegistration is NULL -- will CREATE new row for Date={model.Date:yyyy-MM-dd}, SdkSitId={sdkSite.MicrotingUid}");
            model.Date = new DateTime(model.Date.Year, model.Date.Month, model.Date.Day, 0, 0, 0);
            planRegistration = new PlanRegistration
            {
                MessageId = null,
                PlanText = "",
                Date = model.Date,
                PlanHours = 0,
                UpdatedByUserId = userService.UserId,
                CommentOffice = "",
                CommentOfficeAll = "",
                NettoHours = 0,
                PaiedOutFlex = 0,
                Pause1Id = model.Shift1Pause ?? 0,
                Pause2Id = model.Shift2Pause ?? 0,
                Start1Id = model.Shift1Start ?? 0,
                Start2Id = model.Shift2Start ?? 0,
                Stop1Id = model.Shift1Stop ?? 0,
                Stop2Id = model.Shift2Stop ?? 0,
                Start3Id = model.Shift3Start ?? 0,
                Stop3Id = model.Shift3Stop ?? 0,
                Pause3Id = model.Shift3Pause ?? 0,
                Start4Id = model.Shift4Start ?? 0,
                Stop4Id = model.Shift4Stop ?? 0,
                Pause4Id = model.Shift4Pause ?? 0,
                Start5Id = model.Shift5Start ?? 0,
                Stop5Id = model.Shift5Stop ?? 0,
                Pause5Id = model.Shift5Pause ?? 0,
                Start1StartedAt = string.IsNullOrEmpty(model.Start1StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Start1StartedAt, userTimeZone),
                Stop1StoppedAt = string.IsNullOrEmpty(model.Stop1StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Stop1StoppedAt, userTimeZone),
                Pause1StartedAt = string.IsNullOrEmpty(model.Pause1StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause1StartedAt, userTimeZone),
                Pause1StoppedAt = string.IsNullOrEmpty(model.Pause1StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause1StoppedAt, userTimeZone),
                Start2StartedAt = string.IsNullOrEmpty(model.Start2StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Start2StartedAt, userTimeZone),
                Stop2StoppedAt = string.IsNullOrEmpty(model.Stop2StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Stop2StoppedAt, userTimeZone),
                Start3StartedAt = string.IsNullOrEmpty(model.Start3StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Start3StartedAt, userTimeZone),
                Stop3StoppedAt = string.IsNullOrEmpty(model.Stop3StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Stop3StoppedAt, userTimeZone),
                Start4StartedAt = string.IsNullOrEmpty(model.Start4StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Start4StartedAt, userTimeZone),
                Stop4StoppedAt = string.IsNullOrEmpty(model.Stop4StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Stop4StoppedAt, userTimeZone),
                Start5StartedAt = string.IsNullOrEmpty(model.Start5StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Start5StartedAt, userTimeZone),
                Stop5StoppedAt = string.IsNullOrEmpty(model.Stop5StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Stop5StoppedAt, userTimeZone),
                Pause10StartedAt = string.IsNullOrEmpty(model.Pause10StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause10StartedAt, userTimeZone),
                Pause10StoppedAt = string.IsNullOrEmpty(model.Pause10StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause10StoppedAt, userTimeZone),
                Pause11StartedAt = string.IsNullOrEmpty(model.Pause11StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause11StartedAt, userTimeZone),
                Pause11StoppedAt = string.IsNullOrEmpty(model.Pause11StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause11StoppedAt, userTimeZone),
                Pause12StartedAt = string.IsNullOrEmpty(model.Pause12StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause12StartedAt, userTimeZone),
                Pause12StoppedAt = string.IsNullOrEmpty(model.Pause12StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause12StoppedAt, userTimeZone),
                Pause13StartedAt = string.IsNullOrEmpty(model.Pause13StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause13StartedAt, userTimeZone),
                Pause13StoppedAt = string.IsNullOrEmpty(model.Pause13StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause13StoppedAt, userTimeZone),
                Pause14StartedAt = string.IsNullOrEmpty(model.Pause14StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause14StartedAt, userTimeZone),
                Pause14StoppedAt = string.IsNullOrEmpty(model.Pause14StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause14StoppedAt, userTimeZone),
                Pause15StartedAt = string.IsNullOrEmpty(model.Pause15StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause15StartedAt, userTimeZone),
                Pause15StoppedAt = string.IsNullOrEmpty(model.Pause15StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause15StoppedAt, userTimeZone),
                Pause16StartedAt = string.IsNullOrEmpty(model.Pause16StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause16StartedAt, userTimeZone),
                Pause16StoppedAt = string.IsNullOrEmpty(model.Pause16StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause16StoppedAt, userTimeZone),
                Pause17StartedAt = string.IsNullOrEmpty(model.Pause17StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause17StartedAt, userTimeZone),
                Pause17StoppedAt = string.IsNullOrEmpty(model.Pause17StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause17StoppedAt, userTimeZone),
                Pause18StartedAt = string.IsNullOrEmpty(model.Pause18StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause18StartedAt, userTimeZone),
                Pause18StoppedAt = string.IsNullOrEmpty(model.Pause18StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause18StoppedAt, userTimeZone),
                Pause19StartedAt = string.IsNullOrEmpty(model.Pause19StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause19StartedAt, userTimeZone),
                Pause19StoppedAt = string.IsNullOrEmpty(model.Pause19StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause19StoppedAt, userTimeZone),
                Pause100StartedAt = string.IsNullOrEmpty(model.Pause100StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause100StartedAt, userTimeZone),
                Pause100StoppedAt = string.IsNullOrEmpty(model.Pause100StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause100StoppedAt, userTimeZone),
                Pause101StartedAt = string.IsNullOrEmpty(model.Pause101StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause101StartedAt, userTimeZone),
                Pause101StoppedAt = string.IsNullOrEmpty(model.Pause101StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause101StoppedAt, userTimeZone),
                Pause102StartedAt = string.IsNullOrEmpty(model.Pause102StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause102StartedAt, userTimeZone),
                Pause102StoppedAt = string.IsNullOrEmpty(model.Pause102StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause102StoppedAt, userTimeZone),

                Pause2StartedAt = string.IsNullOrEmpty(model.Pause2StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause2StartedAt, userTimeZone),
                Pause2StoppedAt = string.IsNullOrEmpty(model.Pause2StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause2StoppedAt, userTimeZone),
                Pause20StartedAt = string.IsNullOrEmpty(model.Pause20StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause20StartedAt, userTimeZone),
                Pause20StoppedAt = string.IsNullOrEmpty(model.Pause20StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause20StoppedAt, userTimeZone),
                Pause21StartedAt = string.IsNullOrEmpty(model.Pause21StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause21StartedAt, userTimeZone),
                Pause21StoppedAt = string.IsNullOrEmpty(model.Pause21StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause21StoppedAt, userTimeZone),
                Pause22StartedAt = string.IsNullOrEmpty(model.Pause22StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause22StartedAt, userTimeZone),
                Pause22StoppedAt = string.IsNullOrEmpty(model.Pause22StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause22StoppedAt, userTimeZone),
                Pause23StartedAt = string.IsNullOrEmpty(model.Pause23StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause23StartedAt, userTimeZone),
                Pause23StoppedAt = string.IsNullOrEmpty(model.Pause23StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause23StoppedAt, userTimeZone),
                Pause24StartedAt = string.IsNullOrEmpty(model.Pause24StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause24StartedAt, userTimeZone),
                Pause24StoppedAt = string.IsNullOrEmpty(model.Pause24StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause24StoppedAt, userTimeZone),
                Pause25StartedAt = string.IsNullOrEmpty(model.Pause25StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause25StartedAt, userTimeZone),
                Pause25StoppedAt = string.IsNullOrEmpty(model.Pause25StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause25StoppedAt, userTimeZone),
                Pause26StartedAt = string.IsNullOrEmpty(model.Pause26StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause26StartedAt, userTimeZone),
                Pause26StoppedAt = string.IsNullOrEmpty(model.Pause26StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause26StoppedAt, userTimeZone),
                Pause27StartedAt = string.IsNullOrEmpty(model.Pause27StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause27StartedAt, userTimeZone),
                Pause27StoppedAt = string.IsNullOrEmpty(model.Pause27StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause27StoppedAt, userTimeZone),
                Pause28StartedAt = string.IsNullOrEmpty(model.Pause28StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause28StartedAt, userTimeZone),
                Pause28StoppedAt = string.IsNullOrEmpty(model.Pause28StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause28StoppedAt, userTimeZone),
                Pause29StartedAt = string.IsNullOrEmpty(model.Pause29StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause29StartedAt, userTimeZone),
                Pause29StoppedAt = string.IsNullOrEmpty(model.Pause29StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause29StoppedAt, userTimeZone),
                Pause200StartedAt = string.IsNullOrEmpty(model.Pause200StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause200StartedAt, userTimeZone),
                Pause200StoppedAt = string.IsNullOrEmpty(model.Pause200StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause200StoppedAt, userTimeZone),
                Pause201StartedAt = string.IsNullOrEmpty(model.Pause201StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause201StartedAt, userTimeZone),
                Pause201StoppedAt = string.IsNullOrEmpty(model.Pause201StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause201StoppedAt, userTimeZone),
                Pause202StartedAt = string.IsNullOrEmpty(model.Pause202StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause202StartedAt, userTimeZone),
                Pause202StoppedAt = string.IsNullOrEmpty(model.Pause202StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause202StoppedAt, userTimeZone),
                Pause3StartedAt = string.IsNullOrEmpty(model.Pause3StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause3StartedAt, userTimeZone),
                Pause3StoppedAt = string.IsNullOrEmpty(model.Pause3StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause3StoppedAt, userTimeZone),
                Pause4StartedAt = string.IsNullOrEmpty(model.Pause4StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause4StartedAt, userTimeZone),
                Pause4StoppedAt = string.IsNullOrEmpty(model.Pause4StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause4StoppedAt, userTimeZone),
                Pause5StartedAt = string.IsNullOrEmpty(model.Pause5StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause5StartedAt, userTimeZone),
                Pause5StoppedAt = string.IsNullOrEmpty(model.Pause5StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause5StoppedAt, userTimeZone),
                Flex = 0,
                WorkerComment = model.CommentWorker,
                SdkSitId = sdkSite.MicrotingUid!.Value,
                Shift1PauseNumber = model.Shift1PauseNumber,
                Shift2PauseNumber = model.Shift2PauseNumber,
            };


            planRegistration = PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(assignedSite, planRegistration);

            // Self-heal a corrupt incoming pauseNId from the truthful pause
            // timestamps before the inline netto math (5-min sites, flag on).
            SelfHealCorruptPauseIds(planRegistration, assignedSite);

            double nettoMinutes = ComputeFlagOffNettoMinutes(planRegistration);

            double hours = nettoMinutes / 60;
            var preTimePlanning =
                await dbContext.PlanRegistrations.AsNoTracking()
                    .Where(x => x.Date < planRegistration.Date && x.SdkSitId == sdkSite.MicrotingUid)
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .OrderByDescending(x => x.Date).FirstOrDefaultAsync();

            // Phase 2: when UseOneMinuteIntervals is on, recompute NettoHours
            // from DateTime deltas (precise to the second) and write
            // *InSeconds columns as the source of truth; back-derive the
            // legacy double hour fields. Flag-off path stays byte-identical.
            if (assignedSite != null && assignedSite.UseOneMinuteIntervals)
            {
                PlanRegistrationHelper.ApplyNettoFlexChainSecondPrecision(
                    planRegistration,
                    preTimePlanning?.SumFlexEndInSeconds ?? 0,
                    preTimePlanning != null);
            }
            else
            {
                planRegistration.NettoHours = hours;
                planRegistration.Flex = hours - planRegistration.PlanHours;
                if (preTimePlanning != null)
                {
                    planRegistration.SumFlexEnd =
                        preTimePlanning.SumFlexEnd + planRegistration.Flex - planRegistration.PaiedOutFlex;
                    planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                }
                else
                {
                    planRegistration.SumFlexEnd = planRegistration.Flex - planRegistration.PaiedOutFlex;
                    planRegistration.SumFlexStart = 0;
                }
            }

            Console.WriteLine($"[DEBUG-GRPC-UPDATE] PERSONAL CREATE: Before planRegistration.Create(dbContext) -- SdkSitId={planRegistration.SdkSitId}, Date={planRegistration.Date:yyyy-MM-dd}, Start1Id={planRegistration.Start1Id}, Stop1Id={planRegistration.Stop1Id}, Pause1Id={planRegistration.Pause1Id}, Start1StartedAt={planRegistration.Start1StartedAt}, Stop1StoppedAt={planRegistration.Stop1StoppedAt}, NettoHours={planRegistration.NettoHours}");
            await planRegistration.Create(dbContext).ConfigureAwait(false);
            Console.WriteLine($"[DEBUG-GRPC-UPDATE] PERSONAL CREATE: After planRegistration.Create -- Id={planRegistration.Id}, WorkflowState={planRegistration.WorkflowState}");
        }
        else
        {
            Console.WriteLine($"[DEBUG-GRPC-UPDATE] PERSONAL UPDATE: planRegistration EXISTS -- will UPDATE existing row Id={planRegistration.Id}, Date={planRegistration.Date:yyyy-MM-dd}, current Start1Id={planRegistration.Start1Id}, current Stop1Id={planRegistration.Stop1Id}");
            planRegistration.UpdatedByUserId = userService.UserId;
            planRegistration.Pause1Id = model.Shift1Pause ?? 0;
            planRegistration.Pause2Id = model.Shift2Pause ?? 0;
            planRegistration.Start1Id = model.Shift1Start ?? 0;
            planRegistration.Start2Id = model.Shift2Start ?? 0;
            planRegistration.Stop1Id = model.Shift1Stop ?? 0;
            planRegistration.Stop2Id = model.Shift2Stop ?? 0;
            planRegistration.Start3Id = model.Shift3Start ?? 0;
            planRegistration.Stop3Id = model.Shift3Stop ?? 0;
            planRegistration.Pause3Id = model.Shift3Pause ?? 0;
            planRegistration.Start4Id = model.Shift4Start ?? 0;
            planRegistration.Stop4Id = model.Shift4Stop ?? 0;
            planRegistration.Pause4Id = model.Shift4Pause ?? 0;
            planRegistration.Start5Id = model.Shift5Start ?? 0;
            planRegistration.Stop5Id = model.Shift5Stop ?? 0;
            planRegistration.WorkerComment = model.CommentWorker;

            planRegistration.Start1StartedAt = string.IsNullOrEmpty(model.Start1StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Start1StartedAt, userTimeZone);
            planRegistration.Stop1StoppedAt = string.IsNullOrEmpty(model.Stop1StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Stop1StoppedAt, userTimeZone);
            planRegistration.Pause1StartedAt = string.IsNullOrEmpty(model.Pause1StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause1StartedAt, userTimeZone);
            planRegistration.Pause1StoppedAt = string.IsNullOrEmpty(model.Pause1StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause1StoppedAt, userTimeZone);
            planRegistration.Start2StartedAt = string.IsNullOrEmpty(model.Start2StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Start2StartedAt, userTimeZone);
            planRegistration.Stop2StoppedAt = string.IsNullOrEmpty(model.Stop2StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Stop2StoppedAt, userTimeZone);
            planRegistration.Start3StartedAt = string.IsNullOrEmpty(model.Start3StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Start3StartedAt, userTimeZone);
            planRegistration.Stop3StoppedAt = string.IsNullOrEmpty(model.Stop3StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Stop3StoppedAt, userTimeZone);
            planRegistration.Start4StartedAt = string.IsNullOrEmpty(model.Start4StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Start4StartedAt, userTimeZone);
            planRegistration.Stop4StoppedAt = string.IsNullOrEmpty(model.Stop4StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Stop4StoppedAt, userTimeZone);
            planRegistration.Start5StartedAt = string.IsNullOrEmpty(model.Start5StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Start5StartedAt, userTimeZone);
            planRegistration.Stop5StoppedAt = string.IsNullOrEmpty(model.Stop5StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Stop5StoppedAt, userTimeZone);

            planRegistration.Pause10StartedAt = string.IsNullOrEmpty(model.Pause10StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause10StartedAt, userTimeZone);
            planRegistration.Pause10StoppedAt = string.IsNullOrEmpty(model.Pause10StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause10StoppedAt, userTimeZone);
            planRegistration.Pause11StartedAt = string.IsNullOrEmpty(model.Pause11StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause11StartedAt, userTimeZone);
            planRegistration.Pause11StoppedAt = string.IsNullOrEmpty(model.Pause11StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause11StoppedAt, userTimeZone);
            planRegistration.Pause12StartedAt = string.IsNullOrEmpty(model.Pause12StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause12StartedAt, userTimeZone);
            planRegistration.Pause12StoppedAt = string.IsNullOrEmpty(model.Pause12StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause12StoppedAt, userTimeZone);
            planRegistration.Pause13StartedAt = string.IsNullOrEmpty(model.Pause13StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause13StartedAt, userTimeZone);
            planRegistration.Pause13StoppedAt = string.IsNullOrEmpty(model.Pause13StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause13StoppedAt, userTimeZone);
            planRegistration.Pause14StartedAt = string.IsNullOrEmpty(model.Pause14StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause14StartedAt, userTimeZone);
            planRegistration.Pause14StoppedAt = string.IsNullOrEmpty(model.Pause14StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause14StoppedAt, userTimeZone);
            planRegistration.Pause15StartedAt = string.IsNullOrEmpty(model.Pause15StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause15StartedAt, userTimeZone);
            planRegistration.Pause15StoppedAt = string.IsNullOrEmpty(model.Pause15StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause15StoppedAt, userTimeZone);
            planRegistration.Pause16StartedAt = string.IsNullOrEmpty(model.Pause16StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause16StartedAt, userTimeZone);
            planRegistration.Pause16StoppedAt = string.IsNullOrEmpty(model.Pause16StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause16StoppedAt, userTimeZone);
            planRegistration.Pause17StartedAt = string.IsNullOrEmpty(model.Pause17StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause17StartedAt, userTimeZone);
            planRegistration.Pause17StoppedAt = string.IsNullOrEmpty(model.Pause17StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause17StoppedAt, userTimeZone);
            planRegistration.Pause18StartedAt = string.IsNullOrEmpty(model.Pause18StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause18StartedAt, userTimeZone);
            planRegistration.Pause18StoppedAt = string.IsNullOrEmpty(model.Pause18StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause18StoppedAt, userTimeZone);
            planRegistration.Pause19StartedAt = string.IsNullOrEmpty(model.Pause19StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause19StartedAt, userTimeZone);
            planRegistration.Pause19StoppedAt = string.IsNullOrEmpty(model.Pause19StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause19StoppedAt, userTimeZone);
            planRegistration.Pause100StartedAt = string.IsNullOrEmpty(model.Pause100StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause100StartedAt, userTimeZone);
            planRegistration.Pause100StoppedAt = string.IsNullOrEmpty(model.Pause100StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause100StoppedAt, userTimeZone);
            planRegistration.Pause101StartedAt = string.IsNullOrEmpty(model.Pause101StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause101StartedAt, userTimeZone);
            planRegistration.Pause101StoppedAt = string.IsNullOrEmpty(model.Pause101StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause101StoppedAt, userTimeZone);
            planRegistration.Pause102StartedAt = string.IsNullOrEmpty(model.Pause102StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause102StartedAt, userTimeZone);
            planRegistration.Pause102StoppedAt = string.IsNullOrEmpty(model.Pause102StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause102StoppedAt, userTimeZone);

            planRegistration.Pause2StartedAt = string.IsNullOrEmpty(model.Pause2StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause2StartedAt, userTimeZone);
            planRegistration.Pause2StoppedAt = string.IsNullOrEmpty(model.Pause2StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause2StoppedAt, userTimeZone);
            planRegistration.Pause20StartedAt = string.IsNullOrEmpty(model.Pause20StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause20StartedAt, userTimeZone);
            planRegistration.Pause20StoppedAt = string.IsNullOrEmpty(model.Pause20StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause20StoppedAt, userTimeZone);
            planRegistration.Pause21StartedAt = string.IsNullOrEmpty(model.Pause21StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause21StartedAt, userTimeZone);
            planRegistration.Pause21StoppedAt = string.IsNullOrEmpty(model.Pause21StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause21StoppedAt, userTimeZone);
            planRegistration.Pause22StartedAt = string.IsNullOrEmpty(model.Pause22StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause22StartedAt, userTimeZone);
            planRegistration.Pause22StoppedAt = string.IsNullOrEmpty(model.Pause22StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause22StoppedAt, userTimeZone);
            planRegistration.Pause23StartedAt = string.IsNullOrEmpty(model.Pause23StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause23StartedAt, userTimeZone);
            planRegistration.Pause23StoppedAt = string.IsNullOrEmpty(model.Pause23StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause23StoppedAt, userTimeZone);
            planRegistration.Pause24StartedAt = string.IsNullOrEmpty(model.Pause24StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause24StartedAt, userTimeZone);
            planRegistration.Pause24StoppedAt = string.IsNullOrEmpty(model.Pause24StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause24StoppedAt, userTimeZone);
            planRegistration.Pause25StartedAt = string.IsNullOrEmpty(model.Pause25StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause25StartedAt, userTimeZone);
            planRegistration.Pause25StoppedAt = string.IsNullOrEmpty(model.Pause25StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause25StoppedAt, userTimeZone);
            planRegistration.Pause26StartedAt = string.IsNullOrEmpty(model.Pause26StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause26StartedAt, userTimeZone);
            planRegistration.Pause26StoppedAt = string.IsNullOrEmpty(model.Pause26StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause26StoppedAt, userTimeZone);
            planRegistration.Pause27StartedAt = string.IsNullOrEmpty(model.Pause27StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause27StartedAt, userTimeZone);
            planRegistration.Pause27StoppedAt = string.IsNullOrEmpty(model.Pause27StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause27StoppedAt, userTimeZone);
            planRegistration.Pause28StartedAt = string.IsNullOrEmpty(model.Pause28StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause28StartedAt, userTimeZone);
            planRegistration.Pause28StoppedAt = string.IsNullOrEmpty(model.Pause28StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause28StoppedAt, userTimeZone);
            planRegistration.Pause29StartedAt = string.IsNullOrEmpty(model.Pause29StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause29StartedAt, userTimeZone);
            planRegistration.Pause29StoppedAt = string.IsNullOrEmpty(model.Pause29StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause29StoppedAt, userTimeZone);
            planRegistration.Pause200StartedAt = string.IsNullOrEmpty(model.Pause200StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause200StartedAt, userTimeZone);
            planRegistration.Pause200StoppedAt = string.IsNullOrEmpty(model.Pause200StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause200StoppedAt, userTimeZone);
            planRegistration.Pause201StartedAt = string.IsNullOrEmpty(model.Pause201StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause201StartedAt, userTimeZone);
            planRegistration.Pause201StoppedAt = string.IsNullOrEmpty(model.Pause201StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause201StoppedAt, userTimeZone);
            planRegistration.Pause202StartedAt = string.IsNullOrEmpty(model.Pause202StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause202StartedAt, userTimeZone);
            planRegistration.Pause202StoppedAt = string.IsNullOrEmpty(model.Pause202StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause202StoppedAt, userTimeZone);

            planRegistration.Pause3StartedAt = string.IsNullOrEmpty(model.Pause3StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause3StartedAt, userTimeZone);
            planRegistration.Pause3StoppedAt = string.IsNullOrEmpty(model.Pause3StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause3StoppedAt, userTimeZone);

            planRegistration.Pause4StartedAt = string.IsNullOrEmpty(model.Pause4StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause4StartedAt, userTimeZone);
            planRegistration.Pause4StoppedAt = string.IsNullOrEmpty(model.Pause4StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause4StoppedAt, userTimeZone);

            planRegistration.Pause5StartedAt = string.IsNullOrEmpty(model.Pause5StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause5StartedAt, userTimeZone);
            planRegistration.Pause5StoppedAt = string.IsNullOrEmpty(model.Pause5StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause5StoppedAt, userTimeZone);

            planRegistration.Shift1PauseNumber = model.Shift1PauseNumber;
            planRegistration.Shift2PauseNumber = model.Shift2PauseNumber;


            planRegistration = PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(assignedSite, planRegistration);

            // Self-heal a corrupt incoming pauseNId from the truthful pause
            // timestamps before the inline netto math (5-min sites, flag on).
            SelfHealCorruptPauseIds(planRegistration, assignedSite);

            double nettoMinutes = ComputeFlagOffNettoMinutes(planRegistration);

            double hours = nettoMinutes / 60;
            var preTimePlanning =
                await dbContext.PlanRegistrations.AsNoTracking()
                    .Where(x => x.Date < planRegistration.Date && x.SdkSitId == sdkSite.MicrotingUid)
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .OrderByDescending(x => x.Date).FirstOrDefaultAsync();

            // Phase 2: when UseOneMinuteIntervals is on, recompute NettoHours
            // from DateTime deltas (precise to the second) and write
            // *InSeconds columns as the source of truth; back-derive the
            // legacy double hour fields. Flag-off path stays byte-identical.
            if (assignedSite != null && assignedSite.UseOneMinuteIntervals)
            {
                PlanRegistrationHelper.ApplyNettoFlexChainSecondPrecision(
                    planRegistration,
                    preTimePlanning?.SumFlexEndInSeconds ?? 0,
                    preTimePlanning != null);
            }
            else
            {
                planRegistration.NettoHours = hours;
                planRegistration.Flex = hours - planRegistration.PlanHours;
                if (preTimePlanning != null)
                {
                    planRegistration.SumFlexEnd =
                        preTimePlanning.SumFlexEnd + planRegistration.Flex - planRegistration.PaiedOutFlex;
                    planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                }
                else
                {
                    planRegistration.SumFlexEnd = planRegistration.Flex - planRegistration.PaiedOutFlex;
                    planRegistration.SumFlexStart = 0;
                }
            }

            Console.WriteLine($"[DEBUG-GRPC-UPDATE] PERSONAL UPDATE: Before planRegistration.Update(dbContext) -- Id={planRegistration.Id}, SdkSitId={planRegistration.SdkSitId}, Date={planRegistration.Date:yyyy-MM-dd}, Start1Id={planRegistration.Start1Id}, Stop1Id={planRegistration.Stop1Id}, Pause1Id={planRegistration.Pause1Id}, Start1StartedAt={planRegistration.Start1StartedAt}, Stop1StoppedAt={planRegistration.Stop1StoppedAt}, NettoHours={planRegistration.NettoHours}");
            await planRegistration.Update(dbContext).ConfigureAwait(false);
            Console.WriteLine($"[DEBUG-GRPC-UPDATE] PERSONAL UPDATE: After planRegistration.Update -- Id={planRegistration.Id}, WorkflowState={planRegistration.WorkflowState}");
        }

        Console.WriteLine($"[DEBUG-GRPC-UPDATE] PERSONAL mode: returning OperationResult(true)");
        return new OperationResult(true);
    }

    public async Task<OperationResult> UpdateWorkingHour(int? sdkSiteId, TimePlanningWorkingHoursUpdateModel model,
        string token)
    {
        Console.WriteLine($"[DEBUG-GRPC-UPDATE] === UpdateWorkingHour (KIOSK mode, 3-param) entered === sdkSiteId={sdkSiteId}, Date={model.Date:yyyy-MM-dd}, token={token[..Math.Min(8, token.Length)]}...");
        Console.WriteLine($"[DEBUG-GRPC-UPDATE] KIOSK: model.Shift1Start={model.Shift1Start}, Shift1Stop={model.Shift1Stop}, Shift1Pause={model.Shift1Pause}, Start1StartedAt={model.Start1StartedAt}, Stop1StoppedAt={model.Stop1StoppedAt}");

        var registrationDevice = await dbContext.RegistrationDevices
            .Where(x => x.Token == token).FirstOrDefaultAsync();
        if (registrationDevice == null)
        {
            Console.WriteLine($"[DEBUG-GRPC-UPDATE] KIOSK: EARLY RETURN -- Token not found in RegistrationDevices");
            return new OperationDataResult<TimePlanningWorkingHoursModel>(false, "Token not found");
        }
        Console.WriteLine($"[DEBUG-GRPC-UPDATE] KIOSK: registrationDevice found, Id={registrationDevice.Id}");

        registrationDevice.OsVersion = model.OsVersion;
        registrationDevice.Model = model.Model;
        registrationDevice.Manufacturer = model.Manufacturer;
        registrationDevice.SoftwareVersion = model.SoftwareVersion;

        await registrationDevice.Update(dbContext);

        // Wall-time-at-rest hardening: the kiosk flow authenticates via a
        // device token — there is no authenticated user whose timezone could
        // be resolved, so Z/offset-carrying stamps are normalized into the
        // documented default zone (Europe/Copenhagen), the same assumption the
        // wall-time interval ids and EnsureTimestampsFromIds have always
        // encoded. Naive digits (the shape the kiosk app actually sends) pass
        // through verbatim.
        var userTimeZone = WallTimeNormalizer.DefaultTimeZone;

        var todayAtMidnight = model.Date;
        Console.WriteLine($"[DEBUG-GRPC-UPDATE] KIOSK: Querying PlanRegistrations: Date={todayAtMidnight:yyyy-MM-dd}, SdkSitId={sdkSiteId}");

        var planRegistration = await dbContext.PlanRegistrations
            .Where(x => x.Date == todayAtMidnight)
            .Where(x => x.SdkSitId == sdkSiteId)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync();
        Console.WriteLine($"[DEBUG-GRPC-UPDATE] KIOSK: DB lookup result: planRegistration found={planRegistration != null}, Id={planRegistration?.Id}, WorkflowState={planRegistration?.WorkflowState}");

        if (planRegistration == null)
        {
            Console.WriteLine($"[DEBUG-GRPC-UPDATE] KIOSK CREATE: planRegistration is NULL -- will CREATE new row");
            model.Date = new DateTime(model.Date.Year, model.Date.Month, model.Date.Day, 0, 0, 0);
            planRegistration = new PlanRegistration
            {
                MessageId = null,
                PlanText = "",
                Date = model.Date,
                PlanHours = 0,
                UpdatedByUserId = userService.UserId,
                CommentOffice = "",
                CommentOfficeAll = "",
                NettoHours = 0,
                PaiedOutFlex = 0,
                Pause1Id = model.Shift1Pause ?? 0,
                Pause2Id = model.Shift2Pause ?? 0,
                Start1Id = model.Shift1Start ?? 0,
                Start2Id = model.Shift2Start ?? 0,
                Stop1Id = model.Shift1Stop ?? 0,
                Stop2Id = model.Shift2Stop ?? 0,
                Start3Id = model.Shift3Start ?? 0,
                Stop3Id = model.Shift3Stop ?? 0,
                Pause3Id = model.Shift3Pause ?? 0,
                Start4Id = model.Shift4Start ?? 0,
                Stop4Id = model.Shift4Stop ?? 0,
                Pause4Id = model.Shift4Pause ?? 0,
                Start5Id = model.Shift5Start ?? 0,
                Stop5Id = model.Shift5Stop ?? 0,
                Pause5Id = model.Shift5Pause ?? 0,
                Start1StartedAt = string.IsNullOrEmpty(model.Start1StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Start1StartedAt, userTimeZone),
                Stop1StoppedAt = string.IsNullOrEmpty(model.Stop1StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Stop1StoppedAt, userTimeZone),

                Start2StartedAt = string.IsNullOrEmpty(model.Start2StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Start2StartedAt, userTimeZone),
                Stop2StoppedAt = string.IsNullOrEmpty(model.Stop2StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Stop2StoppedAt, userTimeZone),

                Start3StartedAt = string.IsNullOrEmpty(model.Start3StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Start3StartedAt, userTimeZone),
                Stop3StoppedAt = string.IsNullOrEmpty(model.Stop3StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Stop3StoppedAt, userTimeZone),

                Start4StartedAt = string.IsNullOrEmpty(model.Start4StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Start4StartedAt, userTimeZone),
                Stop4StoppedAt = string.IsNullOrEmpty(model.Stop4StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Stop4StoppedAt, userTimeZone),

                Start5StartedAt = string.IsNullOrEmpty(model.Start5StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Start5StartedAt, userTimeZone),
                Stop5StoppedAt = string.IsNullOrEmpty(model.Stop5StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Stop5StoppedAt, userTimeZone),

                Pause1StartedAt = string.IsNullOrEmpty(model.Pause1StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause1StartedAt, userTimeZone),
                Pause1StoppedAt = string.IsNullOrEmpty(model.Pause1StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause1StoppedAt, userTimeZone),
                Pause10StartedAt = string.IsNullOrEmpty(model.Pause10StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause10StartedAt, userTimeZone),
                Pause10StoppedAt = string.IsNullOrEmpty(model.Pause10StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause10StoppedAt, userTimeZone),
                Pause11StartedAt = string.IsNullOrEmpty(model.Pause11StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause11StartedAt, userTimeZone),
                Pause11StoppedAt = string.IsNullOrEmpty(model.Pause11StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause11StoppedAt, userTimeZone),
                Pause12StartedAt = string.IsNullOrEmpty(model.Pause12StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause12StartedAt, userTimeZone),
                Pause12StoppedAt = string.IsNullOrEmpty(model.Pause12StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause12StoppedAt, userTimeZone),
                Pause13StartedAt = string.IsNullOrEmpty(model.Pause13StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause13StartedAt, userTimeZone),
                Pause13StoppedAt = string.IsNullOrEmpty(model.Pause13StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause13StoppedAt, userTimeZone),
                Pause14StartedAt = string.IsNullOrEmpty(model.Pause14StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause14StartedAt, userTimeZone),
                Pause14StoppedAt = string.IsNullOrEmpty(model.Pause14StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause14StoppedAt, userTimeZone),
                Pause15StartedAt = string.IsNullOrEmpty(model.Pause15StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause15StartedAt, userTimeZone),
                Pause15StoppedAt = string.IsNullOrEmpty(model.Pause15StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause15StoppedAt, userTimeZone),
                Pause16StartedAt = string.IsNullOrEmpty(model.Pause16StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause16StartedAt, userTimeZone),
                Pause16StoppedAt = string.IsNullOrEmpty(model.Pause16StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause16StoppedAt, userTimeZone),
                Pause17StartedAt = string.IsNullOrEmpty(model.Pause17StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause17StartedAt, userTimeZone),
                Pause17StoppedAt = string.IsNullOrEmpty(model.Pause17StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause17StoppedAt, userTimeZone),
                Pause18StartedAt = string.IsNullOrEmpty(model.Pause18StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause18StartedAt, userTimeZone),
                Pause18StoppedAt = string.IsNullOrEmpty(model.Pause18StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause18StoppedAt, userTimeZone),
                Pause19StartedAt = string.IsNullOrEmpty(model.Pause19StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause19StartedAt, userTimeZone),
                Pause19StoppedAt = string.IsNullOrEmpty(model.Pause19StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause19StoppedAt, userTimeZone),
                Pause100StartedAt = string.IsNullOrEmpty(model.Pause100StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause100StartedAt, userTimeZone),
                Pause100StoppedAt = string.IsNullOrEmpty(model.Pause100StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause100StoppedAt, userTimeZone),
                Pause101StartedAt = string.IsNullOrEmpty(model.Pause101StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause101StartedAt, userTimeZone),
                Pause101StoppedAt = string.IsNullOrEmpty(model.Pause101StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause101StoppedAt, userTimeZone),
                Pause102StartedAt = string.IsNullOrEmpty(model.Pause102StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause102StartedAt, userTimeZone),
                Pause102StoppedAt = string.IsNullOrEmpty(model.Pause102StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause102StoppedAt, userTimeZone),

                Pause2StartedAt = string.IsNullOrEmpty(model.Pause2StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause2StartedAt, userTimeZone),
                Pause2StoppedAt = string.IsNullOrEmpty(model.Pause2StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause2StoppedAt, userTimeZone),
                Pause20StartedAt = string.IsNullOrEmpty(model.Pause20StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause20StartedAt, userTimeZone),
                Pause20StoppedAt = string.IsNullOrEmpty(model.Pause20StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause20StoppedAt, userTimeZone),
                Pause21StartedAt = string.IsNullOrEmpty(model.Pause21StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause21StartedAt, userTimeZone),
                Pause21StoppedAt = string.IsNullOrEmpty(model.Pause21StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause21StoppedAt, userTimeZone),
                Pause22StartedAt = string.IsNullOrEmpty(model.Pause22StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause22StartedAt, userTimeZone),
                Pause22StoppedAt = string.IsNullOrEmpty(model.Pause22StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause22StoppedAt, userTimeZone),
                Pause23StartedAt = string.IsNullOrEmpty(model.Pause23StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause23StartedAt, userTimeZone),
                Pause23StoppedAt = string.IsNullOrEmpty(model.Pause23StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause23StoppedAt, userTimeZone),
                Pause24StartedAt = string.IsNullOrEmpty(model.Pause24StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause24StartedAt, userTimeZone),
                Pause24StoppedAt = string.IsNullOrEmpty(model.Pause24StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause24StoppedAt, userTimeZone),
                Pause25StartedAt = string.IsNullOrEmpty(model.Pause25StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause25StartedAt, userTimeZone),
                Pause25StoppedAt = string.IsNullOrEmpty(model.Pause25StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause25StoppedAt, userTimeZone),
                Pause26StartedAt = string.IsNullOrEmpty(model.Pause26StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause26StartedAt, userTimeZone),
                Pause26StoppedAt = string.IsNullOrEmpty(model.Pause26StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause26StoppedAt, userTimeZone),
                Pause27StartedAt = string.IsNullOrEmpty(model.Pause27StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause27StartedAt, userTimeZone),
                Pause27StoppedAt = string.IsNullOrEmpty(model.Pause27StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause27StoppedAt, userTimeZone),
                Pause28StartedAt = string.IsNullOrEmpty(model.Pause28StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause28StartedAt, userTimeZone),
                Pause28StoppedAt = string.IsNullOrEmpty(model.Pause28StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause28StoppedAt, userTimeZone),
                Pause29StartedAt = string.IsNullOrEmpty(model.Pause29StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause29StartedAt, userTimeZone),
                Pause29StoppedAt = string.IsNullOrEmpty(model.Pause29StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause29StoppedAt, userTimeZone),
                Pause200StartedAt = string.IsNullOrEmpty(model.Pause200StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause200StartedAt, userTimeZone),
                Pause200StoppedAt = string.IsNullOrEmpty(model.Pause200StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause200StoppedAt, userTimeZone),
                Pause201StartedAt = string.IsNullOrEmpty(model.Pause201StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause201StartedAt, userTimeZone),
                Pause201StoppedAt = string.IsNullOrEmpty(model.Pause201StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause201StoppedAt, userTimeZone),
                Pause202StartedAt = string.IsNullOrEmpty(model.Pause202StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause202StartedAt, userTimeZone),
                Pause202StoppedAt = string.IsNullOrEmpty(model.Pause202StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause202StoppedAt, userTimeZone),

                Pause3StartedAt = string.IsNullOrEmpty(model.Pause3StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause3StartedAt, userTimeZone),
                Pause3StoppedAt = string.IsNullOrEmpty(model.Pause3StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause3StoppedAt, userTimeZone),

                Pause4StartedAt = string.IsNullOrEmpty(model.Pause4StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause4StartedAt, userTimeZone),
                Pause4StoppedAt = string.IsNullOrEmpty(model.Pause4StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause4StoppedAt, userTimeZone),

                Pause5StartedAt = string.IsNullOrEmpty(model.Pause5StartedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause5StartedAt, userTimeZone),
                Pause5StoppedAt = string.IsNullOrEmpty(model.Pause5StoppedAt)
                    ? null
                    : WallTimeNormalizer.NormalizeToWallTime(model.Pause5StoppedAt, userTimeZone),
                Flex = 0,
                WorkerComment = model.CommentWorker,
                SdkSitId = sdkSiteId!.Value,
                RegistrationDeviceId = registrationDevice?.Id,
                Shift1PauseNumber = model.Shift1PauseNumber,
                Shift2PauseNumber = model.Shift2PauseNumber,
            };


            var assignedSite = await dbContext.AssignedSites
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefaultAsync(x => x.SiteId == sdkSiteId.Value);

            planRegistration = PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(assignedSite, planRegistration);

            // Self-heal a corrupt incoming pauseNId from the truthful pause
            // timestamps before the inline netto math (5-min sites, flag on).
            SelfHealCorruptPauseIds(planRegistration, assignedSite);

            double nettoMinutes = ComputeFlagOffNettoMinutes(planRegistration);

            double hours = nettoMinutes / 60;
            var preTimePlanning =
                await dbContext.PlanRegistrations.AsNoTracking()
                    .Where(x => x.Date < planRegistration.Date && x.SdkSitId == sdkSiteId)
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .OrderByDescending(x => x.Date).FirstOrDefaultAsync();

            // Phase 2: when UseOneMinuteIntervals is on, recompute NettoHours
            // from DateTime deltas (precise to the second) and write
            // *InSeconds columns as the source of truth; back-derive the
            // legacy double hour fields. Flag-off path stays byte-identical.
            if (assignedSite != null && assignedSite.UseOneMinuteIntervals)
            {
                PlanRegistrationHelper.ApplyNettoFlexChainSecondPrecision(
                    planRegistration,
                    preTimePlanning?.SumFlexEndInSeconds ?? 0,
                    preTimePlanning != null);
            }
            else
            {
                planRegistration.NettoHours = hours;
                planRegistration.Flex = hours - planRegistration.PlanHours;
                if (preTimePlanning != null)
                {
                    planRegistration.SumFlexEnd =
                        preTimePlanning.SumFlexEnd + planRegistration.Flex - planRegistration.PaiedOutFlex;
                    planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                }
                else
                {
                    planRegistration.SumFlexEnd = planRegistration.Flex - planRegistration.PaiedOutFlex;
                    planRegistration.SumFlexStart = 0;
                }
            }

            Console.WriteLine($"[DEBUG-GRPC-UPDATE] KIOSK CREATE: Before planRegistration.Create(dbContext) -- SdkSitId={planRegistration.SdkSitId}, Date={planRegistration.Date:yyyy-MM-dd}, Start1Id={planRegistration.Start1Id}, Stop1Id={planRegistration.Stop1Id}, Pause1Id={planRegistration.Pause1Id}, Start1StartedAt={planRegistration.Start1StartedAt}, Stop1StoppedAt={planRegistration.Stop1StoppedAt}, NettoHours={planRegistration.NettoHours}");
            await planRegistration.Create(dbContext).ConfigureAwait(false);
            Console.WriteLine($"[DEBUG-GRPC-UPDATE] KIOSK CREATE: After planRegistration.Create -- Id={planRegistration.Id}, WorkflowState={planRegistration.WorkflowState}");
        }
        else
        {
            Console.WriteLine($"[DEBUG-GRPC-UPDATE] KIOSK UPDATE: planRegistration EXISTS -- will UPDATE existing row Id={planRegistration.Id}, Date={planRegistration.Date:yyyy-MM-dd}, current Start1Id={planRegistration.Start1Id}, current Stop1Id={planRegistration.Stop1Id}");
            planRegistration.UpdatedByUserId = userService.UserId;
            planRegistration.Pause1Id = model.Shift1Pause ?? 0;
            planRegistration.Pause2Id = model.Shift2Pause ?? 0;
            planRegistration.Start1Id = model.Shift1Start ?? 0;
            planRegistration.Start2Id = model.Shift2Start ?? 0;
            planRegistration.Stop1Id = model.Shift1Stop ?? 0;
            planRegistration.Stop2Id = model.Shift2Stop ?? 0;
            planRegistration.Start3Id = model.Shift3Start ?? 0;
            planRegistration.Stop3Id = model.Shift3Stop ?? 0;
            planRegistration.Pause3Id = model.Shift3Pause ?? 0;
            planRegistration.Start4Id = model.Shift4Start ?? 0;
            planRegistration.Stop4Id = model.Shift4Stop ?? 0;
            planRegistration.Pause4Id = model.Shift4Pause ?? 0;
            planRegistration.Start5Id = model.Shift5Start ?? 0;
            planRegistration.Stop5Id = model.Shift5Stop ?? 0;
            planRegistration.Pause5Id = model.Shift5Pause ?? 0;
            planRegistration.WorkerComment = model.CommentWorker;
            planRegistration.RegistrationDeviceId = registrationDevice?.Id;

            planRegistration.Start1StartedAt = string.IsNullOrEmpty(model.Start1StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Start1StartedAt, userTimeZone);
            planRegistration.Stop1StoppedAt = string.IsNullOrEmpty(model.Stop1StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Stop1StoppedAt, userTimeZone);

            planRegistration.Start2StartedAt = string.IsNullOrEmpty(model.Start2StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Start2StartedAt, userTimeZone);
            planRegistration.Stop2StoppedAt = string.IsNullOrEmpty(model.Stop2StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Stop2StoppedAt, userTimeZone);

            planRegistration.Start3StartedAt = string.IsNullOrEmpty(model.Start3StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Start3StartedAt, userTimeZone);
            planRegistration.Stop3StoppedAt = string.IsNullOrEmpty(model.Stop3StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Stop3StoppedAt, userTimeZone);

            planRegistration.Start4StartedAt = string.IsNullOrEmpty(model.Start4StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Start4StartedAt, userTimeZone);
            planRegistration.Stop4StoppedAt = string.IsNullOrEmpty(model.Stop4StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Stop4StoppedAt, userTimeZone);

            planRegistration.Start5StartedAt = string.IsNullOrEmpty(model.Start5StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Start5StartedAt, userTimeZone);
            planRegistration.Stop5StoppedAt = string.IsNullOrEmpty(model.Stop5StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Stop5StoppedAt, userTimeZone);

            planRegistration.Pause1StartedAt = string.IsNullOrEmpty(model.Pause1StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause1StartedAt, userTimeZone);
            planRegistration.Pause1StoppedAt = string.IsNullOrEmpty(model.Pause1StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause1StoppedAt, userTimeZone);
            planRegistration.Pause10StartedAt = string.IsNullOrEmpty(model.Pause10StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause10StartedAt, userTimeZone);
            planRegistration.Pause10StoppedAt = string.IsNullOrEmpty(model.Pause10StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause10StoppedAt, userTimeZone);
            planRegistration.Pause11StartedAt = string.IsNullOrEmpty(model.Pause11StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause11StartedAt, userTimeZone);
            planRegistration.Pause11StoppedAt = string.IsNullOrEmpty(model.Pause11StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause11StoppedAt, userTimeZone);
            planRegistration.Pause12StartedAt = string.IsNullOrEmpty(model.Pause12StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause12StartedAt, userTimeZone);
            planRegistration.Pause12StoppedAt = string.IsNullOrEmpty(model.Pause12StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause12StoppedAt, userTimeZone);
            planRegistration.Pause13StartedAt = string.IsNullOrEmpty(model.Pause13StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause13StartedAt, userTimeZone);
            planRegistration.Pause13StoppedAt = string.IsNullOrEmpty(model.Pause13StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause13StoppedAt, userTimeZone);
            planRegistration.Pause14StartedAt = string.IsNullOrEmpty(model.Pause14StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause14StartedAt, userTimeZone);
            planRegistration.Pause14StoppedAt = string.IsNullOrEmpty(model.Pause14StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause14StoppedAt, userTimeZone);
            planRegistration.Pause15StartedAt = string.IsNullOrEmpty(model.Pause15StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause15StartedAt, userTimeZone);
            planRegistration.Pause15StoppedAt = string.IsNullOrEmpty(model.Pause15StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause15StoppedAt, userTimeZone);
            planRegistration.Pause16StartedAt = string.IsNullOrEmpty(model.Pause16StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause16StartedAt, userTimeZone);
            planRegistration.Pause16StoppedAt = string.IsNullOrEmpty(model.Pause16StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause16StoppedAt, userTimeZone);
            planRegistration.Pause17StartedAt = string.IsNullOrEmpty(model.Pause17StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause17StartedAt, userTimeZone);
            planRegistration.Pause17StoppedAt = string.IsNullOrEmpty(model.Pause17StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause17StoppedAt, userTimeZone);
            planRegistration.Pause18StartedAt = string.IsNullOrEmpty(model.Pause18StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause18StartedAt, userTimeZone);
            planRegistration.Pause18StoppedAt = string.IsNullOrEmpty(model.Pause18StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause18StoppedAt, userTimeZone);
            planRegistration.Pause19StartedAt = string.IsNullOrEmpty(model.Pause19StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause19StartedAt, userTimeZone);
            planRegistration.Pause19StoppedAt = string.IsNullOrEmpty(model.Pause19StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause19StoppedAt, userTimeZone);
            planRegistration.Pause100StartedAt = string.IsNullOrEmpty(model.Pause100StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause100StartedAt, userTimeZone);
            planRegistration.Pause100StoppedAt = string.IsNullOrEmpty(model.Pause100StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause100StoppedAt, userTimeZone);
            planRegistration.Pause101StartedAt = string.IsNullOrEmpty(model.Pause101StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause101StartedAt, userTimeZone);
            planRegistration.Pause101StoppedAt = string.IsNullOrEmpty(model.Pause101StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause101StoppedAt, userTimeZone);
            planRegistration.Pause102StartedAt = string.IsNullOrEmpty(model.Pause102StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause102StartedAt, userTimeZone);
            planRegistration.Pause102StoppedAt = string.IsNullOrEmpty(model.Pause102StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause102StoppedAt, userTimeZone);

            planRegistration.Pause2StartedAt = string.IsNullOrEmpty(model.Pause2StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause2StartedAt, userTimeZone);
            planRegistration.Pause2StoppedAt = string.IsNullOrEmpty(model.Pause2StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause2StoppedAt, userTimeZone);
            planRegistration.Pause20StartedAt = string.IsNullOrEmpty(model.Pause20StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause20StartedAt, userTimeZone);
            planRegistration.Pause20StoppedAt = string.IsNullOrEmpty(model.Pause20StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause20StoppedAt, userTimeZone);
            planRegistration.Pause21StartedAt = string.IsNullOrEmpty(model.Pause21StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause21StartedAt, userTimeZone);
            planRegistration.Pause21StoppedAt = string.IsNullOrEmpty(model.Pause21StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause21StoppedAt, userTimeZone);
            planRegistration.Pause22StartedAt = string.IsNullOrEmpty(model.Pause22StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause22StartedAt, userTimeZone);
            planRegistration.Pause22StoppedAt = string.IsNullOrEmpty(model.Pause22StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause22StoppedAt, userTimeZone);
            planRegistration.Pause23StartedAt = string.IsNullOrEmpty(model.Pause23StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause23StartedAt, userTimeZone);
            planRegistration.Pause23StoppedAt = string.IsNullOrEmpty(model.Pause23StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause23StoppedAt, userTimeZone);
            planRegistration.Pause24StartedAt = string.IsNullOrEmpty(model.Pause24StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause24StartedAt, userTimeZone);
            planRegistration.Pause24StoppedAt = string.IsNullOrEmpty(model.Pause24StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause24StoppedAt, userTimeZone);
            planRegistration.Pause25StartedAt = string.IsNullOrEmpty(model.Pause25StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause25StartedAt, userTimeZone);
            planRegistration.Pause25StoppedAt = string.IsNullOrEmpty(model.Pause25StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause25StoppedAt, userTimeZone);
            planRegistration.Pause26StartedAt = string.IsNullOrEmpty(model.Pause26StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause26StartedAt, userTimeZone);
            planRegistration.Pause26StoppedAt = string.IsNullOrEmpty(model.Pause26StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause26StoppedAt, userTimeZone);
            planRegistration.Pause27StartedAt = string.IsNullOrEmpty(model.Pause27StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause27StartedAt, userTimeZone);
            planRegistration.Pause27StoppedAt = string.IsNullOrEmpty(model.Pause27StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause27StoppedAt, userTimeZone);
            planRegistration.Pause28StartedAt = string.IsNullOrEmpty(model.Pause28StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause28StartedAt, userTimeZone);
            planRegistration.Pause28StoppedAt = string.IsNullOrEmpty(model.Pause28StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause28StoppedAt, userTimeZone);
            planRegistration.Pause29StartedAt = string.IsNullOrEmpty(model.Pause29StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause29StartedAt, userTimeZone);
            planRegistration.Pause29StoppedAt = string.IsNullOrEmpty(model.Pause29StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause29StoppedAt, userTimeZone);
            planRegistration.Pause200StartedAt = string.IsNullOrEmpty(model.Pause200StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause200StartedAt, userTimeZone);
            planRegistration.Pause200StoppedAt = string.IsNullOrEmpty(model.Pause200StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause200StoppedAt, userTimeZone);
            planRegistration.Pause201StartedAt = string.IsNullOrEmpty(model.Pause201StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause201StartedAt, userTimeZone);
            planRegistration.Pause201StoppedAt = string.IsNullOrEmpty(model.Pause201StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause201StoppedAt, userTimeZone);
            planRegistration.Pause202StartedAt = string.IsNullOrEmpty(model.Pause202StartedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause202StartedAt, userTimeZone);
            planRegistration.Pause202StoppedAt = string.IsNullOrEmpty(model.Pause202StoppedAt)
                ? null
                : WallTimeNormalizer.NormalizeToWallTime(model.Pause202StoppedAt, userTimeZone);

            planRegistration.Shift1PauseNumber = model.Shift1PauseNumber;
            planRegistration.Shift2PauseNumber = model.Shift2PauseNumber;


            var assignedSite = await dbContext.AssignedSites
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefaultAsync(x => x.SiteId == sdkSiteId!.Value);

            planRegistration = PlanRegistrationHelper.CalculatePauseAutoBreakCalculationActive(assignedSite, planRegistration);

            // Self-heal a corrupt incoming pauseNId from the truthful pause
            // timestamps before the inline netto math (5-min sites, flag on).
            SelfHealCorruptPauseIds(planRegistration, assignedSite);

            double nettoMinutes = ComputeFlagOffNettoMinutes(planRegistration);

            double hours = nettoMinutes / 60;
            var preTimePlanning =
                await dbContext.PlanRegistrations.AsNoTracking()
                    .Where(x => x.Date < planRegistration.Date && x.SdkSitId == sdkSiteId)
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .OrderByDescending(x => x.Date).FirstOrDefaultAsync();

            // Phase 2: when UseOneMinuteIntervals is on, recompute NettoHours
            // from DateTime deltas (precise to the second) and write
            // *InSeconds columns as the source of truth; back-derive the
            // legacy double hour fields. Flag-off path stays byte-identical.
            if (assignedSite != null && assignedSite.UseOneMinuteIntervals)
            {
                PlanRegistrationHelper.ApplyNettoFlexChainSecondPrecision(
                    planRegistration,
                    preTimePlanning?.SumFlexEndInSeconds ?? 0,
                    preTimePlanning != null);
            }
            else
            {
                planRegistration.NettoHours = hours;
                planRegistration.Flex = hours - planRegistration.PlanHours;
                if (preTimePlanning != null)
                {
                    planRegistration.SumFlexEnd =
                        preTimePlanning.SumFlexEnd + planRegistration.Flex - planRegistration.PaiedOutFlex;
                    planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                }
                else
                {
                    planRegistration.SumFlexEnd = planRegistration.Flex - planRegistration.PaiedOutFlex;
                    planRegistration.SumFlexStart = 0;
                }
            }

            Console.WriteLine($"[DEBUG-GRPC-UPDATE] KIOSK UPDATE: Before planRegistration.Update(dbContext) -- Id={planRegistration.Id}, SdkSitId={planRegistration.SdkSitId}, Date={planRegistration.Date:yyyy-MM-dd}, Start1Id={planRegistration.Start1Id}, Stop1Id={planRegistration.Stop1Id}, Pause1Id={planRegistration.Pause1Id}, Start1StartedAt={planRegistration.Start1StartedAt}, Stop1StoppedAt={planRegistration.Stop1StoppedAt}, NettoHours={planRegistration.NettoHours}");
            await planRegistration.Update(dbContext).ConfigureAwait(false);
            Console.WriteLine($"[DEBUG-GRPC-UPDATE] KIOSK UPDATE: After planRegistration.Update -- Id={planRegistration.Id}, WorkflowState={planRegistration.WorkflowState}");
        }

        Console.WriteLine($"[DEBUG-GRPC-UPDATE] KIOSK mode: returning OperationResult(true)");
        return new OperationResult(true);
    }

    public async Task<OperationDataResult<Stream>> GenerateExcelDashboard(TimePlanningWorkingHoursRequestModel model)
    {
        try
        {
            var core = await coreHelper.GetCore();
            var sdkContext = core.DbContextHelper.GetDbContext();
            var site = await sdkContext.Sites.FirstAsync(x => x.MicrotingUid == model.SiteId);
            var siteWorker = await sdkContext.SiteWorkers.FirstAsync(x => x.SiteId == site.Id);
            var worker = await sdkContext.Workers.FirstAsync(x => x.Id == siteWorker!.WorkerId);
            var language = await userService.GetCurrentUserLanguage();
            var assignedSite = await dbContext.AssignedSites
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstAsync(x => x.SiteId == site!.MicrotingUid);

            var isThirdShiftEnabled = assignedSite.ThirdShiftActive;

            var isFourthShiftEnabled = assignedSite.FourthShiftActive;

            var isFifthShiftEnabled = assignedSite.FifthShiftActive;

            // Load PayRuleSet with day rules + tiers AND day type rules + time bands
            PayRuleSet payRuleSet = null;
            if (assignedSite.PayRuleSetId.HasValue)
            {
                payRuleSet = await dbContext.PayRuleSets
                    .Include(p => p.DayRules)
                    .ThenInclude(d => d.Tiers)
                    .Include(p => p.DayTypeRules)
                    .ThenInclude(d => d.TimeBandRules)
                    .FirstOrDefaultAsync(p => p.Id == assignedSite.PayRuleSetId.Value);
            }

            Thread.CurrentThread.CurrentUICulture = new CultureInfo(language.LanguageCode);
            var culture = new CultureInfo(language.LanguageCode);
            Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "results"));

            var timeStamp = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            var filePath = Path.Combine(Path.GetTempPath(), "results", $"{timeStamp}_.xlsx");

            // Fetch data early so we can pre-compute pay lines for header discovery
            var content = await Index(model);
            if (!content.Success) return new OperationDataResult<Stream>(false, content.Message);

            // remove the first entry from the content.Model
            var timePlannings = content.Model.Skip(1).ToList();

            // Pre-compute pay lines for each day and collect unique pay codes
            var payLinesByDate = new Dictionary<DateTime, List<PlanRegistrationPayLine>>();
            var allPayCodes = new List<string>();

            if (payRuleSet != null)
            {
                foreach (var planning in timePlannings)
                {
                    var nettoHours = planning.NettoHoursOverrideActive
                        ? planning.NettoHoursOverride
                        : planning.NettoHours;
                    var totalSeconds = (int)(nettoHours * 3600);

                    var payLines = CalculatePayLinesForDay(
                        planning.Id ?? 0,
                        planning.Date,
                        planning,
                        totalSeconds,
                        payRuleSet);

                    payLinesByDate[planning.Date] = payLines;

                    foreach (var pl in payLines)
                    {
                        if (!allPayCodes.Contains(pl.PayCode))
                        {
                            allPayCodes.Add(pl.PayCode);
                        }
                    }
                }
            }

            using (SpreadsheetDocument
                   document = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook))
            {
                WorkbookPart workbookPart1 = document.AddWorkbookPart();
                OpenXMLHelper.GenerateWorkbookPart1Content(workbookPart1,
                    [new(Translations.DayOverview, "rId1"), new("Dashboard", "rId2")]);

                WorkbookStylesPart workbookStylesPart1 = workbookPart1.AddNewPart<WorkbookStylesPart>("rId4");
                OpenXMLHelper.GenerateWorkbookStylesPart1Content(workbookStylesPart1);

                ThemePart themePart1 = workbookPart1.AddNewPart<ThemePart>("rId3");
                OpenXMLHelper.GenerateThemePart1Content(themePart1);

                // Dagsoversigt (Day overview) — first tab
                WorksheetPart dayOverviewWorksheetPart = workbookPart1.AddNewPart<WorksheetPart>("rId1");
                var dayOverviewRows = timePlannings.Select(p => new DayOverviewRow
                {
                    EmployeeNo = worker.EmployeeNo ?? string.Empty,
                    WorkerName = site.Name,
                    Date = p.Date,
                    Planning = p,
                    UseOneMinuteIntervals = assignedSite.UseOneMinuteIntervals
                }).ToList();
                BuildDayOverviewWorksheet(dayOverviewWorksheetPart, dayOverviewRows, culture);

                WorksheetPart worksheetPart1 = workbookPart1.AddNewPart<WorksheetPart>("rId2");

                var headers = new[]
                {
                    Translations.Employee_no,
                    Translations.Worker,
                    Translations.DayOfWeek,
                    Translations.Date,
                    Translations.Week_number,
                    Translations.PlanText,
                    Translations.PlanHours,
                    Translations.Shift_1__start,
                    Translations.Shift_1__end,
                    Translations.Shift_1__pause,
                    Translations.Shift_2__start,
                    Translations.Shift_2__end,
                    Translations.Shift_2__pause,
                    Translations.NettoHours,
                    Translations.Flex,
                    Translations.SumFlexStart,
                    Translations.PaidOutFlex,
                    Translations.Message,
                    Translations.Comments,
                    Translations.Comment_office
                };

                if (isThirdShiftEnabled)
                {
                    headers = headers.Concat(new[]
                    {
                        Translations.Shift_3__start,
                        Translations.Shift_3__end,
                        Translations.Shift_3__pause
                    }).ToArray();
                }

                if (isFourthShiftEnabled)
                {
                    headers = headers.Concat(new[]
                    {
                        Translations.Shift_4__start,
                        Translations.Shift_4__end,
                        Translations.Shift_4__pause
                    }).ToArray();
                }

                if (isFifthShiftEnabled)
                {
                    headers = headers.Concat(new[]
                    {
                        Translations.Shift_5__start,
                        Translations.Shift_5__end,
                        Translations.Shift_5__pause
                    }).ToArray();
                }

                List<string> headerStrings = new List<string>();
                foreach (var header in headers)
                {
                    headerStrings.Add(localizationService.GetString(header));
                }

                // Add pay code columns as dynamic headers
                foreach (var payCode in allPayCodes)
                {
                    headerStrings.Add(payCode);
                }

                Worksheet worksheet1 = new Worksheet()
                    { MCAttributes = new MarkupCompatibilityAttributes() { Ignorable = "x14ac xr xr2 xr3" } };
                worksheet1.AddNamespaceDeclaration("r",
                    "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
                worksheet1.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
                worksheet1.AddNamespaceDeclaration("x14ac",
                    "http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac");
                worksheet1.AddNamespaceDeclaration("xr",
                    "http://schemas.microsoft.com/office/spreadsheetml/2014/revision");
                worksheet1.AddNamespaceDeclaration("xr2",
                    "http://schemas.microsoft.com/office/spreadsheetml/2015/revision2");
                worksheet1.AddNamespaceDeclaration("xr3",
                    "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3");
                worksheet1.SetAttribute(new OpenXmlAttribute("xr", "uid",
                    "http://schemas.microsoft.com/office/spreadsheetml/2014/revision",
                    "{00000000-0001-0000-0000-000000000000}"));

                SheetFormatProperties sheetFormatProperties1 = new SheetFormatProperties()
                    { DefaultRowHeight = 15D, DyDescent = 0.25D };

                SheetData sheetData1 = new SheetData();

                Row row1 = new Row()
                {
                    RowIndex = (UInt32Value)1U, Spans = new ListValue<StringValue>() { InnerText = "1:19" },
                    DyDescent = 0.25D
                };

                foreach (var header in headerStrings)
                {
                    var cell = new Cell()
                    {
                        CellValue = new CellValue(header),
                        DataType = CellValues.String,
                        StyleIndex = (UInt32Value)1U
                    };
                    row1.Append(cell);
                }

                sheetData1.Append(row1);

                var plr = new PlanRegistration();

                // Running totals for the totals row
                double totalPlanHours = 0;
                double totalNettoHours = 0;
                double totalFlexHours = 0;
                double totalPaidOutFlex = 0;
                var totalsByPayCode = allPayCodes.ToDictionary(p => p, p => 0.0);

                // Fill data
                int rowIndex = 2;
                foreach (var planning in timePlannings)
                {
                    var dataRow = new Row() { RowIndex = (uint)rowIndex };
                    FillDataRow(dataRow, worker, site, culture, planning, plr, language, isThirdShiftEnabled, isFourthShiftEnabled, isFifthShiftEnabled, assignedSite.UseOneMinuteIntervals);

                    // Append pay code values for this day
                    var dayPayLines = payLinesByDate.ContainsKey(planning.Date)
                        ? payLinesByDate[planning.Date]
                        : new List<PlanRegistrationPayLine>();

                    if (allPayCodes.Count > 0)
                    {
                        foreach (var payCode in allPayCodes)
                        {
                            var payLine = dayPayLines.FirstOrDefault(pl => pl.PayCode == payCode);
                            dataRow.Append(CreateNumericCell(payLine?.Hours ?? 0));
                        }
                    }

                    sheetData1.Append(dataRow);
                    rowIndex++;

                    // Accumulate totals
                    totalPlanHours += planning.PlanHours;
                    totalNettoHours += planning.NettoHoursOverrideActive
                        ? planning.NettoHoursOverride
                        : planning.NettoHours;
                    totalFlexHours += planning.FlexHours;
                    if (!string.IsNullOrEmpty(planning.PaidOutFlex)
                        && double.TryParse(planning.PaidOutFlex.Replace(",", "."),
                            NumberStyles.Any, CultureInfo.InvariantCulture, out var paidOut))
                    {
                        totalPaidOutFlex += paidOut;
                    }
                    foreach (var pl in dayPayLines)
                    {
                        if (totalsByPayCode.ContainsKey(pl.PayCode))
                        {
                            totalsByPayCode[pl.PayCode] += pl.Hours;
                        }
                    }
                }

                // Append totals row
                var totalsRow = new Row { RowIndex = (uint)rowIndex };
                totalsRow.Append(CreateCell((Resources.Translations.ResourceManager.GetString("PayRuleSetTotalRow") ?? "Total"))); // EmployeeNo column → "Total" label
                totalsRow.Append(CreateCell(string.Empty)); // Worker
                totalsRow.Append(CreateCell(string.Empty)); // DayOfWeek
                totalsRow.Append(CreateCell(string.Empty)); // Date
                totalsRow.Append(CreateCell(string.Empty)); // Week
                totalsRow.Append(CreateCell(string.Empty)); // PlanText
                totalsRow.Append(CreateNumericCell(totalPlanHours)); // PlanHours
                totalsRow.Append(CreateCell(string.Empty)); // Shift1 Start
                totalsRow.Append(CreateCell(string.Empty)); // Shift1 Stop
                totalsRow.Append(CreateCell(string.Empty)); // Shift1 Pause
                totalsRow.Append(CreateCell(string.Empty)); // Shift2 Start
                totalsRow.Append(CreateCell(string.Empty)); // Shift2 Stop
                totalsRow.Append(CreateCell(string.Empty)); // Shift2 Pause
                totalsRow.Append(CreateNumericCell(totalNettoHours)); // NettoHours
                totalsRow.Append(CreateNumericCell(totalFlexHours)); // FlexHours
                totalsRow.Append(CreateCell(string.Empty)); // SumFlexEnd (running balance, not summable)
                totalsRow.Append(CreateNumericCell(totalPaidOutFlex)); // PaidOutFlex
                totalsRow.Append(CreateCell(string.Empty)); // Message
                totalsRow.Append(CreateCell(string.Empty)); // CommentWorker
                totalsRow.Append(CreateCell(string.Empty)); // CommentOffice
                if (isThirdShiftEnabled)
                {
                    totalsRow.Append(CreateCell(string.Empty));
                    totalsRow.Append(CreateCell(string.Empty));
                    totalsRow.Append(CreateCell(string.Empty));
                }
                if (isFourthShiftEnabled)
                {
                    totalsRow.Append(CreateCell(string.Empty));
                    totalsRow.Append(CreateCell(string.Empty));
                    totalsRow.Append(CreateCell(string.Empty));
                }
                if (isFifthShiftEnabled)
                {
                    totalsRow.Append(CreateCell(string.Empty));
                    totalsRow.Append(CreateCell(string.Empty));
                    totalsRow.Append(CreateCell(string.Empty));
                }
                foreach (var payCode in allPayCodes)
                {
                    totalsRow.Append(CreateNumericCell(totalsByPayCode[payCode]));
                }
                sheetData1.Append(totalsRow);
                rowIndex++;

                var columnLetter = GetColumnLetter(headerStrings.Count);
                AutoFilter autoFilter1 = new AutoFilter() { Reference = $"A1:{columnLetter}{rowIndex}" };
                autoFilter1.SetAttribute(new OpenXmlAttribute("xr", "uid",
                    "http://schemas.microsoft.com/office/spreadsheetml/2014/revision",
                    "{00000000-0001-0000-0000-000000000000}"));
                PageMargins pageMargins1 = new PageMargins()
                    { Left = 0.7D, Right = 0.7D, Top = 0.75D, Bottom = 0.75D, Header = 0.3D, Footer = 0.3D };

                worksheet1.Append(sheetFormatProperties1);
                worksheet1.Append(sheetData1);
                worksheet1.Append(autoFilter1);
                worksheet1.Append(pageMargins1);

                worksheetPart1.Worksheet = worksheet1;

            }

            ValidateExcel(filePath);

            // Return the Excel file as a Stream
            return new OperationDataResult<Stream>(true, File.Open(filePath, FileMode.Open));
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            logger.LogError(ex.Message);
            return new OperationDataResult<Stream>(false,
                localizationService.GetString("ErrorWhileCreatingExcelFile"));
        }
    }

    private void FillDataRow(Row dataRow, Worker worker, Microting.eForm.Infrastructure.Data.Entities.Site site, CultureInfo culture,
        TimePlanningWorkingHoursModel planning, PlanRegistration plr, Language language, bool isThirdShiftEnabled, bool isFourthShiftEnabled, bool isFifthShiftEnabled, bool useOneMinuteIntervals = false)
    {
        try {
            dataRow.Append(CreateCell(worker.EmployeeNo ?? string.Empty));
            dataRow.Append(CreateCell(site.Name));
            dataRow.Append(CreateCell(planning.Date.ToString("dddd", culture)));
            dataRow.Append(CreateDateCell(planning.Date));
            dataRow.Append(CreateWeekNumberCell(planning.Date));
            dataRow.Append(CreateCell(planning.PlanText));
            dataRow.Append(CreateNumericCell(planning.PlanHours));
            // Phase 4: when UseOneMinuteIntervals is on, format actual stamps (start/stop)
            // from the precise DateTime stamps with second precision; pause columns have
            // no single representative stamp in the legacy 5-min Options[] view, so they
            // pass actualStamp=null and fall through to the existing 2-arg lookup.
            dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift1Start, planning.Start1StartedAt, useOneMinuteIntervals)));
            dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift1Stop, planning.Stop1StoppedAt, useOneMinuteIntervals)));
            dataRow.Append(CreateCell(FormatPauseMinutesAsTime(planning.Shift1PauseMinutes, planning.Shift1Pause)));
            dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift2Start, planning.Start2StartedAt, useOneMinuteIntervals)));
            dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift2Stop, planning.Stop2StoppedAt, useOneMinuteIntervals)));
            dataRow.Append(CreateCell(FormatPauseMinutesAsTime(planning.Shift2PauseMinutes, planning.Shift2Pause)));
            dataRow.Append(CreateNumericCell(planning.NettoHoursOverrideActive ? planning.NettoHoursOverride : planning.NettoHours));
            dataRow.Append(CreateNumericCell(planning.FlexHours));
            dataRow.Append(CreateNumericCell(planning.SumFlexEnd));
            dataRow.Append(CreateNumericCell(string.IsNullOrEmpty(planning.PaidOutFlex)
                ? 0
                : double.Parse(planning.PaidOutFlex.Replace(",", "."), CultureInfo.InvariantCulture)));
            dataRow.Append(CreateCell(GetMessageText(planning.Message, language)));
            dataRow.Append(CreateCell(planning.CommentWorker?.Replace("<br>", "\n")));
            dataRow.Append(CreateCell(planning.CommentOffice?.Replace("<br>", "\n")));
            if (isThirdShiftEnabled)
            {
                dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift3Start, planning.Start3StartedAt, useOneMinuteIntervals)));
                dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift3Stop, planning.Stop3StoppedAt, useOneMinuteIntervals)));
                dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift3Pause, null, useOneMinuteIntervals)));
            }
            if (isFourthShiftEnabled)
            {
                dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift4Start, planning.Start4StartedAt, useOneMinuteIntervals)));
                dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift4Stop, planning.Stop4StoppedAt, useOneMinuteIntervals)));
                dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift4Pause, null, useOneMinuteIntervals)));
            }
            if (isFifthShiftEnabled)
            {
                dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift5Start, planning.Start5StartedAt, useOneMinuteIntervals)));
                dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift5Stop, planning.Stop5StoppedAt, useOneMinuteIntervals)));
                dataRow.Append(CreateCell(GetShiftTime(plr, planning.Shift5Pause, null, useOneMinuteIntervals)));
            }
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            logger.LogError($"Error while filling data row: {ex.Message}");
            throw;
        }
    }

    private Cell CreateCell(string? value)
    {
        return new Cell()
        {
            CellValue = new CellValue(value ?? string.Empty),
            DataType = CellValues.String // Explicitly setting the data type to string
        };
    }

    private Cell CreateNumericCell(double value)
    {
        return new Cell()
        {
            CellValue = new CellValue(value.ToString(CultureInfo.InvariantCulture)),
            DataType = CellValues.Number
        };
    }

    private Cell CreateDateCell(DateTime dateValue)
    {
        return new Cell()
        {
            CellValue = new CellValue(dateValue.ToOADate()
                .ToString(CultureInfo.InvariantCulture)), // Excel stores dates as OLE Automation date values
            DataType = CellValues.Number, // Excel treats dates as numbers
            StyleIndex = 2 // Assuming StyleIndex 2 corresponds to the date format in the stylesheet
        };
    }

    private Cell CreateWeekNumberCell(DateTime dateValue)
    {
        var culture = CultureInfo.CurrentCulture;
        var weekNumber = culture.Calendar.GetWeekOfYear(dateValue, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        return new Cell()
        {
            CellValue = new CellValue(weekNumber.ToString()),
            DataType = CellValues.Number
        };
    }


    /// <summary>
    /// A shift pause cell is blank only when the shift has no pause at all: the
    /// canonical all-slots total is zero AND the legacy single-slot Pause{N}Id is
    /// absent (id &lt;= 0). A present-but-zero pause (legacy id 1 -&gt; 0 min) still
    /// renders, preserving the old "00:00"/0.0 output; a real/multi pause
    /// (minutes &gt; 0) always renders regardless of the legacy id.
    /// </summary>
    private static bool IsShiftPauseBlank(int minutes, int? legacyPauseId)
    {
        return minutes == 0 && (legacyPauseId ?? 0) <= 0;
    }

    /// <summary>
    /// Formats a total pause duration in minutes as <c>HH:mm</c> (the Dashboard
    /// sheet's pause-cell format). Source is the canonical all-slots pause total
    /// (<c>Shift{N}PauseMinutes</c>); the legacy single-slot Pause{N}Id is used
    /// only to decide blank-vs-present-zero (see <see cref="IsShiftPauseBlank"/>).
    /// </summary>
    internal static string FormatPauseMinutesAsTime(int minutes, int? legacyPauseId)
    {
        if (IsShiftPauseBlank(minutes, legacyPauseId))
        {
            return "";
        }
        return $"{minutes / 60:00}:{minutes % 60:00}";
    }

    /// <summary>
    /// The day-fraction (minutes / 1440) of a total pause duration for the
    /// Dagsoversigt tab, mirroring <see cref="GetShiftTimeFraction"/>'s output.
    /// The legacy single-slot Pause{N}Id only decides blank-vs-present-zero.
    /// </summary>
    internal static double? PauseMinutesAsDayFraction(int minutes, int? legacyPauseId)
    {
        if (IsShiftPauseBlank(minutes, legacyPauseId))
        {
            return null;
        }
        return minutes / 1440.0;
    }

    internal string GetShiftTime(PlanRegistration plr, int? shift)
    {
        if (shift is null or <= 0)
        {
            return "";
        }
        // A shift slot id encodes a 5-minute time-of-day: slot s -> (s-1)*5 minutes.
        // Computed arithmetically instead of indexing the fixed 288-entry plr.Options,
        // so cross-midnight / out-of-range slot ids (>= 290) don't overflow:
        // 288 -> 23:55, 289 -> 24:00, 290 -> 24:05, 313 -> 26:00 (don't-wrap convention).
        var minutes = (shift.Value - 1) * 5;
        return $"{minutes / 60:00}:{minutes % 60:00}";
    }

    /// <summary>
    /// Phase 4 second-precision overload: when <paramref name="useOneMinuteIntervals"/>
    /// is on AND a precise <paramref name="actualStamp"/> is available, format the
    /// stamp as <c>HH:mm</c> directly (sourcing the value from
    /// <c>PlanRegistration.Start1StartedAt</c> / <c>Stop1StoppedAt</c> / etc.
    /// instead of the legacy 5-minute <c>plr.Options</c> lookup). For every
    /// other case (flag off OR no actual stamp) this delegates to the existing
    /// 2-arg method so the byte-identical 5-minute path is preserved.
    /// The flag controls input granularity, not display precision — frontend
    /// convention (<c>time-planning.model.ts</c>) is always <c>HH:mm</c>.
    /// </summary>
    internal string GetShiftTime(PlanRegistration plr, int? shift, DateTime? actualStamp, bool useOneMinuteIntervals)
    {
        if (useOneMinuteIntervals && actualStamp.HasValue)
        {
            return actualStamp.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
        }
        return GetShiftTime(plr, shift);
    }

    private string GetMessageText(int? messageId, Language language)
    {
        if (messageId == null) return string.Empty;

        var message = dbContext.Messages.SingleOrDefault(x => x.Id == messageId);
        return message == null
            ? string.Empty
            : language.LanguageCode switch
            {
                "da" => message.DaName,
                "de" => message.DeName,
                _ => message.EnName
            };
    }

    private void ValidateExcel(string fileName)
    {
        try
        {
            var validator = new OpenXmlValidator();
            int count = 0;
            StringBuilder sb = new StringBuilder();
            var doc = SpreadsheetDocument.Open(fileName, true);
            foreach (ValidationErrorInfo error in validator.Validate(doc))
            {

                count++;
                sb.Append(("Error Count : " + count) + "\r\n");
                sb.Append(("Description : " + error.Description) + "\r\n");
                sb.Append(("Path: " + error.Path?.XPath) + "\r\n");
                sb.Append(("Part: " + error.Part?.Uri) + "\r\n");
                sb.Append("\r\n-------------------------------------------------\r\n");
            }

            doc.Dispose();
            if (count <= 0) return;
            sb.Append(("Total Errors in file: " + count));
            throw new Exception(sb.ToString());
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            logger.LogError(ex.Message);
        }
    }

    public async Task<OperationDataResult<Stream>> GenerateExcelDashboard(
        TimePlanningWorkingHoursReportForAllWorkersRequestModel model)
    {
        try
        {
            var siteIds = await dbContext.AssignedSites
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.Resigned == false)
                .Select(x => x.SiteId)
                .Distinct()
                .ToListAsync();

            var isThirdShiftEnabled = dbContext.AssignedSites
                .Any(x => x.ThirdShiftActive && x.WorkflowState != Constants.WorkflowStates.Removed);

            var isFourthShiftEnabled = dbContext.AssignedSites
                .Any(x => x.FourthShiftActive && x.WorkflowState != Constants.WorkflowStates.Removed);

            var isFifthShiftEnabled = dbContext.AssignedSites
                .Any(x => x.FifthShiftActive && x.WorkflowState != Constants.WorkflowStates.Removed);

            var core = await coreHelper.GetCore();
            var sdkContext = core.DbContextHelper.GetDbContext();
            Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "results"));
            var timeStamp = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            var resultDocument = Path.Combine(Path.GetTempPath(), "results", $"{timeStamp}_.xlsx");

            // Set CurrentUICulture so Translations.X resolves in the user's language for all
            // downstream header/lookup calls (matches the single-worker export at line ~2375).
            var language = await userService.GetCurrentUserLanguage();
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(language.LanguageCode);
            var culture = new CultureInfo(language.LanguageCode);

            // Pre-pass: for every site, load PayRuleSet, fetch working hours, compute pay lines per day,
            // and collect the global union of pay codes used across all sites in this report.
            // Cached so the per-site sheet writing and Total sheet writing both consume the same data.
            var perSiteCache = new Dictionary<int, AllWorkersSiteCache>();
            var allPayCodes = new List<string>();
            for (int i = 0; i < siteIds.Count; i++)
            {
                var siteForCache = await sdkContext.Sites.SingleOrDefaultAsync(x =>
                    x.MicrotingUid == siteIds[i] && x.WorkflowState != Constants.WorkflowStates.Removed);
                if (siteForCache == null) continue;

                var assignedSiteForCache = await dbContext.AssignedSites
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .FirstOrDefaultAsync(x => x.SiteId == siteForCache.MicrotingUid);
                if (assignedSiteForCache == null) continue;

                PayRuleSet payRuleSetForCache = null;
                if (assignedSiteForCache.PayRuleSetId.HasValue)
                {
                    payRuleSetForCache = await dbContext.PayRuleSets
                        .Include(p => p.DayRules)
                        .ThenInclude(d => d.Tiers)
                        .Include(p => p.DayTypeRules)
                        .ThenInclude(d => d.TimeBandRules)
                        .FirstOrDefaultAsync(p => p.Id == assignedSiteForCache.PayRuleSetId.Value);
                }

                var dataResult = await Index(new TimePlanningWorkingHoursRequestModel
                {
                    DateFrom = model.DateFrom,
                    DateTo = model.DateTo,
                    SiteId = (int)siteForCache.MicrotingUid!
                });
                if (!dataResult.Success) return new OperationDataResult<Stream>(false, dataResult.Message);

                var siteTimePlannings = dataResult.Model.Skip(1).ToList();

                var payLinesByDate = new Dictionary<DateTime, List<PlanRegistrationPayLine>>();
                if (payRuleSetForCache != null)
                {
                    foreach (var planning in siteTimePlannings)
                    {
                        var nettoHours = planning.NettoHoursOverrideActive
                            ? planning.NettoHoursOverride
                            : planning.NettoHours;
                        var totalSeconds = (int)(nettoHours * 3600);

                        var payLines = CalculatePayLinesForDay(
                            planning.Id ?? 0,
                            planning.Date,
                            planning,
                            totalSeconds,
                            payRuleSetForCache);

                        payLinesByDate[planning.Date] = payLines;
                        foreach (var pl in payLines)
                        {
                            if (!allPayCodes.Contains(pl.PayCode))
                            {
                                allPayCodes.Add(pl.PayCode);
                            }
                        }
                    }
                }

                perSiteCache[siteIds[i]] = new AllWorkersSiteCache
                {
                    Site = siteForCache,
                    AssignedSite = assignedSiteForCache,
                    PayRuleSet = payRuleSetForCache,
                    TimePlannings = siteTimePlannings,
                    PayLinesByDate = payLinesByDate
                };
            }

            using (var document =
                   SpreadsheetDocument.Create(resultDocument, SpreadsheetDocumentType.Workbook))
            {
                var siteIdCount = siteIds.Count;

                var worksheetNames = new List<KeyValuePair<string, string>>();
                worksheetNames.Add(new KeyValuePair<string, string>(Translations.DayOverview, "rId1"));
                worksheetNames.Add(new KeyValuePair<string, string>("Total", "rId2"));

                for (int i = 0; i < siteIdCount; i++)
                {
                    var site = await sdkContext.Sites.SingleOrDefaultAsync(x =>
                        x.MicrotingUid == siteIds[i] && x.WorkflowState != Constants.WorkflowStates.Removed);
                    if (site == null) continue;
                    worksheetNames.Add(
                        new KeyValuePair<string, string>($"{site.Name.Substring(0, Math.Min(31, site.Name.Length))}",
                            $"rId{i + 3}"));
                }

                WorkbookPart workbookPart1 = document.AddWorkbookPart();
                OpenXMLHelper.GenerateWorkbookPart1Content(workbookPart1, worksheetNames);

                WorkbookStylesPart workbookStylesPart1 =
                    workbookPart1.AddNewPart<WorkbookStylesPart>($"rId{siteIdCount + 4}");
                OpenXMLHelper.GenerateWorkbookStylesPart1Content(workbookStylesPart1);

                ThemePart themePart1 = workbookPart1.AddNewPart<ThemePart>($"rId{siteIdCount + 3}");
                OpenXMLHelper.GenerateThemePart1Content(themePart1);

                #region DayOverviewSheetSetup

                WorksheetPart dayOverviewWorksheetPart = workbookPart1.AddNewPart<WorksheetPart>("rId1");
                var dayOverviewRows = new List<DayOverviewRow>();
                for (int i = 0; i < siteIdCount; i++)
                {
                    var doSite = await sdkContext.Sites.FirstOrDefaultAsync(x => x.MicrotingUid == siteIds[i]);
                    if (doSite == null) continue;
                    var doSiteWorker = await sdkContext.SiteWorkers.FirstAsync(x => x.SiteId == doSite.Id);
                    var doWorker = await sdkContext.Workers.FirstAsync(x => x.Id == doSiteWorker.WorkerId);
                    perSiteCache.TryGetValue(siteIds[i], out var doCache);
                    var doPlannings = doCache?.TimePlannings ?? new List<TimePlanningWorkingHoursModel>();
                    var doUseOneMinute = doCache?.AssignedSite?.UseOneMinuteIntervals ?? false;
                    foreach (var planning in doPlannings)
                    {
                        dayOverviewRows.Add(new DayOverviewRow
                        {
                            EmployeeNo = doWorker.EmployeeNo ?? string.Empty,
                            WorkerName = doSite.Name,
                            Date = planning.Date,
                            Planning = planning,
                            UseOneMinuteIntervals = doUseOneMinute
                        });
                    }
                }
                dayOverviewRows = dayOverviewRows
                    .OrderBy(r => r.Date)
                    .ThenBy(r => int.TryParse(r.EmployeeNo, out var n) ? n : int.MaxValue)
                    .ThenBy(r => r.EmployeeNo)
                    .ToList();
                BuildDayOverviewWorksheet(dayOverviewWorksheetPart, dayOverviewRows, culture);

                #endregion

                #region TotalSheetSetup

                WorksheetPart totalWorksheetPart1 = workbookPart1.AddNewPart<WorksheetPart>($"rId2");
                var seedMessages = new TimePlanningSeedMessages().Data;

                var totalHeaders = new[]
                {
                    Translations.From,
                    Translations.To,
                    Translations.Employee_no,
                    Translations.Worker,
                    Translations.PlanHours,
                    Translations.NettoHours,
                    Translations.SumFlexStart,
                    Translations.Normal_Hours,
                    Translations.Hours_Sunday,
                    Translations.Comments,
                    Translations.Message,
                    Translations.Hours_Saturday
                };
                List<string> totalHeaderStrings = new List<string>();
                foreach (var header in totalHeaders)
                {
                    totalHeaderStrings.Add(localizationService.GetString(header));
                }
                // Append one column header per pay code discovered across all sites
                foreach (var payCode in allPayCodes)
                {
                    totalHeaderStrings.Add(payCode);
                }

                // Add a column header for each seed message
                var currentLanguage = await userService.GetCurrentUserLanguage();
                foreach (var seedMessage in seedMessages)
                {
                    var messageHeader = currentLanguage.LanguageCode switch
                    {
                        "da" => seedMessage.DaName,
                        "de" => seedMessage.DeName,
                        _ => seedMessage.EnName
                    };
                    totalHeaderStrings.Add(messageHeader);
                }

                Worksheet totalWorksheet1 = new Worksheet()
                    { MCAttributes = new MarkupCompatibilityAttributes() { Ignorable = "x14ac xr xr2 xr3" } };
                totalWorksheet1.AddNamespaceDeclaration("r",
                    "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
                totalWorksheet1.AddNamespaceDeclaration("mc",
                    "http://schemas.openxmlformats.org/markup-compatibility/2006");
                totalWorksheet1.AddNamespaceDeclaration("x14ac",
                    "http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac");
                totalWorksheet1.AddNamespaceDeclaration("xr",
                    "http://schemas.microsoft.com/office/spreadsheetml/2014/revision");
                totalWorksheet1.AddNamespaceDeclaration("xr2",
                    "http://schemas.microsoft.com/office/spreadsheetml/2015/revision2");
                totalWorksheet1.AddNamespaceDeclaration("xr3",
                    "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3");
                totalWorksheet1.SetAttribute(new OpenXmlAttribute("xr", "uid",
                    "http://schemas.microsoft.com/office/spreadsheetml/2014/revision",
                    "{00000000-0001-0000-0000-000000000000}"));

                SheetFormatProperties totalSheetFormatProperties1 = new SheetFormatProperties()
                    { DefaultRowHeight = 15D, DyDescent = 0.25D };

                SheetData totalSheetData1 = new SheetData();

                Row totalRow1 = new Row()
                {
                    RowIndex = (UInt32Value)1U, Spans = new ListValue<StringValue>() { InnerText = "1:19" },
                    DyDescent = 0.25D
                };

                foreach (var totalHeader in totalHeaderStrings)
                {
                    var cell = new Cell()
                    {
                        CellValue = new CellValue(totalHeader),
                        DataType = CellValues.String,
                        StyleIndex = (UInt32Value)1U
                    };
                    totalRow1.Append(cell);
                }

                totalSheetData1.Append(totalRow1);

                var totalRowIndex = 2;

                #endregion

                for (int i = 0; i < siteIdCount; i++)
                {
                    var site = await sdkContext.Sites.FirstOrDefaultAsync(x =>
                        x.MicrotingUid == siteIds[i]);
                    if (site == null) continue;
                    var siteWorker = await sdkContext.SiteWorkers.FirstAsync(x => x.SiteId == site.Id);
                    var worker = await sdkContext.Workers.FirstAsync(x => x.Id == siteWorker.WorkerId);
                    WorksheetPart worksheetPart1 = workbookPart1.AddNewPart<WorksheetPart>($"rId{i + 3}");

                    var headers = new[]
                    {
                        Translations.Employee_no,
                        Translations.Worker,
                        Translations.DayOfWeek,
                        Translations.Date,
                        Translations.Week_number,
                        Translations.PlanText,
                        Translations.PlanHours,
                        Translations.Shift_1__start,
                        Translations.Shift_1__end,
                        Translations.Shift_1__pause,
                        Translations.Shift_2__start,
                        Translations.Shift_2__end,
                        Translations.Shift_2__pause,
                        Translations.NettoHours,
                        Translations.Flex,
                        Translations.SumFlexStart,
                        Translations.PaidOutFlex,
                        Translations.Message,
                        Translations.Comments,
                        Translations.Comment_office
                    };

                    if (isThirdShiftEnabled)
                    {
                        headers = headers.Concat(new[]
                        {
                            Translations.Shift_3__start,
                            Translations.Shift_3__end,
                            Translations.Shift_3__pause
                        }).ToArray();
                    }

                    if (isFourthShiftEnabled)
                    {
                        headers = headers.Concat(new[]
                        {
                            Translations.Shift_4__start,
                            Translations.Shift_4__end,
                            Translations.Shift_4__pause
                        }).ToArray();
                    }

                    if (isFifthShiftEnabled)
                    {
                        headers = headers.Concat(new[]
                        {
                            Translations.Shift_5__start,
                            Translations.Shift_5__end,
                            Translations.Shift_5__pause
                        }).ToArray();
                    }
                    List<string> headerStrings = new List<string>();
                    foreach (var header in headers)
                    {
                        headerStrings.Add(localizationService.GetString(header));
                    }
                    // Per-worker pay-code columns: only the codes declared in THIS site's
                    // pay-rule-set (empty when the site has no rule-set). The global allPayCodes
                    // is still used by the Total sheet, which is intentionally unchanged.
                    perSiteCache.TryGetValue(siteIds[i], out var siteCacheForCodes);
                    var sitePayCodes = GetDeclaredPayCodes(siteCacheForCodes?.PayRuleSet);
                    foreach (var payCode in sitePayCodes)
                    {
                        headerStrings.Add(payCode);
                    }

                    Worksheet worksheet1 = new Worksheet()
                        { MCAttributes = new MarkupCompatibilityAttributes() { Ignorable = "x14ac xr xr2 xr3" } };
                    worksheet1.AddNamespaceDeclaration("r",
                        "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
                    worksheet1.AddNamespaceDeclaration("mc",
                        "http://schemas.openxmlformats.org/markup-compatibility/2006");
                    worksheet1.AddNamespaceDeclaration("x14ac",
                        "http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac");
                    worksheet1.AddNamespaceDeclaration("xr",
                        "http://schemas.microsoft.com/office/spreadsheetml/2014/revision");
                    worksheet1.AddNamespaceDeclaration("xr2",
                        "http://schemas.microsoft.com/office/spreadsheetml/2015/revision2");
                    worksheet1.AddNamespaceDeclaration("xr3",
                        "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3");
                    worksheet1.SetAttribute(new OpenXmlAttribute("xr", "uid",
                        "http://schemas.microsoft.com/office/spreadsheetml/2014/revision",
                        "{00000000-0001-0000-0000-000000000000}"));

                    SheetFormatProperties sheetFormatProperties1 = new SheetFormatProperties()
                        { DefaultRowHeight = 15D, DyDescent = 0.25D };

                    SheetData sheetData1 = new SheetData();

                    Row row1 = new Row()
                    {
                        RowIndex = (UInt32Value)1U, Spans = new ListValue<StringValue>() { InnerText = "1:19" },
                        DyDescent = 0.25D
                    };

                    foreach (var header in headerStrings)
                    {
                        var cell = new Cell()
                        {
                            CellValue = new CellValue(header),
                            DataType = CellValues.String,
                            StyleIndex = (UInt32Value)1U
                        };
                        row1.Append(cell);
                    }

                    sheetData1.Append(row1);

                    // Use cached working-hours data + per-day pay lines computed in the pre-pass
                    perSiteCache.TryGetValue(siteIds[i], out var cache);
                    var timePlannings = cache?.TimePlannings ?? new List<TimePlanningWorkingHoursModel>();
                    var siteContent = new OperationDataResult<List<TimePlanningWorkingHoursModel>>(true, timePlannings);
                    var plr = new PlanRegistration();

                    // Per-site running totals
                    double siteTotalPlanHours = 0;
                    double siteTotalNettoHours = 0;
                    double siteTotalFlexHours = 0;
                    double siteTotalPaidOutFlex = 0;
                    var siteTotalsByPayCode = allPayCodes.ToDictionary(p => p, p => 0.0);

                    // Fill data
                    int rowIndex = 2;
                    foreach (var planning in timePlannings)
                    {
                        var dataRow = new Row() { RowIndex = (uint)rowIndex };
                        try
                        {
                            FillDataRow(dataRow, worker, site, culture, planning, plr, language, isThirdShiftEnabled, isFourthShiftEnabled, isFifthShiftEnabled, cache?.AssignedSite?.UseOneMinuteIntervals ?? false);

                            // Append pay code values for this day
                            var dayPayLines = (cache != null && cache.PayLinesByDate.ContainsKey(planning.Date))
                                ? cache.PayLinesByDate[planning.Date]
                                : new List<PlanRegistrationPayLine>();
                            foreach (var payCode in sitePayCodes)
                            {
                                var payLine = dayPayLines.FirstOrDefault(pl => pl.PayCode == payCode);
                                dataRow.Append(CreateNumericCell(payLine?.Hours ?? 0));
                            }

                            sheetData1.Append(dataRow);

                            // Accumulate per-site totals
                            siteTotalPlanHours += planning.PlanHours;
                            siteTotalNettoHours += planning.NettoHoursOverrideActive
                                ? planning.NettoHoursOverride
                                : planning.NettoHours;
                            siteTotalFlexHours += planning.FlexHours;
                            if (!string.IsNullOrEmpty(planning.PaidOutFlex)
                                && double.TryParse(planning.PaidOutFlex.Replace(",", "."),
                                    NumberStyles.Any, CultureInfo.InvariantCulture, out var paidOut))
                            {
                                siteTotalPaidOutFlex += paidOut;
                            }
                            foreach (var pl in dayPayLines)
                            {
                                if (siteTotalsByPayCode.ContainsKey(pl.PayCode))
                                {
                                    siteTotalsByPayCode[pl.PayCode] += pl.Hours;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            SentrySdk.CaptureException(e);
                            logger.LogError(e.Message);
                            logger.LogError(e.StackTrace);
                            logger.LogError($"Error while filling data row for site {site.Name} on row {rowIndex}: {e.Message}");
                            throw;
                        }
                        rowIndex++;
                    }

                    // Append per-site totals row at the bottom of the per-site sheet
                    var siteTotalsRow = new Row { RowIndex = (uint)rowIndex };
                    siteTotalsRow.Append(CreateCell((Resources.Translations.ResourceManager.GetString("PayRuleSetTotalRow") ?? "Total")));
                    siteTotalsRow.Append(CreateCell(string.Empty)); // Worker
                    siteTotalsRow.Append(CreateCell(string.Empty)); // DayOfWeek
                    siteTotalsRow.Append(CreateCell(string.Empty)); // Date
                    siteTotalsRow.Append(CreateCell(string.Empty)); // Week
                    siteTotalsRow.Append(CreateCell(string.Empty)); // PlanText
                    siteTotalsRow.Append(CreateNumericCell(siteTotalPlanHours)); // PlanHours
                    siteTotalsRow.Append(CreateCell(string.Empty)); // Shift1 Start
                    siteTotalsRow.Append(CreateCell(string.Empty)); // Shift1 Stop
                    siteTotalsRow.Append(CreateCell(string.Empty)); // Shift1 Pause
                    siteTotalsRow.Append(CreateCell(string.Empty)); // Shift2 Start
                    siteTotalsRow.Append(CreateCell(string.Empty)); // Shift2 Stop
                    siteTotalsRow.Append(CreateCell(string.Empty)); // Shift2 Pause
                    siteTotalsRow.Append(CreateNumericCell(siteTotalNettoHours)); // NettoHours
                    siteTotalsRow.Append(CreateNumericCell(siteTotalFlexHours)); // FlexHours
                    siteTotalsRow.Append(CreateCell(string.Empty)); // SumFlexStart
                    siteTotalsRow.Append(CreateNumericCell(siteTotalPaidOutFlex)); // PaidOutFlex
                    siteTotalsRow.Append(CreateCell(string.Empty)); // Message
                    siteTotalsRow.Append(CreateCell(string.Empty)); // CommentWorker
                    siteTotalsRow.Append(CreateCell(string.Empty)); // CommentOffice
                    if (isThirdShiftEnabled)
                    {
                        siteTotalsRow.Append(CreateCell(string.Empty));
                        siteTotalsRow.Append(CreateCell(string.Empty));
                        siteTotalsRow.Append(CreateCell(string.Empty));
                    }
                    if (isFourthShiftEnabled)
                    {
                        siteTotalsRow.Append(CreateCell(string.Empty));
                        siteTotalsRow.Append(CreateCell(string.Empty));
                        siteTotalsRow.Append(CreateCell(string.Empty));
                    }
                    if (isFifthShiftEnabled)
                    {
                        siteTotalsRow.Append(CreateCell(string.Empty));
                        siteTotalsRow.Append(CreateCell(string.Empty));
                        siteTotalsRow.Append(CreateCell(string.Empty));
                    }
                    foreach (var payCode in sitePayCodes)
                    {
                        siteTotalsRow.Append(CreateNumericCell(siteTotalsByPayCode.GetValueOrDefault(payCode, 0)));
                    }
                    sheetData1.Append(siteTotalsRow);
                    rowIndex++;

                    var columnLetter = GetColumnLetter(headerStrings.Count);
                    AutoFilter autoFilter1 = new AutoFilter() { Reference = $"A1:{columnLetter}{rowIndex}" };
                    autoFilter1.SetAttribute(new OpenXmlAttribute("xr", "uid",
                        "http://schemas.microsoft.com/office/spreadsheetml/2014/revision",
                        "{00000000-0001-0000-0000-000000000000}"));
                    PageMargins pageMargins1 = new PageMargins()
                        { Left = 0.7D, Right = 0.7D, Top = 0.75D, Bottom = 0.75D, Header = 0.3D, Footer = 0.3D };

                    worksheet1.Append(sheetFormatProperties1);
                    worksheet1.Append(sheetData1);
                    worksheet1.Append(autoFilter1);
                    worksheet1.Append(pageMargins1);

                    worksheetPart1.Worksheet = worksheet1;

                    #region TotalSheetFillData

                    var totalRow = new Row() { RowIndex = (uint)totalRowIndex };
                    totalRow.Append(CreateDateCell(model.DateFrom));
                    totalRow.Append(CreateDateCell(model.DateTo));
                    totalRow.Append(CreateCell(worker.EmployeeNo ?? string.Empty));
                    totalRow.Append(CreateCell(site.Name));
                    totalRow.Append(CreateNumericCell(siteTotalPlanHours));
                    totalRow.Append(CreateNumericCell(siteTotalNettoHours));
                    totalRow.Append(CreateNumericCell(timePlannings.Count > 0 ? timePlannings.Last().SumFlexEnd : 0.0));

                    // Sunday + holiday hours (Grundlovsdag only counts hours after 12:00)
                    var sumHoursSundayAndHoliday = 0.0;
                    foreach (var day in timePlannings)
                    {
                        var isSundayOrHoliday = day.IsSunday || PlanRegistrationHelper.IsOfficialHoliday(day.Date);
                        if (!isSundayOrHoliday) continue;

                        sumHoursSundayAndHoliday += PlanRegistrationHelper.IsGrundlovsdag(day.Date)
                            ? CalculateHoursAfterNoon(day)
                            : day.NettoHours;
                    }
                    var normalHours = siteTotalNettoHours - sumHoursSundayAndHoliday;
                    var sumHoursSaturday = timePlannings.Where(x => x.IsSaturday).Sum(x => x.NettoHours);

                    totalRow.Append(CreateNumericCell(normalHours));
                    totalRow.Append(CreateNumericCell(sumHoursSundayAndHoliday));

                    var countCommentFromWorker = timePlannings.Count(x => !string.IsNullOrEmpty(x.CommentWorker));
                    var countMessages = timePlannings.Count(x => x.Message != null);
                    totalRow.Append(CreateNumericCell(countCommentFromWorker));
                    totalRow.Append(CreateNumericCell(countMessages));
                    totalRow.Append(CreateNumericCell(sumHoursSaturday));

                    // Append per-pay-code total for this worker (matches the dynamic columns added to the headers)
                    foreach (var payCode in allPayCodes)
                    {
                        totalRow.Append(CreateNumericCell(siteTotalsByPayCode[payCode]));
                    }

                    // Add netto hours sum for each seed message
                    foreach (var seedMessage in seedMessages)
                    {
                        var messageNettoHours = timePlannings
                            .Where(x => x.Message == seedMessage.Id && x.NettoHoursOverrideActive == false)
                            .Sum(x => x.NettoHours);
                        var messageNettoHoursOverride = timePlannings
                            .Where(x => x.Message == seedMessage.Id && x.NettoHoursOverrideActive)
                            .Sum(x => x.NettoHoursOverride);

                        totalRow.Append(CreateNumericCell(messageNettoHours + messageNettoHoursOverride));
                    }

                    totalSheetData1.Append(totalRow);
                    totalRowIndex++;

                    #endregion

                }

                #region TotalSheetFinalize

                var totalColumnLetter = GetColumnLetter(totalHeaderStrings.Count);
                AutoFilter totalAutoFilter1 = new AutoFilter() { Reference = $"A1:{totalColumnLetter}{totalRowIndex}" };
                totalAutoFilter1.SetAttribute(new OpenXmlAttribute("xr", "uid",
                    "http://schemas.microsoft.com/office/spreadsheetml/2014/revision",
                    "{00000000-0001-0000-0000-000000000000}"));
                PageMargins totalPageMargins1 = new PageMargins()
                    { Left = 0.7D, Right = 0.7D, Top = 0.75D, Bottom = 0.75D, Header = 0.3D, Footer = 0.3D };

                totalWorksheet1.Append(totalSheetFormatProperties1);
                totalWorksheet1.Append(totalSheetData1);
                totalWorksheet1.Append(totalAutoFilter1);
                totalWorksheet1.Append(totalPageMargins1);

                totalWorksheetPart1.Worksheet = totalWorksheet1;

                #endregion
            }

            ValidateExcel(resultDocument);

            // Return the Excel file as a Stream
            return new OperationDataResult<Stream>(true, File.Open(resultDocument, FileMode.Open));
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            logger.LogError(ex.Message);
            return new OperationDataResult<Stream>(false,
                localizationService.GetString("ErrorWhileCreatingExcelFile"));
        }
    }

    public async Task<OperationResult> Import(IFormFile file)
    {
        try
        {
            // Get core
            var core = await coreHelper.GetCore();
            var sdkContext = core.DbContextHelper.GetDbContext();

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);

                // Open the Excel document using OpenXML
                using (var spreadsheetDocument = SpreadsheetDocument.Open(stream, false))
                {
                    var workbookPart = spreadsheetDocument.WorkbookPart;
                    if (workbookPart == null)
                    {
                        return new OperationResult(false, localizationService.GetString("FileFormatError"));
                    }
                    var sheets = workbookPart.Workbook.Sheets;
                    if (sheets == null)
                    {
                        return new OperationResult(false, localizationService.GetString("FileFormatError"));
                    }

                    foreach (Sheet sheet in sheets)
                    {
                        if (sheet.Name?.Value == null || sheet.Id?.Value == null)
                        {
                            continue;
                        }
                        var site = await sdkContext.Sites.FirstOrDefaultAsync(x => x.Name.Replace(" ", "").ToLower() == sheet.Name.Value.Replace(" ", "").ToLower());
                        if (site == null)
                        {
                            continue;
                        }

                        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id.Value);
                        var sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();

                        var rows = sheetData.Elements<Row>();
                        foreach (var row in rows)
                        {
                            // Skip header row
                            if (row.RowIndex?.Value == 1)
                            {
                                continue;
                            }

                            // Extract cell values
                            // var dateCell = row.Elements<Cell>().ElementAt(0); // First column
                            // var planHoursCell = row.Elements<Cell>().ElementAt(1); // Second column
                            // var planTextCell = row.Elements<Cell>().ElementAt(2); // Third column

                            var date = GetCellValue(workbookPart, row, 1);
                            var planHours = GetCellValue(workbookPart, row, 2);
                            var planText = GetCellValue(workbookPart, row, 3);

                            if (string.IsNullOrEmpty(planHours))
                            {
                                planHours = "0";
                            }

                            // Replace comma with dot if needed
                            if (planHours.Contains(','))
                            {
                                planHours = planHours.Replace(",", ".");
                            }

                            double parsedPlanHours = double.Parse(planHours, NumberStyles.AllowDecimalPoint,
                                NumberFormatInfo.InvariantInfo);

                            // Parse date and validate
                            if (!DateTime.TryParseExact(date, "dd.MM.yyyy", CultureInfo.InvariantCulture,
                                    DateTimeStyles.None, out var _))
                            {
                                continue;
                            }

                            var dateValue = DateTime.ParseExact(date, "dd.MM.yyyy", CultureInfo.InvariantCulture);                            if (dateValue < DateTime.Now.AddDays(-1))
                            {
                                continue;
                            }

                            if (dateValue > DateTime.Now.AddDays(180))
                            {
                                continue;
                            }

                            var preTimePlanning = await dbContext.PlanRegistrations.AsNoTracking()
                                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                                .Where(x => x.Date < dateValue && x.SdkSitId == (int)site.MicrotingUid!)
                                .OrderByDescending(x => x.Date)
                                .FirstOrDefaultAsync();

                            var planRegistration = await dbContext.PlanRegistrations.SingleOrDefaultAsync(x =>
                                x.Date == dateValue && x.SdkSitId == site.MicrotingUid);

                            if (planRegistration == null)
                            {
                                planRegistration = new PlanRegistration
                                {
                                    Date = dateValue,
                                    PlanText = planText,
                                    PlanHours = parsedPlanHours,
                                    SdkSitId = (int)site.MicrotingUid!,
                                    CreatedByUserId = userService.UserId,
                                    UpdatedByUserId = userService.UserId,
                                    NettoHours = 0,
                                    PaiedOutFlex = 0,
                                    Pause1Id = 0,
                                    Pause2Id = 0,
                                    Start1Id = 0,
                                    Start2Id = 0,
                                    Stop1Id = 0,
                                    Stop2Id = 0,
                                    Flex = 0,
                                    StatusCaseId = 0
                                };

                                if (preTimePlanning != null)
                                {
                                    planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                                    planRegistration.SumFlexEnd = preTimePlanning.SumFlexEnd + planRegistration.Flex -
                                                                  planRegistration.PaiedOutFlex;
                                    planRegistration.Flex = -planRegistration.PlanHours;
                                }
                                else
                                {
                                    planRegistration.Flex = -planRegistration.PlanHours;
                                    planRegistration.SumFlexEnd = planRegistration.Flex;
                                    planRegistration.SumFlexStart = 0;
                                }

                                await planRegistration.Create(dbContext);
                            }
                            else
                            {
                                planRegistration.PlanText = planText;
                                planRegistration.PlanHours = parsedPlanHours;
                                planRegistration.UpdatedByUserId = userService.UserId;

                                if (preTimePlanning != null)
                                {
                                    planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                                    planRegistration.SumFlexEnd =
                                        preTimePlanning.SumFlexEnd + planRegistration.PlanHours -
                                        planRegistration.NettoHours -
                                        planRegistration.PaiedOutFlex;
                                    planRegistration.Flex = planRegistration.NettoHours - planRegistration.PlanHours;
                                }
                                else
                                {
                                    planRegistration.SumFlexEnd =
                                        planRegistration.PlanHours - planRegistration.NettoHours -
                                        planRegistration.PaiedOutFlex;
                                    planRegistration.SumFlexStart = 0;
                                    planRegistration.Flex = planRegistration.NettoHours - planRegistration.PlanHours;
                                }

                                await planRegistration.Update(dbContext);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            logger.LogError(ex.Message);
            return new OperationResult(false, ex.Message);
        }
        return new OperationResult(true, "Imported");
    }

    private string GetCellValue(WorkbookPart workbookPart, Row row, int columnIndex)
    {
        // Get the column letter for the given columnIndex (e.g., A, B, C)
        var columnLetter = GetColumnLetter(columnIndex);

        // Create the cell reference (e.g., A1, B1, C1)
        var cellReference = columnLetter + row.RowIndex?.Value;

        // Find the cell with the matching CellReference
        var cell = row.Elements<Cell>().FirstOrDefault(c => c.CellReference?.Value == cellReference);

        if (cell == null || cell.CellValue == null)
        {
            return string.Empty; // Handle empty or missing cells
        }

        // Check if the cell is using a Shared String Table
        if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
        {
            var sharedStringTablePart = workbookPart.GetPartsOfType<SharedStringTablePart>().FirstOrDefault();
            if (sharedStringTablePart != null)
            {
                var sharedStringTable = sharedStringTablePart.SharedStringTable;
                if (sharedStringTable != null)
                {
                    return sharedStringTable.ElementAt(int.Parse(cell.CellValue.Text)).InnerText;
                }
            }
        }

        // Check if the cell has a StyleIndex (to determine if it's a date)
        if (cell.StyleIndex != null)
        {
            var stylesPart = workbookPart.WorkbookStylesPart;
            if (stylesPart?.Stylesheet?.CellFormats != null)
            {
                var cellFormat = stylesPart.Stylesheet.CellFormats.ElementAt((int)cell.StyleIndex.Value) as CellFormat;
                var isDate = IsDateFormat(stylesPart, cellFormat);

                // If it's a date format, interpret the numeric value as a date
                if (isDate && double.TryParse(cell.CellValue.Text, out var oaDate))
                {
                    var dateValue = DateTime.FromOADate(oaDate);
                    return dateValue.ToString("dd.MM.yyyy"); // Format as a date
                }
            }
        }

        // Handle other numbers or strings
        return cell.CellValue.Text;
    }

    private bool IsDateFormat(WorkbookStylesPart stylesPart, CellFormat? cellFormat)
    {
        if (cellFormat == null || cellFormat.NumberFormatId == null)
        {
            return false;
        }

        // Check if the format ID is a known date format in Excel
        var dateFormatIds = new HashSet<uint> { 14, 15, 16, 17, 22, 164 }; // Common Excel date format IDs

        if (dateFormatIds.Contains(cellFormat.NumberFormatId.Value))
        {
            return true;
        }

        // Look for custom number formats defined in the workbook
        var numberFormats = stylesPart.Stylesheet.NumberingFormats?.Elements<NumberingFormat>();
        if (numberFormats != null)
        {
            var format = numberFormats.FirstOrDefault(nf => nf.NumberFormatId?.Value == cellFormat.NumberFormatId.Value);
            if (format?.FormatCode?.Value != null)
            {
                // Check if the custom format code looks like a date format
                var formatCode = format.FormatCode.Value.ToLower();
                return formatCode.Contains("m") || formatCode.Contains("d") || formatCode.Contains("y");
            }
        }

        return false;
    }

    /// <summary>
    /// Calculate hours worked after 12:00 (noon) for a given day.
    /// Used for Grundlovsdag where only afternoon hours count as holiday hours.
    /// </summary>
    private double CalculateHoursAfterNoon(TimePlanningWorkingHoursModel day)
    {
        var noonTime = new DateTime(day.Date.Year, day.Date.Month, day.Date.Day, 12, 0, 0);
        double totalSecondsAfterNoon = 0;

        // Helper to calculate overlap with period after noon
        double CalculateOverlap(DateTime? start, DateTime? stop)
        {
            if (!start.HasValue || !stop.HasValue || start >= stop)
                return 0;

            // If the entire period is before noon, no overlap
            if (stop <= noonTime)
                return 0;

            // Calculate the overlapping portion
            var effectiveStart = start < noonTime ? noonTime : start.Value;
            var effectiveEnd = stop.Value;

            return (effectiveEnd - effectiveStart).TotalSeconds;
        }

        // Calculate overlap for each shift
        totalSecondsAfterNoon += CalculateOverlap(day.Start1StartedAt, day.Stop1StoppedAt);
        totalSecondsAfterNoon += CalculateOverlap(day.Start2StartedAt, day.Stop2StoppedAt);
        totalSecondsAfterNoon += CalculateOverlap(day.Start3StartedAt, day.Stop3StoppedAt);
        totalSecondsAfterNoon += CalculateOverlap(day.Start4StartedAt, day.Stop4StoppedAt);
        totalSecondsAfterNoon += CalculateOverlap(day.Start5StartedAt, day.Stop5StoppedAt);

        // Subtract pauses that occur after noon
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause1StartedAt, day.Pause1StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause2StartedAt, day.Pause2StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause3StartedAt, day.Pause3StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause4StartedAt, day.Pause4StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause5StartedAt, day.Pause5StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause10StartedAt, day.Pause10StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause11StartedAt, day.Pause11StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause12StartedAt, day.Pause12StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause13StartedAt, day.Pause13StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause14StartedAt, day.Pause14StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause15StartedAt, day.Pause15StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause16StartedAt, day.Pause16StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause17StartedAt, day.Pause17StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause18StartedAt, day.Pause18StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause19StartedAt, day.Pause19StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause20StartedAt, day.Pause20StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause21StartedAt, day.Pause21StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause22StartedAt, day.Pause22StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause23StartedAt, day.Pause23StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause24StartedAt, day.Pause24StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause25StartedAt, day.Pause25StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause26StartedAt, day.Pause26StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause27StartedAt, day.Pause27StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause28StartedAt, day.Pause28StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause29StartedAt, day.Pause29StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause100StartedAt, day.Pause100StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause101StartedAt, day.Pause101StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause102StartedAt, day.Pause102StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause200StartedAt, day.Pause200StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause201StartedAt, day.Pause201StoppedAt);
        totalSecondsAfterNoon -= CalculateOverlap(day.Pause202StartedAt, day.Pause202StoppedAt);

        // Convert to hours and ensure non-negative
        return Math.Max(0, totalSecondsAfterNoon / 3600.0);
    }

    private string GetColumnLetter(int columnIndex)
    {
        string columnLetter = "";
        while (columnIndex > 0)
        {
            int modulo = (columnIndex - 1) % 26;
            columnLetter = Convert.ToChar(65 + modulo) + columnLetter;
            columnIndex = (columnIndex - modulo) / 26;
        }

        return columnLetter;
    }

    /// <summary>
    /// Classify the date and return the day code for pay rule matching.
    /// Returns: SUNDAY, SATURDAY, HOLIDAY, GRUNDLOVSDAG, or WEEKDAY
    /// </summary>
    internal static string GetDayCodeForDate(DateTime date)
    {
        // Check if it's Grundlovsdag (June 5th) - highest priority
        if (date.Month == 6 && date.Day == 5)
        {
            return "GRUNDLOVSDAG";
        }

        // Check against holiday configuration for official holidays
        if (PlanRegistrationHelper.IsOfficialHoliday(date))
        {
            return "HOLIDAY";
        }

        if (date.DayOfWeek == DayOfWeek.Sunday)
        {
            return "SUNDAY";
        }

        if (date.DayOfWeek == DayOfWeek.Saturday)
        {
            return "SATURDAY";
        }

        return "WEEKDAY";
    }

    /// <summary>
    /// Resolves the <see cref="DayType"/> for a given date and pre-computed day code.
    /// Returns false for GRUNDLOVSDAG (no DayType equivalent — only tier rules apply).
    /// HOLIDAY → DayType.Holiday regardless of weekday.
    /// </summary>
    internal static bool TryGetDayType(DateTime date, string dayCode, out DayType dayType)
    {
        if (dayCode == "GRUNDLOVSDAG")
        {
            dayType = DayType.Monday; // unused
            return false;
        }

        if (dayCode == "HOLIDAY")
        {
            dayType = DayType.Holiday;
            return true;
        }

        switch (date.DayOfWeek)
        {
            case DayOfWeek.Monday: dayType = DayType.Monday; return true;
            case DayOfWeek.Tuesday: dayType = DayType.Tuesday; return true;
            case DayOfWeek.Wednesday: dayType = DayType.Wednesday; return true;
            case DayOfWeek.Thursday: dayType = DayType.Thursday; return true;
            case DayOfWeek.Friday: dayType = DayType.Friday; return true;
            case DayOfWeek.Saturday: dayType = DayType.Saturday; return true;
            case DayOfWeek.Sunday: dayType = DayType.Sunday; return true;
            default: dayType = DayType.Monday; return false;
        }
    }

    /// <summary>
    /// Yields each populated shift's (startSecondOfDay, stopSecondOfDay) pair for
    /// time-band pay line attribution.
    ///
    /// Source of truth is the real DateTime in Start{N}StartedAt / Stop{N}StoppedAt.
    /// The Shift{N}Start / Shift{N}Stop slot fields (1-based 5-minute indices into
    /// PlanRegistration.Options) are LEGACY — kept in sync alongside the real timestamps
    /// purely for backwards compatibility with old consumers, and never used for payroll
    /// calculations here. If a shift has no real timestamps populated, it has no
    /// recorded clock time and contributes no time-band pay lines.
    /// </summary>
    internal static IEnumerable<(int Start, int Stop)> EnumerateShiftSegments(TimePlanningWorkingHoursModel dayModel)
    {
        var shift1 = ResolveShiftSeconds(dayModel.Start1StartedAt, dayModel.Stop1StoppedAt);
        if (shift1.HasValue) yield return shift1.Value;

        var shift2 = ResolveShiftSeconds(dayModel.Start2StartedAt, dayModel.Stop2StoppedAt);
        if (shift2.HasValue) yield return shift2.Value;

        var shift3 = ResolveShiftSeconds(dayModel.Start3StartedAt, dayModel.Stop3StoppedAt);
        if (shift3.HasValue) yield return shift3.Value;

        var shift4 = ResolveShiftSeconds(dayModel.Start4StartedAt, dayModel.Stop4StoppedAt);
        if (shift4.HasValue) yield return shift4.Value;

        var shift5 = ResolveShiftSeconds(dayModel.Start5StartedAt, dayModel.Stop5StoppedAt);
        if (shift5.HasValue) yield return shift5.Value;
    }

    /// <summary>
    /// Resolves a single shift's (start, stop) seconds-of-day from real wall-clock
    /// DateTime values. Returns null when either timestamp is missing or the duration
    /// is non-positive. For shifts that span midnight, the stop is clamped to end of day
    /// because pay rules are scoped per-day.
    /// </summary>
    internal static (int Start, int Stop)? ResolveShiftSeconds(DateTime? realStart, DateTime? realStop)
    {
        if (!realStart.HasValue || !realStop.HasValue)
        {
            return null;
        }

        var startSec = (int)realStart.Value.TimeOfDay.TotalSeconds;
        var stopSec = realStop.Value.Date > realStart.Value.Date
            ? 86400
            : (int)realStop.Value.TimeOfDay.TotalSeconds;

        return stopSec > startSec ? (startSec, stopSec) : null;
    }

    /// <summary>
    /// Aggregates pay lines with the same PayCode by summing seconds and hours.
    /// Preserves PlanRegistrationId / PayRuleSetId / CalculatedAt from the first occurrence.
    /// </summary>
    private static List<PlanRegistrationPayLine> MergeByPayCode(List<PlanRegistrationPayLine> lines)
    {
        return lines
            .GroupBy(l => l.PayCode)
            .Select(g => new PlanRegistrationPayLine
            {
                PlanRegistrationId = g.First().PlanRegistrationId,
                PayCode = g.Key,
                PayrollCode = g.First().PayrollCode,
                HoursInSeconds = g.Sum(x => x.HoursInSeconds),
                Hours = g.Sum(x => x.Hours),
                PayRuleSetId = g.First().PayRuleSetId,
                CalculatedAt = g.First().CalculatedAt
            })
            .ToList();
    }

    /// <summary>
    /// Returns the pay codes DECLARED by a pay-rule-set, in structural order
    /// (day-rule tiers by Order, then day-type default codes and their time-band codes,
    /// then the holiday code), de-duplicated (first-seen wins), skipping null/empty codes.
    /// Returns an empty list when payRuleSet is null.
    /// </summary>
    internal static List<string> GetDeclaredPayCodes(PayRuleSet payRuleSet)
    {
        var codes = new List<string>();
        if (payRuleSet == null)
        {
            return codes;
        }

        void Add(string code)
        {
            if (!string.IsNullOrWhiteSpace(code) && !codes.Contains(code))
            {
                codes.Add(code);
            }
        }

        if (payRuleSet.DayRules != null)
        {
            foreach (var dayRule in payRuleSet.DayRules)
            {
                if (dayRule.Tiers == null) continue;
                foreach (var tier in dayRule.Tiers.OrderBy(t => t.Order))
                {
                    Add(tier.PayCode);
                }
            }
        }

        if (payRuleSet.DayTypeRules != null)
        {
            foreach (var dayTypeRule in payRuleSet.DayTypeRules)
            {
                Add(dayTypeRule.DefaultPayCode);
                if (dayTypeRule.TimeBandRules == null) continue;
                foreach (var timeBand in dayTypeRule.TimeBandRules)
                {
                    Add(timeBand.PayCode);
                }
            }
        }

        Add(payRuleSet.HolidayPaidOffPayCode);

        return codes;
    }

    /// <summary>
    /// Calculates pay lines for a single day, choosing time-band rules when defined
    /// for the day's DayType, otherwise falling back to tier-based logic on totalSeconds.
    /// Returns an empty list if payRuleSet is null.
    /// </summary>
    /// <param name="planRegistrationId">PlanRegistration ID to stamp on the pay lines</param>
    /// <param name="date">The date the work was performed</param>
    /// <param name="dayModel">Day model with shift Start/Stop seconds for time-band routing</param>
    /// <param name="totalSeconds">Pre-computed total worked seconds (after override + pause adjustment) for tier path</param>
    /// <param name="payRuleSet">The PayRuleSet to apply (loaded with DayRules+Tiers AND DayTypeRules+TimeBandRules)</param>
    /// <summary>
    /// Internal cache used by the all-workers Excel export so the per-site sheet
    /// generation and the Total sheet generation share a single computed pay-line dataset.
    /// </summary>
    private sealed class AllWorkersSiteCache
    {
        public Microting.eForm.Infrastructure.Data.Entities.Site Site { get; set; }
        public AssignedSite AssignedSite { get; set; }
        public PayRuleSet PayRuleSet { get; set; }
        public List<TimePlanningWorkingHoursModel> TimePlannings { get; set; }
        public Dictionary<DateTime, List<PlanRegistrationPayLine>> PayLinesByDate { get; set; }
    }

    private sealed class DayOverviewRow
    {
        public string EmployeeNo { get; set; }
        public string WorkerName { get; set; }
        public DateTime Date { get; set; }
        public TimePlanningWorkingHoursModel Planning { get; set; }
        public bool UseOneMinuteIntervals { get; set; }
    }

    internal double? GetShiftTimeFraction(int? shift, DateTime? actualStamp, bool useOneMinuteIntervals)
    {
        if (useOneMinuteIntervals && actualStamp.HasValue)
        {
            return actualStamp.Value.TimeOfDay.TotalMinutes / 1440.0;
        }
        if (!shift.HasValue || shift.Value <= 0)
        {
            return null;
        }
        if (shift.Value == 289)
        {
            return 1.0; // 24:00 — end of day; renders as 00:00 under hh:mm (known minor edge)
        }
        return (shift.Value - 1) * 5 / 1440.0;
    }

    private Cell DayOverviewStringCell(int col, uint rowIdx, string value)
    {
        return new Cell
        {
            CellReference = $"{GetColumnLetter(col)}{rowIdx}",
            CellValue = new CellValue(value ?? string.Empty),
            DataType = CellValues.String
        };
    }

    private Cell DayOverviewNumberCell(int col, uint rowIdx, double value, uint? styleIndex)
    {
        var cell = new Cell
        {
            CellReference = $"{GetColumnLetter(col)}{rowIdx}",
            CellValue = new CellValue(value.ToString(CultureInfo.InvariantCulture)),
            DataType = CellValues.Number
        };
        if (styleIndex.HasValue)
        {
            cell.StyleIndex = styleIndex.Value;
        }
        return cell;
    }

    private Cell DayOverviewDateCell(int col, uint rowIdx, DateTime date)
    {
        return new Cell
        {
            CellReference = $"{GetColumnLetter(col)}{rowIdx}",
            CellValue = new CellValue(date.ToOADate().ToString(CultureInfo.InvariantCulture)),
            DataType = CellValues.Number,
            StyleIndex = (UInt32Value)5U // dd/mm/yyyy
        };
    }

    private Cell DayOverviewTimeCell(int col, uint rowIdx, double? fraction)
    {
        var cell = new Cell
        {
            CellReference = $"{GetColumnLetter(col)}{rowIdx}",
            DataType = CellValues.Number,
            StyleIndex = (UInt32Value)3U // hh:mm
        };
        if (fraction.HasValue)
        {
            cell.CellValue = new CellValue(fraction.Value.ToString(CultureInfo.InvariantCulture));
        }
        return cell;
    }

    private void BuildDayOverviewWorksheet(WorksheetPart worksheetPart, List<DayOverviewRow> rows, CultureInfo culture)
    {
        const int colCount = 21;
        var headers = new[]
        {
            Translations.Employee_no, Translations.Worker, Translations.DayOfWeek,
            Translations.Date, Translations.Week_number,
            Translations.Shift_1__start, Translations.Shift_1__end, Translations.Shift_1__pause,
            Translations.Shift_2__start, Translations.Shift_2__end, Translations.Shift_2__pause,
            Translations.Shift_3__start, Translations.Shift_3__end, Translations.Shift_3__pause,
            Translations.Shift_4__start, Translations.Shift_4__end, Translations.Shift_4__pause,
            Translations.Shift_5__start, Translations.Shift_5__end, Translations.Shift_5__pause,
            Translations.NettoHours
        };

        var worksheet = new Worksheet()
            { MCAttributes = new MarkupCompatibilityAttributes() { Ignorable = "x14ac xr xr2 xr3" } };
        worksheet.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
        worksheet.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
        worksheet.AddNamespaceDeclaration("x14ac", "http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac");
        worksheet.AddNamespaceDeclaration("xr", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision");
        worksheet.AddNamespaceDeclaration("xr2", "http://schemas.microsoft.com/office/spreadsheetml/2015/revision2");
        worksheet.AddNamespaceDeclaration("xr3", "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3");

        var sheetFormatProperties = new SheetFormatProperties() { DefaultRowHeight = 15D, DyDescent = 0.25D };

        var columns = new Columns();
        columns.Append(new Column() { Min = 1U, Max = 1U, Width = 18D, CustomWidth = true });
        columns.Append(new Column() { Min = 2U, Max = 2U, Width = 15D, CustomWidth = true });
        columns.Append(new Column() { Min = 3U, Max = 3U, Width = 10D, CustomWidth = true });
        columns.Append(new Column() { Min = 4U, Max = 4U, Width = 11D, CustomWidth = true });
        columns.Append(new Column() { Min = 5U, Max = 5U, Width = 8D, CustomWidth = true });
        columns.Append(new Column() { Min = 6U, Max = 20U, Width = 13.5D, CustomWidth = true });
        columns.Append(new Column() { Min = 21U, Max = 21U, Width = 12D, CustomWidth = true });

        var sheetData = new SheetData();

        var headerRow = new Row() { RowIndex = (UInt32Value)1U };
        for (int c = 0; c < colCount; c++)
        {
            headerRow.Append(new Cell
            {
                CellReference = $"{GetColumnLetter(c + 1)}1",
                CellValue = new CellValue(headers[c]),
                DataType = CellValues.String,
                StyleIndex = (UInt32Value)1U
            });
        }
        sheetData.Append(headerRow);

        uint rowIndex = 2;
        foreach (var row in rows)
        {
            var dataRow = new Row() { RowIndex = rowIndex };
            int c = 1;
            dataRow.Append(DayOverviewStringCell(c++, rowIndex, row.EmployeeNo));
            dataRow.Append(DayOverviewStringCell(c++, rowIndex, row.WorkerName));
            dataRow.Append(DayOverviewStringCell(c++, rowIndex, row.Date.ToString("dddd", culture)));
            dataRow.Append(DayOverviewDateCell(c++, rowIndex, row.Date));
            var weekNumber = culture.Calendar.GetWeekOfYear(row.Date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            dataRow.Append(DayOverviewNumberCell(c++, rowIndex, weekNumber, null));

            var p = row.Planning;
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift1Start, p.Start1StartedAt, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift1Stop, p.Stop1StoppedAt, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, PauseMinutesAsDayFraction(p.Shift1PauseMinutes, p.Shift1Pause)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift2Start, p.Start2StartedAt, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift2Stop, p.Stop2StoppedAt, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, PauseMinutesAsDayFraction(p.Shift2PauseMinutes, p.Shift2Pause)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift3Start, p.Start3StartedAt, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift3Stop, p.Stop3StoppedAt, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift3Pause, null, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift4Start, p.Start4StartedAt, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift4Stop, p.Stop4StoppedAt, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift4Pause, null, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift5Start, p.Start5StartedAt, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift5Stop, p.Stop5StoppedAt, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift5Pause, null, row.UseOneMinuteIntervals)));

            var netto = p.NettoHoursOverrideActive ? p.NettoHoursOverride : p.NettoHours;
            dataRow.Append(DayOverviewNumberCell(c, rowIndex, netto, (UInt32Value)4U));

            sheetData.Append(dataRow);
            rowIndex++;
        }

        uint lastRow = rowIndex - 1; // header-only when no data => 1
        string reference = $"A1:{GetColumnLetter(colCount)}{lastRow}";

        var pageMargins = new PageMargins() { Left = 0.7D, Right = 0.7D, Top = 0.75D, Bottom = 0.75D, Header = 0.3D, Footer = 0.3D };

        worksheet.Append(sheetFormatProperties);
        worksheet.Append(columns);
        worksheet.Append(sheetData);
        worksheet.Append(pageMargins);

        var tableDefinitionPart = worksheetPart.AddNewPart<TableDefinitionPart>("rIdDayOverviewTable");
        var table = new Table()
        {
            Id = (UInt32Value)1U,
            Name = "DayOverview",
            DisplayName = "DayOverview",
            Reference = reference,
            TotalsRowShown = false
        };
        table.Append(new AutoFilter() { Reference = reference });
        var tableColumns = new TableColumns() { Count = (UInt32Value)(uint)colCount };
        for (uint tc = 1; tc <= colCount; tc++)
        {
            tableColumns.Append(new TableColumn() { Id = (UInt32Value)tc, Name = headers[tc - 1] });
        }
        table.Append(tableColumns);
        table.Append(new TableStyleInfo()
        {
            Name = "TableStyleMedium2",
            ShowFirstColumn = false,
            ShowLastColumn = false,
            ShowRowStripes = true,
            ShowColumnStripes = false
        });
        tableDefinitionPart.Table = table;

        var tableParts = new TableParts() { Count = (UInt32Value)1U };
        tableParts.Append(new TablePart() { Id = "rIdDayOverviewTable" });
        worksheet.Append(tableParts);

        worksheetPart.Worksheet = worksheet;
    }

    internal static List<PlanRegistrationPayLine> CalculatePayLinesForDay(
        int planRegistrationId,
        DateTime date,
        TimePlanningWorkingHoursModel dayModel,
        int totalSeconds,
        PayRuleSet payRuleSet)
    {
        if (payRuleSet == null)
        {
            return new List<PlanRegistrationPayLine>();
        }

        var dayCode = GetDayCodeForDate(date);

        // Time-band path: if PayRuleSet defines time-band rules for the day type, use them.
        if (TryGetDayType(date, dayCode, out var dayType))
        {
            var hasTimeBandRule = payRuleSet.DayTypeRules?
                .Any(r => r.DayType == dayType
                    && r.TimeBandRules != null
                    && r.TimeBandRules.Any()) ?? false;

            if (hasTimeBandRule)
            {
                var bandResults = new List<PlanRegistrationPayLine>();
                foreach (var (start, stop) in EnumerateShiftSegments(dayModel))
                {
                    bandResults.AddRange(PayLineGenerator.GenerateTimeBandPayLines(
                        planRegistrationId, dayType, start, stop, payRuleSet, DateTime.UtcNow));
                }
                return MergeByPayCode(bandResults);
            }
        }

        // Tier path: existing behavior — split totalSeconds across DayRule tiers.
        if (totalSeconds <= 0)
        {
            return new List<PlanRegistrationPayLine>();
        }

        return PayLineGenerator.GeneratePayLines(
            planRegistrationId, dayCode, totalSeconds, payRuleSet, DateTime.UtcNow);
    }
}