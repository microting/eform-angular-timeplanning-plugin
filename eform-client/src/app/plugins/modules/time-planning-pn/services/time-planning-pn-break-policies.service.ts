import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  OperationDataResult,
  OperationResult,
} from 'src/app/common/models';
import { ApiBaseService } from 'src/app/common/services';
import {
  BreakPolicyModel,
  BreakPolicyCreateModel,
  BreakPolicyUpdateModel,
  BreakPoliciesRequestModel,
  BreakPoliciesListModel,
} from '../models';

export let BreakPolicyMethods = {
  BreakPolicies: 'api/time-planning-pn/break-policies',
};

@Injectable()
export class TimePlanningPnBreakPoliciesService {
  constructor(private apiBaseService: ApiBaseService) {}

  getBreakPolicies(model: BreakPoliciesRequestModel): Observable<OperationDataResult<BreakPoliciesListModel>> {
    const params = {
      offset: model.offset.toString(),
      pageSize: model.pageSize.toString(),
    };
    return this.apiBaseService.get(BreakPolicyMethods.BreakPolicies, params);
  }

  getBreakPolicy(id: number): Observable<OperationDataResult<BreakPolicyModel>> {
    return this.apiBaseService.get(`${BreakPolicyMethods.BreakPolicies}/${id}`);
  }

  createBreakPolicy(model: BreakPolicyCreateModel): Observable<OperationResult> {
    return this.apiBaseService.post(BreakPolicyMethods.BreakPolicies, model);
  }

  updateBreakPolicy(model: BreakPolicyUpdateModel): Observable<OperationResult> {
    return this.apiBaseService.put(`${BreakPolicyMethods.BreakPolicies}/${model.id}`, model);
  }

  deleteBreakPolicy(id: number): Observable<OperationResult> {
    return this.apiBaseService.delete(`${BreakPolicyMethods.BreakPolicies}/${id}`);
  }
}
