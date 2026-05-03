import { Component, OnInit, Inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators, FormArray } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatDialog } from '@angular/material/dialog';
import { ToastrService } from 'ngx-toastr';
import { TranslateService } from '@ngx-translate/core';
import { TimePlanningPnPayRuleSetsService } from '../../../../services';
import { PayRuleSetUpdateModel, PayRuleSetModel, PAY_RULE_SET_PRESETS, PayRuleSetPreset } from '../../../../models';
import { PayDayRuleDialogComponent, PayDayRuleDialogData } from '../pay-day-rule-dialog/pay-day-rule-dialog.component';
import { DayTypeRuleDialogComponent, DayTypeRuleDialogData } from '../day-type-rule-dialog/day-type-rule-dialog.component';

export interface PayRuleSetsEditModalData {
  payRuleSetId: number;
}

@Component({
  selector: 'app-pay-rule-sets-edit-modal',
  standalone: false,
  templateUrl: './pay-rule-sets-edit-modal.component.html',
  styleUrls: ['./pay-rule-sets-edit-modal.component.scss']
})
export class PayRuleSetsEditModalComponent implements OnInit {
  form!: FormGroup;
  payRuleSet!: PayRuleSetModel;
  loading = true;

  /**
   * Resolves the matching locked preset for the loaded rule set, by name.
   * Returns null when the loaded rule set is custom (or the preset is not
   * marked locked). When non-null, the modal renders a read-only summary
   * view instead of the editable form, matching the create modal's locked
   * preset path.
   */
  get lockedPreset(): PayRuleSetPreset | null {
    if (!this.payRuleSet) return null;
    return PAY_RULE_SET_PRESETS.find(p => p.locked && p.name === this.payRuleSet.name) ?? null;
  }

  get isLocked(): boolean {
    return this.lockedPreset !== null;
  }

  constructor(
    private fb: FormBuilder,
    private dialog: MatDialog,
    private payRuleSetsService: TimePlanningPnPayRuleSetsService,
    private toastrService: ToastrService,
    private translateService: TranslateService,
    public dialogRef: MatDialogRef<PayRuleSetsEditModalComponent>,
    @Inject(MAT_DIALOG_DATA) public data: PayRuleSetsEditModalData
  ) {}

  ngOnInit(): void {
    this.initForm();
    this.loadPayRuleSet();
  }

  private initForm(): void {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(2)]],
      payDayRules: this.fb.array([]),
      payDayTypeRules: this.fb.array([])
    });
  }

  private loadPayRuleSet(): void {
    this.loading = true;
    this.payRuleSetsService.getPayRuleSet(this.data.payRuleSetId).subscribe({
      next: (result) => {
        if (result && result.success && result.model) {
          this.payRuleSet = result.model;
          this.populateForm();
          this.loading = false;
        }
      },
      error: () => {
        this.toastrService.error(this.translateService.instant('Failed to load pay rule set'));
        this.dialogRef.close();
      }
    });
  }

  private populateForm(): void {
    // Set name
    this.form.patchValue({
      name: this.payRuleSet.name
    });

    // Load PayDayRules
    if (this.payRuleSet.payDayRules && this.payRuleSet.payDayRules.length > 0) {
      this.payRuleSet.payDayRules.forEach(rule => {
        const ruleForm = this.createPayDayRuleFormGroup(rule);
        this.payDayRulesFormArray.push(ruleForm);
      });
    }

    // Load PayDayTypeRules
    if (this.payRuleSet.payDayTypeRules && this.payRuleSet.payDayTypeRules.length > 0) {
      this.payRuleSet.payDayTypeRules.forEach(rule => {
        const ruleForm = this.createDayTypeRuleFormGroup(rule);
        this.payDayTypeRulesFormArray.push(ruleForm);
      });
    }
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

  updatePayRuleSet(): void {
    console.log('updatePayRuleSet called');
    console.log('Form valid:', this.form.valid);
    console.log('Form value:', this.form.value);

    if (this.form.invalid) {
      console.log('Form is invalid, not proceeding');
      console.log('Form errors:', this.form.errors);
      return;
    }

    const model = new PayRuleSetUpdateModel();
    // Do NOT set model.id - it will be passed separately
    model.name = this.form.get('name')?.value;
    model.payDayRules = this.payDayRulesFormArray.value;
    model.payDayTypeRules = this.payDayTypeRulesFormArray.value;

    console.log('Sending model to API:', JSON.stringify(model, null, 2));
    console.log('With ID:', this.payRuleSet.id);

    this.payRuleSetsService.updatePayRuleSet(this.payRuleSet.id, model).subscribe({
      next: (response) => {
        console.log('Update success response:', response);
        this.toastrService.success(this.translateService.instant('Pay rule set updated successfully'));
        this.dialogRef.close(true);
      },
      error: (error) => {
        console.error('Update error:', error);
        console.error('Error details:', JSON.stringify(error, null, 2));
        this.toastrService.error(this.translateService.instant('Failed to update pay rule set'));
      }
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }

  formatTierChain(tiers: Array<{ order: number; upToSeconds: number | null; payCode: string }>): string {
    return [...tiers]
      .sort((a, b) => a.order - b.order)
      .map(t => {
        if (t.upToSeconds != null) {
          return `${t.payCode} (${this.secondsToHM(t.upToSeconds)})`;
        }
        return t.payCode;
      })
      .join(' → ');
  }

  formatTimeBands(bands: Array<{ startSecondOfDay: number; endSecondOfDay: number; payCode: string; priority: number }>): string {
    return bands
      .map(b => `${this.secondsToHHMM(b.startSecondOfDay)}-${this.secondsToHHMM(b.endSecondOfDay)} ${b.payCode}`)
      .join(' | ');
  }

  private secondsToHM(seconds: number): string {
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    if (m === 0) {
      return `${h}h`;
    }
    return `${h}h${m}m`;
  }

  private secondsToHHMM(seconds: number): string {
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    return `${h.toString().padStart(2, '0')}:${m.toString().padStart(2, '0')}`;
  }
}
