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

using System;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TimePlanning.Pn.Extensions;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours;

namespace TimePlanning.Pn.Controllers
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure.Models.WorkingHours.Index;
    using Infrastructure.Models.WorkingHours.UpdateCreate;
    using Microsoft.AspNetCore.Mvc;
    using Microting.eFormApi.BasePn.Infrastructure.Models.API;
    using Services.TimePlanningWorkingHoursService;

    [Authorize]
    [Route("api/time-planning-pn/working-hours")]
    public class TimePlanningWorkingHoursController : Controller
    {
        private readonly ITimePlanningWorkingHoursService _workingHoursService;

        public TimePlanningWorkingHoursController(ITimePlanningWorkingHoursService workingHoursService)
        {
            _workingHoursService = workingHoursService;
        }

        [HttpPost]
        [Route("index")]
        public async Task<OperationDataResult<List<TimePlanningWorkingHoursModel>>> Index(
            [FromBody] TimePlanningWorkingHoursRequestModel model)
        {
            return await _workingHoursService.Index(model);
        }

        [HttpGet]
        [Route("read")]
        [AllowAnonymous]
        public async Task<OperationDataResult<TimePlanningWorkingHoursModel>> Read(int sdkSiteId, DateTime date, string token)
        {
            return await _workingHoursService.Read(sdkSiteId, date, token);
        }

        [HttpGet]
        [Route("read-simple")]
        public async Task<OperationDataResult<TimePlanningWorkingHourSimpleModel>> Read(DateTime dateTime, string? softwareVersion, string? model, string? manufacturer, string? osVersion)
        {
            return await _workingHoursService.ReadSimple(dateTime, softwareVersion, model, manufacturer, osVersion);
        }

        [HttpGet]
        [Route("calculate-hours-summary")]
        public async Task<OperationDataResult<TimePlanningHoursSummaryModel>> CalculateHoursSummary(DateTime startDate, DateTime endDate, string? softwareVersion, string? model, string? manufacturer, string? osVersion)
        {
            return await _workingHoursService.CalculateHoursSummary(startDate, endDate, softwareVersion, model, manufacturer, osVersion);
        }

        [HttpPut]
        public async Task<OperationResult> Update([FromBody] TimePlanningWorkingHoursUpdateCreateModel model)
        {
            return await _workingHoursService.CreateUpdate(model);
        }

        [HttpPut]
        [Route("update")]
        [AllowAnonymous]
        public async Task<OperationResult> UpdateWorkingHour([FromForm] int? sdkSiteId, [FromForm] TimePlanningWorkingHoursUpdateModel obj, [FromForm] string token)
        {
            return await _workingHoursService.UpdateWorkingHour(sdkSiteId, obj, token);
        }

        /// <summary>
        /// Download records export word
        /// </summary>
        /// <param name="requestModel">The request model.</param>
        [HttpGet]
        [Route("reports/file")]
        [ProducesResponseType(typeof(string), 400)]
        public async Task GenerateReportFile(TimePlanningWorkingHoursRequestModel requestModel)
        {
            var result = await _workingHoursService.GenerateExcelDashboard(requestModel);
            const int bufferSize = 4086;
            byte[] buffer = new byte[bufferSize];
            Response.OnStarting(async () =>
            {
                if (!result.Success)
                {
                    Response.ContentLength = result.Message.Length;
                    Response.ContentType = "text/plain";
                    Response.StatusCode = 400;
                    byte[] bytes = Encoding.UTF8.GetBytes(result.Message);
                    await Response.Body.WriteAsync(bytes, 0, result.Message.Length);
                    await Response.Body.FlushAsync();
                }
                else
                {
                    await using var wordStream = result.Model;
                    int bytesRead;
                    Response.ContentLength = wordStream.Length;
                    Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

                    while ((bytesRead = wordStream.Read(buffer, 0, buffer.Length)) > 0 &&
                           !HttpContext.RequestAborted.IsCancellationRequested)
                    {
                        await Response.Body.WriteAsync(buffer, 0, bytesRead);
                        await Response.Body.FlushAsync();
                    }
                }
            });
        }


        /// <summary>
        /// Download records export word
        /// </summary>
        /// <param name="requestModel">The request model.</param>
        [HttpGet]
        [Route("reports/file-all-workers")]
        [ProducesResponseType(typeof(string), 400)]
        public async Task GenerateReportFileByAllWorkers(TimePlanningWorkingHoursReportForAllWorkersRequestModel requestModel)
        {
            var result = await _workingHoursService.GenerateExcelDashboard(requestModel);
            const int bufferSize = 4086;
            var buffer = new byte[bufferSize];
            Response.OnStarting(async () =>
            {
                if (!result.Success)
                {
                    Response.ContentLength = result.Message.Length;
                    Response.ContentType = "text/plain";
                    Response.StatusCode = 400;
                    var bytes = Encoding.UTF8.GetBytes(result.Message);
                    await Response.Body.WriteAsync(bytes, 0, result.Message.Length);
                    await Response.Body.FlushAsync();
                }
                else
                {
                    await using var wordStream = result.Model;
                    int bytesRead;
                    Response.ContentLength = wordStream.Length;
                    Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

                    while ((bytesRead = wordStream.Read(buffer, 0, buffer.Length)) > 0 &&
                           !HttpContext.RequestAborted.IsCancellationRequested)
                    {
                        await Response.Body.WriteAsync(buffer, 0, bytesRead);
                        await Response.Body.FlushAsync();
                    }
                }
            });
        }

        [HttpPost]
        [Route("reports/import")]
        public async Task<OperationResult> Import(WorkingHourUploadModel workingHourUploadModel)
        {
            return await _workingHoursService.Import(workingHourUploadModel.File);
        }
    }
}