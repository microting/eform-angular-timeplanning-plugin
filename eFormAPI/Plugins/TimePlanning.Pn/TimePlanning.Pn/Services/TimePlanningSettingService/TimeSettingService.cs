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
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.Models.Settings;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
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

        public TimeSettingService(
            IPluginDbOptions<TimePlanningBaseSettings> options,
            TimePlanningPnDbContext dbContext,
            ILogger<TimeSettingService> logger,
            IUserService userService,
            ITimePlanningLocalizationService localizationService)
        {
            _options = options;
            _dbContext = dbContext;
            _logger = logger;
            _userService = userService;
            _localizationService = localizationService;
        }

        public async Task<OperationDataResult<TimePlanningSettingsModel>> GetSettings()
        {
            try
            {
                var timePlanningSettingsModel = new TimePlanningSettingsModel
                {
                    FolderId = _options.Value.FolderId == 0 ? null : _options.Value.FolderId,
                    EformId = _options.Value.EformId == 0 ? null : _options.Value.EformId,
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

                await assignedSite.Delete(_dbContext);
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
    }
}
