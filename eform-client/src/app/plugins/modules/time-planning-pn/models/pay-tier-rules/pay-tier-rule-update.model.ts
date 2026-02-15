export class PayTierRuleUpdateModel {
  id: number;
  payDayRuleId: number;
  order: number;
  upToSeconds: number | null;
  payCode: string;
}
