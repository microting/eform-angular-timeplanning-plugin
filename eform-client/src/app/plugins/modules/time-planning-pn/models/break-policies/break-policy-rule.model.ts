export class BreakPolicyRuleModel {
  id: number;
  breakPolicyId?: number;
  dayOfWeek: number; // 0-6 (Sunday-Saturday)
  paidBreakMinutes: number;
  unpaidBreakMinutes: number;
}
