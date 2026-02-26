import {Component, Inject, OnInit} from '@angular/core';
import {MAT_DIALOG_DATA, MatDialogRef} from '@angular/material/dialog';
import {FormGroup, FormControl, Validators} from '@angular/forms';
import {BreakPolicyRuleFormValue} from '../break-policy-rules-list/break-policy-rules-list.component';

export interface BreakPolicyRuleDialogData {
  mode: 'create' | 'edit';
  rule?: BreakPolicyRuleFormValue;
}

@Component({
  selector: 'app-break-policy-rule-dialog',
  standalone: false,
  templateUrl: './break-policy-rule-dialog.component.html',
  styleUrls: ['./break-policy-rule-dialog.component.scss']
})
export class BreakPolicyRuleDialogComponent implements OnInit {
  ruleForm!: FormGroup;
  mode: 'create' | 'edit';

  constructor(
    public dialogRef: MatDialogRef<BreakPolicyRuleDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: BreakPolicyRuleDialogData
  ) {
    this.mode = data.mode;
  }

  ngOnInit(): void {
    this.ruleForm = this.createRuleForm();
    
    if (this.mode === 'edit' && this.data.rule) {
      this.ruleForm.patchValue(this.data.rule);
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

  get dialogTitle(): string {
    return this.mode === 'create' ? 'Add Break Rule' : 'Edit Break Rule';
  }

  get submitButtonText(): string {
    return this.mode === 'create' ? 'Add' : 'Save';
  }

  onCancel(): void {
    this.dialogRef.close();
  }

  onSave(): void {
    if (this.ruleForm.valid) {
      this.dialogRef.close(this.ruleForm.value);
    }
  }
}
