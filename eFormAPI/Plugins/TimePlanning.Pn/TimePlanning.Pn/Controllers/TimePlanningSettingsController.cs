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

namespace TimePlanning.Pn.Controllers;

using System.Threading.Tasks;
using Infrastructure.Models.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Services.TimePlanningSettingService;

[Route("api/time-planning-pn/settings")]
public class TimePlanningSettingsController(ISettingService settingService) : Controller
{
    [HttpGet]
    [Authorize(Roles = EformRole.Admin)]
    public async Task<OperationDataResult<TimePlanningSettingsModel>> GetSettings()
    {
        return await settingService.GetSettings();
    }

    [HttpPut]
    [Authorize(Roles = EformRole.Admin)]
    public async Task<OperationResult> UpdateSettings([FromBody] TimePlanningSettingsModel timePlanningSettingsModel)
    {
        return await settingService.UpdateSettings(timePlanningSettingsModel);
    }

    [HttpPut]
    [Route("sites")]
    [Authorize(Roles = EformRole.Admin)]
    public async Task<OperationResult> AddSite([FromBody] int siteId)
    {
        return await settingService.AddSite(siteId);
    }

    [HttpDelete]
    [Route("sites")]
    [Authorize(Roles = EformRole.User)]
    public async Task<OperationResult> DeleteSite(int siteId)
    {
        return await settingService.DeleteSite(siteId);
    }

    [HttpGet]
    [Route("sites")]
    [Authorize(Policy = TimePlanningClaims.GetWorkingHours)]
    public async Task<OperationDataResult<List<Site>>> GetAvailableSites()
    {
        return await settingService.GetAvailableSites(null);
    }

    [HttpGet]
    [Route("registration-sites")]
    public async Task<OperationDataResult<List<Site>>> RegistrationSites(string token)
    {
        return await settingService.GetAvailableSites(token);
    }

    [HttpGet]
    [Route("assigned-sites")]
    [Authorize(Policy = TimePlanningClaims.GetWorkingHours)]
    public async Task<OperationDataResult<AssignedSite>> GetAssignedSite(int siteId)
    {
        return await settingService.GetAssignedSite(siteId);
    }

    [HttpGet]
    [Route("assigned-site")]
    public async Task<OperationDataResult<AssignedSite>> GetAssignedSiteByCurrentUserName()
    {
        return await settingService.GetAssignedSiteByCurrentUserName();
    }

    [HttpPut]
    [Route("assigned-site")]
    [Authorize(Policy = TimePlanningClaims.GetWorkingHours)]
    public async Task<OperationResult> UpdateAssignedSite([FromBody] AssignedSite site)
    {
        return await settingService.UpdateAssignedSite(site);
    }

    [HttpGet]
    [Route("global-auto-break-settings")]
    public async Task<OperationDataResult<GlobalAutoBreakSettings>> GetGlobalAutoBreakSettings()
    {
        return await settingService.GetGlobalAutoBreakSettings();
    }

    [HttpDelete]
    [Route("reset-global-auto-break-settings")]
    [Authorize(Roles = EformRole.Admin)]
    public OperationResult ResetGlobalAutoBreakSettings()
    {
        return settingService.ResetGlobalAutoBreakSettings();
    }
}