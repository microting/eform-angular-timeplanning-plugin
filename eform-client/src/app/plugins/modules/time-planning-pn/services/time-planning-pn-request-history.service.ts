import { Injectable } from '@angular/core';
import { ApiBaseService } from 'src/app/common/services';
import { Observable } from 'rxjs';
import { OperationDataResult } from 'src/app/common/models';

export interface RequestHistoryQueryParams {
  status?: string;
  fromDate?: string;
  toDate?: string;
  sdkSiteId?: number;
}

export let TimePlanningPnRequestHistoryMethods = {
  GetAllHandovers: 'api/time-planning-pn/content-handover-requests/all',
  GetAllAbsences: 'api/time-planning-pn/absence-requests/all',
};

@Injectable({
  providedIn: 'root'
})
export class TimePlanningPnRequestHistoryService {
  constructor(private apiBaseService: ApiBaseService) {}

  getAllHandoverRequests(params: RequestHistoryQueryParams): Observable<OperationDataResult<any[]>> {
    return this.apiBaseService.get(TimePlanningPnRequestHistoryMethods.GetAllHandovers, params);
  }

  getAllAbsenceRequests(params: RequestHistoryQueryParams): Observable<OperationDataResult<any[]>> {
    return this.apiBaseService.get(TimePlanningPnRequestHistoryMethods.GetAllAbsences, params);
  }
}
