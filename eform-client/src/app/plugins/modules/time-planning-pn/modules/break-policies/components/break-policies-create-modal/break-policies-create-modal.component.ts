import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { ToastrService } from 'ngx-toastr';
import { BreakPolicyCreateModel } from '../../../../models';
import { TimePlanningPnBreakPoliciesService } from '../../../../services';
import { BreakPolicyRuleDialogComponent, BreakPolicyRuleDialogData } from '../break-policy-rule-dialog/break-policy-rule-dialog.component';
import { BreakPolicyRuleFormValue } from '../break-policy-rules-list/break-policy-rules-list.component';

@Component({
  selector: 'app-break-policies-create-modal',
  templateUrl: './break-policies-create-modal.component.html',
  styleUrls: ['./break-policies-create-modal.component.scss'],
  standalone: false
})
export class BreakPoliciesCreateModalComponent implements OnInit {
  private breakPoliciesService = inject(TimePlanningPnBreakPoliciesService);
  private toastrService = inject(ToastrService);
  private fb = inject(FormBuilder);
  private dialog = inject(MatDialog);
  public dialogRef = inject(MatDialogRef<BreakPoliciesCreateModalComponent>);

  breakPolicyForm: FormGroup;

  ngOnInit() {
    this.initForm();
  }

  initForm() {
    this.breakPolicyForm = this.fb.group({
      name: ['', Validators.required],
      rules: this.fb.array([]),
    });
  }

  get rulesArray(): FormArray {
    return this.breakPolicyForm.get('rules') as FormArray;
  }

  onAddRule(): void {
    const dialogRef = this.dialog.open(BreakPolicyRuleDialogComponent, {
      data: { mode: 'create' } as BreakPolicyRuleDialogData,
      width: '500px',
    });

    dialogRef.afterClosed().subscribe((result: BreakPolicyRuleFormValue) => {
      if (result) {
        const ruleGroup = this.fb.group({
          id: [result.id || null],
          dayOfWeek: [result.dayOfWeek, Validators.required],
          paidBreakMinutes: [result.paidBreakMinutes, [Validators.required, Validators.min(0)]],
          unpaidBreakMinutes: [result.unpaidBreakMinutes, [Validators.required, Validators.min(0)]],
        });
        this.rulesArray.push(ruleGroup);
      }
    });
  }

  onEditRule(index: number): void {
    const rule = this.rulesArray.at(index).value;
    const dialogRef = this.dialog.open(BreakPolicyRuleDialogComponent, {
      data: { 
        mode: 'edit',
        rule: rule
      } as BreakPolicyRuleDialogData,
      width: '500px',
    });

    dialogRef.afterClosed().subscribe((result: BreakPolicyRuleFormValue) => {
      if (result) {
        this.rulesArray.at(index).patchValue(result);
      }
    });
  }

  onDeleteRule(index: number): void {
    this.rulesArray.removeAt(index);
    this.toastrService.info('Rule removed');
  }

  createBreakPolicy() {
    if (this.breakPolicyForm.invalid) return;

    const model: BreakPolicyCreateModel = {
      name: this.breakPolicyForm.value.name,
      breakPolicyRules: this.rulesArray.value.map((rule: BreakPolicyRuleFormValue) => ({
        dayOfWeek: rule.dayOfWeek,
        paidBreakMinutes: rule.paidBreakMinutes,
        unpaidBreakMinutes: rule.unpaidBreakMinutes,
      })),
    };

    this.breakPoliciesService.createBreakPolicy(model).subscribe((result) => {
      if (result.success) {
        this.toastrService.success('Break policy created successfully');
        this.hide(true);
      } else {
        this.toastrService.error('Failed to create break policy');
      }
    });
  }

  hide(result = false) {
    this.dialogRef.close(result);
  }
}
