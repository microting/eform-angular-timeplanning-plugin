import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators, FormArray } from '@angular/forms';
import { MatDialogRef } from '@angular/material/dialog';
import { MatDialog } from '@angular/material/dialog';
import { ToastrService } from 'ngx-toastr';
import { TimePlanningPnPayRuleSetsService } from '../../../../services';
import { PayRuleSetCreateModel } from '../../../../models';
import { PayDayRuleDialogComponent, PayDayRuleDialogData } from '../pay-day-rule-dialog/pay-day-rule-dialog.component';
import { DayTypeRuleDialogComponent, DayTypeRuleDialogData } from '../day-type-rule-dialog/day-type-rule-dialog.component';

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
      payDayRules: this.fb.array([]),
      payDayTypeRules: this.fb.array([])
    });
  }

  get payDayRulesFormArray(): FormArray {
    return this.form.get('payDayRules') as FormArray;
  }

  get payDayTypeRulesFormArray(): FormArray {
    return this.form.get('payDayTypeRules') as FormArray;
  }

  // --- Day Type Rule methods ---

  onAddDayTypeRule(): void {
    const dialogRef = this.dialog.open(DayTypeRuleDialogComponent, {
      data: { mode: 'create' } as DayTypeRuleDialogData,
      minWidth: 1280,
      maxWidth: 1440
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        const ruleForm = this.createDayTypeRuleFormGroup(result);
        this.payDayTypeRulesFormArray.push(ruleForm);
      }
    });
  }

  onEditDayTypeRule(index: number): void {
    const rule = this.payDayTypeRulesFormArray.at(index).value;

    const dialogRef = this.dialog.open(DayTypeRuleDialogComponent, {
      data: { mode: 'edit', rule } as DayTypeRuleDialogData,
      minWidth: 1280,
      maxWidth: 1440
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.payDayTypeRulesFormArray.at(index).patchValue(result);

        // Update the timeBandRules array
        const timeBandRulesArray = this.payDayTypeRulesFormArray.at(index).get('timeBandRules') as FormArray;
        timeBandRulesArray.clear();

        if (result.timeBandRules && result.timeBandRules.length > 0) {
          result.timeBandRules.forEach((band: any) => {
            timeBandRulesArray.push(this.createTimeBandRuleFormGroup(band));
          });
        }
      }
    });
  }

  onDeleteDayTypeRule(index: number): void {
    this.payDayTypeRulesFormArray.removeAt(index);
  }

  private createDayTypeRuleFormGroup(rule: any): FormGroup {
    const timeBandRules = this.fb.array(
      (rule.timeBandRules || []).map((band: any) => this.createTimeBandRuleFormGroup(band))
    );

    return this.fb.group({
      id: [rule.id || null],
      dayType: [rule.dayType, Validators.required],
      defaultPayCode: [rule.defaultPayCode || '', Validators.required],
      priority: [rule.priority ?? 0, [Validators.required, Validators.min(0)]],
      timeBandRules: timeBandRules
    });
  }

  private createTimeBandRuleFormGroup(band: any): FormGroup {
    return this.fb.group({
      id: [band.id || null],
      startSecondOfDay: [band.startSecondOfDay ?? null, [Validators.required, Validators.min(0)]],
      endSecondOfDay: [band.endSecondOfDay ?? null, [Validators.required, Validators.min(0)]],
      payCode: [band.payCode || '', Validators.required],
      priority: [band.priority ?? 0, [Validators.required, Validators.min(0)]]
    });
  }

  // --- Pay Day Rule methods ---

  onAddPayDayRule(): void {
    const dialogRef = this.dialog.open(PayDayRuleDialogComponent, {
      data: { mode: 'create' } as PayDayRuleDialogData,
      minWidth: 1280,
      maxWidth: 1440
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
      minWidth: 1280,
      maxWidth: 1440
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
    model.payDayTypeRules = this.payDayTypeRulesFormArray.value;

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
