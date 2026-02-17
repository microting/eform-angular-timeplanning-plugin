import {Component, Input, OnInit} from '@angular/core';
import {FormGroup, FormControl, Validators, AbstractControl, ValidationErrors} from '@angular/forms';

@Component({
  selector: 'app-break-policy-rule-form',
  standalone: false,
  templateUrl: './break-policy-rule-form.component.html',
  styleUrls: ['./break-policy-rule-form.component.scss']
})
export class BreakPolicyRuleFormComponent implements OnInit {
  @Input() ruleForm!: FormGroup;

  ngOnInit(): void {
    if (!this.ruleForm) {
      this.ruleForm = this.createRuleForm();
    }
    
    // Watch for changes to update unpaid automatically
    this.ruleForm.get('breakDurationMinutes')?.valueChanges.subscribe(() => {
      this.updateUnpaidBreak();
    });
    
    this.ruleForm.get('paidBreakMinutes')?.valueChanges.subscribe(() => {
      this.updateUnpaidBreak();
    });
  }

  createRuleForm(): FormGroup {
    return new FormGroup({
      id: new FormControl<number | null>(null),
      breakAfterMinutes: new FormControl<number | null>(null, [Validators.required, Validators.min(1)]),
      breakDurationMinutes: new FormControl<number | null>(null, [Validators.required, Validators.min(1)]),
      paidBreakMinutes: new FormControl<number | null>(null, [Validators.required, Validators.min(0)]),
      unpaidBreakMinutes: new FormControl<number | null>({value: null, disabled: true}),
    }, {validators: this.breakSumValidator});
  }

  breakSumValidator(control: AbstractControl): ValidationErrors | null {
    const group = control as FormGroup;
    const duration = group.get('breakDurationMinutes')?.value;
    const paid = group.get('paidBreakMinutes')?.value;
    const unpaid = group.get('unpaidBreakMinutes')?.value;

    if (duration == null || paid == null || unpaid == null) {
      return null; // Don't validate if values are not set yet
    }

    if (paid + unpaid !== duration) {
      return {breakSumInvalid: true};
    }

    return null;
  }

  updateUnpaidBreak(): void {
    const duration = this.ruleForm.get('breakDurationMinutes')?.value;
    const paid = this.ruleForm.get('paidBreakMinutes')?.value;

    if (duration != null && paid != null) {
      const unpaid = duration - paid;
      this.ruleForm.get('unpaidBreakMinutes')?.setValue(unpaid, {emitEvent: false});
    }
  }

  get breakAfterMinutes() {
    return this.ruleForm.get('breakAfterMinutes');
  }

  get breakDurationMinutes() {
    return this.ruleForm.get('breakDurationMinutes');
  }

  get paidBreakMinutes() {
    return this.ruleForm.get('paidBreakMinutes');
  }

  get unpaidBreakMinutes() {
    return this.ruleForm.get('unpaidBreakMinutes');
  }
}
