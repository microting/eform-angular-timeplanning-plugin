import { BreakPolicyRuleModel } from './break-policy-rule.model';

export class BreakPolicyModel {
  id: number;
  name: string;
  rules: BreakPolicyRuleModel[];
}
