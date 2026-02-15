import { BreakPolicyRuleModel } from './break-policy-rule.model';

export class BreakPolicyUpdateModel {
  id: number;
  name: string;
  rules: BreakPolicyRuleModel[];
}
