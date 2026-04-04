import { PayTimeBandRuleModel } from '../pay-time-band-rules/pay-time-band-rule.model';

export class PayDayTypeRuleModel {
  id: number;
  payRuleSetId: number;
  dayType: string;
  defaultPayCode: string;
  priority: number;
  timeBandRules: PayTimeBandRuleModel[];
}
