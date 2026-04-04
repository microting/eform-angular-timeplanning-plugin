import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { OperationDataResult, OperationResult } from 'src/app/common/models';
import { ApiBaseService } from 'src/app/common/services';
import {
  TimeFlexesModel,
  TimeFlexesUpdateModel,
  TimePlanningModel,
  TimePlanningsRequestModel,
  TimePlanningsUpdateModel,
  TimePlanningUpdateModel,
} from '../models';

export let TimePlanningPnFlexesMethods = {
  IndexFlex: 'api/time-planning-pn/flex/index',
  Flex: 'api/time-planning-pn/flex',
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

  getFlexes(): Observable<OperationDataResult<TimeFlexesModel[]>> {
    return this.apiBaseService.get(TimePlanningPnFlexesMethods.IndexFlex);
  }

  updateFlexes(model: TimeFlexesUpdateModel[]): Observable<OperationResult> {
    return this.apiBaseService.put(TimePlanningPnFlexesMethods.Flex, model);
  }
}
