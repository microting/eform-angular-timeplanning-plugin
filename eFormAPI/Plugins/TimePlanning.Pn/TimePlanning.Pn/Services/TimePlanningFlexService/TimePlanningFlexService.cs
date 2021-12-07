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
        private readonly TimePlanningPnDbContext _dbContext;
        private readonly IUserService _userService;
        private readonly ITimePlanningLocalizationService _localizationService;
        private readonly IEFormCoreService _core;

        public TimePlanningFlexService(
            ILogger<TimePlanningFlexService> logger,
            TimePlanningPnDbContext dbContext,
            IUserService userService,
            ITimePlanningLocalizationService localizationService,
            IEFormCoreService core)
        {
            _logger = logger;
            _dbContext = dbContext;
            _userService = userService;
            _localizationService = localizationService;
            _core = core;
        }

        public async Task<OperationDataResult<List<TimePlanningFlexIndexModel>>> Index()
        {
            try
            {
                var core = await _core.GetCore();

                var listSiteIds = await _dbContext.PlanRegistrations
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Select(x => x.SdkSitId).Distinct().ToListAsync();

                List<PlanRegistration> planRegistrations = new List<PlanRegistration>();

                foreach (var listSiteId in listSiteIds)
                {
                    var r = await _dbContext.PlanRegistrations
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .Where(x => x.Date < DateTime.UtcNow.AddDays(-1).Date)
                        .Where(x => x.SdkSitId == listSiteId)
                        .OrderByDescending(x => x.Date).FirstOrDefaultAsync();

                    planRegistrations.Add(r);
                }

                // var planRegistrations = await _dbContext.PlanRegistrations
                //     .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                //     .Where(x => x.Date < DateTime.UtcNow.AddDays(-1).Date)
                //     .OrderByDescending(x => x.Date)
                //     .Select(x => new
                //     {
                //         x.Date,
                //         x.SdkSitId,
                //         x.SumFlex,
                //         PaidOutFlex = x.PaiedOutFlex,
                //         CommentWorker = "",
                //         x.CommentOffice,
                //         x.CommentOfficeAll,
                //     })
                //     .ToListAsync();

                var resultWorkers = new List<TimePlanningFlexIndexModel>();

                foreach (var planRegistration in planRegistrations)
                {
                    var siteDto = await core.SiteRead(planRegistration.SdkSitId);

                    resultWorkers.Add(new TimePlanningFlexIndexModel
                    {
                        Date = planRegistration.Date,
                        Worker = new CommonDictionaryModel
                        {
                            Id = planRegistration.SdkSitId,
                            Name = siteDto.SiteName,
                        },
                        SumFlex = planRegistration.SumFlex,
                        PaidOutFlex = planRegistration.PaiedOutFlex,
                        CommentWorker = planRegistration.WorkerComment ?? "",
                        CommentOffice = planRegistration.CommentOffice ?? "",
                        CommentOfficeAll = planRegistration.CommentOfficeAll ?? "",
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

        public async Task<OperationResult> UpdateCreate(TimePlanningFlexUpdateModel model)
        {
            try
            {
                var planRegistration = await _dbContext.PlanRegistrations
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.Date == model.Date)
                    .Where(x => x.SdkSitId == model.Site.Id)
                    .FirstOrDefaultAsync();

                if (planRegistration != null)
                {
                    return await UpdatePlanning(planRegistration, model);
                }
                return await CreatePlanning(model, (int)model.Site.Id);
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

        private async Task<OperationResult> CreatePlanning(TimePlanningFlexUpdateModel model, int sdkSiteId)
        {
            try
            {
                var planning = new PlanRegistration
                {
                    CommentOffice = model.CommentOffice,
                    CommentOfficeAll = model.CommentOfficeAll,
                    SdkSitId = sdkSiteId,
                    Date = model.Date,
                    SumFlex = model.SumFlex,
                    PaiedOutFlex = model.PaidOutFlex,
                    CreatedByUserId = _userService.UserId,
                    UpdatedByUserId = _userService.UserId,
                };

                await planning.Create(_dbContext);

                return new OperationResult(
                    true,
                    _localizationService.GetString("SuccessfullyCreatePlanning"));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                return new OperationResult(
                    false,
                    _localizationService.GetString("ErrorWhileCreatePlanning"));
            }
        }

        private async Task<OperationResult> UpdatePlanning(PlanRegistration planRegistration, TimePlanningFlexUpdateModel model)
        {
            try
            {
                planRegistration.CommentOfficeAll = model.CommentOfficeAll;
                planRegistration.CommentOffice = model.CommentOffice;
                planRegistration.PaiedOutFlex = model.PaidOutFlex;
                planRegistration.SumFlex = model.SumFlex;
                planRegistration.UpdatedByUserId = _userService.UserId;

                await planRegistration.Update(_dbContext);

                return new OperationResult(
                    true,
                    _localizationService.GetString("SuccessfullyUpdatePlanning"));
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
    }
}