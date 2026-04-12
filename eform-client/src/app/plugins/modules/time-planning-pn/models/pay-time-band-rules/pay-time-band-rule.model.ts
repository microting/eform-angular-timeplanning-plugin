export class PayTimeBandRuleModel {
  id: number;
  payDayTypeRuleId: number;
  startSecondOfDay: number;
  endSecondOfDay: number;
  payCode: string;
  payrollCode?: string;
  priority: number;
}
