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

namespace TimePlanning.Pn.Services.TimePlanningSettingService;

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
                MondayBreakMinutesDivider = int.Parse(_options.Value.MondayBreakMinutesDivider),
                MondayBreakMinutesPrDivider = int.Parse(_options.Value.MondayBreakMinutesPrDivider),
                TuesdayBreakMinutesDivider = int.Parse(_options.Value.TuesdayBreakMinutesDivider),
                TuesdayBreakMinutesPrDivider = int.Parse(_options.Value.TuesdayBreakMinutesPrDivider),
                WednesdayBreakMinutesDivider = int.Parse(_options.Value.WednesdayBreakMinutesDivider),
                WednesdayBreakMinutesPrDivider = int.Parse(_options.Value.WednesdayBreakMinutesPrDivider),
                ThursdayBreakMinutesDivider = int.Parse(_options.Value.ThursdayBreakMinutesDivider),
                ThursdayBreakMinutesPrDivider = int.Parse(_options.Value.ThursdayBreakMinutesPrDivider),
                FridayBreakMinutesDivider = int.Parse(_options.Value.FridayBreakMinutesDivider),
                FridayBreakMinutesPrDivider = int.Parse(_options.Value.FridayBreakMinutesPrDivider),
                SaturdayBreakMinutesDivider = int.Parse(_options.Value.SaturdayBreakMinutesDivider),
                SaturdayBreakMinutesPrDivider = int.Parse(_options.Value.SaturdayBreakMinutesPrDivider),
                SundayBreakMinutesDivider = int.Parse(_options.Value.SundayBreakMinutesDivider),
                SundayBreakMinutesPrDivider = int.Parse(_options.Value.SundayBreakMinutesPrDivider),
                AutoBreakCalculationActive = _options.Value.AutoBreakCalculationActive == "1",
                MondayBreakMinutesUpperLimit = int.Parse(_options.Value.MondayBreakMinutesUpperLimit),
                TuesdayBreakMinutesUpperLimit = int.Parse(_options.Value.TuesdayBreakMinutesUpperLimit),
                WednesdayBreakMinutesUpperLimit = int.Parse(_options.Value.WednesdayBreakMinutesUpperLimit),
                ThursdayBreakMinutesUpperLimit = int.Parse(_options.Value.ThursdayBreakMinutesUpperLimit),
                FridayBreakMinutesUpperLimit = int.Parse(_options.Value.FridayBreakMinutesUpperLimit),
                SaturdayBreakMinutesUpperLimit = int.Parse(_options.Value.SaturdayBreakMinutesUpperLimit),
                SundayBreakMinutesUpperLimit = int.Parse(_options.Value.SundayBreakMinutesUpperLimit)
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
                settings.MondayBreakMinutesDivider = timePlanningSettingsModel.MondayBreakMinutesDivider.ToString();
                settings.MondayBreakMinutesPrDivider = timePlanningSettingsModel.MondayBreakMinutesPrDivider.ToString();
                settings.TuesdayBreakMinutesDivider = timePlanningSettingsModel.TuesdayBreakMinutesDivider.ToString();
                settings.TuesdayBreakMinutesPrDivider =
                    timePlanningSettingsModel.TuesdayBreakMinutesPrDivider.ToString();
                settings.WednesdayBreakMinutesDivider =
                    timePlanningSettingsModel.WednesdayBreakMinutesDivider.ToString();
                settings.WednesdayBreakMinutesPrDivider =
                    timePlanningSettingsModel.WednesdayBreakMinutesPrDivider.ToString();
                settings.ThursdayBreakMinutesDivider = timePlanningSettingsModel.ThursdayBreakMinutesDivider.ToString();
                settings.ThursdayBreakMinutesPrDivider =
                    timePlanningSettingsModel.ThursdayBreakMinutesPrDivider.ToString();
                settings.FridayBreakMinutesDivider = timePlanningSettingsModel.FridayBreakMinutesDivider.ToString();
                settings.FridayBreakMinutesPrDivider = timePlanningSettingsModel.FridayBreakMinutesPrDivider.ToString();
                settings.SaturdayBreakMinutesDivider = timePlanningSettingsModel.SaturdayBreakMinutesDivider.ToString();
                settings.SaturdayBreakMinutesPrDivider =
                    timePlanningSettingsModel.SaturdayBreakMinutesPrDivider.ToString();
                settings.SundayBreakMinutesDivider = timePlanningSettingsModel.SundayBreakMinutesDivider.ToString();
                settings.SundayBreakMinutesPrDivider = timePlanningSettingsModel.SundayBreakMinutesPrDivider.ToString();
                settings.AutoBreakCalculationActive = timePlanningSettingsModel.AutoBreakCalculationActive ? "1" : "0";
                settings.MondayBreakMinutesUpperLimit =
                    timePlanningSettingsModel.MondayBreakMinutesUpperLimit.ToString();
                settings.TuesdayBreakMinutesUpperLimit =
                    timePlanningSettingsModel.TuesdayBreakMinutesUpperLimit.ToString();
                settings.WednesdayBreakMinutesUpperLimit =
                    timePlanningSettingsModel.WednesdayBreakMinutesUpperLimit.ToString();
                settings.ThursdayBreakMinutesUpperLimit =
                    timePlanningSettingsModel.ThursdayBreakMinutesUpperLimit.ToString();
                settings.FridayBreakMinutesUpperLimit =
                    timePlanningSettingsModel.FridayBreakMinutesUpperLimit.ToString();
                settings.SaturdayBreakMinutesUpperLimit =
                    timePlanningSettingsModel.SaturdayBreakMinutesUpperLimit.ToString();
                settings.SundayBreakMinutesUpperLimit =
                    timePlanningSettingsModel.SundayBreakMinutesUpperLimit.ToString();
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
            await _options.UpdateDb(settings => { settings.EformId = eformId; }, _dbContext, _userService.UserId);
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
            var assignmentSite = new Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite
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
            await _options.UpdateDb(settings => { settings.FolderId = folderId; }, _dbContext, _userService.UserId);
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
                var site = await sdkDbContext.Sites.SingleOrDefaultAsync(x =>
                    x.MicrotingUid == assignedSite && x.WorkflowState != Constants.WorkflowStates.Removed);
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

    public async Task<OperationDataResult<Infrastructure.Models.Settings.AssignedSite>> GetAssignedSite(int siteId)
    {
        Infrastructure.Models.Settings.AssignedSite dbAssignedSite = await _dbContext.AssignedSites
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync(x => x.SiteId == siteId);
        if (dbAssignedSite == null)
        {
            return new OperationDataResult<Infrastructure.Models.Settings.AssignedSite>(false, "Site not found");
        }

        var core = await _core.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();
        var site = await sdkDbContext.Sites.FirstOrDefaultAsync(x => x.MicrotingUid == siteId);

        if (site == null)
        {
            return new OperationDataResult<Infrastructure.Models.Settings.AssignedSite>(false, "Site not found");
        }

        var autoBreakCalculationActive = _options.Value.AutoBreakCalculationActive == "1";
        dbAssignedSite.AutoBreakCalculationActive = autoBreakCalculationActive;
        dbAssignedSite.SiteName = site.Name;

        return new OperationDataResult<Infrastructure.Models.Settings.AssignedSite>(true, dbAssignedSite);

    }

    public async Task<OperationResult> UpdateAssignedSite(Infrastructure.Models.Settings.AssignedSite site)
    {
        var siteId = site.SiteId;
        var dbAssignedSite = await _dbContext.AssignedSites
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync(x => x.SiteId == siteId);
        if (dbAssignedSite == null)
        {
            return new OperationDataResult<Infrastructure.Models.Settings.AssignedSite>(false, "Site not found");
        }

        dbAssignedSite.UseGoogleSheetAsDefault = site.UseGoogleSheetAsDefault;
        dbAssignedSite.UseOnlyPlanHours = site.UseOnlyPlanHours;
        dbAssignedSite.AllowEditOfRegistrations = site.AllowEditOfRegistrations;
        dbAssignedSite.AllowPersonalTimeRegistration = site.AllowPersonalTimeRegistration;
        dbAssignedSite.AllowAcceptOfPlannedHours = site.AllowAcceptOfPlannedHours;
        dbAssignedSite.Resigned = site.Resigned;
        dbAssignedSite.UseOneMinuteIntervals = site.UseOneMinuteIntervals;


        dbAssignedSite.StartMonday = site.StartMonday;
        dbAssignedSite.StartTuesday = site.StartTuesday;
        dbAssignedSite.StartWednesday = site.StartWednesday;
        dbAssignedSite.StartThursday = site.StartThursday;
        dbAssignedSite.StartFriday = site.StartFriday;
        dbAssignedSite.StartSaturday = site.StartSaturday;
        dbAssignedSite.StartSunday = site.StartSunday;
        dbAssignedSite.EndMonday = site.EndMonday;
        dbAssignedSite.EndTuesday = site.EndTuesday;
        dbAssignedSite.EndWednesday = site.EndWednesday;
        dbAssignedSite.EndThursday = site.EndThursday;
        dbAssignedSite.EndFriday = site.EndFriday;
        dbAssignedSite.EndSaturday = site.EndSaturday;
        dbAssignedSite.EndSunday = site.EndSunday;
        dbAssignedSite.BreakMonday = site.BreakMonday;
        dbAssignedSite.BreakTuesday = site.BreakTuesday;
        dbAssignedSite.BreakWednesday = site.BreakWednesday;
        dbAssignedSite.BreakThursday = site.BreakThursday;
        dbAssignedSite.BreakFriday = site.BreakFriday;
        dbAssignedSite.BreakSaturday = site.BreakSaturday;
        dbAssignedSite.BreakSunday = site.BreakSunday;
        dbAssignedSite.StartMonday2NdShift = site.StartMonday2NdShift;
        dbAssignedSite.StartTuesday2NdShift = site.StartTuesday2NdShift;
        dbAssignedSite.StartWednesday2NdShift = site.StartWednesday2NdShift;
        dbAssignedSite.StartThursday2NdShift = site.StartThursday2NdShift;
        dbAssignedSite.StartFriday2NdShift = site.StartFriday2NdShift;
        dbAssignedSite.StartSaturday2NdShift = site.StartSaturday2NdShift;
        dbAssignedSite.StartSunday2NdShift = site.StartSunday2NdShift;
        dbAssignedSite.EndMonday2NdShift = site.EndMonday2NdShift;
        dbAssignedSite.EndTuesday2NdShift = site.EndTuesday2NdShift;
        dbAssignedSite.EndWednesday2NdShift = site.EndWednesday2NdShift;
        dbAssignedSite.EndThursday2NdShift = site.EndThursday2NdShift;
        dbAssignedSite.EndFriday2NdShift = site.EndFriday2NdShift;
        dbAssignedSite.EndSaturday2NdShift = site.EndSaturday2NdShift;
        dbAssignedSite.EndSunday2NdShift = site.EndSunday2NdShift;
        dbAssignedSite.BreakMonday2NdShift = site.BreakMonday2NdShift;
        dbAssignedSite.BreakTuesday2NdShift = site.BreakTuesday2NdShift;
        dbAssignedSite.BreakWednesday2NdShift = site.BreakWednesday2NdShift;
        dbAssignedSite.BreakThursday2NdShift = site.BreakThursday2NdShift;
        dbAssignedSite.BreakFriday2NdShift = site.BreakFriday2NdShift;
        dbAssignedSite.BreakSaturday2NdShift = site.BreakSaturday2NdShift;
        dbAssignedSite.BreakSunday2NdShift = site.BreakSunday2NdShift;
        dbAssignedSite.MondayBreakMinutesDivider = site.MondayBreakMinutesDivider;
        dbAssignedSite.TuesdayBreakMinutesDivider = site.TuesdayBreakMinutesDivider;
        dbAssignedSite.WednesdayBreakMinutesDivider = site.WednesdayBreakMinutesDivider;
        dbAssignedSite.ThursdayBreakMinutesDivider = site.ThursdayBreakMinutesDivider;
        dbAssignedSite.FridayBreakMinutesDivider = site.FridayBreakMinutesDivider;
        dbAssignedSite.SaturdayBreakMinutesDivider = site.SaturdayBreakMinutesDivider;
        dbAssignedSite.SundayBreakMinutesDivider = site.SundayBreakMinutesDivider;
        dbAssignedSite.MondayBreakMinutesPrDivider = site.MondayBreakMinutesPrDivider;
        dbAssignedSite.TuesdayBreakMinutesPrDivider = site.TuesdayBreakMinutesPrDivider;
        dbAssignedSite.WednesdayBreakMinutesPrDivider = site.WednesdayBreakMinutesPrDivider;
        dbAssignedSite.ThursdayBreakMinutesPrDivider = site.ThursdayBreakMinutesPrDivider;
        dbAssignedSite.FridayBreakMinutesPrDivider = site.FridayBreakMinutesPrDivider;
        dbAssignedSite.SaturdayBreakMinutesPrDivider = site.SaturdayBreakMinutesPrDivider;
        dbAssignedSite.SundayBreakMinutesPrDivider = site.SundayBreakMinutesPrDivider;
        dbAssignedSite.MondayBreakMinutesUpperLimit = site.MondayBreakMinutesUpperLimit;
        dbAssignedSite.TuesdayBreakMinutesUpperLimit = site.TuesdayBreakMinutesUpperLimit;
        dbAssignedSite.WednesdayBreakMinutesUpperLimit = site.WednesdayBreakMinutesUpperLimit;
        dbAssignedSite.ThursdayBreakMinutesUpperLimit = site.ThursdayBreakMinutesUpperLimit;
        dbAssignedSite.FridayBreakMinutesUpperLimit = site.FridayBreakMinutesUpperLimit;
        dbAssignedSite.SaturdayBreakMinutesUpperLimit = site.SaturdayBreakMinutesUpperLimit;
        dbAssignedSite.SundayBreakMinutesUpperLimit = site.SundayBreakMinutesUpperLimit;
        dbAssignedSite.MondayPlanHours = site.MondayPlanHours;
        dbAssignedSite.TuesdayPlanHours = site.TuesdayPlanHours;
        dbAssignedSite.WednesdayPlanHours = site.WednesdayPlanHours;
        dbAssignedSite.ThursdayPlanHours = site.ThursdayPlanHours;
        dbAssignedSite.FridayPlanHours = site.FridayPlanHours;
        dbAssignedSite.SaturdayPlanHours = site.SaturdayPlanHours;
        dbAssignedSite.SundayPlanHours = site.SundayPlanHours;

        await dbAssignedSite.Update(_dbContext);

        if (!dbAssignedSite.UseGoogleSheetAsDefault)
        {
            var midnight = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, 0, 0, 0);
            var planRegistrationsFromTodayAndForward = await _dbContext.PlanRegistrations
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.SdkSitId == siteId)
                .Where(x => x.Date >= midnight)
                .ToListAsync();

            if (planRegistrationsFromTodayAndForward.Count == 0)
            {
                // create new plannings for 30 days as of today and forward
                for (int i = 0; i < 30; i++)
                {
                    var newPlanRegistration = new PlanRegistration
                    {
                        Date = midnight.AddDays(i),
                        SdkSitId = siteId,
                        CreatedByUserId = _userService.UserId,
                        UpdatedByUserId = _userService.UserId
                    };

                    await newPlanRegistration.Create(_dbContext);
                }
            } else
            {
                if (planRegistrationsFromTodayAndForward.Count < 30)
                {
                    // we need to fill all the gaps from today and forward with a new planning
                    var datesInPeriod = planRegistrationsFromTodayAndForward.Select(x => x.Date).ToList();
                    var missingDates = new List<DateTime>();
                    for (int i = 0; i < 30; i++)
                    {
                        var date = midnight.AddDays(i);
                        if (!datesInPeriod.Contains(date))
                        {
                            missingDates.Add(date);
                        }
                    }

                    foreach (var missingDate in missingDates)
                    {
                        var newPlanRegistration = new PlanRegistration
                        {
                            Date = missingDate,
                            SdkSitId = siteId,
                            CreatedByUserId = _userService.UserId,
                            UpdatedByUserId = _userService.UserId
                        };

                        await newPlanRegistration.Create(_dbContext);
                    }
                }
            }

            planRegistrationsFromTodayAndForward = await _dbContext.PlanRegistrations
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.SdkSitId == siteId)
                .Where(x => x.Date >= midnight)
                .ToListAsync();

            foreach (var planRegistration in planRegistrationsFromTodayAndForward)
            {
                var dayOfWeek = planRegistration.Date.DayOfWeek;
                switch (dayOfWeek)
                {
                    case DayOfWeek.Monday:
                        planRegistration.PlanHours = dbAssignedSite.MondayPlanHours != 0 ? (double)dbAssignedSite.MondayPlanHours / 60 : 0;
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
                        planRegistration.PlanHours = dbAssignedSite.TuesdayPlanHours != 0 ? (double)dbAssignedSite.TuesdayPlanHours / 60 : 0;
                        if (!dbAssignedSite.UseOnlyPlanHours)
                        {
                            planRegistration.PlannedStartOfShift1 = dbAssignedSite.StartTuesday ?? 0;
                            planRegistration.PlannedEndOfShift1 = dbAssignedSite.EndTuesday ?? 0;
                            planRegistration.PlannedBreakOfShift1 = dbAssignedSite.BreakTuesday ?? 0;
                            planRegistration.PlannedStartOfShift2 = dbAssignedSite.StartTuesday2NdShift ?? 0;
                            planRegistration.PlannedEndOfShift2 = dbAssignedSite.EndTuesday2NdShift ?? 0;
                            planRegistration.PlannedBreakOfShift2 = dbAssignedSite.BreakTuesday2NdShift ?? 0;
                            planRegistration.PlannedStartOfShift3 = dbAssignedSite.StartTuesday3RdShift ?? 0;
                            planRegistration.PlannedEndOfShift3 = dbAssignedSite.EndTuesday3RdShift ?? 0;
                            planRegistration.PlannedBreakOfShift3 = dbAssignedSite.BreakTuesday3RdShift ?? 0;
                            planRegistration.PlannedStartOfShift4 = dbAssignedSite.StartTuesday4ThShift ?? 0;
                            planRegistration.PlannedEndOfShift4 = dbAssignedSite.EndTuesday4ThShift ?? 0;
                            planRegistration.PlannedBreakOfShift4 = dbAssignedSite.BreakTuesday4ThShift ?? 0;
                            planRegistration.PlannedStartOfShift5 = dbAssignedSite.StartTuesday5ThShift ?? 0;
                            planRegistration.PlannedEndOfShift5 = dbAssignedSite.EndTuesday5ThShift ?? 0;
                            planRegistration.PlannedBreakOfShift5 = dbAssignedSite.BreakTuesday5ThShift ?? 0;
                        }

                        break;
                    case DayOfWeek.Wednesday:
                        planRegistration.PlanHours = dbAssignedSite.WednesdayPlanHours != 0 ? (double)dbAssignedSite.WednesdayPlanHours / 60 : 0;
                        if (!dbAssignedSite.UseOnlyPlanHours)
                        {
                            planRegistration.PlannedStartOfShift1 = dbAssignedSite.StartWednesday ?? 0;
                            planRegistration.PlannedEndOfShift1 = dbAssignedSite.EndWednesday ?? 0;
                            planRegistration.PlannedBreakOfShift1 = dbAssignedSite.BreakWednesday ?? 0;
                            planRegistration.PlannedStartOfShift2 = dbAssignedSite.StartWednesday2NdShift ?? 0;
                            planRegistration.PlannedEndOfShift2 = dbAssignedSite.EndWednesday2NdShift ?? 0;
                            planRegistration.PlannedBreakOfShift2 = dbAssignedSite.BreakWednesday2NdShift ?? 0;
                            planRegistration.PlannedStartOfShift3 = dbAssignedSite.StartWednesday3RdShift ?? 0;
                            planRegistration.PlannedEndOfShift3 = dbAssignedSite.EndWednesday3RdShift ?? 0;
                            planRegistration.PlannedBreakOfShift3 = dbAssignedSite.BreakWednesday3RdShift ?? 0;
                            planRegistration.PlannedStartOfShift4 = dbAssignedSite.StartWednesday4ThShift ?? 0;
                            planRegistration.PlannedEndOfShift4 = dbAssignedSite.EndWednesday4ThShift ?? 0;
                            planRegistration.PlannedBreakOfShift4 = dbAssignedSite.BreakWednesday4ThShift ?? 0;
                            planRegistration.PlannedStartOfShift5 = dbAssignedSite.StartWednesday5ThShift ?? 0;
                            planRegistration.PlannedEndOfShift5 = dbAssignedSite.EndWednesday5ThShift ?? 0;
                            planRegistration.PlannedBreakOfShift5 = dbAssignedSite.BreakWednesday5ThShift ?? 0;
                        }

                        break;
                    case DayOfWeek.Thursday:
                        planRegistration.PlanHours = dbAssignedSite.ThursdayPlanHours != 0 ? (double)dbAssignedSite.ThursdayPlanHours / 60 : 0;
                        if (!dbAssignedSite.UseOnlyPlanHours)
                        {
                            planRegistration.PlannedStartOfShift1 = dbAssignedSite.StartThursday ?? 0;
                            planRegistration.PlannedEndOfShift1 = dbAssignedSite.EndThursday ?? 0;
                            planRegistration.PlannedBreakOfShift1 = dbAssignedSite.BreakThursday ?? 0;
                            planRegistration.PlannedStartOfShift2 = dbAssignedSite.StartThursday2NdShift ?? 0;
                            planRegistration.PlannedEndOfShift2 = dbAssignedSite.EndThursday2NdShift ?? 0;
                            planRegistration.PlannedBreakOfShift2 = dbAssignedSite.BreakThursday2NdShift ?? 0;
                            planRegistration.PlannedStartOfShift3 = dbAssignedSite.StartThursday3RdShift ?? 0;
                            planRegistration.PlannedEndOfShift3 = dbAssignedSite.EndThursday3RdShift ?? 0;
                            planRegistration.PlannedBreakOfShift3 = dbAssignedSite.BreakThursday3RdShift ?? 0;
                            planRegistration.PlannedStartOfShift4 = dbAssignedSite.StartThursday4ThShift ?? 0;
                            planRegistration.PlannedEndOfShift4 = dbAssignedSite.EndThursday4ThShift ?? 0;
                            planRegistration.PlannedBreakOfShift4 = dbAssignedSite.BreakThursday4ThShift ?? 0;
                            planRegistration.PlannedStartOfShift5 = dbAssignedSite.StartThursday5ThShift ?? 0;
                            planRegistration.PlannedEndOfShift5 = dbAssignedSite.EndThursday5ThShift ?? 0;
                            planRegistration.PlannedBreakOfShift5 = dbAssignedSite.BreakThursday5ThShift ?? 0;
                        }

                        break;
                    case DayOfWeek.Friday:
                        planRegistration.PlanHours = dbAssignedSite.FridayPlanHours != 0 ? (double)dbAssignedSite.FridayPlanHours / 60 : 0;
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
                        planRegistration.PlanHours = dbAssignedSite.SaturdayPlanHours != 0 ? (double)dbAssignedSite.SaturdayPlanHours / 60 : 0;
                        if (!dbAssignedSite.UseOnlyPlanHours)
                        {
                            planRegistration.PlannedStartOfShift1 = dbAssignedSite.StartSaturday ?? 0;
                            planRegistration.PlannedEndOfShift1 = dbAssignedSite.EndSaturday ?? 0;
                            planRegistration.PlannedBreakOfShift1 = dbAssignedSite.BreakSaturday ?? 0;
                            planRegistration.PlannedStartOfShift2 = dbAssignedSite.StartSaturday2NdShift ?? 0;
                            planRegistration.PlannedEndOfShift2 = dbAssignedSite.EndSaturday2NdShift ?? 0;
                            planRegistration.PlannedBreakOfShift2 = dbAssignedSite.BreakSaturday2NdShift ?? 0;
                            planRegistration.PlannedStartOfShift3 = dbAssignedSite.StartSaturday3RdShift ?? 0;
                            planRegistration.PlannedEndOfShift3 = dbAssignedSite.EndSaturday3RdShift ?? 0;
                            planRegistration.PlannedBreakOfShift3 = dbAssignedSite.BreakSaturday3RdShift ?? 0;
                            planRegistration.PlannedStartOfShift4 = dbAssignedSite.StartSaturday4ThShift ?? 0;
                            planRegistration.PlannedEndOfShift4 = dbAssignedSite.EndSaturday4ThShift ?? 0;
                            planRegistration.PlannedBreakOfShift4 = dbAssignedSite.BreakSaturday4ThShift ?? 0;
                            planRegistration.PlannedStartOfShift5 = dbAssignedSite.StartSaturday5ThShift ?? 0;
                            planRegistration.PlannedEndOfShift5 = dbAssignedSite.EndSaturday5ThShift ?? 0;
                            planRegistration.PlannedBreakOfShift5 = dbAssignedSite.BreakSaturday5ThShift ?? 0;
                        }

                        break;
                    case DayOfWeek.Sunday:
                        planRegistration.PlanHours = dbAssignedSite.SundayPlanHours != 0 ? (double)dbAssignedSite.SundayPlanHours / 60 : 0;
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

                await planRegistration.Update(_dbContext);
            }

        }

        return new OperationResult(true, _localizationService.GetString("AssignedSiteUpdatedSuccessfuly"));
    }
}