import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  OperationDataResult,
  OperationResult,
} from 'src/app/common/models';
import { ApiBaseService } from 'src/app/common/services';
import {
  PayDayTypeRuleModel,
  PayDayTypeRuleCreateModel,
  PayDayTypeRuleUpdateModel,
  PayDayTypeRulesRequestModel,
  PayDayTypeRulesListModel,
} from '../models';

export let PayDayTypeRuleMethods = {
  PayDayTypeRules: 'api/time-planning-pn/pay-day-type-rules',
};

@Injectable()
export class TimePlanningPnPayDayTypeRulesService {
  constructor(private apiBaseService: ApiBaseService) {}

  getPayDayTypeRules(model: PayDayTypeRulesRequestModel): Observable<OperationDataResult<PayDayTypeRulesListModel>> {
    const params: any = {
      offset: model.offset.toString(),
      pageSize: model.pageSize.toString(),
    };
    if (model.payRuleSetId !== undefined) {
      params.payRuleSetId = model.payRuleSetId.toString();
    }
    return this.apiBaseService.get(PayDayTypeRuleMethods.PayDayTypeRules, params);
  }

  getPayDayTypeRule(id: number): Observable<OperationDataResult<PayDayTypeRuleModel>> {
    return this.apiBaseService.get(`${PayDayTypeRuleMethods.PayDayTypeRules}/${id}`);
  }

  createPayDayTypeRule(model: PayDayTypeRuleCreateModel): Observable<OperationResult> {
    return this.apiBaseService.post(PayDayTypeRuleMethods.PayDayTypeRules, model);
  }

  updatePayDayTypeRule(model: PayDayTypeRuleUpdateModel): Observable<OperationResult> {
    return this.apiBaseService.put(`${PayDayTypeRuleMethods.PayDayTypeRules}/${model.id}`, model);
  }

  deletePayDayTypeRule(id: number): Observable<OperationResult> {
    return this.apiBaseService.delete(`${PayDayTypeRuleMethods.PayDayTypeRules}/${id}`);
  }
}
