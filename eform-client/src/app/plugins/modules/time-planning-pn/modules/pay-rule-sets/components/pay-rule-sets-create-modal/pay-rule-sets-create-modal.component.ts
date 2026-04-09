import { Component, Inject, OnInit, Optional } from '@angular/core';
import { FormBuilder, FormGroup, Validators, FormArray } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatDialog } from '@angular/material/dialog';
import { ToastrService } from 'ngx-toastr';
import { TimePlanningPnPayRuleSetsService } from '../../../../services';
import { PayRuleSetCreateModel, PAY_RULE_SET_PRESETS, PayRuleSetPreset } from '../../../../models';
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
  availablePresets: PayRuleSetPreset[] = [];
  selectedPreset: PayRuleSetPreset | null = null;

  constructor(
    private fb: FormBuilder,
    private dialog: MatDialog,
    private payRuleSetsService: TimePlanningPnPayRuleSetsService,
    private toastrService: ToastrService,
    public dialogRef: MatDialogRef<PayRuleSetsCreateModalComponent>,
    @Optional() @Inject(MAT_DIALOG_DATA) public data: { existingNames: string[] } | null
  ) {}

  ngOnInit(): void {
    this.initForm();
    const existingNames = this.data?.existingNames || [];
    this.availablePresets = PAY_RULE_SET_PRESETS.filter(
      p => !existingNames.includes(p.name)
    );
  }

  get isLocked(): boolean {
    return this.selectedPreset?.locked ?? false;
  }

  get presetGroups(): string[] {
    const groups = new Set(this.availablePresets.map(p => p.group));
    return Array.from(groups);
  }

  getPresetsForGroup(group: string): PayRuleSetPreset[] {
    return this.availablePresets.filter(p => p.group === group);
  }

  onPresetChanged(preset: PayRuleSetPreset | null): void {
    this.selectedPreset = preset;

    // Clear existing form arrays
    this.payDayRulesFormArray.clear();
    this.payDayTypeRulesFormArray.clear();

    if (!preset) {
      this.form.get('name')?.setValue('');
      return;
    }

    // Set name
    this.form.get('name')?.setValue(preset.name);

    // Populate payDayRules
    for (const rule of preset.payDayRules) {
      const ruleForm = this.createPayDayRuleFormGroup({
        dayCode: rule.dayCode,
        payTierRules: rule.payTierRules.map(t => ({
          order: t.order,
          upToSeconds: t.upToSeconds,
          payCode: t.payCode,
        })),
      });
      this.payDayRulesFormArray.push(ruleForm);
    }

    // Populate payDayTypeRules
    for (const rule of preset.payDayTypeRules) {
      const ruleForm = this.createDayTypeRuleFormGroup({
        dayType: rule.dayType,
        defaultPayCode: rule.defaultPayCode,
        priority: rule.priority,
        timeBandRules: rule.timeBandRules.map(b => ({
          startSecondOfDay: b.startSecondOfDay,
          endSecondOfDay: b.endSecondOfDay,
          payCode: b.payCode,
          priority: b.priority,
        })),
      });
      this.payDayTypeRulesFormArray.push(ruleForm);
    }
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
      .join(' \u2192 ');
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
    if (!this.isLocked && this.form.invalid) {
      return;
    }

    const model = new PayRuleSetCreateModel();
    model.name = this.isLocked ? this.selectedPreset!.name : this.form.get('name')?.value;
    model.payDayRules = this.payDayRulesFormArray.value;
    model.payDayTypeRules = this.payDayTypeRulesFormArray.value;

    this.payRuleSetsService.createPayRuleSet(model).subscribe({
      next: (response) => {
        this.toastrService.success('Pay rule set created successfully');
        this.dialogRef.close(true);
      },
      error: (error) => {
        console.error('Create error:', error);
        this.toastrService.error('Failed to create pay rule set');
      }
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
