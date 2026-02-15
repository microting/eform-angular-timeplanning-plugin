import { PayDayRuleModel } from './pay-day-rule.model';

export class PayRuleSetUpdateModel {
  id: number;
  name: string;
  payDayRules: PayDayRuleModel[];
}
