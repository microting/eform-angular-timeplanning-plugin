﻿/*
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
using System;
using System.IO;
using Microsoft.AspNetCore.Http;

namespace TimePlanning.Pn.Services.TimePlanningWorkingHoursService;

using System.Collections.Generic;
using System.Threading.Tasks;
using Infrastructure.Models.WorkingHours.Index;
using Infrastructure.Models.WorkingHours.UpdateCreate;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;

/// <summary>
/// Interface ITimePlanningWorkingHoursService
/// </summary>
public interface ITimePlanningWorkingHoursService
{
    Task<OperationDataResult<List<TimePlanningWorkingHoursModel>>> Index(TimePlanningWorkingHoursRequestModel model);
    Task<OperationResult> CreateUpdate(TimePlanningWorkingHoursUpdateCreateModel model);
    Task<OperationDataResult<Stream>> GenerateExcelDashboard(TimePlanningWorkingHoursRequestModel model);
    Task<OperationDataResult<Stream>> GenerateExcelDashboard(TimePlanningWorkingHoursReportForAllWorkersRequestModel model);
    Task<OperationResult> Import(IFormFile file);
    Task<OperationDataResult<TimePlanningWorkingHoursModel>> Read(int sdkSiteId, DateTime dateTime, string token);
    Task<OperationResult> UpdateWorkingHour(int? sdkSiteId, TimePlanningWorkingHoursUpdateModel model, string token);
    Task<OperationDataResult<TimePlanningWorkingHourSimpleModel>> ReadSimple(DateTime dateTime, string? softwareVersion, string? model, string? manufacturer, string? osVersion);
    Task<OperationDataResult<TimePlanningHoursSummaryModel>> CalculateHoursSummary(DateTime startDate, DateTime endDate, string? softwareVersion, string? model, string? manufacturer, string? osVersion);
}