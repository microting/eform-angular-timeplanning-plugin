#nullable enable
namespace TimePlanning.Pn.Services.PlannerService;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Infrastructure.Models.PlanPattern;
using Infrastructure.Models.Planner;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;

/// <summary>
/// The web planner behind "Planlægning (beta)" (#5489). Every per-worker call
/// is limited to the workers the caller sees on the planning board
/// (SiteScopeResolver); a worker outside it is refused as not found.
/// The Google Sheet import preview lists only the sheet workers in the
/// caller's scope. Applying the import and changing the settings retire or
/// restore the sheet for the whole organisation (a restored sheet re-seeds
/// every worker onto it), so they need full scope: the caller must see every
/// active worker in the organisation, else they are refused with
/// PlannerNeedsFullScope. Reading the settings needs only the permission.
/// </summary>
public interface IPlannerService
{
    Task<OperationDataResult<PlannerGridModel>> GetGridAsync(PlannerGridRequestModel model);

    Task<OperationDataResult<PlannerCellsUpdateResultModel>> UpdateCellsAsync(PlannerCellsUpdateModel model);

    Task<OperationDataResult<PlannerPatternModel>> GetPatternAsync(int assignedSiteId);

    Task<OperationDataResult<PlanPatternApplyResult>> SavePatternAsync(int assignedSiteId, PlannerPatternSaveModel model);

    Task<OperationResult> DeletePatternAsync(int assignedSiteId);

    Task<OperationDataResult<List<DateTime>>> GetManualDaysAsync(int assignedSiteId, DateTime validFrom);

    /// <summary>The source's pattern as an unsaved pattern for the target; saving is the PUT.</summary>
    Task<OperationDataResult<PlannerPatternModel>> CopyPatternAsync(int assignedSiteId, int sourceAssignedSiteId);

    /// <summary>
    /// An administrator, or a manager of the worker, registers an absence over
    /// a period as one Approved request.
    /// </summary>
    Task<OperationDataResult<PlannerAbsenceResultModel>> CreateAbsenceAsync(PlannerAbsenceCreateModel model);

    /// <summary>
    /// What <see cref="CreateAbsenceAsync"/> would do with the same body, day by
    /// day, by the same decision and permission; writes nothing.
    /// </summary>
    Task<OperationDataResult<PlannerAbsencePreviewModel>> PreviewAbsenceAsync(PlannerAbsenceCreateModel model);

    Task<OperationDataResult<PlannerImportPreviewModel>> GetImportPreviewAsync();

    Task<OperationDataResult<PlannerImportApplyResultModel>> ApplyImportAsync(PlannerImportApplyModel model);

    Task<OperationDataResult<PlannerSettingsModel>> GetSettingsAsync();

    Task<OperationResult> UpdateSettingsAsync(PlannerSettingsModel model);
}
