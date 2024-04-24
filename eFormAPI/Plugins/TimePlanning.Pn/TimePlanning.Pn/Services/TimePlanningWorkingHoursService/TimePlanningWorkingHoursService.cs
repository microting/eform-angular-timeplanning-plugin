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

using System.Globalization;
using System.IO;
using System.Threading;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Sentry;
using TimePlanning.Pn.Helpers;
using TimePlanning.Pn.Resources;

namespace TimePlanning.Pn.Services.TimePlanningWorkingHoursService
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.Models.Settings;
    using Infrastructure.Models.WorkingHours.Index;
    using Infrastructure.Models.WorkingHours.UpdateCreate;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microting.eForm.Infrastructure.Constants;
    using Microting.eFormApi.BasePn.Abstractions;
    using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
    using Microting.eFormApi.BasePn.Infrastructure.Models.API;
    using Microting.TimePlanningBase.Infrastructure.Data;
    using Microting.TimePlanningBase.Infrastructure.Data.Entities;
    using TimePlanningLocalizationService;

    /// <summary>
    /// TimePlanningWorkingHoursService
    /// </summary>
    public class TimePlanningWorkingHoursService : ITimePlanningWorkingHoursService
    {
        private readonly IPluginDbOptions<TimePlanningBaseSettings> _options;
        private readonly ILogger<TimePlanningWorkingHoursService> _logger;
        private readonly TimePlanningPnDbContext _dbContext;
        private readonly IUserService _userService;
        private readonly IEFormCoreService _coreHelper;
        private readonly ITimePlanningLocalizationService _localizationService;
        private readonly IEFormCoreService _core;

        public TimePlanningWorkingHoursService(
            ILogger<TimePlanningWorkingHoursService> logger,
            TimePlanningPnDbContext dbContext,
            IUserService userService,
            ITimePlanningLocalizationService localizationService,
            IEFormCoreService core,
            IPluginDbOptions<TimePlanningBaseSettings> options, IEFormCoreService coreHelper)
        {
            _logger = logger;
            _dbContext = dbContext;
            _userService = userService;
            _localizationService = localizationService;
            _core = core;
            _options = options;
            _coreHelper = coreHelper;
        }

        public async Task<OperationDataResult<List<TimePlanningWorkingHoursModel>>> Index(TimePlanningWorkingHoursRequestModel model)
        {
            try
            {
                model.DateFrom = new DateTime(model.DateFrom.Year, model.DateFrom.Month, model.DateFrom.Day, 0, 0, 0);
                model.DateTo = new DateTime(model.DateTo.Year, model.DateTo.Month, model.DateTo.Day, 0, 0, 0);
                var core = await _core.GetCore();
                await using var sdkDbContext = core.DbContextHelper.GetDbContext();
                var maxDaysEditable = _options.Value.MaxDaysEditable;
                var language = await _userService.GetCurrentUserLanguage();
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

                var timePlanningRequest = _dbContext.PlanRegistrations
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

                var timePlannings = await timePlanningRequest
                    .Select(x => new TimePlanningWorkingHoursModel
                    {
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
                        NettoHours = Math.Round(x.NettoHours,2),
                        FlexHours = Math.Round(x.Flex,2),
                        SumFlexStart = Math.Round(x.SumFlexStart,2),
                        PaidOutFlex = x.PaiedOutFlex,
                        Message = x.MessageId,
                        CommentWorker = x.WorkerComment.Replace("\r", "<br />"),
                        CommentOffice = x.CommentOffice.Replace("\r", "<br />"),
                        // CommentOfficeAll = x.CommentOfficeAll,
                        IsLocked = x.Date < DateTime.Now.AddDays(-(int)maxDaysEditable),
                        IsWeekend = x.Date.DayOfWeek == DayOfWeek.Saturday || x.Date.DayOfWeek == DayOfWeek.Sunday
                    })
                    .ToListAsync();

                var totalDays = (int)(model.DateTo - model.DateFrom).TotalDays + 1;

                var lastPlanning = _dbContext.PlanRegistrations
                    .AsNoTracking()
                    .Where(x => x.Date < model.DateFrom)
                    .Where(x => x.SdkSitId == model.SiteId).OrderBy(x => x.Date).LastOrDefault();

                var prePlanning = new TimePlanningWorkingHoursModel
                {
                    WorkerName = site.Name,
                    WeekDay = lastPlanning != null ? (int)lastPlanning.Date.DayOfWeek : (int)model.DateFrom.AddDays(-1).DayOfWeek,
                    Date = lastPlanning?.Date ?? model.DateFrom.AddDays(-1),
                    PlanText = lastPlanning?.PlanText,
                    PlanHours = lastPlanning?.PlanHours ?? 0,
                    Shift1Start = lastPlanning?.Start1Id,
                    Shift1Stop = lastPlanning?.Stop1Id,
                    Shift1Pause = lastPlanning?.Pause1Id,
                    Shift2Start = lastPlanning?.Start2Id,
                    Shift2Stop = lastPlanning?.Stop2Id,
                    Shift2Pause = lastPlanning?.Pause2Id,
                    NettoHours = Math.Round(lastPlanning?.NettoHours ?? 0 ,2),
                    FlexHours = Math.Round(lastPlanning?.Flex ?? 0 ,2),
                    SumFlexStart= lastPlanning?.SumFlexStart ?? 0,
                    PaidOutFlex = lastPlanning?.PaiedOutFlex ?? 0,
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
                                IsLocked = model.DateFrom.AddDays(i) < DateTime.Now.AddDays(-(int)maxDaysEditable),
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
                double SumFlexStart= 0;
                foreach (var timePlanningWorkingHoursModel in timePlannings)
                {
                    if (j == 0)
                    {
                        timePlanningWorkingHoursModel.SumFlexStart = Math.Round(timePlanningWorkingHoursModel.SumFlexStart, 2);
                        timePlanningWorkingHoursModel.SumFlexEnd = Math.Round(timePlanningWorkingHoursModel.SumFlexStart + timePlanningWorkingHoursModel.FlexHours - timePlanningWorkingHoursModel.PaidOutFlex, 2);
                        sumFlexEnd = timePlanningWorkingHoursModel.SumFlexEnd;
                    }
                    else
                    {
                        timePlanningWorkingHoursModel.SumFlexStart = sumFlexEnd;
                        timePlanningWorkingHoursModel.SumFlexEnd = Math.Round(timePlanningWorkingHoursModel.SumFlexStart + timePlanningWorkingHoursModel.FlexHours - timePlanningWorkingHoursModel.PaidOutFlex, 2);
                        sumFlexEnd = timePlanningWorkingHoursModel.SumFlexEnd;
                    }
                    j++;
                }

                return new OperationDataResult<List<TimePlanningWorkingHoursModel>>(
                    true,
                    timePlannings);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                return new OperationDataResult<List<TimePlanningWorkingHoursModel>>(
                    false,
                    _localizationService.GetString("ErrorWhileObtainingPlannings"));
            }
        }

        public async Task<OperationResult> CreateUpdate(TimePlanningWorkingHoursUpdateCreateModel model)
        {
            try
            {
                var planRegistrations = await _dbContext.PlanRegistrations
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
                var core = await _core.GetCore();
                await using var sdkDbContext = core.DbContextHelper.GetDbContext();
                var site = await sdkDbContext.Sites.SingleOrDefaultAsync(x => x.MicrotingUid == model.SiteId);
                var language = await sdkDbContext.Languages.SingleAsync(x => x.Id == site.LanguageId);
                var folderId = _options.Value.FolderId == 0 ? null : _options.Value.FolderId;
                var maxHistoryDays = _options.Value.MaxHistoryDays == 0 ? null : _options.Value.MaxHistoryDays;
                var eFormId = _options.Value.InfoeFormId;

                var lastDate = model.Plannings.Last().Date;
                var allPlannings = await _dbContext.PlanRegistrations
                    .Where(x => x.Date >= lastDate)
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.SdkSitId == site.MicrotingUid)
                    .OrderBy(x => x.Date).ToListAsync();

                var preSumFlexStart = allPlannings.Any() ? allPlannings.First().SumFlexEnd : 0;

                foreach (var planRegistration in allPlannings)
                {
                    if (planRegistration.Date > lastDate)
                    {
                        planRegistration.SumFlexStart = preSumFlexStart;
                        planRegistration.SumFlexEnd = preSumFlexStart + planRegistration.Flex - planRegistration.PaiedOutFlex;
                        preSumFlexStart = planRegistration.SumFlexEnd;
                        await planRegistration.Update(_dbContext);
                    }
                }

                if (_options.Value.MaxHistoryDays != null)
                {
                    var maxHistoryDaysInd = (int)_options.Value.MaxHistoryDays;
                    var firstDate = model.Plannings.First(x => x.Date >= DateTime.Now.AddDays(-maxHistoryDaysInd)).Date;
                    var list = await _dbContext.PlanRegistrations.Where(x => x.Date >= firstDate && x.Date <= DateTime.UtcNow
                            && x.SdkSitId == site.MicrotingUid && x.DataFromDevice)
                        .OrderBy(x => x.Date).ToListAsync();
                    foreach (var planRegistration in list)
                    {

                        var message =
                            await _dbContext.Messages.SingleOrDefaultAsync(x => x.Id == planRegistration.MessageId);
                        Console.WriteLine($"Updating planRegistration {planRegistration.Id} for date {planRegistration.Date}");
                        string theMessage;
                        switch (language.LanguageCode)
                        {
                            case "da":
                                theMessage = message != null ? message.DaName : "";
                                break;
                            case "de":
                                theMessage = message != null ? message.DeName : "";
                                break;
                            default:
                                theMessage = message != null ? message.EnName : "";
                                break;
                        }
                        planRegistration.StatusCaseId = await new DeploymentHelper().DeployResults(planRegistration,(int)maxHistoryDays!, (int)eFormId!, core, site, (int)folderId!, theMessage);
                        await planRegistration.Update(_dbContext);
                    }
                }

                return new OperationResult(
                    true,
                    _localizationService.GetString("SuccessfullyCreateOrUpdatePlanning"));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                return new OperationDataResult<List<TimePlanningWorkingHoursModel>>(
                    false,
                    _localizationService.GetString("ErrorWhileCreateUpdatePlannings"));
            }
        }

        private async Task CreatePlanning(bool first, TimePlanningWorkingHoursModel model, int sdkSiteId, int microtingUid, string commentWorker)
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
                    CreatedByUserId = _userService.UserId,
                    UpdatedByUserId = _userService.UserId,
                    CommentOffice = model.CommentOffice,
                    CommentOfficeAll = model.CommentOfficeAll,
                    NettoHours = model.NettoHours,
                    PaiedOutFlex = model.PaidOutFlex,
                    Pause1Id = model.Shift1Pause ?? 0,
                    Pause2Id = model.Shift1Pause ?? 0,
                    Start1Id = model.Shift1Start ?? 0,
                    Start2Id = model.Shift2Start ?? 0,
                    Stop1Id = model.Shift1Stop ?? 0,
                    Stop2Id = model.Shift2Stop ?? 0,
                    Flex = model.FlexHours,
                    StatusCaseId = 0
                };

                var preTimePlanning =
                    await _dbContext.PlanRegistrations.AsNoTracking().Where(x => x.Date < planRegistration.Date
                        && x.SdkSitId == planRegistration.SdkSitId).OrderByDescending(x => x.Date).FirstOrDefaultAsync();
                if (preTimePlanning != null)
                {
                    planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                    planRegistration.SumFlexEnd = preTimePlanning.SumFlexEnd + planRegistration.Flex - planRegistration.PaiedOutFlex;
                }
                else
                {
                    planRegistration.SumFlexEnd = planRegistration.Flex - planRegistration.PaiedOutFlex;
                    planRegistration.SumFlexStart = 0;
                }

                await planRegistration.Create(_dbContext);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
            }
        }

        private async Task UpdatePlanning(bool first, PlanRegistration planRegistration,
            TimePlanningWorkingHoursModel model,
            int microtingUid)
        {
            try
            {
                planRegistration.MessageId = model.Message == 10 ? null : model.Message;
                planRegistration.PlanText = model.PlanText;
                planRegistration.Date = model.Date;
                planRegistration.PlanHours = model.PlanHours;
                planRegistration.UpdatedByUserId = _userService.UserId;
                planRegistration.CommentOffice = model.CommentOffice;
                planRegistration.CommentOfficeAll = model.CommentOfficeAll;
                planRegistration.NettoHours = model.NettoHours;
                planRegistration.PaiedOutFlex = model.PaidOutFlex;
                planRegistration.Pause1Id = model.Shift1Pause ?? 0;
                planRegistration.Pause2Id = model.Shift2Pause ?? 0;
                planRegistration.Start1Id = model.Shift1Start ?? 0;
                planRegistration.Start2Id = model.Shift2Start ?? 0;
                planRegistration.Stop1Id = model.Shift1Stop ?? 0;
                planRegistration.Stop2Id = model.Shift2Stop ?? 0;
                planRegistration.Flex = model.FlexHours;
                planRegistration.PaiedOutFlex = model.PaidOutFlex;
                var preTimePlanning =
                    await _dbContext.PlanRegistrations.AsNoTracking()
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .Where(x => x.Date < planRegistration.Date
                        && x.SdkSitId == planRegistration.SdkSitId)
                        .OrderByDescending(x => x.Date)
                        .FirstOrDefaultAsync();
                if (preTimePlanning != null)
                {
                    planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                    planRegistration.SumFlexEnd = preTimePlanning.SumFlexEnd + planRegistration.Flex - planRegistration.PaiedOutFlex;
                }
                else
                {
                    planRegistration.SumFlexEnd = planRegistration.Flex - planRegistration.PaiedOutFlex;
                    planRegistration.SumFlexStart = 0;
                }

                await planRegistration.Update(_dbContext);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
            }
        }

        // Lene Plov
        // Dorth Smith
        // Majbrit skovgård
        // Emma Pedersen -17,16 Sandheden er 6,33 pr. 9/6/2022

        public async Task<OperationDataResult<Stream>> GenerateExcelDashboard(TimePlanningWorkingHoursRequestModel model)
        {
            try
            {
                // get core
                var core = await _coreHelper.GetCore();
                var sdkContext = core.DbContextHelper.GetDbContext();
                var site = await sdkContext.Sites.SingleOrDefaultAsync(x => x.MicrotingUid == model.SiteId);
                var language = await sdkContext.Languages.SingleAsync(x => x.Id == site.LanguageId);

                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(language.LanguageCode);
                var ci = new CultureInfo(language.LanguageCode);

                Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "results"));

                var timeStamp = $"{DateTime.UtcNow:yyyyMMdd}_{DateTime.UtcNow:hhmmss}";

                var resultDocument = Path.Combine(Path.GetTempPath(), "results",
                    $"{timeStamp}_.xlsx");

                Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "results"));

                IXLWorkbook wb = new XLWorkbook();

                var worksheet = wb.Worksheets.Add("Dashboard");

                var x = 0;
                var y = 0;

                worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Worker);
                worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                y++;
                worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.DayOfWeek);
                worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                y++;
                worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Date);
                worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                y++;
                worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.PlanText);
                worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                y++;
                worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.PlanHours);
                worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                y++;
                worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Shift_1__start);
                worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                y++;
                worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Shift_1__end);
                worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                y++;
                worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Shift_1__pause);
                worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                y++;
                worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Shift_2__start);
                worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                y++;
                worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Shift_2__end);
                worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                y++;
                worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Shift_2__pause);
                worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                y++;
                worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.NettoHours);
                worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                y++;
                worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Flex);
                worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                y++;
                worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.SumFlexStart);
                worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                y++;
                worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.PaidOutFlex);
                worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                y++;
                worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Message);
                worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                y++;
                worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Comments);
                worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                y++;
                worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Comment_office);
                worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                y++;
                // worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Comment_office_all);
                // worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                // y++;

                var content = await Index(model);

                var plr = new PlanRegistration();

                if (content.Success)
                {
                    var rows = content.Model;

                    var firstDone = false;
                    foreach (var timePlanningWorkingHoursModel in rows)
                    {
                        if (firstDone)
                        {
                            var theMessage =
                                await _dbContext.Messages.SingleOrDefaultAsync(x =>
                                    x.Id == timePlanningWorkingHoursModel.Message);
                            var messageText = theMessage != null ? theMessage.EnName : "";
                            switch (language.LanguageCode)
                            {
                                case "da":
                                    messageText = theMessage != null ? theMessage.DaName : "";
                                    break;
                                case "de":
                                    messageText = theMessage != null ? theMessage.DeName : "";
                                    break;
                            }

                            x++;
                            y = 0;

                            worksheet.Cell(x + 1, y + 1).Value = site.Name;
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value =
                                timePlanningWorkingHoursModel.Date.ToString("dddd", ci);
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.Date;
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.PlanText; // TODO plan text
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.PlanHours;
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value = plr.Options[
                                timePlanningWorkingHoursModel.Shift1Start > 0
                                    ? (int) timePlanningWorkingHoursModel.Shift1Start - 1
                                    : 0];
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value = plr.Options[
                                timePlanningWorkingHoursModel.Shift1Stop > 0
                                    ? (int) timePlanningWorkingHoursModel.Shift1Stop - 1
                                    : 0];
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value = plr.Options[
                                timePlanningWorkingHoursModel.Shift1Pause > 0
                                    ? (int) timePlanningWorkingHoursModel.Shift1Pause - 1
                                    : 0];
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value = plr.Options[
                                timePlanningWorkingHoursModel.Shift2Start > 0
                                    ? (int) timePlanningWorkingHoursModel.Shift2Start - 1
                                    : 0];
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value = plr.Options[
                                timePlanningWorkingHoursModel.Shift2Stop > 0
                                    ? (int) timePlanningWorkingHoursModel.Shift2Stop - 1
                                    : 0];
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value = plr.Options[
                                timePlanningWorkingHoursModel.Shift2Pause > 0
                                    ? (int) timePlanningWorkingHoursModel.Shift2Pause - 1
                                    : 0];
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.NettoHours;
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.FlexHours;
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.SumFlexEnd;
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.PaidOutFlex;
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value = messageText;
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.CommentWorker?.Replace("<br>", "\n");
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.CommentOffice?.Replace("<br>", "\n");
                            // y++;
                            // worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.CommentOfficeAll;
                        }
                        firstDone = true;
                    }
                }
                worksheet.RangeUsed().SetAutoFilter();

                wb.SaveAs(resultDocument);
                // TODO check adjustment for width of text for row 0

                Stream result = File.Open(resultDocument, FileMode.Open);
                return new OperationDataResult<Stream>(true, result);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                return new OperationDataResult<Stream>(
                    false,
                    _localizationService.GetString("ErrorWhileCreatingWordFile"));
            }
        }


        public async Task<OperationDataResult<Stream>> GenerateExcelDashboard(TimePlanningWorkingHoursReportForAllWorkersRequestModel model)
        {
            try
            {
                var siteIds = await _dbContext.AssignedSites
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Select(x => x.SiteId)
                    //.Distinct()
                    .ToListAsync();
                // get core
                var core = await _coreHelper.GetCore();
                var sdkContext = core.DbContextHelper.GetDbContext();

                Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "results"));

                var timeStamp = $"{DateTime.UtcNow:yyyyMMdd}_{DateTime.UtcNow:hhmmss}";

                var resultDocument = Path.Combine(Path.GetTempPath(), "results",
                    $"{timeStamp}_.xlsx");

                Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "results"));

                IXLWorkbook wb = new XLWorkbook();

                foreach (var siteId in siteIds)
                {
                    var site = await sdkContext.Sites.SingleOrDefaultAsync(x => x.MicrotingUid == siteId);
                    if (site.WorkflowState == Constants.WorkflowStates.Removed)
                    {
                        continue;
                    }
                    var language = await sdkContext.Languages.SingleAsync(x => x.Id == site.LanguageId);

                    Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(language.LanguageCode);
                    var ci = new CultureInfo(language.LanguageCode);
                    var worksheet = wb.Worksheets.Add(site.Name);

                    var x = 0;
                    var y = 0;

                    worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Worker);
                    worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                    y++;
                    worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.DayOfWeek);
                    worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                    y++;
                    worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Date);
                    worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                    y++;
                    worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.PlanText);
                    worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                    y++;
                    worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.PlanHours);
                    worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                    y++;
                    worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Shift_1__start);
                    worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                    y++;
                    worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Shift_1__end);
                    worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                    y++;
                    worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Shift_1__pause);
                    worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                    y++;
                    worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Shift_2__start);
                    worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                    y++;
                    worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Shift_2__end);
                    worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                    y++;
                    worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Shift_2__pause);
                    worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                    y++;
                    worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.NettoHours);
                    worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                    y++;
                    worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Flex);
                    worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                    y++;
                    worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.SumFlexStart);
                    worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                    y++;
                    worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.PaidOutFlex);
                    worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                    y++;
                    worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Message);
                    worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                    y++;
                    worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Comments);
                    worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                    y++;
                    worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Comment_office);
                    worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                    // y++;
                    // worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Comment_office_all);
                    // worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                    // y++;

                    var content = await Index(new TimePlanningWorkingHoursRequestModel{DateFrom = model.DateFrom, DateTo = model.DateTo, SiteId = siteId});

                    var plr = new PlanRegistration();

                    if (content.Success)
                    {
                        var rows = content.Model;

                        var firstDone = false;
                        foreach (var timePlanningWorkingHoursModel in rows)
                        {
                            if (firstDone)
                            {
                                var theMessage =
                                    await _dbContext.Messages.SingleOrDefaultAsync(x =>
                                        x.Id == timePlanningWorkingHoursModel.Message);
                                var messageText = theMessage != null ? theMessage.EnName : "";
                                switch (language.LanguageCode)
                                {
                                    case "da":
                                        messageText = theMessage != null ? theMessage.DaName : "";
                                        break;
                                    case "de":
                                        messageText = theMessage != null ? theMessage.DeName : "";
                                        break;
                                }

                                x++;
                                y = 0;

                                worksheet.Cell(x + 1, y + 1).Value = site.Name;
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value =
                                    timePlanningWorkingHoursModel.Date.ToString("dddd", ci);
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.Date;
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.PlanText; // TODO plan text
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.PlanHours;
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value = plr.Options[
                                    timePlanningWorkingHoursModel.Shift1Start > 0
                                        ? (int)timePlanningWorkingHoursModel.Shift1Start - 1
                                        : 0];
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value = plr.Options[
                                    timePlanningWorkingHoursModel.Shift1Stop > 0
                                        ? (int)timePlanningWorkingHoursModel.Shift1Stop - 1
                                        : 0];
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value = plr.Options[
                                    timePlanningWorkingHoursModel.Shift1Pause > 0
                                        ? (int)timePlanningWorkingHoursModel.Shift1Pause - 1
                                        : 0];
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value = plr.Options[
                                    timePlanningWorkingHoursModel.Shift2Start > 0
                                        ? (int)timePlanningWorkingHoursModel.Shift2Start - 1
                                        : 0];
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value = plr.Options[
                                    timePlanningWorkingHoursModel.Shift2Stop > 0
                                        ? (int)timePlanningWorkingHoursModel.Shift2Stop - 1
                                        : 0];
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value = plr.Options[
                                    timePlanningWorkingHoursModel.Shift2Pause > 0
                                        ? (int)timePlanningWorkingHoursModel.Shift2Pause - 1
                                        : 0];
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.NettoHours;
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.FlexHours;
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.SumFlexEnd;
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.PaidOutFlex;
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value = messageText;
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.CommentWorker?.Replace("<br>", "\n");
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.CommentOffice?.Replace("<br>", "\n");
                                // y++;
                                // worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.CommentOfficeAll;
                            }
                            firstDone = true;
                        }
                    }

                    worksheet.RangeUsed().SetAutoFilter();
                }

                wb.SaveAs(resultDocument);
                // TODO check adjustment for width of text for row 0

                Stream result = File.Open(resultDocument, FileMode.Open);
                return new OperationDataResult<Stream>(true, result);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                SentrySdk.CaptureException(e);
                return new OperationDataResult<Stream>(
                    false,
                    _localizationService.GetString("ErrorWhileCreatingWordFile"));
            }
        }

        public async Task<OperationResult> Import(IFormFile file)
        {

            // file is a excel file and each sheet corresponds to a site in the SDK
            // each row is a day and column E is the date
            // column F is the plan text
            // column G is the plan hours
            // only import if the date is not in the past

            // get core
            var core = await _coreHelper.GetCore();
            var sdkContext = core.DbContextHelper.GetDbContext();
            // open file
            // loop through sheets
            // get site
            // loop through rows
            // get date
            // get plan text
            // get plan hours
            // check if date is in the past
            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                using (var package = new XLWorkbook(stream))
                {
                    var sheets = package.Worksheets;
                    foreach (var sheet in sheets)
                    {
                        var site = await sdkContext.Sites.SingleOrDefaultAsync(x => x.Name == sheet.Name);
                        if (site == null)
                        {
                            continue;
                        }

                        var rows = sheet.RangeUsed()
                            .RowsUsed();
                        foreach (var row in rows)
                        {
                            // skip first row
                            if (row.RowNumber() == 1)
                            {
                                continue;
                            }
                            var date = row.Cell(5).Value;
                            var planText = row.Cell(6).Value;
                            var planHours = row.Cell(7).Value;
                            // if (date == null || planText == null || planHours == null)
                            // {
                            //     continue;
                            // }
                            var dateValue = DateTime.Parse(date.ToString());
                            if (dateValue < DateTime.Now)
                            {
                                continue;
                            }

                            var preTimePlanning =
                                await _dbContext.PlanRegistrations.AsNoTracking()
                                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                                    .Where(x => x.Date < dateValue
                                                && x.SdkSitId == (int)site.MicrotingUid!)
                                    .OrderByDescending(x => x.Date)
                                    .FirstOrDefaultAsync();
                            var planRegistration = await _dbContext.PlanRegistrations.SingleOrDefaultAsync(x =>
                                x.Date == dateValue && x.SdkSitId == site.MicrotingUid);
                            if (planRegistration == null)
                            {
                                planRegistration = new PlanRegistration
                                {
                                    Date = dateValue,
                                    PlanText = planText.ToString(),
                                    PlanHours = string.IsNullOrEmpty(planHours.ToString()) ? 0 : double.Parse(planHours.ToString()),
                                    SdkSitId = (int)site.MicrotingUid!,
                                    CreatedByUserId = _userService.UserId,
                                    UpdatedByUserId = _userService.UserId,
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
                                    planRegistration.SumFlexEnd = preTimePlanning.SumFlexEnd + planRegistration.Flex - planRegistration.PaiedOutFlex;
                                    planRegistration.Flex = -planRegistration.PlanHours;
                                }
                                else
                                {
                                    planRegistration.Flex = -planRegistration.PlanHours;
                                    planRegistration.SumFlexEnd = planRegistration.Flex;
                                    planRegistration.SumFlexStart = 0;
                                }
                                await planRegistration.Create(_dbContext);
                            }
                            else
                            {
                                planRegistration.PlanText = planText.ToString();
                                planRegistration.PlanHours = string.IsNullOrEmpty(planHours.ToString())
                                    ? 0
                                    : double.Parse(planHours.ToString());
                                planRegistration.UpdatedByUserId = _userService.UserId;

                                if (preTimePlanning != null)
                                {
                                    planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                                    planRegistration.SumFlexEnd = preTimePlanning.SumFlexEnd + planRegistration.PlanHours - planRegistration.NettoHours - planRegistration.PaiedOutFlex;
                                    planRegistration.Flex = planRegistration.NettoHours - planRegistration.PlanHours;
                                }
                                else
                                {
                                    planRegistration.SumFlexEnd = planRegistration.PlanHours - planRegistration.NettoHours - planRegistration.PaiedOutFlex;
                                    planRegistration.SumFlexStart = 0;
                                    planRegistration.Flex = planRegistration.NettoHours - planRegistration.PlanHours;
                                }
                                await planRegistration.Update(_dbContext);
                            }
                        }
                    }
                }
            }

            return new OperationResult(true, "Imported");
        }
    }
}