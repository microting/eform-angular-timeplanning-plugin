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
using Microting.eForm.Dto;
using Microting.eForm.Infrastructure.Models;
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
    using Microting.eForm.Infrastructure.Data.Entities;
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
                var core = await _core.GetCore();
                await using var sdkDbContext = core.DbContextHelper.GetDbContext();
                var maxDaysEditable = _options.Value.MaxDaysEditable;
                var language = await _userService.GetCurrentUserLanguage();
                CultureInfo ci = new CultureInfo(language.LanguageCode);
                List<(DateTime, string)> tupleValueList = new();
                var site = await sdkDbContext.Sites
                    .Where(x => x.MicrotingUid == model.SiteId)
                    .Select(x => new
                    {
                        x.Id,
                        x.Name,
                    })
                    .FirstAsync();

                var timePlanningRequest = _dbContext.PlanRegistrations
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
                        SumFlex = Math.Round(x.SumFlex,2),
                        PaidOutFlex = x.PaiedOutFlex,
                        Message = x.MessageId,
                        CommentWorker = x.WorkerComment.Replace("\r", "<br />"),
                        CommentOffice = x.CommentOffice.Replace("\r", "<br />"),
                        // CommentOfficeAll = x.CommentOfficeAll,
                        IsLocked = x.Date < DateTime.Now.AddDays(-(int)maxDaysEditable),
                        IsWeekend = x.Date.DayOfWeek == DayOfWeek.Saturday || x.Date.DayOfWeek == DayOfWeek.Sunday,
                    })
                    .ToListAsync();

                var totalDays = (int)(model.DateTo - model.DateFrom).TotalDays + 1;

                double sumFlex = 0;
                var lastPlanning = _dbContext.PlanRegistrations
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
                    SumFlex = lastPlanning?.SumFlex ?? 0,
                    PaidOutFlex = lastPlanning?.PaiedOutFlex ?? 0,
                    Message = lastPlanning?.MessageId,
                    CommentWorker = lastPlanning?.WorkerComment?.Replace("\r", "<br />"),
                    CommentOffice = lastPlanning?.CommentOffice?.Replace("\r", "<br />"),
                    IsLocked = true,
                    IsWeekend = lastPlanning != null
                        ? lastPlanning.Date.DayOfWeek == DayOfWeek.Saturday ||
                          lastPlanning.Date.DayOfWeek == DayOfWeek.Sunday
                        : model.DateFrom.AddDays(-1).DayOfWeek == DayOfWeek.Saturday ||
                          model.DateFrom.AddDays(-1).DayOfWeek == DayOfWeek.Sunday,
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
                                || model.DateFrom.AddDays(i).DayOfWeek == DayOfWeek.Sunday,
                                //WorkerId = model.WorkerId,
                            });
                        }
                    }
                    timePlannings.AddRange(timePlanningForAdd);
                }

                timePlannings = timePlannings.OrderBy(x => x.Date).ToList();

                int j = 0;
                foreach (TimePlanningWorkingHoursModel timePlanningWorkingHoursModel in timePlannings)
                {
                    if (j > 0)
                    {
                        timePlanningWorkingHoursModel.SumFlex = Math.Round(sumFlex + timePlanningWorkingHoursModel.FlexHours - timePlanningWorkingHoursModel.PaidOutFlex, 2);
                    }

                    j++;
                    sumFlex = timePlanningWorkingHoursModel.SumFlex;
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
                    .ToListAsync();
                foreach (var planning in model.Plannings)
                {
                    var planRegistration = planRegistrations.FirstOrDefault(x => x.Date == planning.Date);
                    if (planRegistration != null)
                    {
                        await UpdatePlanning(planRegistration, planning, model.SiteId);
                    }
                    else
                    {
                        await CreatePlanning(planning, model.SiteId, model.SiteId, planning.CommentWorker);
                    }
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
                    .Where(x => x.SdkSitId == site.MicrotingUid)
                    .OrderBy(x => x.Date).ToListAsync();

                double preSumFlex = allPlannings.Any() ? allPlannings.First().SumFlex : 0;

                foreach (PlanRegistration planRegistration in allPlannings)
                {
                    if (planRegistration.Date > lastDate)
                    {
                        planRegistration.SumFlex = preSumFlex + planRegistration.Flex;
                        preSumFlex = planRegistration.SumFlex;
                        await planRegistration.Update(_dbContext);
                    }

                }

                if (_options.Value.MaxHistoryDays != null)
                {
                    int maxHistoryDaysInd = (int)_options.Value.MaxHistoryDays;
                    var firstDate = model.Plannings.First(x => x.Date >= DateTime.Now.AddDays(-maxHistoryDaysInd)).Date;
                    var list = await _dbContext.PlanRegistrations.Where(x => x.Date >= firstDate && x.Date <= DateTime.UtcNow
                            && x.SdkSitId == site.MicrotingUid && x.StatusCaseId != 0)
                        .OrderBy(x => x.Date).ToListAsync();
                    foreach (PlanRegistration planRegistration in list)
                    {

                        Message _message =
                            await _dbContext.Messages.SingleOrDefaultAsync(x => x.Id == planRegistration.MessageId);
                        Console.WriteLine($"Updating planRegistration {planRegistration.Id} for date {planRegistration.Date}");
                        string theMessage;
                        switch (language.LanguageCode)
                        {
                            case "da":
                                theMessage = _message != null ? _message.DaName : "";
                                break;
                            case "de":
                                theMessage = _message != null ? _message.DeName : "";
                                break;
                            default:
                                theMessage = _message != null ? _message.EnName : "";
                                break;
                        }
                        planRegistration.StatusCaseId = await new DeploymentHelper().DeployResults(planRegistration,(int)maxHistoryDays, (int)eFormId, core, site, (int)folderId, theMessage);
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

        private async Task CreatePlanning(TimePlanningWorkingHoursModel model, int sdkSiteId, int microtingUid, string commentWorker)
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
                    SumFlex = model.SumFlex,
                    StatusCaseId = 0
                };

                await planRegistration.Create(_dbContext);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
            }
        }

        private async Task UpdatePlanning(PlanRegistration planRegistration,
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
                planRegistration.SumFlex = model.SumFlex;

                await planRegistration.Update(_dbContext);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
            }
        }

        public async Task<OperationDataResult<Stream>> GenerateExcelDashboard(TimePlanningWorkingHoursRequestModel model)
        {
            try
            {
                // get core
                var core = await _coreHelper.GetCore();
                var sdkContext = core.DbContextHelper.GetDbContext();
                Site site = await sdkContext.Sites.SingleOrDefaultAsync(x => x.MicrotingUid == model.SiteId);
                var language = await sdkContext.Languages.SingleAsync(x => x.Id == site.LanguageId);

                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(language.LanguageCode);
                CultureInfo ci = new CultureInfo(language.LanguageCode);

                Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "results"));

                var timeStamp = $"{DateTime.UtcNow:yyyyMMdd}_{DateTime.UtcNow:hhmmss}";

                var resultDocument = Path.Combine(Path.GetTempPath(), "results",
                    $"{timeStamp}_.xlsx");

                Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "results"));

                IXLWorkbook wb = new XLWorkbook();

                IXLWorksheet worksheet = wb.Worksheets.Add("Dashboard");

                int x = 0;
                int y = 0;

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
                worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.SumFlex);
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
                worksheet.Cell(x + 1, y + 1).Value = _localizationService.GetString(Translations.Comment_office_all);
                worksheet.Cell(x + 1, y + 1).Style.Font.Bold = true;
                y++;

                var content = await Index(model);

                PlanRegistration plr = new PlanRegistration();

                if (content.Success)
                {
                    var rows = content.Model;

                    bool firstDone = false;
                    foreach (TimePlanningWorkingHoursModel timePlanningWorkingHoursModel in rows)
                    {
                        if (firstDone)
                        {
                            Message theMessage =
                                await _dbContext.Messages.SingleOrDefaultAsync(x =>
                                    x.Id == timePlanningWorkingHoursModel.Message);
                            string messageText = theMessage != null ? theMessage.EnName : "";
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
                            worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.PlanText;
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
                            worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.SumFlex;
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.PaidOutFlex;
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value = messageText;
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.CommentWorker;
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.CommentOffice;
                            y++;
                            worksheet.Cell(x + 1, y + 1).Value = timePlanningWorkingHoursModel.CommentOfficeAll;
                        }
                        firstDone = true;
                    }
                }

                wb.SaveAs(resultDocument);

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


    }
}