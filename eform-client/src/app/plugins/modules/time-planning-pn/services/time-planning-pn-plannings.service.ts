import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { OperationDataResult, OperationResult } from 'src/app/common/models';
import { ApiBaseService } from 'src/app/common/services';
import {
  PlanningPrDayModel,
  TimeFlexesModel,
  TimeFlexesUpdateModel,
  TimePlanningModel,
  TimePlanningsRequestModel,
  TimePlanningsUpdateModel,
  TimePlanningUpdateModel,
  PlanRegistrationVersionHistoryModel,
} from '../models';

export let TimePlanningPnPlanningsMethods = {
  Plannings: 'api/time-planning-pn/plannings',
  SimplePlannings: 'api/time-planning-pn/plannings/index',
  IndexWorkingHours: 'api/time-planning-pn/working-hours/index',
  WorkingHours: 'api/time-planning-pn/working-hours',
};

@Injectable({
  providedIn: 'root',
})
export class TimePlanningPnPlanningsService {
  constructor(private apiBaseService: ApiBaseService) {
  }

  getPlannings(
    model: TimePlanningsRequestModel
  ): Observable<OperationDataResult<TimePlanningModel[]>> {
    return this.apiBaseService.post(
      TimePlanningPnPlanningsMethods.SimplePlannings,
      model
    );
  }

  updatePlanning(
    model: PlanningPrDayModel, id: number
  ): Observable<OperationResult> {
    return this.apiBaseService.put(
      TimePlanningPnPlanningsMethods.Plannings + '/' + id,
      model
    );
  }

  getVersionHistory(
    planRegistrationId: number
  ): Observable<OperationDataResult<PlanRegistrationVersionHistoryModel>> {
    return this.apiBaseService.get(
      TimePlanningPnPlanningsMethods.Plannings + '/' + planRegistrationId + '/version-history'
    );
  }
}
