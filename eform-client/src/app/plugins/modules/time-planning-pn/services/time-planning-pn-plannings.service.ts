import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { OperationDataResult, OperationResult } from 'src/app/common/models';
import { ApiBaseService } from 'src/app/common/services';
import {
  TimePlanningModel,
  TimePlanningsRequestModel,
  TimePlanningsUpdateModel,
  TimePlanningUpdateModel,
} from '../models';

export let TimePlanningPnPlanningsMethods = {
  Plannings: 'api/time-planning-pn/plannings',
  SimplePlannings: 'api/time-planning-pn/plannings/simple',
  IndexPlannings: 'api/time-planning-pn/plannings/index',
  SinglePlanning: 'api/time-planning-pn/plannings/single',
};

@Injectable({
  providedIn: 'root',
})
export class TimePlanningPnPlanningsService {
  constructor(private apiBaseService: ApiBaseService) {}

  getSimplePlannings(
    model: TimePlanningsRequestModel
  ): Observable<OperationDataResult<TimePlanningModel[]>> {
    return this.apiBaseService.post(
      TimePlanningPnPlanningsMethods.SimplePlannings,
      model
    );
  }

  getPlannings(
    model: TimePlanningsRequestModel
  ): Observable<OperationDataResult<TimePlanningModel[]>> {
    return this.apiBaseService.post(
      TimePlanningPnPlanningsMethods.IndexPlannings,
      model
    );
  }

  updateSinglePlanning(
    model: TimePlanningUpdateModel
  ): Observable<OperationResult> {
    return this.apiBaseService.put(
      TimePlanningPnPlanningsMethods.SinglePlanning,
      model
    );
  }

  updatePlannings(
    model: TimePlanningsUpdateModel
  ): Observable<OperationResult> {
    return this.apiBaseService.put(
      TimePlanningPnPlanningsMethods.Plannings,
      model
    );
  }
}
