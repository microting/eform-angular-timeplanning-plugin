import { HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { OperationDataResult, OperationResult } from 'src/app/common/models';
import { ApiBaseService } from 'src/app/common/services';
import {
  WorkingHoursModel,
  WorkingHourRequestModel,
  WorkingHourModel,
  TimePlanningsReportAllWorkersDownloadRequestModel
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
    model: WorkingHourRequestModel
  ): Observable<OperationDataResult<WorkingHourModel[]>> {
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

  downloadReport(model: WorkingHourRequestModel): Observable<any> {
    return this.apiBaseService.getBlobData(
      TimePlanningPnWorkingHoursMethods.Reports,
      model
    );
  }

  downloadReportAllWorkers(model: TimePlanningsReportAllWorkersDownloadRequestModel): Observable<any> {
    const {tagIds, ...params} = model;
    // The tags cannot go through the shared params helper: it sets one value per key, so
    // an array would be flattened to "tagIds=1,2", which the server cannot read back as a
    // list — a filtered export would then silently contain every worker. The list has to
    // repeat the key, "tagIds=1&tagIds=2", which is what append builds. Angular joins this
    // query string and the remaining params with "&".
    const tagQuery = (tagIds || [])
      .reduce((httpParams, tagId) => httpParams.append('tagIds', tagId), new HttpParams())
      .toString();
    const url = tagQuery
      ? `${TimePlanningPnWorkingHoursMethods.ReportsAllWorkers}?${tagQuery}`
      : TimePlanningPnWorkingHoursMethods.ReportsAllWorkers;
    return this.apiBaseService.getBlobData(url, params);
  }
}
