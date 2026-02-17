export class BreakPolicyRuleModel {
  id: number;
  breakPolicyId?: number;
  breakAfterMinutes: number;
  breakDurationMinutes: number;
  paidBreakMinutes: number;
  unpaidBreakMinutes: number;
}
