import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  OperationDataResult,
  OperationResult,
} from 'src/app/common/models';
import { ApiBaseService } from 'src/app/common/services';
import {
  PayTimeBandRuleModel,
  PayTimeBandRuleCreateModel,
  PayTimeBandRuleUpdateModel,
  PayTimeBandRulesRequestModel,
  PayTimeBandRulesListModel,
} from '../models';

export let PayTimeBandRuleMethods = {
  PayTimeBandRules: 'api/time-planning-pn/pay-time-band-rules',
};

@Injectable()
export class TimePlanningPnPayTimeBandRulesService {
  constructor(private apiBaseService: ApiBaseService) {}

  getPayTimeBandRules(model: PayTimeBandRulesRequestModel): Observable<OperationDataResult<PayTimeBandRulesListModel>> {
    const params: any = {
      offset: model.offset.toString(),
      pageSize: model.pageSize.toString(),
    };
    if (model.payDayTypeRuleId !== undefined) {
      params.payDayTypeRuleId = model.payDayTypeRuleId.toString();
    }
    return this.apiBaseService.get(PayTimeBandRuleMethods.PayTimeBandRules, params);
  }

  getPayTimeBandRule(id: number): Observable<OperationDataResult<PayTimeBandRuleModel>> {
    return this.apiBaseService.get(`${PayTimeBandRuleMethods.PayTimeBandRules}/${id}`);
  }

  createPayTimeBandRule(model: PayTimeBandRuleCreateModel): Observable<OperationResult> {
    return this.apiBaseService.post(PayTimeBandRuleMethods.PayTimeBandRules, model);
  }

  updatePayTimeBandRule(model: PayTimeBandRuleUpdateModel): Observable<OperationResult> {
    return this.apiBaseService.put(`${PayTimeBandRuleMethods.PayTimeBandRules}/${model.id}`, model);
  }

  deletePayTimeBandRule(id: number): Observable<OperationResult> {
    return this.apiBaseService.delete(`${PayTimeBandRuleMethods.PayTimeBandRules}/${id}`);
  }
}
