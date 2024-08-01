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
using System.Globalization;
using System.IO;
using System.Threading;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Sentry;
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
        private readonly ITimePlanningLocalizationService _localizationService;
        private readonly IEFormCoreService _coreHelper;

        public TimePlanningWorkingHoursService(
            ILogger<TimePlanningWorkingHoursService> logger,
            TimePlanningPnDbContext dbContext,
            IUserService userService,
            ITimePlanningLocalizationService localizationService,
            IPluginDbOptions<TimePlanningBaseSettings> options, IEFormCoreService coreHelper)
        {
            _logger = logger;
            _dbContext = dbContext;
            _userService = userService;
            _localizationService = localizationService;
            _options = options;
            _coreHelper = coreHelper;
        }

        public async Task<OperationDataResult<List<TimePlanningWorkingHoursModel>>> Index(
            TimePlanningWorkingHoursRequestModel model)
        {
            try
            {
                model.DateFrom = new DateTime(model.DateFrom.Year, model.DateFrom.Month, model.DateFrom.Day, 0, 0, 0);
                model.DateTo = new DateTime(model.DateTo.Year, model.DateTo.Month, model.DateTo.Day, 0, 0, 0);
                var core = await _coreHelper.GetCore();
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
                var dateTime = DateTime.Now;
                var midnight = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, 0, 0, 0);

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
                        NettoHours = Math.Round(x.NettoHours, 2),
                        FlexHours = Math.Round(x.Flex, 2),
                        SumFlexStart = Math.Round(x.SumFlexStart, 2),
                        PaidOutFlex = x.PaiedOutFlex,
                        Message = x.MessageId,
                        CommentWorker = x.WorkerComment.Replace("\r", "<br />"),
                        CommentOffice = x.CommentOffice.Replace("\r", "<br />"),
                        // CommentOfficeAll = x.CommentOfficeAll,
                        IsLocked = (x.Date < DateTime.Now.AddDays(-(int)maxDaysEditable) || x.Date == midnight),
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
                    NettoHours = Math.Round(lastPlanning?.NettoHours ?? 0, 2),
                    FlexHours = Math.Round(lastPlanning?.Flex ?? 0, 2),
                    SumFlexStart = lastPlanning?.SumFlexStart ?? 0,
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
                                IsLocked = model.DateFrom.AddDays(i) < DateTime.Now.AddDays(-(int)maxDaysEditable) || model.DateFrom.AddDays(i) == midnight,
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
                double SumFlexStart = 0;
                foreach (var timePlanningWorkingHoursModel in timePlannings)
                {
                    if (j == 0)
                    {
                        timePlanningWorkingHoursModel.SumFlexStart =
                            Math.Round(timePlanningWorkingHoursModel.SumFlexStart, 2);
                        timePlanningWorkingHoursModel.SumFlexEnd = Math.Round(
                            timePlanningWorkingHoursModel.SumFlexStart + timePlanningWorkingHoursModel.FlexHours -
                            timePlanningWorkingHoursModel.PaidOutFlex, 2);
                        sumFlexEnd = timePlanningWorkingHoursModel.SumFlexEnd;
                    }
                    else
                    {
                        timePlanningWorkingHoursModel.SumFlexStart = sumFlexEnd;
                        timePlanningWorkingHoursModel.SumFlexEnd = Math.Round(
                            timePlanningWorkingHoursModel.SumFlexStart + timePlanningWorkingHoursModel.FlexHours -
                            timePlanningWorkingHoursModel.PaidOutFlex, 2);
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

            var registrationDevices = await _dbContext.RegistrationDevices
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .ToListAsync().ConfigureAwait(false);

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

                var core = await _coreHelper.GetCore();
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
                    .OrderBy(x => x.Date)
                    .Take(365)
                    .ToListAsync();

                var preSumFlexStart = allPlannings.Any() ? allPlannings.First().SumFlexEnd : 0;

                foreach (var planRegistration in allPlannings)
                {
                    if (planRegistration.Date > lastDate)
                    {
                        planRegistration.SumFlexStart = preSumFlexStart;
                        planRegistration.SumFlexEnd =
                            preSumFlexStart + planRegistration.Flex - planRegistration.PaiedOutFlex;
                        preSumFlexStart = planRegistration.SumFlexEnd;
                        await planRegistration.Update(_dbContext);
                    }
                }

                if (_options.Value.MaxHistoryDays != null)
                {
                    var maxHistoryDaysInd = (int)_options.Value.MaxHistoryDays;
                    var firstDate = model.Plannings.First(x => x.Date >= DateTime.Now.AddDays(-maxHistoryDaysInd)).Date;
                    var list = await _dbContext.PlanRegistrations.Where(x => x.Date >= firstDate &&
                                                                             x.Date <= DateTime.UtcNow
                                                                             && x.SdkSitId == site.MicrotingUid &&
                                                                             x.DataFromDevice)
                        .OrderBy(x => x.Date).ToListAsync();
                    foreach (var planRegistration in list)
                    {

                        var message =
                            await _dbContext.Messages.SingleOrDefaultAsync(x => x.Id == planRegistration.MessageId);
                        Console.WriteLine(
                            $"Updating planRegistration {planRegistration.Id} for date {planRegistration.Date}");
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
                        CreatedByUserId = _userService.UserId,
                        UpdatedByUserId = _userService.UserId,
                        CommentOffice = model.CommentOffice,
                        CommentOfficeAll = model.CommentOfficeAll,
                        NettoHours = model.NettoHours,
                        PaiedOutFlex = model.PaidOutFlex,
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
                        await _dbContext.PlanRegistrations.AsNoTracking().Where(x => x.Date < planRegistration.Date
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

                    await planRegistration.Create(_dbContext);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    _logger.LogError(e.Message);
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
            }

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
                planRegistration.SumFlexEnd = preTimePlanning.SumFlexEnd + planRegistration.Flex -
                                              planRegistration.PaiedOutFlex;
            }
            else
            {
                planRegistration.SumFlexEnd = planRegistration.Flex - planRegistration.PaiedOutFlex;
                planRegistration.SumFlexStart = 0;
            }

            await planRegistration.Update(_dbContext);
        }

        public async Task<OperationDataResult<Stream>> GenerateExcelDashboard(
            TimePlanningWorkingHoursRequestModel model)
        {
            try
            {
                // get core
                var core = await _coreHelper.GetCore();
                var sdkContext = core.DbContextHelper.GetDbContext();
                var site = await sdkContext.Sites.SingleOrDefaultAsync(x => x.MicrotingUid == model.SiteId);
                var siteWorker = await sdkContext.SiteWorkers.SingleOrDefaultAsync(x => x.SiteId == site.Id);
                var worker = await sdkContext.Workers.SingleOrDefaultAsync(x => x.Id == siteWorker.WorkerId);
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

                worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Employee_no);
                worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                y++;
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

                            worksheet.Cell(x + 1, y + 1).Value = worker.EmployeeNo;
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value = site.Name;
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value =
                                timePlanningWorkingHoursModel.Date.ToString("dddd", ci);
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.Date;
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value =
                                timePlanningWorkingHoursModel.PlanText; // TODO plan text
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
                            worksheet.Cell(x + 1, y + 1).Value =
                                timePlanningWorkingHoursModel.CommentWorker?.Replace("<br>", "\n");
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value =
                                timePlanningWorkingHoursModel.CommentOffice?.Replace("<br>", "\n");
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


        public async Task<OperationDataResult<Stream>> GenerateExcelDashboard(
            TimePlanningWorkingHoursReportForAllWorkersRequestModel model)
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

                var totalWorksheet = wb.Worksheets.Add("Total");
                totalWorksheet.Cell(1, 1).Value = _localizationService.GetString(Translations.Employee_no);
                totalWorksheet.Cell(1, 1).Style.Font.Bold = true;
                totalWorksheet.Cell(1, 2).Value = _localizationService.GetString(Translations.Worker);
                totalWorksheet.Cell(1, 2).Style.Font.Bold = true;
                totalWorksheet.Cell(1, 3).Value = _localizationService.GetString(Translations.PlanHours);
                totalWorksheet.Cell(1, 3).Style.Font.Bold = true;
                totalWorksheet.Cell(1, 4).Value = _localizationService.GetString(Translations.NettoHours);
                totalWorksheet.Cell(1, 4).Style.Font.Bold = true;
                totalWorksheet.Cell(1, 5).Value = _localizationService.GetString(Translations.SumFlexStart);
                totalWorksheet.Cell(1, 5).Style.Font.Bold = true;

                var z = 1;
                foreach (var siteId in siteIds)
                {
                    var totalPlannedHours = 0.0;
                    var totalNettoHours = 0.0;
                    var totalSumFlexStart = 0.0;
                    var site = await sdkContext.Sites.SingleOrDefaultAsync(x => x.MicrotingUid == siteId);
                    if (site.WorkflowState == Constants.WorkflowStates.Removed)
                    {
                        continue;
                    }
                    z++;
                    var siteWorker = await sdkContext.SiteWorkers.SingleOrDefaultAsync(x => x.SiteId == site.Id);
                    var worker = await sdkContext.Workers.SingleOrDefaultAsync(x => x.Id == siteWorker.WorkerId);

                    var language = await sdkContext.Languages.SingleAsync(x => x.Id == site.LanguageId);

                    Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(language.LanguageCode);
                    var ci = new CultureInfo(language.LanguageCode);
                    var worksheet = wb.Worksheets.Add(site.Name);

                    var x = 0;
                    var y = 0;

                    totalWorksheet.Cell(z, 1).Value = worker.EmployeeNo;
                    totalWorksheet.Cell(z, 2).Value = site.Name;

                    worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Employee_no);
                    worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                    y++;
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

                    var content = await Index(new TimePlanningWorkingHoursRequestModel
                        { DateFrom = model.DateFrom, DateTo = model.DateTo, SiteId = siteId });

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

                                worksheet.Cell(x + 1, y + 1).Value = worker.EmployeeNo;
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value = site.Name;
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value =
                                    timePlanningWorkingHoursModel.Date.ToString("dddd", ci);
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.Date;
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value =
                                    timePlanningWorkingHoursModel.PlanText; // TODO plan text
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.PlanHours;
                                totalPlannedHours += timePlanningWorkingHoursModel.PlanHours;
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
                                totalNettoHours += timePlanningWorkingHoursModel.NettoHours;
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.FlexHours;
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.SumFlexEnd;
                                totalSumFlexStart = timePlanningWorkingHoursModel.SumFlexEnd;
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.PaidOutFlex;
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value = messageText;
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value =
                                    timePlanningWorkingHoursModel.CommentWorker?.Replace("<br>", "\n");
                                y++;
                                worksheet.Cell(x + 1, y + 1).Value =
                                    timePlanningWorkingHoursModel.CommentOffice?.Replace("<br>", "\n");
                                // y++;
                                // worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.CommentOfficeAll;
                            }

                            firstDone = true;
                        }


                        totalWorksheet.Cell(z, 3).Value = totalPlannedHours;
                        totalWorksheet.Cell(z, 4).Value = totalNettoHours;
                        totalWorksheet.Cell(z, 5).Value = totalSumFlexStart;
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
                        var site = await sdkContext.Sites.FirstOrDefaultAsync(x => x.Name == sheet.Name);
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

                            var date = row.Cell(1).Value;
                            var planHours = row.Cell(2).Value;
                            var planText = row.Cell(3).Value;
                            // if (date == null || planText == null || planHours == null)
                            // {
                            //     continue;
                            // }
                            var dateValue = DateTime.Parse(date.ToString());
                            if (dateValue < DateTime.Now.AddDays(-1))
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
                                    PlanHours = string.IsNullOrEmpty(planHours.ToString())
                                        ? 0
                                        : double.Parse(planHours.ToString()),
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
                                    planRegistration.SumFlexEnd = preTimePlanning.SumFlexEnd +
                                                                  planRegistration.PlanHours -
                                                                  planRegistration.NettoHours -
                                                                  planRegistration.PaiedOutFlex;
                                    planRegistration.Flex = planRegistration.NettoHours - planRegistration.PlanHours;
                                }
                                else
                                {
                                    planRegistration.SumFlexEnd = planRegistration.PlanHours -
                                                                  planRegistration.NettoHours -
                                                                  planRegistration.PaiedOutFlex;
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

        public async Task<OperationDataResult<TimePlanningWorkingHourSimpleModel>> ReadSimple(DateTime dateTime)
        {
            var currentUser = await _userService.GetCurrentUserAsync();
            var fullName = currentUser.FirstName + " " + currentUser.LastName;
            var core = await _coreHelper.GetCore();
            var sdkContext = core.DbContextHelper.GetDbContext();
            var sdkSite = await sdkContext.Sites.SingleOrDefaultAsync(x => x.Name == fullName && x.WorkflowState != Constants.WorkflowStates.Removed);

            if (sdkSite == null)
            {
                return new OperationDataResult<TimePlanningWorkingHourSimpleModel>(false, "Site not found", null);
            }

            var midnight = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, 0, 0, 0);

            var planRegistration = await _dbContext.PlanRegistrations
                .Where(x => x.Date == midnight)
                .Where(x => x.SdkSitId == sdkSite.MicrotingUid)
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefaultAsync();

            if (planRegistration == null)
            {
                var preTimePlanning = await _dbContext.PlanRegistrations.AsNoTracking()
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

                    return new OperationDataResult<TimePlanningWorkingHourSimpleModel>(true, _localizationService.GetString("PlanRegistrationLoaded"),
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

                    return new OperationDataResult<TimePlanningWorkingHourSimpleModel>(true, _localizationService.GetString("PlanRegistrationLoaded"),
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
                NettoHours = Math.Round(planRegistration.NettoHours, 2),
                FlexHours = Math.Round(planRegistration.Flex, 2),
                SumFlexStart = Math.Round(planRegistration.SumFlexStart, 2).ToString(CultureInfo.InvariantCulture),
                SumFlexEnd = Math.Round(planRegistration.SumFlexEnd, 2).ToString(CultureInfo.InvariantCulture),
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


            return new OperationDataResult<TimePlanningWorkingHourSimpleModel>(true, _localizationService.GetString("PlanRegistrationLoaded"),
                timePlanningWorkingHoursModel);
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

        public async Task<OperationDataResult<TimePlanningWorkingHoursModel>> Read(int sdkSiteId, DateTime dateTime, string token)
        {
            if (token != null)
            {
                var registrationDevice = await _dbContext.RegistrationDevices
                    .Where(x => x.Token == token).FirstOrDefaultAsync();
                if (registrationDevice == null)
                {
                    return new OperationDataResult<TimePlanningWorkingHoursModel>(false, "Token not found", null);
                }
            }
            var today = DateTime.Now;
            var midnight = new DateTime(today.Year, today.Month, today.Day, 0, 0, 0);

            var planRegistration = await _dbContext.PlanRegistrations
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
                    PaidOutFlex = 0,
                    Message = 0,
                    CommentWorker = "",
                    CommentOffice = "",
                    CommentOfficeAll = "",
                    Shift1PauseNumber = 0,
                    Shift2PauseNumber = 0,
                };

                return new OperationDataResult<TimePlanningWorkingHoursModel>(true, "Plan registration found",
                    newTimePlanningWorkingHoursModel);
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
                NettoHours = planRegistration.NettoHours,
                FlexHours = planRegistration.Flex,
                SumFlexStart = planRegistration.SumFlexStart,
                SumFlexEnd = planRegistration.SumFlexEnd,
                PaidOutFlex = planRegistration.PaiedOutFlex,
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
                Shift1PauseNumber = planRegistration.Shift1PauseNumber,
                Shift2PauseNumber = planRegistration.Shift2PauseNumber
            };

            return new OperationDataResult<TimePlanningWorkingHoursModel>(true, "Plan registration found",
                timePlanningWorkingHoursModel);
        }

        public async Task<OperationResult> UpdateWorkingHour(int sdkSiteId, TimePlanningWorkingHoursUpdateModel model, string token)
        {

            if (token == null)
            {
                return new OperationResult(false, "Token not found");
            }

            var registrationDevice = await _dbContext.RegistrationDevices
                .Where(x => x.Token == token).FirstOrDefaultAsync();
            if (registrationDevice == null)
            {
                return new OperationDataResult<TimePlanningWorkingHoursModel>(false, "Token not found");
            }

            var todayAtMidnight = DateTime.UtcNow.Date;

            var planRegistration = await _dbContext.PlanRegistrations
                .Where(x => x.Date == todayAtMidnight)
                .Where(x => x.SdkSitId == sdkSiteId)
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefaultAsync();

            if (planRegistration == null)
            {
                planRegistration = new PlanRegistration
                {
                    MessageId = null,
                    PlanText = "",
                    Date = model.Date,
                    PlanHours = 0,
                    UpdatedByUserId = _userService.UserId,
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
                    Start1StartedAt = string.IsNullOrEmpty(model.Start1StartedAt) ? null : DateTime.Parse(model.Start1StartedAt),
                    Stop1StoppedAt = string.IsNullOrEmpty(model.Stop1StoppedAt) ? null : DateTime.Parse(model.Stop1StoppedAt),
                    Pause1StartedAt = string.IsNullOrEmpty(model.Pause1StartedAt) ? null : DateTime.Parse(model.Pause1StartedAt),
                    Pause1StoppedAt = string.IsNullOrEmpty(model.Pause1StoppedAt) ? null : DateTime.Parse(model.Pause1StoppedAt),
                    Start2StartedAt = string.IsNullOrEmpty(model.Start2StartedAt) ? null : DateTime.Parse(model.Start2StartedAt),
                    Stop2StoppedAt = string.IsNullOrEmpty(model.Stop2StoppedAt) ? null : DateTime.Parse(model.Stop2StoppedAt),
                    Pause10StartedAt = string.IsNullOrEmpty(model.Pause10StartedAt) ? null : DateTime.Parse(model.Pause10StartedAt),
                    Pause10StoppedAt = string.IsNullOrEmpty(model.Pause10StoppedAt) ? null : DateTime.Parse(model.Pause10StoppedAt),
                    Pause11StartedAt = string.IsNullOrEmpty(model.Pause11StartedAt) ? null : DateTime.Parse(model.Pause11StartedAt),
                    Pause11StoppedAt = string.IsNullOrEmpty(model.Pause11StoppedAt) ? null : DateTime.Parse(model.Pause11StoppedAt),
                    Pause12StartedAt = string.IsNullOrEmpty(model.Pause12StartedAt) ? null : DateTime.Parse(model.Pause12StartedAt),
                    Pause12StoppedAt = string.IsNullOrEmpty(model.Pause12StoppedAt) ? null : DateTime.Parse(model.Pause12StoppedAt),
                    Pause13StartedAt = string.IsNullOrEmpty(model.Pause13StartedAt) ? null : DateTime.Parse(model.Pause13StartedAt),
                    Pause13StoppedAt = string.IsNullOrEmpty(model.Pause13StoppedAt) ? null : DateTime.Parse(model.Pause13StoppedAt),
                    Pause14StartedAt = string.IsNullOrEmpty(model.Pause14StartedAt) ? null : DateTime.Parse(model.Pause14StartedAt),
                    Pause14StoppedAt = string.IsNullOrEmpty(model.Pause14StoppedAt) ? null : DateTime.Parse(model.Pause14StoppedAt),
                    Pause15StartedAt = string.IsNullOrEmpty(model.Pause15StartedAt) ? null : DateTime.Parse(model.Pause15StartedAt),
                    Pause15StoppedAt = string.IsNullOrEmpty(model.Pause15StoppedAt) ? null : DateTime.Parse(model.Pause15StoppedAt),
                    Pause16StartedAt = string.IsNullOrEmpty(model.Pause16StartedAt) ? null : DateTime.Parse(model.Pause16StartedAt),
                    Pause16StoppedAt = string.IsNullOrEmpty(model.Pause16StoppedAt) ? null : DateTime.Parse(model.Pause16StoppedAt),
                    Pause17StartedAt = string.IsNullOrEmpty(model.Pause17StartedAt) ? null : DateTime.Parse(model.Pause17StartedAt),
                    Pause17StoppedAt = string.IsNullOrEmpty(model.Pause17StoppedAt) ? null : DateTime.Parse(model.Pause17StoppedAt),
                    Pause18StartedAt = string.IsNullOrEmpty(model.Pause18StartedAt) ? null : DateTime.Parse(model.Pause18StartedAt),
                    Pause18StoppedAt = string.IsNullOrEmpty(model.Pause18StoppedAt) ? null : DateTime.Parse(model.Pause18StoppedAt),
                    Pause19StartedAt = string.IsNullOrEmpty(model.Pause19StartedAt) ? null : DateTime.Parse(model.Pause19StartedAt),
                    Pause19StoppedAt = string.IsNullOrEmpty(model.Pause19StoppedAt) ? null : DateTime.Parse(model.Pause19StoppedAt),
                    Pause100StartedAt = string.IsNullOrEmpty(model.Pause100StartedAt) ? null : DateTime.Parse(model.Pause100StartedAt),
                    Pause100StoppedAt = string.IsNullOrEmpty(model.Pause100StoppedAt) ? null : DateTime.Parse(model.Pause100StoppedAt),
                    Pause101StartedAt = string.IsNullOrEmpty(model.Pause101StartedAt) ? null : DateTime.Parse(model.Pause101StartedAt),
                    Pause101StoppedAt = string.IsNullOrEmpty(model.Pause101StoppedAt) ? null : DateTime.Parse(model.Pause101StoppedAt),
                    Pause102StartedAt = string.IsNullOrEmpty(model.Pause102StartedAt) ? null : DateTime.Parse(model.Pause102StartedAt),
                    Pause102StoppedAt = string.IsNullOrEmpty(model.Pause102StoppedAt) ? null : DateTime.Parse(model.Pause102StoppedAt),

                    Pause2StartedAt = string.IsNullOrEmpty(model.Pause2StartedAt) ? null : DateTime.Parse(model.Pause2StartedAt),
                    Pause2StoppedAt = string.IsNullOrEmpty(model.Pause2StoppedAt) ? null : DateTime.Parse(model.Pause2StoppedAt),
                    Pause20StartedAt = string.IsNullOrEmpty(model.Pause20StartedAt) ? null : DateTime.Parse(model.Pause20StartedAt),
                    Pause20StoppedAt = string.IsNullOrEmpty(model.Pause20StoppedAt) ? null : DateTime.Parse(model.Pause20StoppedAt),
                    Pause21StartedAt = string.IsNullOrEmpty(model.Pause21StartedAt) ? null : DateTime.Parse(model.Pause21StartedAt),
                    Pause21StoppedAt = string.IsNullOrEmpty(model.Pause21StoppedAt) ? null : DateTime.Parse(model.Pause21StoppedAt),
                    Pause22StartedAt = string.IsNullOrEmpty(model.Pause22StartedAt) ? null : DateTime.Parse(model.Pause22StartedAt),
                    Pause22StoppedAt = string.IsNullOrEmpty(model.Pause22StoppedAt) ? null : DateTime.Parse(model.Pause22StoppedAt),
                    Pause23StartedAt = string.IsNullOrEmpty(model.Pause23StartedAt) ? null : DateTime.Parse(model.Pause23StartedAt),
                    Pause23StoppedAt = string.IsNullOrEmpty(model.Pause23StoppedAt) ? null : DateTime.Parse(model.Pause23StoppedAt),
                    Pause24StartedAt = string.IsNullOrEmpty(model.Pause24StartedAt) ? null : DateTime.Parse(model.Pause24StartedAt),
                    Pause24StoppedAt = string.IsNullOrEmpty(model.Pause24StoppedAt) ? null : DateTime.Parse(model.Pause24StoppedAt),
                    Pause25StartedAt = string.IsNullOrEmpty(model.Pause25StartedAt) ? null : DateTime.Parse(model.Pause25StartedAt),
                    Pause25StoppedAt = string.IsNullOrEmpty(model.Pause25StoppedAt) ? null : DateTime.Parse(model.Pause25StoppedAt),
                    Pause26StartedAt = string.IsNullOrEmpty(model.Pause26StartedAt) ? null : DateTime.Parse(model.Pause26StartedAt),
                    Pause26StoppedAt = string.IsNullOrEmpty(model.Pause26StoppedAt) ? null : DateTime.Parse(model.Pause26StoppedAt),
                    Pause27StartedAt = string.IsNullOrEmpty(model.Pause27StartedAt) ? null : DateTime.Parse(model.Pause27StartedAt),
                    Pause27StoppedAt = string.IsNullOrEmpty(model.Pause27StoppedAt) ? null : DateTime.Parse(model.Pause27StoppedAt),
                    Pause28StartedAt = string.IsNullOrEmpty(model.Pause28StartedAt) ? null : DateTime.Parse(model.Pause28StartedAt),
                    Pause28StoppedAt = string.IsNullOrEmpty(model.Pause28StoppedAt) ? null : DateTime.Parse(model.Pause28StoppedAt),
                    Pause29StartedAt = string.IsNullOrEmpty(model.Pause29StartedAt) ? null : DateTime.Parse(model.Pause29StartedAt),
                    Pause29StoppedAt = string.IsNullOrEmpty(model.Pause29StoppedAt) ? null : DateTime.Parse(model.Pause29StoppedAt),
                    Pause200StartedAt = string.IsNullOrEmpty(model.Pause200StartedAt) ? null : DateTime.Parse(model.Pause200StartedAt),
                    Pause200StoppedAt = string.IsNullOrEmpty(model.Pause200StoppedAt) ? null : DateTime.Parse(model.Pause200StoppedAt),
                    Pause201StartedAt = string.IsNullOrEmpty(model.Pause201StartedAt) ? null : DateTime.Parse(model.Pause201StartedAt),
                    Pause201StoppedAt = string.IsNullOrEmpty(model.Pause201StoppedAt) ? null : DateTime.Parse(model.Pause201StoppedAt),
                    Pause202StartedAt = string.IsNullOrEmpty(model.Pause202StartedAt) ? null : DateTime.Parse(model.Pause202StartedAt),
                    Pause202StoppedAt = string.IsNullOrEmpty(model.Pause202StoppedAt) ? null : DateTime.Parse(model.Pause202StoppedAt),
                    Flex = 0,
                    WorkerComment = model.CommentWorker,
                    SdkSitId = sdkSiteId,
                    RegistrationDeviceId = registrationDevice.Id,
                    Shift1PauseNumber = model.Shift1PauseNumber,
                    Shift2PauseNumber = model.Shift2PauseNumber,
                };

                var minutesMultiplier = 5;

                double nettoMinutes = planRegistration.Stop1Id - planRegistration.Start1Id;
                nettoMinutes -= planRegistration.Pause1Id > 0 ? planRegistration.Pause1Id - 1 : 0;
                nettoMinutes = nettoMinutes + planRegistration.Stop2Id - planRegistration.Start2Id;
                nettoMinutes -= planRegistration.Pause2Id > 0 ? planRegistration.Pause2Id - 1 : 0;

                nettoMinutes *= minutesMultiplier;

                double hours = nettoMinutes / 60;
                planRegistration.NettoHours = hours;
                planRegistration.Flex = hours - planRegistration.PlanHours;
                var preTimePlanning =
                    await _dbContext.PlanRegistrations.AsNoTracking()
                        .Where(x => x.Date < planRegistration.Date && x.SdkSitId == sdkSiteId)
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .OrderByDescending(x => x.Date).FirstOrDefaultAsync();
                if (preTimePlanning != null)
                {
                    planRegistration.SumFlexEnd = preTimePlanning.SumFlexEnd + planRegistration.Flex - planRegistration.PaiedOutFlex;
                    planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                }
                else
                {
                    planRegistration.SumFlexEnd = planRegistration.Flex - planRegistration.PaiedOutFlex;
                    planRegistration.SumFlexStart = 0;
                }

                await planRegistration.Create(_dbContext).ConfigureAwait(false);
            }
            else
            {
                planRegistration.UpdatedByUserId = _userService.UserId;
                planRegistration.Pause1Id = model.Shift1Pause ?? 0;
                planRegistration.Pause2Id = model.Shift2Pause ?? 0;
                planRegistration.Start1Id = model.Shift1Start ?? 0;
                planRegistration.Start2Id = model.Shift2Start ?? 0;
                planRegistration.Stop1Id = model.Shift1Stop ?? 0;
                planRegistration.Stop2Id = model.Shift2Stop ?? 0;
                planRegistration.WorkerComment = model.CommentWorker;
                planRegistration.RegistrationDeviceId = registrationDevice.Id;

                planRegistration.Start1StartedAt = string.IsNullOrEmpty(model.Start1StartedAt)
                    ? null
                    : DateTime.Parse(model.Start1StartedAt);
                planRegistration.Stop1StoppedAt = string.IsNullOrEmpty(model.Stop1StoppedAt)
                    ? null
                    : DateTime.Parse(model.Stop1StoppedAt);
                planRegistration.Pause1StartedAt = string.IsNullOrEmpty(model.Pause1StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause1StartedAt);
                planRegistration.Pause1StoppedAt = string.IsNullOrEmpty(model.Pause1StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause1StoppedAt);
                planRegistration.Start2StartedAt = string.IsNullOrEmpty(model.Start2StartedAt)
                    ? null
                    : DateTime.Parse(model.Start2StartedAt);
                planRegistration.Stop2StoppedAt = string.IsNullOrEmpty(model.Stop2StoppedAt)
                    ? null
                    : DateTime.Parse(model.Stop2StoppedAt);

                planRegistration.Pause10StartedAt = string.IsNullOrEmpty(model.Pause10StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause10StartedAt);
                planRegistration.Pause10StoppedAt = string.IsNullOrEmpty(model.Pause10StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause10StoppedAt);
                planRegistration.Pause11StartedAt = string.IsNullOrEmpty(model.Pause11StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause11StartedAt);
                planRegistration.Pause11StoppedAt = string.IsNullOrEmpty(model.Pause11StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause11StoppedAt);
                planRegistration.Pause12StartedAt = string.IsNullOrEmpty(model.Pause12StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause12StartedAt);
                planRegistration.Pause12StoppedAt = string.IsNullOrEmpty(model.Pause12StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause12StoppedAt);
                planRegistration.Pause13StartedAt = string.IsNullOrEmpty(model.Pause13StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause13StartedAt);
                planRegistration.Pause13StoppedAt = string.IsNullOrEmpty(model.Pause13StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause13StoppedAt);
                planRegistration.Pause14StartedAt = string.IsNullOrEmpty(model.Pause14StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause14StartedAt);
                planRegistration.Pause14StoppedAt = string.IsNullOrEmpty(model.Pause14StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause14StoppedAt);
                planRegistration.Pause15StartedAt = string.IsNullOrEmpty(model.Pause15StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause15StartedAt);
                planRegistration.Pause15StoppedAt = string.IsNullOrEmpty(model.Pause15StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause15StoppedAt);
                planRegistration.Pause16StartedAt = string.IsNullOrEmpty(model.Pause16StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause16StartedAt);
                planRegistration.Pause16StoppedAt = string.IsNullOrEmpty(model.Pause16StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause16StoppedAt);
                planRegistration.Pause17StartedAt = string.IsNullOrEmpty(model.Pause17StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause17StartedAt);
                planRegistration.Pause17StoppedAt = string.IsNullOrEmpty(model.Pause17StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause17StoppedAt);
                planRegistration.Pause18StartedAt = string.IsNullOrEmpty(model.Pause18StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause18StartedAt);
                planRegistration.Pause18StoppedAt = string.IsNullOrEmpty(model.Pause18StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause18StoppedAt);
                planRegistration.Pause19StartedAt = string.IsNullOrEmpty(model.Pause19StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause19StartedAt);
                planRegistration.Pause19StoppedAt = string.IsNullOrEmpty(model.Pause19StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause19StoppedAt);
                planRegistration.Pause100StartedAt = string.IsNullOrEmpty(model.Pause100StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause100StartedAt);
                planRegistration.Pause100StoppedAt = string.IsNullOrEmpty(model.Pause100StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause100StoppedAt);
                planRegistration.Pause101StartedAt = string.IsNullOrEmpty(model.Pause101StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause101StartedAt);
                planRegistration.Pause101StoppedAt = string.IsNullOrEmpty(model.Pause101StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause101StoppedAt);
                planRegistration.Pause102StartedAt = string.IsNullOrEmpty(model.Pause102StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause102StartedAt);
                planRegistration.Pause102StoppedAt = string.IsNullOrEmpty(model.Pause102StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause102StoppedAt);

                planRegistration.Pause2StartedAt = string.IsNullOrEmpty(model.Pause2StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause2StartedAt);
                planRegistration.Pause2StoppedAt = string.IsNullOrEmpty(model.Pause2StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause2StoppedAt);
                planRegistration.Pause20StartedAt = string.IsNullOrEmpty(model.Pause20StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause20StartedAt);
                planRegistration.Pause20StoppedAt = string.IsNullOrEmpty(model.Pause20StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause20StoppedAt);
                planRegistration.Pause21StartedAt = string.IsNullOrEmpty(model.Pause21StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause21StartedAt);
                planRegistration.Pause21StoppedAt = string.IsNullOrEmpty(model.Pause21StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause21StoppedAt);
                planRegistration.Pause22StartedAt = string.IsNullOrEmpty(model.Pause22StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause22StartedAt);
                planRegistration.Pause22StoppedAt = string.IsNullOrEmpty(model.Pause22StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause22StoppedAt);
                planRegistration.Pause23StartedAt = string.IsNullOrEmpty(model.Pause23StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause23StartedAt);
                planRegistration.Pause23StoppedAt = string.IsNullOrEmpty(model.Pause23StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause23StoppedAt);
                planRegistration.Pause24StartedAt = string.IsNullOrEmpty(model.Pause24StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause24StartedAt);
                planRegistration.Pause24StoppedAt = string.IsNullOrEmpty(model.Pause24StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause24StoppedAt);
                planRegistration.Pause25StartedAt = string.IsNullOrEmpty(model.Pause25StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause25StartedAt);
                planRegistration.Pause25StoppedAt = string.IsNullOrEmpty(model.Pause25StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause25StoppedAt);
                planRegistration.Pause26StartedAt = string.IsNullOrEmpty(model.Pause26StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause26StartedAt);
                planRegistration.Pause26StoppedAt = string.IsNullOrEmpty(model.Pause26StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause26StoppedAt);
                planRegistration.Pause27StartedAt = string.IsNullOrEmpty(model.Pause27StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause27StartedAt);
                planRegistration.Pause27StoppedAt = string.IsNullOrEmpty(model.Pause27StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause27StoppedAt);
                planRegistration.Pause28StartedAt = string.IsNullOrEmpty(model.Pause28StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause28StartedAt);
                planRegistration.Pause28StoppedAt = string.IsNullOrEmpty(model.Pause28StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause28StoppedAt);
                planRegistration.Pause29StartedAt = string.IsNullOrEmpty(model.Pause29StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause29StartedAt);
                planRegistration.Pause29StoppedAt = string.IsNullOrEmpty(model.Pause29StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause29StoppedAt);
                planRegistration.Pause200StartedAt = string.IsNullOrEmpty(model.Pause200StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause200StartedAt);
                planRegistration.Pause200StoppedAt = string.IsNullOrEmpty(model.Pause200StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause200StoppedAt);
                planRegistration.Pause201StartedAt = string.IsNullOrEmpty(model.Pause201StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause201StartedAt);
                planRegistration.Pause201StoppedAt = string.IsNullOrEmpty(model.Pause201StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause201StoppedAt);
                planRegistration.Pause202StartedAt = string.IsNullOrEmpty(model.Pause202StartedAt)
                    ? null
                    : DateTime.Parse(model.Pause202StartedAt);
                planRegistration.Pause202StoppedAt = string.IsNullOrEmpty(model.Pause202StoppedAt)
                    ? null
                    : DateTime.Parse(model.Pause202StoppedAt);

                planRegistration.Shift1PauseNumber = model.Shift1PauseNumber;
                planRegistration.Shift2PauseNumber = model.Shift2PauseNumber;

                var minutesMultiplier = 5;

                double nettoMinutes = planRegistration.Stop1Id - planRegistration.Start1Id;
                nettoMinutes -= planRegistration.Pause1Id > 0 ? planRegistration.Pause1Id - 1 : 0;
                nettoMinutes = nettoMinutes + planRegistration.Stop2Id - planRegistration.Start2Id;
                nettoMinutes -= planRegistration.Pause2Id > 0 ? planRegistration.Pause2Id - 1 : 0;

                nettoMinutes *= minutesMultiplier;

                double hours = nettoMinutes / 60;
                planRegistration.NettoHours = hours;
                planRegistration.Flex = hours - planRegistration.PlanHours;
                var preTimePlanning =
                    await _dbContext.PlanRegistrations.AsNoTracking()
                        .Where(x => x.Date < planRegistration.Date && x.SdkSitId == sdkSiteId)
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .OrderByDescending(x => x.Date).FirstOrDefaultAsync();
                if (preTimePlanning != null)
                {
                    planRegistration.SumFlexEnd = preTimePlanning.SumFlexEnd + planRegistration.Flex - planRegistration.PaiedOutFlex;
                    planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                }
                else
                {
                    planRegistration.SumFlexEnd = planRegistration.Flex - planRegistration.PaiedOutFlex;
                    planRegistration.SumFlexStart = 0;
                }

                await planRegistration.Update(_dbContext).ConfigureAwait(false);
            }

            return new OperationResult(true);
        }
    }
}