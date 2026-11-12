/*
The MIT License (MIT)

Copyright (c) 2007 - 2026 Microting A/S

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
namespace TimePlanning.Pn.Controllers;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Infrastructure.Const;
using Infrastructure.Models.PlanPattern;
using Infrastructure.Models.Planner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Services.PlannerService;

/// <summary>
/// The web planner, "Planlægning (beta)" (#5489). Every endpoint requires the
/// <see cref="TimePlanningPlannerClaims.Planner"/> permission. Nothing here is
/// used by an existing screen, and no existing endpoint changes.
/// </summary>
[Authorize(Policy = TimePlanningPlannerClaims.Planner)]
[Route("api/time-planning-pn/planner")]
public class TimePlanningPlannerController(IPlannerService plannerService) : Controller
{
    /// <summary>GET planner/grid?from=2026-11-09&amp;days=14&amp;tagIds=1&amp;tagIds=2</summary>
    [HttpGet("grid")]
    public Task<OperationDataResult<PlannerGridModel>> GetGrid(
        [FromQuery] DateTime from, [FromQuery] int days = 14, [FromQuery] List<int>? tagIds = null) =>
        plannerService.GetGridAsync(new PlannerGridRequestModel
        {
            From = from,
            Days = days,
            TagIds = tagIds ?? []
        });

    [HttpPost("cells")]
    public Task<OperationDataResult<PlannerCellsUpdateResultModel>> UpdateCells(
        [FromBody] PlannerCellsUpdateModel model) =>
        plannerService.UpdateCellsAsync(model);

    [HttpGet("patterns/{assignedSiteId:int}")]
    public Task<OperationDataResult<PlannerPatternModel>> GetPattern(int assignedSiteId) =>
        plannerService.GetPatternAsync(assignedSiteId);

    [HttpPut("patterns/{assignedSiteId:int}")]
    public Task<OperationDataResult<PlanPatternApplyResult>> SavePattern(int assignedSiteId,
        [FromBody] PlannerPatternSaveModel model) =>
        plannerService.SavePatternAsync(assignedSiteId, model);

    [HttpDelete("patterns/{assignedSiteId:int}")]
    public Task<OperationResult> DeletePattern(int assignedSiteId) =>
        plannerService.DeletePatternAsync(assignedSiteId);

    /// <summary>GET planner/patterns/{id}/manual-days?validFrom=2026-11-16</summary>
    [HttpGet("patterns/{assignedSiteId:int}/manual-days")]
    public Task<OperationDataResult<List<DateTime>>> GetManualDays(int assignedSiteId,
        [FromQuery] DateTime validFrom) =>
        plannerService.GetManualDaysAsync(assignedSiteId, validFrom);

    [HttpPost("patterns/{assignedSiteId:int}/copy-from/{sourceAssignedSiteId:int}")]
    public Task<OperationDataResult<PlannerPatternModel>> CopyPattern(int assignedSiteId, int sourceAssignedSiteId) =>
        plannerService.CopyPatternAsync(assignedSiteId, sourceAssignedSiteId);

    [HttpPost("absence")]
    public Task<OperationDataResult<PlannerAbsenceResultModel>> CreateAbsence(
        [FromBody] PlannerAbsenceCreateModel model) =>
        plannerService.CreateAbsenceAsync(model);

    [HttpGet("import/preview")]
    public Task<OperationDataResult<PlannerImportPreviewModel>> GetImportPreview() =>
        plannerService.GetImportPreviewAsync();

    [HttpPost("import/apply")]
    public Task<OperationDataResult<PlannerImportApplyResultModel>> ApplyImport(
        [FromBody] PlannerImportApplyModel model) =>
        plannerService.ApplyImportAsync(model);

    [HttpGet("settings")]
    public Task<OperationDataResult<PlannerSettingsModel>> GetSettings() =>
        plannerService.GetSettingsAsync();

    [HttpPut("settings")]
    public Task<OperationResult> UpdateSettings([FromBody] PlannerSettingsModel model) =>
        plannerService.UpdateSettingsAsync(model);
}
