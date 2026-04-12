export class PayTimeBandRuleSimpleModel {
  id: number;
  payCode: string;
  payrollCode?: string;
  startSecondOfDay: number;
  endSecondOfDay: number;
  priority: number;
}
