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

using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using TimePlanning.Pn.Helpers;
using TimePlanning.Pn.Infrastructure.Models.Settings;

namespace TimePlanning.Pn.Services.TimePlanningFlexService
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.Models.Flex.Index;
    using Infrastructure.Models.Flex.Update;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microting.eForm.Infrastructure.Constants;
    using Microting.eFormApi.BasePn.Abstractions;
    using Microting.eFormApi.BasePn.Infrastructure.Models.API;
    using Microting.eFormApi.BasePn.Infrastructure.Models.Common;
    using Microting.TimePlanningBase.Infrastructure.Data;
    using Microting.TimePlanningBase.Infrastructure.Data.Entities;
    using TimePlanningLocalizationService;

    /// <summary>
    /// TimePlanningFlexService
    /// </summary>
    public class TimePlanningFlexService : ITimePlanningFlexService
    {
        private readonly ILogger<TimePlanningFlexService> _logger;
        private readonly IPluginDbOptions<TimePlanningBaseSettings> _options;
        private readonly TimePlanningPnDbContext _dbContext;
        private readonly IUserService _userService;
        private readonly ITimePlanningLocalizationService _localizationService;
        private readonly IEFormCoreService _core;

        public TimePlanningFlexService(
            ILogger<TimePlanningFlexService> logger,
            TimePlanningPnDbContext dbContext,
            IUserService userService,
            ITimePlanningLocalizationService localizationService,
            IEFormCoreService core, IPluginDbOptions<TimePlanningBaseSettings> options)
        {
            _logger = logger;
            _dbContext = dbContext;
            _userService = userService;
            _localizationService = localizationService;
            _core = core;
            _options = options;
        }

        public async Task<OperationDataResult<List<TimePlanningFlexIndexModel>>> Index()
        {
            try
            {
                var core = await _core.GetCore();
                await using var sdkDbContext = core.DbContextHelper.GetDbContext();

                var listSiteIds = await _dbContext.PlanRegistrations
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Select(x => x.SdkSitId).Distinct().ToListAsync();

                List<PlanRegistration> planRegistrations = new List<PlanRegistration>();

                foreach (var listSiteId in listSiteIds)
                {
                    var r = await _dbContext.PlanRegistrations
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .Where(x => x.Date < DateTime.Now.Date)
                        .Where(x => x.SdkSitId == listSiteId)
                        .OrderByDescending(x => x.Date).FirstOrDefaultAsync();

                    if (r != null)
                    {
                        if (r.Date == DateTime.Now.AddDays(-1).Date)
                        {
                            planRegistrations.Add(r);
                        }
                        else
                        {
                            PlanRegistration planRegistration = new PlanRegistration
                            {
                                Date = DateTime.Now.AddDays(-1).Date,
                                SdkSitId = r.SdkSitId,
                                SumFlexEnd = Math.Round(r.SumFlexEnd, 2),
                                PaiedOutFlex = r.PaiedOutFlex,
                                CommentOffice = r.CommentOffice
                            };
                            planRegistrations.Add(planRegistration);
                        }
                    }
                }

                var resultWorkers = new List<TimePlanningFlexIndexModel>();

                foreach (var planRegistration in planRegistrations)
                {

                    var site = await sdkDbContext.Sites.SingleOrDefaultAsync(x => x.MicrotingUid == planRegistration.SdkSitId && x.WorkflowState != Constants.WorkflowStates.Removed);
                    if (site == null)
                    {
                        continue;
                    }

                    resultWorkers.Add(new TimePlanningFlexIndexModel
                    {
                        SdkSiteId = planRegistration.SdkSitId,
                        Date = planRegistration.Date,
                        Worker = new CommonDictionaryModel
                        {
                            Id = planRegistration.SdkSitId,
                            Name = site.Name,
                        },
                        SumFlex = Math.Round(planRegistration.SumFlexEnd, 2),
                        PaidOutFlex = planRegistration.PaiedOutFlex,
                        CommentOffice = planRegistration.CommentOffice?.Replace("\r", "<br />") ?? ""
                    });
                }

                return new OperationDataResult<List<TimePlanningFlexIndexModel>>(
                    true,
                    resultWorkers);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                return new OperationDataResult<List<TimePlanningFlexIndexModel>>(
                    false,
                    _localizationService.GetString("ErrorWhileObtainingPlannings"));
            }
        }

        public async Task<OperationResult> UpdateCreate(List<TimePlanningFlexUpdateModel> model)
        {
            try
            {
                foreach (var updateModel in model)
                {
                    var planRegistration = await _dbContext.PlanRegistrations
                        .Where(x => x.Date == updateModel.Date)
                        .Where(x => x.SdkSitId == updateModel.Worker.Id)
                        .FirstOrDefaultAsync();

                    if (planRegistration != null)
                    {
                        await UpdatePlanning(planRegistration, updateModel);
                    } else {
                        await CreatePlanning(updateModel, (int)updateModel.Worker.Id);
                    }
                }

                var listSiteIds = await _dbContext.PlanRegistrations
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Select(x => x.SdkSitId).Distinct().ToListAsync();

                var maxHistoryDays = _options.Value.MaxHistoryDays == 0 ? null : _options.Value.MaxHistoryDays;
                var eFormId = _options.Value.InfoeFormId;
                var folderId = _options.Value.FolderId == 0 ? null : _options.Value.FolderId;
                var core = await _core.GetCore();
                await using var sdkDbContext = core.DbContextHelper.GetDbContext();
                foreach (int listSiteId in listSiteIds)
                {
                    var plannings = await _dbContext.PlanRegistrations
                        .Where(x => x.StatusCaseId != 0)
                        .Where(x => x.Date > DateTime.Now.AddDays(-2))
                        .Where(x => x.SdkSitId == listSiteId)
                        .ToListAsync();

                    foreach (PlanRegistration planRegistration in plannings)
                    {
                        var site = await sdkDbContext.Sites.SingleOrDefaultAsync(x => x.MicrotingUid == planRegistration.SdkSitId);
                        var language = await sdkDbContext.Languages.SingleAsync(x => x.Id == site.LanguageId);
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
                return new OperationResult(
                    false,
                    _localizationService.GetString("ErrorWhileUpdatePlanning"));
            }
        }

        private async Task CreatePlanning(TimePlanningFlexUpdateModel model, int sdkSiteId)
        {
            var planning = new PlanRegistration
            {
                CommentOffice = model.CommentOffice,
                CommentOfficeAll = model.CommentOfficeAll,
                SdkSitId = sdkSiteId,
                Date = model.Date,
                SumFlexEnd = model.SumFlexStart - model.PaidOutFlex,
                PaiedOutFlex = model.PaidOutFlex,
                CreatedByUserId = _userService.UserId,
                UpdatedByUserId = _userService.UserId,
            };

            await planning.Create(_dbContext);
        }

        private async Task UpdatePlanning(PlanRegistration planRegistration,
            TimePlanningFlexUpdateModel model)
        {
            planRegistration.CommentOfficeAll = model.CommentOfficeAll;
            planRegistration.CommentOffice = model.CommentOffice;
            planRegistration.SumFlexEnd += planRegistration.PaiedOutFlex - model.PaidOutFlex;
            planRegistration.PaiedOutFlex = model.PaidOutFlex;
            planRegistration.UpdatedByUserId = _userService.UserId;

            await planRegistration.Update(_dbContext);
        }
    }
}