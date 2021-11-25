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

                List<(DateTime, string)> tupleValueList = new();
                var site = await sdkDbContext.Sites
                    .Where(x => x.MicrotingUid == model.SiteId)
                    .Select(x => new
                    {
                        x.Id,
                        x.Name,
                    })
                    .FirstOrDefaultAsync();

                var eFormId = _options.Value.EformId == 0 ? null : _options.Value.EformId + 1; // fix correct eform id
                if (eFormId != null)
                {
                    var possibleCases = await sdkDbContext.Cases
                        .Where(x => x.SiteId == site.Id
                                    && x.CheckListId == eFormId - 1)
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
                        .OrderBy(x => x.CaseId)
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
                    .Include(x => x.AssignedSite)
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.AssignedSite.SiteId == model.SiteId);

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
                        NettoHours = x.NettoHours,
                        FlexHours = x.Flex,
                        SumFlex = x.SumFlex,
                        PaidOutFlex = x.PaiedOutFlex,
                        Message = x.MessageId,
                        CommentWorker = "",
                        CommentOffice = x.CommentOffice,
                        CommentOfficeAll = x.CommentOfficeAll,
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
                var assignedSiteId = await _dbContext.AssignedSites.Where(x => x.SiteId == model.SiteId)
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Select(x => x.Id)
                    .FirstAsync();
                var planRegistrations = await _dbContext.PlanRegistrations
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.AssignedSiteId == assignedSiteId)
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
                        await CreatePlanning(planning, assignedSiteId, model.SiteId, planning.CommentWorker);
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

        private async Task CreatePlanning(TimePlanningWorkingHoursModel model, int assignedSiteId, int microtingUid, string commentWorker)
        {
            try
            {
                var planRegistration = new PlanRegistration
                {
                    MessageId = model.Message,
                    PlanText = model.PlanText,
                    AssignedSiteId = assignedSiteId,
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
                var eFormId = _options.Value.EformId == 0 ? null : _options.Value.EformId + 1;
                if (eFormId != null)
                {
                    var siteInfo = await sdkDbContext.Sites
                        .Where(x => x.MicrotingUid == microtingUid)
                        .Select(x => new
                        {
                            x.Id,
                            x.LanguageId,
                        })
                        .FirstOrDefaultAsync();

                    var language = await sdkDbContext.Languages.SingleAsync(x => x.Id == siteInfo.LanguageId);
/*                    var fieldIds = await sdkDbContext.Fields
                        .Where(x => x.CheckListId == eFormId)
                        .Select(x => x.Id)
                        .ToListAsync();*/
                    var mainElement = await core.ReadeForm(eFormId.Value - 1, language);
                    var newMicrotingUid = await core.CaseCreate(mainElement, "", microtingUid, folderId);
/*                    var newCaseId = await sdkDbContext.Cases
                            .Where(x => x.MicrotingUid == newMicrotingUid)
                            .Select(x => x.Id)
                            .FirstOrDefaultAsync();
                    var siteWorkerId = await sdkDbContext.SiteWorkers
                        .Where(x => x.SiteId == siteInfo.Id)
                        .Select(x => x.WorkerId)
                        .FirstOrDefaultAsync();
                    var workerId = await sdkDbContext.Workers
                        .Where(x => x.Id == siteWorkerId)
                        .Select(x => x.Id)
                        .FirstOrDefaultAsync();

                    var newFieldValues = new List<FieldValue>
                        {
                            new FieldValue
                            {
                                CaseId = newCaseId,
                                CheckListId = eFormId,
                                FieldId = fieldIds[2],
                                Value = planRegistration.Date.ToString("yyyy-MM-dd"),
                                WorkerId = workerId,
                            },
                            new FieldValue
                            {
                                CaseId = newCaseId,
                                CheckListId = eFormId,
                                FieldId = fieldIds[3],
                                Value = planRegistration.Start1Id.ToString(),
                                WorkerId = workerId,
                            },
                            new FieldValue
                            {
                                CaseId = newCaseId,
                                CheckListId = eFormId,
                                FieldId = fieldIds[4],
                                Value = planRegistration.Pause1Id.ToString(),
                                WorkerId = workerId,
                            },
                            new FieldValue
                            {
                                CaseId = newCaseId,
                                CheckListId = eFormId,
                                FieldId = fieldIds[5],
                                Value = planRegistration.Stop1Id.ToString(),
                                WorkerId = workerId,
                            },
                            new FieldValue
                            {
                                CaseId = newCaseId,
                                CheckListId = eFormId,
                                FieldId = fieldIds[6],
                                Value = planRegistration.Start2Id.ToString(),
                                WorkerId = workerId,
                            },
                            new FieldValue
                            {
                                CaseId = newCaseId,
                                CheckListId = eFormId,
                                FieldId = fieldIds[7],
                                Value = planRegistration.Pause2Id.ToString(),
                                WorkerId = workerId,
                            },
                            new FieldValue
                            {
                                CaseId = newCaseId,
                                CheckListId = eFormId,
                                FieldId = fieldIds[8],
                                Value = planRegistration.Stop2Id.ToString(),
                                WorkerId = workerId,
                            },
                            new FieldValue
                            {
                                CaseId = newCaseId,
                                CheckListId = eFormId,
                                FieldId = fieldIds[9],
                                Value = commentWorker,
                                WorkerId = workerId,
                            },
                            new FieldValue
                            {
                                CaseId = newCaseId,
                                CheckListId = eFormId,
                                FieldId = fieldIds[10],
                                Value = null,
                                WorkerId = workerId,
                            },
                            new FieldValue
                            {
                                CaseId = newCaseId,
                                CheckListId = eFormId,
                                FieldId = fieldIds[0],
                                Value = null,
                                WorkerId = workerId,
                            },
                            new FieldValue
                            {
                                CaseId = newCaseId,
                                CheckListId = eFormId,
                                FieldId = fieldIds[1],
                                Value = null,
                                WorkerId = workerId,
                            },
                        };

                    foreach (var fieldValue in newFieldValues)
                    {
                        await fieldValue.Create(sdkDbContext);
                    }*/
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
                planRegistration.Pause2Id = model.Shift1Pause ?? 0;
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
                var eFormId = _options.Value.EformId == 0 ? null : _options.Value.EformId + 1;

                if (eFormId != null)
                {
                    var siteInfo = await sdkDbContext.Sites
                        .Where(x => x.MicrotingUid == microtingUid)
                        .Select(x => new
                        {
                            x.Id,
                            x.LanguageId,
                        })
                        .FirstOrDefaultAsync();

                    var language = await sdkDbContext.Languages.SingleAsync(x => x.Id == siteInfo.LanguageId);

                    var possibleCases = await sdkDbContext.Cases
                        .Where(x => x.SiteId == siteInfo.Id && x.CheckListId == eFormId - 1)
                        .Select(x => x.Id)
                        .ToListAsync();

                    var requiredCaseIds = await sdkDbContext.FieldValues
                        .Include(x => x.Field)
                        .ThenInclude(x => x.FieldType)
                        .Where(x => x.CheckListId == eFormId
                                    && possibleCases.Contains(x.CaseId.Value))
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .Where(x => model.Date.ToString("yyyy-MM-dd") == x.Value && x.Field.FieldType.Type == Constants.FieldTypes.Date)
                        .OrderBy(x => x.CaseId)
                        .Select(x => x.CaseId)
                        .ToListAsync();

                    var fieldValuesSdk = await sdkDbContext.FieldValues
                        .Where(x => x.CheckListId == eFormId)
                        .Include(x => x.Field)
                        .ThenInclude(x => x.FieldType)
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .Where(x => requiredCaseIds.Contains(x.CaseId.Value))
                        .OrderBy(x => x.CaseId)
                        .ThenBy(x => x.Id)
                        .ToListAsync();
                    
                    for (var i = 0; i < fieldValuesSdk.Count; i += 11)
                    {
                        if (fieldValuesSdk[i].CaseId != null)
                        {
                            var caseDto = await core.CaseLookupCaseId(fieldValuesSdk[i].CaseId.Value);
                            if (caseDto.MicrotingUId != null)
                            {
                                await core.CaseDelete(caseDto.MicrotingUId.Value);
                            }
                        }
/*
                        var newCaseId = await sdkDbContext.Cases
                            .Where(x => x.MicrotingUid == newMicrotingUid)
                            .Select(x => x.Id)
                            .FirstOrDefaultAsync();

                        var newFieldValues = new List<FieldValue>
                        {
                            new FieldValue
                            {
                                CaseId = newCaseId,
                                CheckListId = fieldValuesSdk[i].CheckListId,
                                FieldId = fieldValuesSdk[i].FieldId,
                                Value = planRegistration.Date.ToString("yyyy-MM-dd"),
                                WorkerId = fieldValuesSdk[i].WorkerId,
                            },
                            new FieldValue
                            {
                                CaseId = newCaseId,
                                CheckListId = fieldValuesSdk[i+1].CheckListId,
                                FieldId = fieldValuesSdk[i+1].FieldId,
                                Value = planRegistration.Start1Id.ToString(),
                                WorkerId = fieldValuesSdk[i+1].WorkerId,
                            },
                            new FieldValue
                            {
                                CaseId = newCaseId,
                                CheckListId = fieldValuesSdk[i+2].CheckListId,
                                FieldId = fieldValuesSdk[i+2].FieldId,
                                Value = planRegistration.Pause1Id.ToString(),
                                WorkerId = fieldValuesSdk[i+2].WorkerId,
                            },
                            new FieldValue
                            {
                                CaseId = newCaseId,
                                CheckListId = fieldValuesSdk[i+3].CheckListId,
                                FieldId = fieldValuesSdk[i+3].FieldId,
                                Value = planRegistration.Stop1Id.ToString(),
                                WorkerId = fieldValuesSdk[i+3].WorkerId,
                            },
                            new FieldValue
                            {
                                CaseId = newCaseId,
                                CheckListId = fieldValuesSdk[i+4].CheckListId,
                                FieldId = fieldValuesSdk[i+4].FieldId,
                                Value = planRegistration.Start2Id.ToString(),
                                WorkerId = fieldValuesSdk[i+4].WorkerId,
                            },
                            new FieldValue
                            {
                                CaseId = newCaseId,
                                CheckListId = fieldValuesSdk[i+5].CheckListId,
                                FieldId = fieldValuesSdk[i+5].FieldId,
                                Value = planRegistration.Pause2Id.ToString(),
                                WorkerId = fieldValuesSdk[i+5].WorkerId,
                            },
                            new FieldValue
                            {
                                CaseId = newCaseId,
                                CheckListId = fieldValuesSdk[i+6].CheckListId,
                                FieldId = fieldValuesSdk[i+6].FieldId,
                                Value = planRegistration.Stop2Id.ToString(),
                                WorkerId = fieldValuesSdk[i+6].WorkerId,
                            },
                            new FieldValue
                            {
                                CaseId = newCaseId,
                                CheckListId = fieldValuesSdk[i+7].CheckListId,
                                FieldId = fieldValuesSdk[i+7].FieldId,
                                Value = fieldValuesSdk[i+7].Value,
                                WorkerId = fieldValuesSdk[i+7].WorkerId,
                            },
                            new FieldValue
                            {
                                CaseId = newCaseId,
                                CheckListId = fieldValuesSdk[i+8].CheckListId,
                                FieldId = fieldValuesSdk[i+8].FieldId,
                                Value = fieldValuesSdk[i+8].Value,
                                WorkerId = fieldValuesSdk[i+8].WorkerId,
                            },
                            new FieldValue
                            {
                                CaseId = newCaseId,
                                CheckListId = fieldValuesSdk[i+9].CheckListId,
                                FieldId = fieldValuesSdk[i+9].FieldId,
                                Value = fieldValuesSdk[i+9].Value,
                                WorkerId = fieldValuesSdk[i+9].WorkerId,
                            },
                            new FieldValue
                            {
                                CaseId = newCaseId,
                                CheckListId = fieldValuesSdk[i+10].CheckListId,
                                FieldId = fieldValuesSdk[i+10].FieldId,
                                Value = fieldValuesSdk[i+10].Value,
                                WorkerId = fieldValuesSdk[i+10].WorkerId,
                            },
                        };

                        foreach (var fieldValue in newFieldValues)
                        {
                            await fieldValue.Create(sdkDbContext);
                        }*/
                    }

                    var mainElement = await core.ReadeForm(eFormId.Value - 1, language);
                    await core.CaseCreate(mainElement, "", microtingUid, folderId);
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
            }
        }

    }
}