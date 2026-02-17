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
      return 'No tiers';
    }

    return tiers.controls
      .map(tier => {
        const upToSeconds = tier.get('upToSeconds')?.value;
        const payCode = tier.get('payCode')?.value || '';
        const timeStr = upToSeconds ? this.formatSeconds(upToSeconds) : 'unlimited';
        return `${timeStr} → ${payCode}`;
      })
      .join(', ');
  }

  /**
   * Format seconds into human-readable time
   */
  formatSeconds(seconds: number | null): string {
    if (seconds === null || seconds === undefined) {
      return 'No limit';
    }
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    if (minutes > 0) {
      return `${hours}h ${minutes}m`;
    }
    return `${hours}h`;
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
