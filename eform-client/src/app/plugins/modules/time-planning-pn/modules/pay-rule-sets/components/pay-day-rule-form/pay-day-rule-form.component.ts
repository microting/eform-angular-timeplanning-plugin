import {Component, Input, OnInit} from '@angular/core';
import {FormGroup, FormControl, Validators, FormArray, FormBuilder, AbstractControl, ValidationErrors} from '@angular/forms';
import {MatTableDataSource} from '@angular/material/table';

@Component({
  selector: 'app-pay-day-rule-form',
  standalone: false,
  templateUrl: './pay-day-rule-form.component.html',
  styleUrls: ['./pay-day-rule-form.component.scss']
})
export class PayDayRuleFormComponent implements OnInit {
  @Input() payDayRuleForm!: FormGroup;

  dataSource = new MatTableDataSource<AbstractControl>();
  displayedColumns: string[] = ['order', 'upToSeconds', 'payCode', 'actions'];

  dayCodes = [
    {value: 'MONDAY', label: 'Monday'},
    {value: 'TUESDAY', label: 'Tuesday'},
    {value: 'WEDNESDAY', label: 'Wednesday'},
    {value: 'THURSDAY', label: 'Thursday'},
    {value: 'FRIDAY', label: 'Friday'},
    {value: 'SATURDAY', label: 'Saturday'},
    {value: 'SUNDAY', label: 'Sunday'},
    {value: 'WEEKDAY', label: 'Weekday'},
    {value: 'WEEKEND', label: 'Weekend'},
    {value: 'HOLIDAY', label: 'Holiday'},
    {value: 'GRUNDLOVSDAG', label: 'Grundlovsdag'}
  ];

  constructor(private fb: FormBuilder) {}

  ngOnInit(): void {
    if (!this.payDayRuleForm) {
      this.payDayRuleForm = this.createPayDayRuleForm();
    }
    this.updateTable();
  }

  createPayDayRuleForm(): FormGroup {
    return this.fb.group({
      id: [null],
      dayCode: [null, Validators.required],
      payTierRules: this.fb.array([])
    });
  }

  get payTierRules(): FormArray {
    return this.payDayRuleForm.get('payTierRules') as FormArray;
  }

  get dayCode() {
    return this.payDayRuleForm.get('dayCode');
  }

  updateTable(): void {
    this.dataSource.data = this.payTierRules.controls;
  }

  addTier(): void {
    const nextOrder = this.payTierRules.length + 1;
    const tierForm = this.fb.group({
      id: [null],
      order: [nextOrder, Validators.required],
      upToSeconds: [null, [Validators.min(0)]],
      payCode: ['', Validators.required]
    });
    this.payTierRules.push(tierForm);
    this.updateTable();
    this.payDayRuleForm.markAsDirty();
    this.payDayRuleForm.updateValueAndValidity();
  }

  deleteTier(index: number): void {
    this.payTierRules.removeAt(index);
    // Renumber remaining tiers
    this.payTierRules.controls.forEach((control, idx) => {
      control.get('order')?.setValue(idx + 1);
    });
    this.updateTable();
    this.payDayRuleForm.markAsDirty();
    this.payDayRuleForm.updateValueAndValidity();
  }

  getOrder(index: number): number {
    return this.payTierRules.at(index).get('order')?.value || 0;
  }

  getUpToSeconds(index: number): number | null {
    return this.payTierRules.at(index).get('upToSeconds')?.value;
  }

  getPayCode(index: number): string {
    return this.payTierRules.at(index).get('payCode')?.value || '';
  }

  getTierFormGroup(index: number): FormGroup {
    return this.payTierRules.at(index) as FormGroup;
  }

  getTierControl(index: number, controlName: string): FormControl {
    return this.payTierRules.at(index).get(controlName) as FormControl;
  }

  formatSeconds(seconds: number | null): string {
    if (seconds === null || seconds === undefined) {
      return 'No limit';
    }
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    if (minutes > 0) {
      return `${hours}h ${minutes}m`;
    }
    return `${hours}h`;
  }
}
