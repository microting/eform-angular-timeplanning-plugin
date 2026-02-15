import { PayDayRuleModel } from './pay-day-rule.model';

export class PayRuleSetModel {
  id: number;
  name: string;
  payDayRules: PayDayRuleModel[];
}
