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

export let TimePlanningPnFlexesMethods = {
  Flexes: 'api/time-planning-pn/flexes',
  FlexesIndex: 'api/time-planning-pn/flexes/index',
};

@Injectable({
  providedIn: 'root',
})
export class TimePlanningPnFlexesService {
  constructor(private apiBaseService: ApiBaseService) {}

  // getPlannings(
  //   model: TimePlanningsRequestModel
  // ): Observable<OperationDataResult<TimePlanningModel[]>> {
  //   return this.apiBaseService.post(
  //     TimePlanningPnPlanningsMethods.SimplePlannings,
  //     model
  //   );
  // }
  //
  // getWorkingHours(
  //   model: TimePlanningsRequestModel
  // ): Observable<OperationDataResult<TimePlanningModel[]>> {
  //   return this.apiBaseService.post(
  //     TimePlanningPnPlanningsMethods.IndexWorkingHours,
  //     model
  //   );
  // }
  //
  // updatePlanning(
  //   model: TimePlanningUpdateModel
  // ): Observable<OperationResult> {
  //   return this.apiBaseService.put(
  //     TimePlanningPnPlanningsMethods.Plannings,
  //     model
  //   );
  // }
  //
  // updateWorkingHours(
  //   model: TimePlanningsUpdateModel
  // ): Observable<OperationResult> {
  //   return this.apiBaseService.put(
  //     TimePlanningPnPlanningsMethods.WorkingHours,
  //     model
  //   );
  // }
}
