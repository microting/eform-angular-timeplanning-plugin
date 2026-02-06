import { Injectable } from '@angular/core';
import { ApiBaseService } from 'src/app/common/services';
import { Observable } from 'rxjs';
import { OperationDataResult, OperationResult } from 'src/app/common/models';
import { AbsenceRequestModel, AbsenceRequestDecisionModel } from '../models';

export let TimePlanningPnAbsenceRequestsMethods = {
  GetInbox: 'api/time-planning-pn/absence-requests/inbox',
  GetMine: 'api/time-planning-pn/absence-requests/mine',
  Approve: 'api/time-planning-pn/absence-requests',
  Reject: 'api/time-planning-pn/absence-requests',
};

@Injectable({
  providedIn: 'root'
})
export class TimePlanningPnAbsenceRequestsService {
  constructor(private apiBaseService: ApiBaseService) {}

  getInbox(managerSdkSitId: number): Observable<OperationDataResult<AbsenceRequestModel[]>> {
    return this.apiBaseService.get(TimePlanningPnAbsenceRequestsMethods.GetInbox, { managerSdkSitId });
  }

  getMine(requestedBySdkSitId: number): Observable<OperationDataResult<AbsenceRequestModel[]>> {
    return this.apiBaseService.get(TimePlanningPnAbsenceRequestsMethods.GetMine, { requestedBySdkSitId });
  }

  approve(id: number, model: AbsenceRequestDecisionModel): Observable<OperationResult> {
    return this.apiBaseService.post(`${TimePlanningPnAbsenceRequestsMethods.Approve}/${id}/approve`, model);
  }

  reject(id: number, model: AbsenceRequestDecisionModel): Observable<OperationResult> {
    return this.apiBaseService.post(`${TimePlanningPnAbsenceRequestsMethods.Reject}/${id}/reject`, model);
  }
}
