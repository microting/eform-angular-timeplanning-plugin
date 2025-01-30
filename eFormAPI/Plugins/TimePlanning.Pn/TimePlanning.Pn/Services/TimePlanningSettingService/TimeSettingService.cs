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
using JetBrains.Annotations;
using Sentry;
using TimePlanning.Pn.Infrastructure.Helpers;

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
                    //GoogleApiKey = _options.Value.GoogleApiKey,
                    GoogleSheetId = _options.Value.GoogleSheetId,
                    MondayBreakMinutesDivider = _options.Value.MondayBreakMinutesDivider,
                    MondayBreakMinutesPrDivider = _options.Value.MondayBreakMinutesPrDivider,
                    TuesdayBreakMinutesDivider = _options.Value.TuesdayBreakMinutesDivider,
                    TuesdayBreakMinutesPrDivider = _options.Value.TuesdayBreakMinutesPrDivider,
                    WednesdayBreakMinutesDivider = _options.Value.WednesdayBreakMinutesDivider,
                    WednesdayBreakMinutesPrDivider = _options.Value.WednesdayBreakMinutesPrDivider,
                    ThursdayBreakMinutesDivider = _options.Value.ThursdayBreakMinutesDivider,
                    ThursdayBreakMinutesPrDivider = _options.Value.ThursdayBreakMinutesPrDivider,
                    FridayBreakMinutesDivider = _options.Value.FridayBreakMinutesDivider,
                    FridayBreakMinutesPrDivider = _options.Value.FridayBreakMinutesPrDivider,
                    SaturdayBreakMinutesDivider = _options.Value.SaturdayBreakMinutesDivider,
                    SaturdayBreakMinutesPrDivider = _options.Value.SaturdayBreakMinutesPrDivider,
                    SundayBreakMinutesDivider = _options.Value.SundayBreakMinutesDivider,
                    SundayBreakMinutesPrDivider = _options.Value.SundayBreakMinutesPrDivider,
                    AutoBreakCalculationActive = _options.Value.AutoBreakCalculationActive == "1"
                };

                //timePlanningSettingsModel.AssignedSites = assignedSites;
                return new OperationDataResult<TimePlanningSettingsModel>(true, timePlanningSettingsModel);
            }
            catch (Exception e)
            {
                SentrySdk.CaptureException(e);
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                return new OperationDataResult<TimePlanningSettingsModel>(
                    false,
                    _localizationService.GetString("ErrorWhileObtainingSettings"));
            }
        }

        public async Task<OperationResult> UpdateSettings(TimePlanningSettingsModel timePlanningSettingsModel)
        {
            try
            {
                // check if the google sheets id is the entire url with gid or anything else than the id, if so, extract the id
                if (timePlanningSettingsModel.GoogleSheetId.Contains("https://docs.google.com/spreadsheets/d/"))
                {
                    var split = timePlanningSettingsModel.GoogleSheetId.Split("/");
                    timePlanningSettingsModel.GoogleSheetId = split[5];
                }

                await _options.UpdateDb(settings =>
                {
                    //settings.GoogleApiKey = timePlanningSettingsModel.GoogleApiKey;
                    settings.GoogleSheetId = timePlanningSettingsModel.GoogleSheetId;
                    settings.MondayBreakMinutesDivider = timePlanningSettingsModel.MondayBreakMinutesDivider;
                    settings.MondayBreakMinutesPrDivider = timePlanningSettingsModel.MondayBreakMinutesPrDivider;
                    settings.TuesdayBreakMinutesDivider = timePlanningSettingsModel.TuesdayBreakMinutesDivider;
                    settings.TuesdayBreakMinutesPrDivider = timePlanningSettingsModel.TuesdayBreakMinutesPrDivider;
                    settings.WednesdayBreakMinutesDivider = timePlanningSettingsModel.WednesdayBreakMinutesDivider;
                    settings.WednesdayBreakMinutesPrDivider = timePlanningSettingsModel.WednesdayBreakMinutesPrDivider;
                    settings.ThursdayBreakMinutesDivider = timePlanningSettingsModel.ThursdayBreakMinutesDivider;
                    settings.ThursdayBreakMinutesPrDivider = timePlanningSettingsModel.ThursdayBreakMinutesPrDivider;
                    settings.FridayBreakMinutesDivider = timePlanningSettingsModel.FridayBreakMinutesDivider;
                    settings.FridayBreakMinutesPrDivider = timePlanningSettingsModel.FridayBreakMinutesPrDivider;
                    settings.SaturdayBreakMinutesDivider = timePlanningSettingsModel.SaturdayBreakMinutesDivider;
                    settings.SaturdayBreakMinutesPrDivider = timePlanningSettingsModel.SaturdayBreakMinutesPrDivider;
                    settings.SundayBreakMinutesDivider = timePlanningSettingsModel.SundayBreakMinutesDivider;
                    settings.SundayBreakMinutesPrDivider = timePlanningSettingsModel.SundayBreakMinutesPrDivider;
                    settings.AutoBreakCalculationActive = timePlanningSettingsModel.AutoBreakCalculationActive ? "1" : "0";
                }, _dbContext, _userService.UserId);
                await GoogleSheetHelper.PushToGoogleSheet(await _core.GetCore(), _dbContext, _logger);

                if (timePlanningSettingsModel.ForceLoadAllPlanningsFromGoogleSheet)
                {
                    await GoogleSheetHelper.PullEverythingFromGoogleSheet(await _core.GetCore(), _dbContext, _logger);
                }

                return new OperationResult(true, _localizationService.GetString("SettingsUpdatedSuccessfuly"));
            }
            catch (Exception e)
            {
                SentrySdk.CaptureException(e);
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                return new OperationResult(
                    false,
                    _localizationService.GetString("ErrorWhileUpdateSettings"));
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
                SentrySdk.CaptureException(e);
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
                    UpdatedByUserId = _userService.UserId
                };
                await assignmentSite.Create(_dbContext);
                var option = _options.Value;
                var newTaskId = option.EformId;
                var folderId = option.FolderId;
                var theCore = await _core.GetCore();
                await using var sdkDbContext = theCore.DbContextHelper.GetDbContext();
                var folder = await sdkDbContext.Folders.SingleOrDefaultAsync(x => x.Id == folderId);
                var site = await sdkDbContext.Sites.SingleOrDefaultAsync(x => x.MicrotingUid == siteId);
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

                return new OperationResult(true, _localizationService.GetString("SitesUpdatedSuccessfuly"));
            }
            catch (Exception e)
            {
                SentrySdk.CaptureException(e);
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
                SentrySdk.CaptureException(e);
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
                SentrySdk.CaptureException(e);
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                return new OperationResult(
                    false,
                    _localizationService.GetString("ErrorWhileUpdateSites"));
            }
        }

        public async Task<OperationDataResult<List<Site>>> GetAvailableSites(string? token)
        {
            try
            {
                if (token != null)
                {
                    var registrationDevice = await _dbContext.RegistrationDevices
                        .Where(x => x.Token == token).FirstOrDefaultAsync();
                    if (registrationDevice == null)
                    {
                        return new OperationDataResult<List<Site>>(
                            false,
                            "Token not found");
                    }
                }

                var core = await _core.GetCore();
                var sdkDbContext = core.DbContextHelper.GetDbContext();
                var assignedSites = await _dbContext.AssignedSites
                    .AsNoTracking()
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Select(x => x.SiteId)
                    .ToListAsync();

                var sites = new List<Site>();
                foreach (var assignedSite in assignedSites)
                {
                    var site = await sdkDbContext.Sites.SingleOrDefaultAsync(x => x.MicrotingUid == assignedSite && x.WorkflowState != Constants.WorkflowStates.Removed);
                    if (site == null) continue;
                    {
                        var siteWorker = await sdkDbContext.SiteWorkers
                            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                            .Where(x => x.SiteId == site.Id)
                            .FirstAsync();
                        var worker = await sdkDbContext.Workers
                            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                            .Where(x => x.Id == siteWorker.WorkerId)
                            .FirstOrDefaultAsync();
                        var unit = await sdkDbContext.Units.FirstOrDefaultAsync(x => x.SiteId == site.Id);
                        var language = await sdkDbContext.Languages.SingleAsync(x => x.Id == site.LanguageId);
                        if (worker != null)
                        {

                            var today = DateTime.UtcNow.Date;
                            var midnight = new DateTime(today.Year, today.Month, today.Day, 0, 0, 0);
                            var planRegistrationForToday = await _dbContext.PlanRegistrations
                                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                                .Where(x => x.SdkSitId == site.MicrotingUid)
                                .Where(x => x.Date == midnight)
                                .FirstOrDefaultAsync();
                            var hoursStarted = false;
                            var pauseStarted = false;
                            if (planRegistrationForToday != null)
                            {
                                hoursStarted =
                                    planRegistrationForToday is { Start1StartedAt: not null, Stop1StoppedAt: null } or
                                        { Start2StartedAt: not null, Stop2StoppedAt: null };
                                pauseStarted =
                                    planRegistrationForToday is { Pause1StartedAt: not null, Pause1StoppedAt: null } or
                                        { Pause10StartedAt: not null, Pause10StoppedAt: null } or
                                        { Pause11StartedAt: not null, Pause11StoppedAt: null } or
                                        { Pause12StartedAt: not null, Pause12StoppedAt: null } or
                                        { Pause13StartedAt: not null, Pause13StoppedAt: null } or
                                        { Pause14StartedAt: not null, Pause14StoppedAt: null } or
                                        { Pause15StartedAt: not null, Pause15StoppedAt: null } or
                                        { Pause16StartedAt: not null, Pause16StoppedAt: null } or
                                        { Pause17StartedAt: not null, Pause17StoppedAt: null } or
                                        { Pause18StartedAt: not null, Pause18StoppedAt: null } or
                                        { Pause19StartedAt: not null, Pause19StoppedAt: null } or
                                        { Pause2StartedAt: not null, Pause2StoppedAt: null } or
                                        { Pause20StartedAt: not null, Pause20StoppedAt: null } or
                                        { Pause21StartedAt: not null, Pause21StoppedAt: null } or
                                        { Pause22StartedAt: not null, Pause22StoppedAt: null } or
                                        { Pause23StartedAt: not null, Pause23StoppedAt: null } or
                                        { Pause24StartedAt: not null, Pause24StoppedAt: null } or
                                        { Pause25StartedAt: not null, Pause25StoppedAt: null } or
                                        { Pause26StartedAt: not null, Pause26StoppedAt: null } or
                                        { Pause27StartedAt: not null, Pause27StoppedAt: null } or
                                        { Pause28StartedAt: not null, Pause28StoppedAt: null } or
                                        { Pause29StartedAt: not null, Pause29StoppedAt: null };
                            }
                            var newSite = new Site
                            {
                                SiteId = (int)site.MicrotingUid!,
                                SiteName = site.Name,
                                FirstName = worker.FirstName,
                                LastName = worker.LastName,
                                CustomerNo = unit!.CustomerNo,
                                OtpCode = unit.OtpCode,
                                UnitId = unit.MicrotingUid,
                                WorkerUid = worker.MicrotingUid,
                                Email = worker.Email,
                                PinCode = worker.PinCode,
                                DefaultLanguage = language.LanguageCode,
                                HoursStarted = hoursStarted,
                                PauseStarted = pauseStarted
                            };
                            sites.Add(newSite);
                        }
                    }
                }

                sites = sites.OrderBy(x => x.SiteName).ToList();

                return new OperationDataResult<List<Site>>(true, sites);
            }
            catch (Exception e)
            {
                SentrySdk.CaptureException(e);
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                return new OperationDataResult<List<Site>>(
                    false,
                    _localizationService.GetString("ErrorWhileObtainingSites"));
            }
        }
    }
}