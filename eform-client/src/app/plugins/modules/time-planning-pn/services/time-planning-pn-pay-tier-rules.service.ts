import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  OperationDataResult,
  OperationResult,
} from 'src/app/common/models';
import { ApiBaseService } from 'src/app/common/services';
import {
  PayTierRuleModel,
  PayTierRuleCreateModel,
  PayTierRuleUpdateModel,
  PayTierRulesRequestModel,
  PayTierRulesListModel,
} from '../models';

export let PayTierRuleMethods = {
  PayTierRules: 'api/time-planning-pn/pay-tier-rules',
};

@Injectable()
export class TimePlanningPnPayTierRulesService {
  constructor(private apiBaseService: ApiBaseService) {}

  getPayTierRules(model: PayTierRulesRequestModel): Observable<OperationDataResult<PayTierRulesListModel>> {
    const params: any = {
      offset: model.offset.toString(),
      pageSize: model.pageSize.toString(),
    };
    if (model.payDayRuleId !== undefined) {
      params.payDayRuleId = model.payDayRuleId.toString();
    }
    return this.apiBaseService.get(PayTierRuleMethods.PayTierRules, params);
  }

  getPayTierRule(id: number): Observable<OperationDataResult<PayTierRuleModel>> {
    return this.apiBaseService.get(`${PayTierRuleMethods.PayTierRules}/${id}`);
  }

  createPayTierRule(model: PayTierRuleCreateModel): Observable<OperationResult> {
    return this.apiBaseService.post(PayTierRuleMethods.PayTierRules, model);
  }

  updatePayTierRule(model: PayTierRuleUpdateModel): Observable<OperationResult> {
    return this.apiBaseService.put(`${PayTierRuleMethods.PayTierRules}/${model.id}`, model);
  }

  deletePayTierRule(id: number): Observable<OperationResult> {
    return this.apiBaseService.delete(`${PayTierRuleMethods.PayTierRules}/${id}`);
  }
}
