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

namespace TimePlanning.Pn.Services.TimePlanningSettingService
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.Models.Settings;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microting.eForm.Dto;
    using Microting.eForm.Infrastructure.Constants;
    using Microting.eFormApi.BasePn.Abstractions;
    using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
    using Microting.eFormApi.BasePn.Infrastructure.Models.API;
    using Microting.TimePlanningBase.Infrastructure.Data;
    using Microting.TimePlanningBase.Infrastructure.Data.Entities;
    using TimePlanningLocalizationService;

    public class TimeSettingService : ISettingService
    {
        private readonly ILogger<TimeSettingService> _logger;
        private readonly IPluginDbOptions<TimePlanningBaseSettings> _options;
        private readonly TimePlanningPnDbContext _dbContext;
        private readonly IUserService _userService;
        private readonly ITimePlanningLocalizationService _localizationService;
        private readonly IEFormCoreService _core;

        public TimeSettingService(
            IPluginDbOptions<TimePlanningBaseSettings> options,
            TimePlanningPnDbContext dbContext,
            ILogger<TimeSettingService> logger,
            IUserService userService,
            ITimePlanningLocalizationService localizationService,
            IEFormCoreService core)
        {
            _options = options;
            _dbContext = dbContext;
            _logger = logger;
            _userService = userService;
            _localizationService = localizationService;
            _core = core;
        }

        public async Task<OperationDataResult<TimePlanningSettingsModel>> GetSettings()
        {
            try
            {
                var timePlanningSettingsModel = new TimePlanningSettingsModel
                {
                    FolderId = _options.Value.FolderId == 0 ? null : _options.Value.FolderId,
                    EformId = _options.Value.EformId == 0 ? null : _options.Value.EformId,
                    InfoeFormId = _options.Value.InfoeFormId == 0 ? null : _options.Value.InfoeFormId,
                };

                var assignedSites = await _dbContext.AssignedSites
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Select(x => x.SiteId)
                    .ToListAsync();

                timePlanningSettingsModel.AssignedSites = assignedSites;
                return new OperationDataResult<TimePlanningSettingsModel>(true, timePlanningSettingsModel);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                return new OperationDataResult<TimePlanningSettingsModel>(
                    false,
                    _localizationService.GetString("ErrorWhileObtainingSettings"));
            }
        }

        public async Task<OperationResult> UpdateEform(int eformId)
        {
            try
            {
                await _options.UpdateDb(settings =>
                {
                    settings.EformId = eformId;
                }, _dbContext, _userService.UserId);
                return new OperationResult(true, _localizationService.GetString("EformUpdatedSuccessfuly"));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                return new OperationResult(
                    false,
                    _localizationService.GetString("ErrorWhileUpdateEform"));
            }
        }

        public async Task<OperationResult> AddSite(int siteId)
        {
            try
            {
                var assignmentSite = new AssignedSite
                {
                    SiteId = siteId,
                    CreatedByUserId = _userService.UserId,
                    UpdatedByUserId = _userService.UserId,
                };
                await assignmentSite.Create(_dbContext);
                var option = _options.Value;
                var newTaskId = option.EformId;
                var folderId = option.FolderId;
                var theCore = await _core.GetCore();
                await using var sdkDbContext = theCore.DbContextHelper.GetDbContext();
                var folder = await sdkDbContext.Folders.SingleOrDefaultAsync(x => x.Id == folderId);
                // if (folder == null)
                // {
                //     return new OperationResult(false, _workOrdersLocalizationService.GetString("FolderNotExist"));
                // }
                var site = await sdkDbContext.Sites.SingleOrDefaultAsync(x => x.MicrotingUid == siteId);
                // if (site == null)
                // {
                //     return new OperationResult(false, _workOrdersLocalizationService.GetString("SiteNotFind"));
                // }
                var language = await sdkDbContext.Languages.SingleAsync(x => x.Id == site.LanguageId);
                var mainElement = await theCore.ReadeForm((int)newTaskId, language);
                mainElement.CheckListFolderName = folder.MicrotingUid.ToString();
                mainElement.EndDate = DateTime.UtcNow.AddYears(10);
                mainElement.DisplayOrder = int.MinValue;
                mainElement.Repeated = 0;
                mainElement.PushMessageTitle = mainElement.Label;
                mainElement.EnableQuickSync = true;
                var caseId = await theCore.CaseCreate(mainElement, "", siteId, folderId);
                assignmentSite.CaseMicrotingUid = caseId;
                await assignmentSite.Update(_dbContext);

                var minutesMultiplier = 5;

                var assignedSites = await _dbContext.AssignedSites
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .ToListAsync();
                foreach (var assignedSite in assignedSites)
                {
                    var timePlannings = await _dbContext.PlanRegistrations
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .Where(x => x.SdkSitId == assignedSite.SiteId)
                        .OrderBy(x => x.Date)
                        .ToListAsync();

                    foreach (var timePlanning in timePlannings)
                    {
                        double nettoMinutes = 0;

                        nettoMinutes = timePlanning.Stop1Id - timePlanning.Start1Id;
                        nettoMinutes = nettoMinutes - (timePlanning.Pause1Id > 0 ? timePlanning.Pause1Id - 1 : 0);
                        nettoMinutes = nettoMinutes + timePlanning.Stop2Id - timePlanning.Start2Id;
                        nettoMinutes = nettoMinutes - (timePlanning.Pause2Id > 0 ? timePlanning.Pause2Id - 1 : 0);

                        nettoMinutes = nettoMinutes * minutesMultiplier;

                        double hours = nettoMinutes / 60;
                        timePlanning.NettoHours = hours;
                        timePlanning.Flex = hours - timePlanning.PlanHours;

                        var preTimePlanning =
                            await _dbContext.PlanRegistrations.AsNoTracking()
                                .Where(x => x.Date < timePlanning.Date
                                && x.SdkSitId == assignedSite.SiteId)
                                .OrderByDescending(x => x.Date)
                                .FirstOrDefaultAsync();

                        //
                        if (preTimePlanning != null)
                        {
                            timePlanning.SumFlexEnd = preTimePlanning.SumFlexEnd + timePlanning.Flex - timePlanning.PaiedOutFlex;
                            timePlanning.SumFlexStart = preTimePlanning.SumFlexEnd;
                        }
                        else
                        {
                            timePlanning.SumFlexEnd = timePlanning.Flex - timePlanning.PaiedOutFlex;
                            timePlanning.SumFlexStart = 0;
                        }
                        await timePlanning.Update(_dbContext);
                    }
                }

                return new OperationResult(true, _localizationService.GetString("SitesUpdatedSuccessfuly"));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                return new OperationResult(
                    false,
                    _localizationService.GetString("ErrorWhileUpdateSites"));
            }
        }


        public async Task<OperationResult> UpdateFolder(int folderId)
        {
            try
            {
                await _options.UpdateDb(settings =>
                {
                    settings.FolderId = folderId;
                }, _dbContext, _userService.UserId);
                return new OperationResult(true, _localizationService.GetString("FolderUpdatedSuccessfuly"));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                return new OperationResult(
                    false,
                    _localizationService.GetString("ErrorWhileUpdateFolder"));
            }
        }

        public async Task<OperationResult> DeleteSite(int siteId)
        {
            try
            {
                var assignedSite = await _dbContext.AssignedSites
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.SiteId == siteId)
                    .FirstOrDefaultAsync();

                var theCore = await _core.GetCore();
                if (assignedSite != null)
                {
                    if (assignedSite.CaseMicrotingUid != null)
                        await theCore.CaseDelete((int)assignedSite.CaseMicrotingUid);
                    await assignedSite.Delete(_dbContext);
                }

                var registrations = await _dbContext.PlanRegistrations
                    .Where(x => x.StatusCaseId != 0)
                    .ToListAsync();

                foreach (PlanRegistration planRegistration in registrations)
                {
                    await theCore.CaseDelete(planRegistration.StatusCaseId);
                    planRegistration.StatusCaseId = 0;
                    await planRegistration.Update(_dbContext);
                }

                return new OperationResult(true, _localizationService.GetString("SitesUpdatedSuccessfuly"));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                return new OperationResult(
                    false,
                    _localizationService.GetString("ErrorWhileUpdateSites"));
            }
        }

        public async Task<OperationDataResult<List<SiteDto>>> GetAvailableites()
        {
            try
            {
                var core = await _core.GetCore();
                var assignedSites = await _dbContext.AssignedSites
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Select(x => x.SiteId)
                    .ToListAsync();

                var sites = new List<SiteDto>();
                foreach (var assignedSite in assignedSites)
                {
                    var site = await core.SiteRead(assignedSite);
                    if (site != null)
                    {
                        sites.Add(site);   
                    }
                }

                return new OperationDataResult<List<SiteDto>>(true, sites);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                return new OperationDataResult<List<SiteDto>>(
                    false,
                    _localizationService.GetString("ErrorWhileObtainingSites"));
            }
        }
    }
}