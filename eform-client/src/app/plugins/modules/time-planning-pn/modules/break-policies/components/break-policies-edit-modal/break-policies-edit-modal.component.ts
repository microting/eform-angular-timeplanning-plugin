import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialog, MatDialogRef } from '@angular/material/dialog';
import { ToastrService } from 'ngx-toastr';
import { BreakPolicyModel, BreakPolicyUpdateModel } from '../../../../models';
import { TimePlanningPnBreakPoliciesService } from '../../../../services';
import { BreakPolicyRuleDialogComponent, BreakPolicyRuleDialogData } from '../break-policy-rule-dialog/break-policy-rule-dialog.component';
import { BreakPolicyRuleFormValue } from '../break-policy-rules-list/break-policy-rules-list.component';

@Component({
  selector: 'app-break-policies-edit-modal',
  templateUrl: './break-policies-edit-modal.component.html',
  styleUrls: ['./break-policies-edit-modal.component.scss'],
  standalone: false
})
export class BreakPoliciesEditModalComponent implements OnInit {
  private breakPoliciesService = inject(TimePlanningPnBreakPoliciesService);
  private toastrService = inject(ToastrService);
  private fb = inject(FormBuilder);
  private dialog = inject(MatDialog);
  public dialogRef = inject(MatDialogRef<BreakPoliciesEditModalComponent>);
  private model = inject<{ selectedBreakPolicy: BreakPolicyModel }>(MAT_DIALOG_DATA);

  breakPolicyForm: FormGroup;
  selectedBreakPolicy: BreakPolicyModel;

  ngOnInit() {
    this.selectedBreakPolicy = { ...this.model.selectedBreakPolicy };
    this.initForm();
    this.loadRules();
  }

  initForm() {
    this.breakPolicyForm = this.fb.group({
      name: [this.selectedBreakPolicy.name || '', Validators.required],
      rules: this.fb.array([]),
    });
  }

  loadRules() {
    const rulesArray = this.breakPolicyForm.get('rules') as FormArray;
    if (this.selectedBreakPolicy.breakPolicyRules && this.selectedBreakPolicy.breakPolicyRules.length > 0) {
      this.selectedBreakPolicy.breakPolicyRules.forEach(rule => {
        const ruleGroup = this.fb.group({
          id: [rule.id],
          dayOfWeek: [rule.dayOfWeek],
          paidBreakMinutes: [rule.paidBreakMinutes],
          unpaidBreakMinutes: [rule.unpaidBreakMinutes],
        });
        rulesArray.push(ruleGroup);
      });
    }
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
          dayOfWeek: [result.dayOfWeek],
          paidBreakMinutes: [result.paidBreakMinutes],
          unpaidBreakMinutes: [result.unpaidBreakMinutes],
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

  updateBreakPolicy() {
    if (this.breakPolicyForm.invalid) return;

    const model: BreakPolicyUpdateModel = {
      name: this.breakPolicyForm.value.name,
      breakPolicyRules: this.rulesArray.value.map((rule: BreakPolicyRuleFormValue) => ({
        id: rule.id || null,
        dayOfWeek: rule.dayOfWeek,
        paidBreakMinutes: rule.paidBreakMinutes,
        unpaidBreakMinutes: rule.unpaidBreakMinutes,
      })),
    };

    this.breakPoliciesService.updateBreakPolicy(this.selectedBreakPolicy.id, model).subscribe((result) => {
      if (result.success) {
        this.toastrService.success('Break policy updated successfully');
        this.hide(true);
      } else {
        this.toastrService.error('Failed to update break policy');
      }
    });
  }

  hide(result = false) {
    this.dialogRef.close(result);
  }
}
