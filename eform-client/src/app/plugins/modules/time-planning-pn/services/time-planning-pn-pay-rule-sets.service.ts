import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  OperationDataResult,
  OperationResult,
} from 'src/app/common/models';
import { ApiBaseService } from 'src/app/common/services';
import {
  PayRuleSetModel,
  PayRuleSetCreateModel,
  PayRuleSetUpdateModel,
  PayRuleSetsRequestModel,
  PayRuleSetsListModel,
} from '../models';

export let PayRuleSetMethods = {
  PayRuleSets: 'api/time-planning-pn/pay-rule-sets',
};

@Injectable()
export class TimePlanningPnPayRuleSetsService {
  constructor(private apiBaseService: ApiBaseService) {}

  getPayRuleSets(model: PayRuleSetsRequestModel): Observable<OperationDataResult<PayRuleSetsListModel>> {
    const params = {
      offset: model.offset.toString(),
      pageSize: model.pageSize.toString(),
    };
    return this.apiBaseService.get(PayRuleSetMethods.PayRuleSets, params);
  }

  getPayRuleSet(id: number): Observable<OperationDataResult<PayRuleSetModel>> {
    return this.apiBaseService.get(`${PayRuleSetMethods.PayRuleSets}/${id}`);
  }

  createPayRuleSet(model: PayRuleSetCreateModel): Observable<OperationResult> {
    return this.apiBaseService.post(PayRuleSetMethods.PayRuleSets, model);
  }

  updatePayRuleSet(model: PayRuleSetUpdateModel): Observable<OperationResult> {
    return this.apiBaseService.put(`${PayRuleSetMethods.PayRuleSets}/${model.id}`, model);
  }

  deletePayRuleSet(id: number): Observable<OperationResult> {
    return this.apiBaseService.delete(`${PayRuleSetMethods.PayRuleSets}/${id}`);
  }
}
