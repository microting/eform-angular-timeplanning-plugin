import { PayDayRuleModel } from './pay-day-rule.model';
import { PayDayTypeRuleModel } from '../pay-day-type-rules/pay-day-type-rule.model';

export class PayRuleSetModel {
  id: number;
  name: string;
  payDayRules: PayDayRuleModel[];
  payDayTypeRules: PayDayTypeRuleModel[];
}
