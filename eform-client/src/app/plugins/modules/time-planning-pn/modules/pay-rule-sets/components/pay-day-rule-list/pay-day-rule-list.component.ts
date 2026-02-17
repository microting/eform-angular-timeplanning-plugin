import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormArray, FormGroup } from '@angular/forms';

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

  /**
   * Get the display name for a day of week number
   */
  getDayName(dayOfWeek: number): string {
    const days = [
      'Sunday',
      'Monday',
      'Tuesday',
      'Wednesday',
      'Thursday',
      'Friday',
      'Saturday'
    ];
    return days[dayOfWeek] || 'Unknown';
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
      return 'No tiers';
    }

    return tiers.controls
      .map(tier => {
        const percent = tier.get('tierPercent')?.value || 0;
        const tierNum = tier.get('tierNumber')?.value || 0;
        return `${percent}% Tier ${tierNum}`;
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
