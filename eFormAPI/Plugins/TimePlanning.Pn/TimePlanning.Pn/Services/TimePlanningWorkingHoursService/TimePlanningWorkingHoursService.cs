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

using Microting.eForm.Infrastructure.Models;

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
        private readonly ITimePlanningLocalizationService _localizationService;
        private readonly IEFormCoreService _core;

        public TimePlanningWorkingHoursService(
            ILogger<TimePlanningWorkingHoursService> logger,
            TimePlanningPnDbContext dbContext,
            IUserService userService,
            ITimePlanningLocalizationService localizationService,
            IEFormCoreService core,
            IPluginDbOptions<TimePlanningBaseSettings> options)
        {
            _logger = logger;
            _dbContext = dbContext;
            _userService = userService;
            _localizationService = localizationService;
            _core = core;
            _options = options;
        }

        public async Task<OperationDataResult<List<TimePlanningWorkingHoursModel>>> Index(TimePlanningWorkingHoursRequestModel model)
        {
            try
            {
                var core = await _core.GetCore();
                await using var sdkDbContext = core.DbContextHelper.GetDbContext();
                var maxDaysEditable = _options.Value.MaxDaysEditable;

                List<(DateTime, string)> tupleValueList = new();
                var site = await sdkDbContext.Sites
                    .Where(x => x.MicrotingUid == model.SiteId)
                    .Select(x => new
                    {
                        x.Id,
                        x.Name,
                    })
                    .FirstOrDefaultAsync();

                var eFormId = _options.Value.EformId;
                if (eFormId != null)
                {
                    var possibleCases = await sdkDbContext.Cases
                        .Where(x => x.SiteId == site.Id
                                    && x.CheckListId == eFormId)
                        .Select(x => x.Id)
                        .ToListAsync();

                    var dateTimeRange = new List<string>();
                    for (int i = 0; i <= (model.DateTo - model.DateFrom).TotalDays; i++)
                    {
                        dateTimeRange.Add(model.DateFrom.AddDays(i).ToString("yyyy-MM-dd"));
                    }

                    var requiredCaseIds = await sdkDbContext.FieldValues
                        .Include(x => x.Field)
                        .ThenInclude(x => x.FieldType)
                        .Where(x => x.CheckListId == eFormId
                                    && possibleCases.Contains(x.CaseId.Value))
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .Where(x => dateTimeRange.Contains(x.Value) && x.Field.FieldType.Type == Constants.FieldTypes.Date)
                        .OrderBy(x => x.CaseId)
                        .Select(x => x.CaseId)
                        .ToListAsync();

                    var fieldValuesSdk = await sdkDbContext.FieldValues
                        .Include(x => x.Field)
                        .ThenInclude(x => x.FieldType)
                        .Where(x => x.CheckListId == eFormId
                                    && requiredCaseIds.Contains(x.CaseId.Value))
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .Where(x => x.Field.FieldType.Type == Constants.FieldTypes.Comment
                                    || x.Field.FieldType.Type == Constants.FieldTypes.Date)
                        .OrderBy(x => x.CaseId).ThenByDescending(x => x.Field.FieldType.Type)
                        .Select(x => new
                        {
                            x.Value,
                            x.Field.FieldType.Type,
                        })
                        .ToListAsync();

                    for (var i = 0; i < fieldValuesSdk.Count; i += 2)
                    {
                        tupleValueList.Add(new(DateTime.Parse(fieldValuesSdk[i].Value), fieldValuesSdk[i + 1].Value));
                    }
                }

                var timePlanningRequest = _dbContext.PlanRegistrations
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
                        SumFlex = Math.Round(x.SumFlex,2),
                        PaidOutFlex = x.PaiedOutFlex,
                        Message = x.MessageId,
                        CommentWorker = "",
                        CommentOffice = x.CommentOffice,
                        CommentOfficeAll = x.CommentOfficeAll,
                        IsLocked = x.Date < DateTime.Now.AddDays(-(int)maxDaysEditable)
                    })
                    .ToListAsync();

                var date = (int)(model.DateTo - model.DateFrom).TotalDays + 1;

                if (timePlannings.Count < date)
                {
                    var timePlanningForAdd = new List<TimePlanningWorkingHoursModel>();
                    for (var i = 0; i < date; i++)
                    {
                        if (timePlannings.All(x => x.Date != model.DateFrom.AddDays(i)))
                        {
                            timePlanningForAdd.Add(new TimePlanningWorkingHoursModel
                            {
                                Date = model.DateFrom.AddDays(i),
                                WeekDay = (int)model.DateFrom.AddDays(i).DayOfWeek,
                                IsLocked = model.DateFrom.AddDays(i) < DateTime.Now.AddDays(-(int)maxDaysEditable)
                                //WorkerId = model.WorkerId,
                            });
                        }
                    }
                    timePlannings.AddRange(timePlanningForAdd);
                }

                if (tupleValueList.Any())
                {
                    foreach (var timePlanning in timePlannings)
                    {
                        var foundComment = tupleValueList
                            .Where(x => x.Item1 == timePlanning.Date)
                            .Select(x => x.Item2).FirstOrDefault();
                        timePlanning.CommentWorker = foundComment;
                    }
                }

                timePlannings = timePlannings.OrderBy(x => x.Date).ToList();

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
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.SdkSitId == model.SiteId)
                    .ToListAsync();
                foreach (var planning in model.Plannings)
                {
                    var planningFomrDb = planRegistrations.FirstOrDefault(x => x.Date == planning.Date);
                    if (planningFomrDb != null)
                    {
                        await UpdatePlanning(planningFomrDb, planning, model.SiteId);
                    }
                    else
                    {
                        await CreatePlanning(planning, model.SiteId, model.SiteId, planning.CommentWorker);
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
                    MessageId = model.Message,
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
                };

                await planRegistration.Create(_dbContext);

                var core = await _core.GetCore();
                await using var sdkDbContext = core.DbContextHelper.GetDbContext();

                var folderId = _options.Value.FolderId == 0 ? null : _options.Value.FolderId;
                var eFormId = _options.Value.InfoeFormId;
                if (eFormId != null)
                {
                    var siteInfo = await sdkDbContext.Sites
                        .Where(x => x.MicrotingUid == microtingUid)
                        .Select(x => new
                        {
                            x.Id,
                            x.MicrotingUid,
                            x.LanguageId,
                        })
                        .FirstOrDefaultAsync();

                    var language = await sdkDbContext.Languages.SingleAsync(x => x.Id == siteInfo.LanguageId);
                    if (planRegistration.Pause1Id != 0 || planRegistration.Pause2Id != 0
                                                       || planRegistration.Start1Id != 0 || planRegistration.Start2Id != 0
                                                       || planRegistration.Stop1Id != 0 || planRegistration.Stop2Id != 0)
                    {
                        MainElement mainElement = new MainElement
                        {
                            Id = 141699,
                            Repeated = 0,
                            Label = "eform-angular-installationchecking-plugin-installation",
                            StartDate = new DateTime(2019, 11, 4),
                            EndDate = new DateTime(2029, 11, 4),
                            Language = "da",
                            MultiApproval = false,
                            FastNavigation = false,
                            DisplayOrder = 0,
                        };

                        var dataItems = new List<DataItem>();

                        dataItems.Add(new None(
                                371267,
                                false,
                                false,
                                "INFO",
                                "",
                                Constants.FieldColors.Yellow,
                                0,
                                false
                            )
                        );
                        var dataElement = new DataElement(
                            141704,
                            "CompanyName",
                            0,
                            "CompanyAddress<br>CompanyAddress2<br>ZipCode<br>CityName<br>Country",
                            false,
                            false,
                            false,
                            false,
                            "",
                            false,
                            new List<DataItemGroup>(),
                            dataItems);

                        mainElement.ElementList.Add(dataElement);
                        planRegistration.StatusCaseId =
                            (int)await core.CaseCreate(mainElement, "", (int)siteInfo.MicrotingUid, (int)folderId);

                    }
/*                    var fieldIds = await sdkDbContext.Fields
                        .Where(x => x.CheckListId == eFormId)
                        .Select(x => x.Id)
                        .ToListAsync();*/
                    // var mainElement = await core.ReadeForm(eFormId.Value - 1, language);
                    // var newMicrotingUid = await core.CaseCreate(mainElement, "", microtingUid, folderId);
                }

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
                planRegistration.MessageId = model.Message;
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

                var core = await _core.GetCore();
                await using var sdkDbContext = core.DbContextHelper.GetDbContext();

                var folderId = _options.Value.FolderId == 0 ? null : _options.Value.FolderId;

                if (planRegistration.Pause1Id != 0 || planRegistration.Pause2Id != 0
                                                   || planRegistration.Start1Id != 0 || planRegistration.Start2Id != 0
                                                   || planRegistration.Stop1Id != 0 || planRegistration.Stop2Id != 0)
                {

                }
                // var eFormId = _options.Value.EformId == 0 ? null : _options.Value.EformId + 1;
                //
                // if (eFormId != null)
                // {
                //     var siteInfo = await sdkDbContext.Sites
                //         .Where(x => x.MicrotingUid == microtingUid)
                //         .Select(x => new
                //         {
                //             x.Id,
                //             x.LanguageId,
                //         })
                //         .FirstOrDefaultAsync();
                //
                //     var language = await sdkDbContext.Languages.SingleAsync(x => x.Id == siteInfo.LanguageId);
                //
                //     var possibleCases = await sdkDbContext.Cases
                //         .Where(x => x.SiteId == siteInfo.Id && x.CheckListId == eFormId - 1)
                //         .Select(x => x.Id)
                //         .ToListAsync();
                //
                //     var requiredCaseIds = await sdkDbContext.FieldValues
                //         .Include(x => x.Field)
                //         .ThenInclude(x => x.FieldType)
                //         .Where(x => x.CheckListId == eFormId
                //                     && possibleCases.Contains(x.CaseId.Value))
                //         .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                //         .Where(x => model.Date.ToString("yyyy-MM-dd") == x.Value && x.Field.FieldType.Type == Constants.FieldTypes.Date)
                //         .OrderBy(x => x.CaseId)
                //         .Select(x => x.CaseId)
                //         .ToListAsync();
                //
                //     var fieldValuesSdk = await sdkDbContext.FieldValues
                //         .Where(x => x.CheckListId == eFormId)
                //         .Include(x => x.Field)
                //         .ThenInclude(x => x.FieldType)
                //         .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                //         .Where(x => requiredCaseIds.Contains(x.CaseId.Value))
                //         .OrderBy(x => x.CaseId)
                //         .ThenBy(x => x.Id)
                //         .ToListAsync();
                //
                //     for (var i = 0; i < fieldValuesSdk.Count; i += 11)
                //     {
                //         if (fieldValuesSdk[i].CaseId != null)
                //         {
                //             var caseDto = await core.CaseLookupCaseId(fieldValuesSdk[i].CaseId.Value);
                //             if (caseDto.MicrotingUId != null)
                //             {
                //                 await core.CaseDelete(caseDto.MicrotingUId.Value);
                //             }
                //         }
                //     }
                //
                //     var mainElement = await core.ReadeForm(eFormId.Value - 1, language);
                //     await core.CaseCreate(mainElement, "", microtingUid, folderId);
                // }

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
            }
        }

    }
}