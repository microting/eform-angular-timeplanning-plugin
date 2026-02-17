import { Component, Inject, OnInit } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

export interface PayDayRuleDialogData {
  mode: 'create' | 'edit';
  rule?: any;
}

@Component({
  selector: 'app-pay-day-rule-dialog',
  standalone: false,
  templateUrl: './pay-day-rule-dialog.component.html',
  styleUrls: ['./pay-day-rule-dialog.component.scss']
})
export class PayDayRuleDialogComponent implements OnInit {
  payDayRuleForm!: FormGroup;

  constructor(
    private fb: FormBuilder,
    public dialogRef: MatDialogRef<PayDayRuleDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: PayDayRuleDialogData
  ) {}

  ngOnInit(): void {
    this.initForm();
    if (this.data.mode === 'edit' && this.data.rule) {
      this.patchFormValues();
    }
  }

  private initForm(): void {
    this.payDayRuleForm = this.fb.group({
      id: [null],
      dayOfWeek: [0, Validators.required],
      payTierRules: this.fb.array([])
    });
  }

  private patchFormValues(): void {
    if (this.data.rule) {
      this.payDayRuleForm.patchValue({
        id: this.data.rule.id || null,
        dayOfWeek: this.data.rule.dayOfWeek || 0
      });

      // Patch tier rules if they exist
      if (this.data.rule.payTierRules && Array.isArray(this.data.rule.payTierRules)) {
        const tierRulesArray = this.payDayRuleForm.get('payTierRules') as FormArray;
        this.data.rule.payTierRules.forEach((tier: any) => {
          const tierForm = this.fb.group({
            id: [tier.id || null],
            tierNumber: [tier.tierNumber, Validators.required],
            tierPercent: [tier.tierPercent, [Validators.required, Validators.min(0), Validators.max(100)]],
            payCodeId: [tier.payCodeId, Validators.required]
          });
          tierRulesArray.push(tierForm);
        });
      }
    }
  }

  get dialogTitle(): string {
    return this.data.mode === 'create' ? 'Add Pay Day Rule' : 'Edit Pay Day Rule';
  }

  save(): void {
    if (this.payDayRuleForm.valid) {
      this.dialogRef.close(this.payDayRuleForm.value);
    }
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
