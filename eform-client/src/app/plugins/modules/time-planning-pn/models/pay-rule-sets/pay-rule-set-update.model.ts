import { PayDayRuleModel } from './pay-day-rule.model';
import { PayDayTypeRuleModel } from '../pay-day-type-rules/pay-day-type-rule.model';

export class PayRuleSetUpdateModel {
  // id is passed separately in the URL, not in the body
  name: string;
  payDayRules: PayDayRuleModel[];
  payDayTypeRules: PayDayTypeRuleModel[];
}
