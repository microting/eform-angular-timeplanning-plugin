export class PayTierRuleCreateModel {
  payDayRuleId: number;
  order: number;
  upToSeconds: number | null;
  payCode: string;
}
