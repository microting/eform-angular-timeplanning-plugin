import {Component, EventEmitter, Input, Output} from '@angular/core';
import {FormArray, FormGroup} from '@angular/forms';

export interface BreakPolicyRuleFormValue {
  id?: number | null;
  dayOfWeek: number;
  paidBreakMinutes: number;
  unpaidBreakMinutes: number;
}

@Component({
  selector: 'app-break-policy-rules-list',
  standalone: false,
  templateUrl: './break-policy-rules-list.component.html',
  styleUrls: ['./break-policy-rules-list.component.scss']
})
export class BreakPolicyRulesListComponent {
  @Input() rulesFormArray!: FormArray<FormGroup>;
  @Output() addRule = new EventEmitter<void>();
  @Output() editRule = new EventEmitter<number>();
  @Output() deleteRule = new EventEmitter<number>();

  displayedColumns: string[] = ['dayOfWeek', 'paid', 'unpaid', 'actions'];

  get rules(): FormGroup[] {
    return this.rulesFormArray?.controls as FormGroup[] || [];
  }

  get hasRules(): boolean {
    return this.rules.length > 0;
  }

  onAddRule(): void {
    this.addRule.emit();
  }

  onEditRule(index: number): void {
    this.editRule.emit(index);
  }

  onDeleteRule(index: number): void {
    this.deleteRule.emit(index);
  }

  getRuleValue(rule: FormGroup): BreakPolicyRuleFormValue {
    return rule.value as BreakPolicyRuleFormValue;
  }

  getDayName(dayOfWeek: number): string {
    const days = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
    return days[dayOfWeek] || '';
  }

  // Calculate total minutes for summary
  getTotalPaidMinutes(): number {
    return this.rules.reduce((total, rule) => {
      const value = this.getRuleValue(rule);
      return total + (value.paidBreakMinutes || 0);
    }, 0);
  }

  getTotalUnpaidMinutes(): number {
    return this.rules.reduce((total, rule) => {
      const value = this.getRuleValue(rule);
      return total + (value.unpaidBreakMinutes || 0);
    }, 0);
  }
}
