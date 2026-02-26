import { PayTierRuleModel } from '../pay-tier-rules/pay-tier-rule.model';

export class PayDayRuleModel {
  id: number;
  payRuleSetId: number;
  dayCode: string;
  payTierRules: PayTierRuleModel[];
}
