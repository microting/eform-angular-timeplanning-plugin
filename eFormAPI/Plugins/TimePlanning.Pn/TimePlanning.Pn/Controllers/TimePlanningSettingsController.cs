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

using System.Collections.Generic;
using Microting.TimePlanningBase.Infrastructure.Const;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;

namespace TimePlanning.Pn.Controllers
{
    using System.Threading.Tasks;
    using Castle.Core;
    using Infrastructure.Models.Settings;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
    using Microting.eFormApi.BasePn.Infrastructure.Models.API;
    using Services.TimePlanningSettingService;

    [Route("api/time-planning-pn/settings")]
    public class TimePlanningSettingsController : Controller
    {
        private readonly ISettingService _settingService;

        public TimePlanningSettingsController(ISettingService settingService)
        {
            _settingService = settingService;
        }

        [HttpGet]
        [Authorize(Roles = EformRole.Admin)]
        public async Task<OperationDataResult<TimePlanningSettingsModel>> GetSettings()
        {
            return await _settingService.GetSettings();
        }

        [HttpPut]
        [Authorize(Roles = EformRole.Admin)]
        public async Task<OperationResult> UpdateSettings([FromBody] TimePlanningSettingsModel timePlanningSettingsModel)
        {
            return await _settingService.UpdateSettings(timePlanningSettingsModel);
        }

        [HttpPut]
        [Route("folder")]
        [Authorize(Roles = EformRole.Admin)]
        public async Task<OperationResult> UpdateFolder([FromBody] int folderId)
        {
            return await _settingService.UpdateFolder(folderId);
        }


        [HttpPut]
        [Route("sites")]
        [Authorize(Roles = EformRole.Admin)]
        public async Task<OperationResult> AddSite([FromBody] int siteId)
        {
            return await _settingService.AddSite(siteId);
        }


/*        [HttpPut]
        [Route("eform")]
        [Authorize(Roles = EformRole.Admin)]
        public async Task<OperationResult> UpdateEform([FromBody] int eformId)
        {
            return await _settingService.UpdateEform(eformId);
        }*/

        [HttpDelete]
        [Route("sites")]
        [Authorize(Roles = EformRole.User)]
        public async Task<OperationResult> DeleteSite(int siteId)
        {
            return await _settingService.DeleteSite(siteId);
        }

        [HttpGet]
        [Route("sites")]
        [Authorize(Policy = TimePlanningClaims.GetWorkingHours)]
        public async Task<OperationDataResult<List<Site>>> GetAvailableSites()
        {
            return await _settingService.GetAvailableSites(null);
        }

        [HttpGet]
        [Route("registration-sites")]
        public async Task<OperationDataResult<List<Site>>> RegistrationSites(string token)
        {
            return await _settingService.GetAvailableSites(token);
        }

        [HttpGet]
        [Route("assigned-sites")]
        [Authorize(Roles = EformRole.Admin)]
        public async Task<OperationDataResult<AssignedSite>> GetAssignedSite(int siteId)
        {
            return await _settingService.GetAssignedSite(siteId);
        }

        [HttpPut]
        [Route("assigned-sites")]
        [Authorize(Roles = EformRole.Admin)]
        public async Task<OperationResult> UpdateAssignedSite([FromBody] AssignedSite site)
        {
            return await _settingService.UpdateAssignedSite(site);
        }
    }
}