import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormArray, FormGroup } from '@angular/forms';
import { TranslateService } from '@ngx-translate/core';
import { secondsToHM } from '../../pay-rule-format.util';

@Component({
  selector: 'app-pay-day-rule-list',
  standalone: false,
  templateUrl: './pay-day-rule-list.component.html',
  styleUrls: ['./pay-day-rule-list.component.scss']
})
export class PayDayRuleListComponent {
  @Input() payDayRulesFormArray!: FormArray;

  @Output() addRule = new EventEmitter<void>();
  @Output() editRule = new EventEmitter<number>();
  @Output() deleteRule = new EventEmitter<number>();

  constructor(private translateService: TranslateService) {}

  /**
   * Get the display label for a day code
   */
  getDayCodeLabel(dayCode: string): string {
    const labels: { [key: string]: string } = {
      'SUNDAY': 'Sunday',
      'MONDAY': 'Monday',
      'TUESDAY': 'Tuesday',
      'WEDNESDAY': 'Wednesday',
      'THURSDAY': 'Thursday',
      'FRIDAY': 'Friday',
      'SATURDAY': 'Saturday',
      'WEEKDAY': 'Weekday',
      'WEEKEND': 'Weekend',
      'HOLIDAY': 'Holiday',
      'GRUNDLOVSDAG': 'Grundlovsdag'
    };
    return labels[dayCode] || dayCode;
  }

  /**
   * Get the number of tiers for a pay day rule
   */
  getTierCount(rule: FormGroup): number {
    const tiers = rule.get('payTierRules') as FormArray;
    return tiers?.length || 0;
  }

  /**
   * Get a formatted string showing the tier breakdown
   */
  getTierBreakdown(rule: FormGroup): string {
    const tiers = rule.get('payTierRules') as FormArray;
    if (!tiers || tiers.length === 0) {
      return this.translateService.instant('No tiers');
    }

    return tiers.controls
      .map(tier => {
        const upToSeconds = tier.get('upToSeconds')?.value;
        const payCode = tier.get('payCode')?.value || '';
        const timeStr = upToSeconds != null
          ? secondsToHM(upToSeconds)
          : this.translateService.instant('Unlimited');
        return `${timeStr} → ${payCode}`;
      })
      .join(', ');
  }

  /**
   * Emit add rule event
   */
  onAddRule(): void {
    this.addRule.emit();
  }

  /**
   * Emit edit rule event with index
   */
  onEditRule(index: number): void {
    this.editRule.emit(index);
  }

  /**
   * Emit delete rule event with index
   */
  onDeleteRule(index: number): void {
    this.deleteRule.emit(index);
  }

  /**
   * Get the FormGroup at a specific index
   */
  getRuleFormGroup(index: number): FormGroup {
    return this.payDayRulesFormArray.at(index) as FormGroup;
  }
}
