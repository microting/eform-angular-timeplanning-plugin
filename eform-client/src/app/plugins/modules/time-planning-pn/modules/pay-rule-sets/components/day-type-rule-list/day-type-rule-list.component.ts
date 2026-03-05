import { Component, DoCheck, EventEmitter, Input, Output } from '@angular/core';
import { AbstractControl, FormArray, FormGroup } from '@angular/forms';
import { MatTableDataSource } from '@angular/material/table';

@Component({
  selector: 'app-day-type-rule-list',
  standalone: false,
  templateUrl: './day-type-rule-list.component.html',
  styleUrls: ['./day-type-rule-list.component.scss']
})
export class DayTypeRuleListComponent implements DoCheck {
  @Input() dayTypeRulesFormArray!: FormArray;

  @Output() addRule = new EventEmitter<void>();
  @Output() editRule = new EventEmitter<number>();
  @Output() deleteRule = new EventEmitter<number>();

  dataSource = new MatTableDataSource<AbstractControl>();
  private previousLength = 0;

  ngDoCheck(): void {
    if (this.dayTypeRulesFormArray && this.dayTypeRulesFormArray.length !== this.previousLength) {
      this.previousLength = this.dayTypeRulesFormArray.length;
      this.dataSource.data = this.dayTypeRulesFormArray.controls;
    }
  }

  /**
   * Get the display label for a day type
   */
  getDayTypeLabel(dayType: string): string {
    const labels: { [key: string]: string } = {
      'Monday': 'Monday',
      'Tuesday': 'Tuesday',
      'Wednesday': 'Wednesday',
      'Thursday': 'Thursday',
      'Friday': 'Friday',
      'Saturday': 'Saturday',
      'Sunday': 'Sunday',
      'Holiday': 'Holiday'
    };
    return labels[dayType] || dayType;
  }

  /**
   * Get the number of time band rules for a day type rule
   */
  getTimeBandCount(rule: FormGroup): number {
    const bands = rule.get('timeBandRules') as FormArray;
    return bands?.length || 0;
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
    return this.dayTypeRulesFormArray.at(index) as FormGroup;
  }
}

