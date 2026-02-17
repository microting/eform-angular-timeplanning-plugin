import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators, FormArray } from '@angular/forms';
import { MatDialogRef } from '@angular/material/dialog';
import { MatDialog } from '@angular/material/dialog';
import { ToastrService } from 'ngx-toastr';
import { TimePlanningPnPayRuleSetsService } from '../../../../services';
import { PayRuleSetCreateModel } from '../../../../models';
import { PayDayRuleDialogComponent, PayDayRuleDialogData } from '../pay-day-rule-dialog/pay-day-rule-dialog.component';

@Component({
  selector: 'app-pay-rule-sets-create-modal',
  standalone: false,
  templateUrl: './pay-rule-sets-create-modal.component.html',
  styleUrls: ['./pay-rule-sets-create-modal.component.scss']
})
export class PayRuleSetsCreateModalComponent implements OnInit {
  form!: FormGroup;

  constructor(
    private fb: FormBuilder,
    private dialog: MatDialog,
    private payRuleSetsService: TimePlanningPnPayRuleSetsService,
    private toastrService: ToastrService,
    public dialogRef: MatDialogRef<PayRuleSetsCreateModalComponent>
  ) {}

  ngOnInit(): void {
    this.initForm();
  }

  private initForm(): void {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(2)]],
      payDayRules: this.fb.array([])
    });
  }

  get payDayRulesFormArray(): FormArray {
    return this.form.get('payDayRules') as FormArray;
  }

  onAddPayDayRule(): void {
    const dialogRef = this.dialog.open(PayDayRuleDialogComponent, {
      data: { mode: 'create' } as PayDayRuleDialogData,
      minWidth: 600,
      maxWidth: 800
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        const ruleForm = this.createPayDayRuleFormGroup(result);
        this.payDayRulesFormArray.push(ruleForm);
      }
    });
  }

  onEditPayDayRule(index: number): void {
    const rule = this.payDayRulesFormArray.at(index).value;

    const dialogRef = this.dialog.open(PayDayRuleDialogComponent, {
      data: { mode: 'edit', rule } as PayDayRuleDialogData,
      minWidth: 600,
      maxWidth: 800
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        // Update the existing rule
        this.payDayRulesFormArray.at(index).patchValue(result);
        
        // Update the payTierRules array
        const payTierRulesArray = this.payDayRulesFormArray.at(index).get('payTierRules') as FormArray;
        payTierRulesArray.clear();
        
        if (result.payTierRules && result.payTierRules.length > 0) {
          result.payTierRules.forEach((tier: any) => {
            payTierRulesArray.push(this.createPayTierRuleFormGroup(tier));
          });
        }
      }
    });
  }

  onDeletePayDayRule(index: number): void {
    this.payDayRulesFormArray.removeAt(index);
  }

  private createPayDayRuleFormGroup(rule: any): FormGroup {
    const tierRules = this.fb.array(
      (rule.payTierRules || []).map((tier: any) => this.createPayTierRuleFormGroup(tier))
    );

    return this.fb.group({
      id: [rule.id || null],
      dayCode: [rule.dayCode, Validators.required],
      payTierRules: tierRules
    });
  }

  private createPayTierRuleFormGroup(tier: any): FormGroup {
    return this.fb.group({
      id: [tier.id || null],
      order: [tier.order, Validators.required],
      upToSeconds: [tier.upToSeconds, [Validators.min(0)]],
      payCode: [tier.payCode, Validators.required]
    });
  }

  createPayRuleSet(): void {
    console.log('createPayRuleSet called');
    console.log('Form valid:', this.form.valid);
    console.log('Form value:', this.form.value);
    
    if (this.form.invalid) {
      console.log('Form is invalid, not proceeding');
      console.log('Form errors:', this.form.errors);
      return;
    }

    const model = new PayRuleSetCreateModel();
    model.name = this.form.get('name')?.value;
    model.payDayRules = this.payDayRulesFormArray.value;
    
    console.log('Sending model to API:', JSON.stringify(model, null, 2));

    this.payRuleSetsService.createPayRuleSet(model).subscribe({
      next: (response) => {
        console.log('Create success response:', response);
        this.toastrService.success('Pay rule set created successfully');
        this.dialogRef.close(true);
      },
      error: (error) => {
        console.error('Create error:', error);
        console.error('Error details:', JSON.stringify(error, null, 2));
        this.toastrService.error('Failed to create pay rule set');
      }
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
