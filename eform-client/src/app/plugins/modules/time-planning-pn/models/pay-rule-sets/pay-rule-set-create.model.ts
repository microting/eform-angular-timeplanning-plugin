import { PayDayRuleModel } from './pay-day-rule.model';

export class PayRuleSetCreateModel {
  name: string;
  payDayRules: PayDayRuleModel[];
}
