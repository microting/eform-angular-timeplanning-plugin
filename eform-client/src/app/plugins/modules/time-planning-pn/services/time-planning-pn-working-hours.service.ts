import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { OperationDataResult, OperationResult } from 'src/app/common/models';
import { ApiBaseService } from 'src/app/common/services';
import {
  TimePlanningModel,
  TimePlanningsRequestModel,
  WorkingHoursModel,
  TimePlanningsReportAllWorkersDownloadRequestModel, WorkingHourModel
} from '../models';

export let TimePlanningPnWorkingHoursMethods = {
  IndexWorkingHours: 'api/time-planning-pn/working-hours/index',
  WorkingHours: 'api/time-planning-pn/working-hours',
  WorkingHourReadSimple: 'api/time-planning-pn/working-hours/read-simple',
  Reports: 'api/time-planning-pn/working-hours/reports/file',
  ReportsAllWorkers: 'api/time-planning-pn/working-hours/reports/file-all-workers',
};

@Injectable({
  providedIn: 'root',
})
export class TimePlanningPnWorkingHoursService {
  constructor(private apiBaseService: ApiBaseService) {}

  getWorkingHours(
    model: TimePlanningsRequestModel
  ): Observable<OperationDataResult<TimePlanningModel[]>> {
    return this.apiBaseService.post(
      TimePlanningPnWorkingHoursMethods.IndexWorkingHours,
      model
    );
  }

  getWorkingHourReadSimple(dateTime: string): Observable<OperationDataResult<WorkingHourModel>> {
    return this.apiBaseService.get(
      TimePlanningPnWorkingHoursMethods.WorkingHourReadSimple, {dateTime: dateTime}
    );
  }

  updateWorkingHours(model: WorkingHoursModel): Observable<OperationResult> {
    return this.apiBaseService.put(
      TimePlanningPnWorkingHoursMethods.WorkingHours,
      model
    );
  }

  downloadReport(model: TimePlanningsRequestModel): Observable<any> {
    return this.apiBaseService.getBlobData(
      TimePlanningPnWorkingHoursMethods.Reports,
      model
    );
  }

  downloadReportAllWorkers(model: TimePlanningsReportAllWorkersDownloadRequestModel): Observable<any> {
    return this.apiBaseService.getBlobData(
      TimePlanningPnWorkingHoursMethods.ReportsAllWorkers,
      model
    );
  }
}
