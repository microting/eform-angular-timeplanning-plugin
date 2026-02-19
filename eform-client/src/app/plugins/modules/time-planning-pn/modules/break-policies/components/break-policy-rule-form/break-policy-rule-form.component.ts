import {Component, Input, OnInit} from '@angular/core';
import {FormGroup, FormControl, Validators} from '@angular/forms';

@Component({
  selector: 'app-break-policy-rule-form',
  standalone: false,
  templateUrl: './break-policy-rule-form.component.html',
  styleUrls: ['./break-policy-rule-form.component.scss']
})
export class BreakPolicyRuleFormComponent implements OnInit {
  @Input() ruleForm!: FormGroup;

  daysOfWeek = [
    { value: 0, label: 'Sunday' },
    { value: 1, label: 'Monday' },
    { value: 2, label: 'Tuesday' },
    { value: 3, label: 'Wednesday' },
    { value: 4, label: 'Thursday' },
    { value: 5, label: 'Friday' },
    { value: 6, label: 'Saturday' }
  ];

  ngOnInit(): void {
    if (!this.ruleForm) {
      this.ruleForm = this.createRuleForm();
    }
  }

  createRuleForm(): FormGroup {
    return new FormGroup({
      id: new FormControl<number | null>(null),
      dayOfWeek: new FormControl<number | null>(null, [Validators.required]),
      paidBreakMinutes: new FormControl<number | null>(null, [Validators.required, Validators.min(0)]),
      unpaidBreakMinutes: new FormControl<number | null>(null, [Validators.required, Validators.min(0)]),
    });
  }

  get dayOfWeek() {
    return this.ruleForm.get('dayOfWeek');
  }

  get paidBreakMinutes() {
    return this.ruleForm.get('paidBreakMinutes');
  }

  get unpaidBreakMinutes() {
    return this.ruleForm.get('unpaidBreakMinutes');
  }
}
