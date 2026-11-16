import {Injectable, inject} from '@angular/core';
import {Observable} from 'rxjs';
import {OperationDataResult, OperationResult} from 'src/app/common/models';
import {ApiBaseService} from 'src/app/common/services';
import {
  PlanPatternApplyResult,
  PlannerAbsenceCreateModel,
  PlannerAbsenceResultModel,
  PlannerCellsUpdateModel,
  PlannerCellsUpdateResultModel,
  PlannerGridModel,
  PlannerImportApplyModel,
  PlannerImportApplyResultModel,
  PlannerImportPreviewModel,
  PlannerPatternModel,
  PlannerPatternSaveModel,
  PlannerSettingsModel,
} from '../models';
import {AbsenceRequestModel} from '../../../models';
import {TimePlanningPnAbsenceRequestsMethods} from '../../../services/time-planning-pn-absence-requests.service';

const Base = 'api/time-planning-pn/planner';

export const TimePlanningPnPlannerMethods = {
  Grid: `${Base}/grid`,
  Cells: `${Base}/cells`,
  Patterns: `${Base}/patterns`,
  Absence: `${Base}/absence`,
  ImportPreview: `${Base}/import/preview`,
  ImportApply: `${Base}/import/apply`,
  Settings: `${Base}/settings`,
};

/**
 * TimePlanningPlannerController (#5489). Every call needs the
 * time_planning_planner permission.
 *
 * ApiBaseService toasts the server's message on its own (error on failure,
 * success when the server sends one), which is the plugin's usual pattern. The
 * calls whose outcome the caller words itself use the NoToast variants.
 */
@Injectable({providedIn: 'root'})
export class TimePlanningPnPlannerService {
  private apiBaseService = inject(ApiBaseService);

  /** from: transport date; days: 7 or 14; tagIds repeat as tagIds=1&tagIds=2 for List<int> binding. */
  getGrid(from: string, days: number, tagIds: number[]): Observable<OperationDataResult<PlannerGridModel>> {
    const query = [`from=${encodeURIComponent(from)}`, `days=${days}`]
      .concat((tagIds ?? []).map(id => `tagIds=${id}`))
      .join('&');
    return this.apiBaseService.get(`${TimePlanningPnPlannerMethods.Grid}?${query}`);
  }

  updateCells(model: PlannerCellsUpdateModel): Observable<OperationDataResult<PlannerCellsUpdateResultModel>> {
    return this.apiBaseService.post(TimePlanningPnPlannerMethods.Cells, model);
  }

  /** The absence-request inbox without a toast: the planner reads it only for the button's pending count. */
  getInboxNoToast(): Observable<OperationDataResult<AbsenceRequestModel[]>> {
    return this.apiBaseService.getNoToast(TimePlanningPnAbsenceRequestsMethods.GetInbox);
  }

  getPattern(assignedSiteId: number): Observable<OperationDataResult<PlannerPatternModel>> {
    return this.apiBaseService.get(`${TimePlanningPnPlannerMethods.Patterns}/${assignedSiteId}`);
  }

  /** NoToast: a save with a warning is success + message, which must not read as a success toast. */
  savePattern(assignedSiteId: number, model: PlannerPatternSaveModel)
    : Observable<OperationDataResult<PlanPatternApplyResult>> {
    return this.apiBaseService.putNoToast(`${TimePlanningPnPlannerMethods.Patterns}/${assignedSiteId}`, model);
  }

  /** NoToast: the dialog words the outcome itself. */
  deletePattern(assignedSiteId: number): Observable<OperationResult> {
    return this.apiBaseService.deleteNoToast(`${TimePlanningPnPlannerMethods.Patterns}/${assignedSiteId}`);
  }

  getManualDays(assignedSiteId: number, validFrom: string): Observable<OperationDataResult<string[]>> {
    return this.apiBaseService.get(
      `${TimePlanningPnPlannerMethods.Patterns}/${assignedSiteId}/manual-days`, {validFrom});
  }

  copyPattern(assignedSiteId: number, sourceAssignedSiteId: number): Observable<OperationDataResult<PlannerPatternModel>> {
    return this.apiBaseService.post(
      `${TimePlanningPnPlannerMethods.Patterns}/${assignedSiteId}/copy-from/${sourceAssignedSiteId}`, {});
  }

  createAbsence(model: PlannerAbsenceCreateModel): Observable<OperationDataResult<PlannerAbsenceResultModel>> {
    return this.apiBaseService.post(TimePlanningPnPlannerMethods.Absence, model);
  }

  getImportPreview(): Observable<OperationDataResult<PlannerImportPreviewModel>> {
    return this.apiBaseService.get(TimePlanningPnPlannerMethods.ImportPreview);
  }

  applyImport(model: PlannerImportApplyModel): Observable<OperationDataResult<PlannerImportApplyResultModel>> {
    return this.apiBaseService.post(TimePlanningPnPlannerMethods.ImportApply, model);
  }

  updateSettings(model: PlannerSettingsModel): Observable<OperationResult> {
    return this.apiBaseService.put(TimePlanningPnPlannerMethods.Settings, model);
  }
}
