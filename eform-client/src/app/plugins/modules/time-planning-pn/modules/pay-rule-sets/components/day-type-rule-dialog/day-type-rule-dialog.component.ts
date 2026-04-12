import { Component, Inject, OnInit } from '@angular/core';
import { AbstractControl, FormArray, FormBuilder, FormControl, FormGroup, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatTableDataSource } from '@angular/material/table';

export interface DayTypeRuleDialogData {
  mode: 'create' | 'edit';
  rule?: any;
}

@Component({
  selector: 'app-day-type-rule-dialog',
  standalone: false,
  templateUrl: './day-type-rule-dialog.component.html',
  styleUrls: ['./day-type-rule-dialog.component.scss']
})
export class DayTypeRuleDialogComponent implements OnInit {
  dayTypeRuleForm!: FormGroup;

  timeBandDataSource = new MatTableDataSource<AbstractControl>();
  timeBandColumns: string[] = ['startTime', 'endTime', 'payCode', 'payrollCode', 'priority', 'actions'];

  dayTypes = [
    { value: 'Monday', label: 'Monday' },
    { value: 'Tuesday', label: 'Tuesday' },
    { value: 'Wednesday', label: 'Wednesday' },
    { value: 'Thursday', label: 'Thursday' },
    { value: 'Friday', label: 'Friday' },
    { value: 'Saturday', label: 'Saturday' },
    { value: 'Sunday', label: 'Sunday' },
    { value: 'Holiday', label: 'Holiday' }
  ];

  constructor(
    private fb: FormBuilder,
    public dialogRef: MatDialogRef<DayTypeRuleDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: DayTypeRuleDialogData
  ) {}

  ngOnInit(): void {
    this.initForm();
    if (this.data.mode === 'edit' && this.data.rule) {
      this.patchFormValues();
    }
    this.updateTimeBandTable();
  }

  private initForm(): void {
    this.dayTypeRuleForm = this.fb.group({
      id: [null],
      dayType: [null, Validators.required],
      defaultPayCode: ['', Validators.required],
      priority: [0, [Validators.required, Validators.min(0)]],
      timeBandRules: this.fb.array([])
    });
  }

  private patchFormValues(): void {
    if (this.data.rule) {
      this.dayTypeRuleForm.patchValue({
        id: this.data.rule.id || null,
        dayType: this.data.rule.dayType || null,
        defaultPayCode: this.data.rule.defaultPayCode || '',
        priority: this.data.rule.priority ?? 0
      });

      if (this.data.rule.timeBandRules && Array.isArray(this.data.rule.timeBandRules)) {
        this.data.rule.timeBandRules.forEach((band: any) => {
          this.timeBandRulesArray.push(this.createTimeBandFormGroup(band));
        });
        this.updateTimeBandTable();
      }
    }
  }

  get dialogTitle(): string {
    return this.data.mode === 'create' ? 'Add Day Type Rule' : 'Edit Day Type Rule';
  }

  get timeBandRulesArray(): FormArray {
    return this.dayTypeRuleForm.get('timeBandRules') as FormArray;
  }

  // --- Time Band Rules management ---

  addTimeBand(): void {
    this.timeBandRulesArray.push(this.createTimeBandFormGroup({}));
    this.updateTimeBandTable();
    this.dayTypeRuleForm.markAsDirty();
  }

  deleteTimeBand(index: number): void {
    this.timeBandRulesArray.removeAt(index);
    this.updateTimeBandTable();
    this.dayTypeRuleForm.markAsDirty();
  }

  private createTimeBandFormGroup(band: any): FormGroup {
    return this.fb.group({
      id: [band.id || null],
      startSecondOfDay: [band.startSecondOfDay ?? null],
      endSecondOfDay: [band.endSecondOfDay ?? null],
      startTime: [this.secondsToTimeString(band.startSecondOfDay), Validators.required],
      endTime: [this.secondsToTimeString(band.endSecondOfDay), Validators.required],
      payCode: [band.payCode || '', Validators.required],
      payrollCode: [band.payrollCode || ''],
      priority: [band.priority ?? 0, [Validators.required, Validators.min(0)]]
    });
  }

  updateTimeBandTable(): void {
    this.timeBandDataSource.data = this.timeBandRulesArray.controls;
  }

  getTimeBandControl(index: number, controlName: string): FormControl {
    return this.timeBandRulesArray.at(index).get(controlName) as FormControl;
  }

  // --- Time conversion helpers ---

  secondsToTimeString(seconds: number | null | undefined): string {
    if (seconds === null || seconds === undefined) {
      return '';
    }
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    return `${hours.toString().padStart(2, '0')}:${minutes.toString().padStart(2, '0')}`;
  }

  timeStringToSeconds(time: string): number | null {
    if (!time) {
      return null;
    }
    const parts = time.split(':');
    if (parts.length < 2) {
      return null;
    }
    const hours = parseInt(parts[0], 10);
    const minutes = parseInt(parts[1], 10);
    return (hours * 3600) + (minutes * 60);
  }

  onStartTimeChanged(index: number, time: string): void {
    this.getTimeBandControl(index, 'startTime').setValue(time);
    const seconds = this.timeStringToSeconds(time);
    this.getTimeBandControl(index, 'startSecondOfDay').setValue(seconds);
  }

  onEndTimeChanged(index: number, time: string): void {
    this.getTimeBandControl(index, 'endTime').setValue(time);
    const seconds = this.timeStringToSeconds(time);
    this.getTimeBandControl(index, 'endSecondOfDay').setValue(seconds);
  }

  save(): void {
    if (this.dayTypeRuleForm.valid) {
      // Ensure seconds are synced from time strings before saving
      for (let i = 0; i < this.timeBandRulesArray.length; i++) {
        const startTime = this.getTimeBandControl(i, 'startTime').value;
        const endTime = this.getTimeBandControl(i, 'endTime').value;
        this.getTimeBandControl(i, 'startSecondOfDay').setValue(this.timeStringToSeconds(startTime));
        this.getTimeBandControl(i, 'endSecondOfDay').setValue(this.timeStringToSeconds(endTime));
      }

      // Strip the display-only startTime/endTime fields before returning
      const value = this.dayTypeRuleForm.value;
      value.timeBandRules = value.timeBandRules.map((band: any) => ({
        id: band.id,
        startSecondOfDay: band.startSecondOfDay,
        endSecondOfDay: band.endSecondOfDay,
        payCode: band.payCode,
        payrollCode: band.payrollCode,
        priority: band.priority
      }));
      this.dialogRef.close(value);
    }
  }

  cancel(): void {
    this.dialogRef.close();
  }
}

