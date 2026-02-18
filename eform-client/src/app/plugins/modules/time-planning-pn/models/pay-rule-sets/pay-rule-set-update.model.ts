import { PayDayRuleModel } from './pay-day-rule.model';

export class PayRuleSetUpdateModel {
  // id is passed separately in the URL, not in the body
  name: string;
  payDayRules: PayDayRuleModel[];
}
