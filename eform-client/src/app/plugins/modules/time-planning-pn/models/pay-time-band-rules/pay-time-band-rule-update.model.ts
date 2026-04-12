export class PayTimeBandRuleUpdateModel {
  id: number;
  payDayTypeRuleId: number;
  startSecondOfDay: number;
  endSecondOfDay: number;
  payCode: string;
  payrollCode?: string;
  priority: number;
}
