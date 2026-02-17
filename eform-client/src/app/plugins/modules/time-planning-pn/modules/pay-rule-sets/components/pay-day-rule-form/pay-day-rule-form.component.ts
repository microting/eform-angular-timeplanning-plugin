import {Component, Input, OnInit} from '@angular/core';
import {FormGroup, FormControl, Validators, FormArray, FormBuilder, AbstractControl, ValidationErrors} from '@angular/forms';

@Component({
  selector: 'app-pay-day-rule-form',
  standalone: false,
  templateUrl: './pay-day-rule-form.component.html',
  styleUrls: ['./pay-day-rule-form.component.scss']
})
export class PayDayRuleFormComponent implements OnInit {
  @Input() payDayRuleForm!: FormGroup;

  daysOfWeek = [
    {value: 0, label: 'Sunday'},
    {value: 1, label: 'Monday'},
    {value: 2, label: 'Tuesday'},
    {value: 3, label: 'Wednesday'},
    {value: 4, label: 'Thursday'},
    {value: 5, label: 'Friday'},
    {value: 6, label: 'Saturday'}
  ];

  constructor(private fb: FormBuilder) {}

  ngOnInit(): void {
    if (!this.payDayRuleForm) {
      this.payDayRuleForm = this.createPayDayRuleForm();
    }
  }

  createPayDayRuleForm(): FormGroup {
    return this.fb.group({
      id: [null],
      dayOfWeek: [null, Validators.required],
      payTierRules: this.fb.array([], this.percentageSumValidator)
    });
  }

  get payTierRules(): FormArray {
    return this.payDayRuleForm.get('payTierRules') as FormArray;
  }

  get dayOfWeek() {
    return this.payDayRuleForm.get('dayOfWeek');
  }

  percentageSumValidator(control: AbstractControl): ValidationErrors | null {
    const formArray = control as FormArray;
    
    if (formArray.length === 0) {
      return null; // Allow empty array
    }

    const total = formArray.controls
      .map(c => c.get('tierPercent')?.value || 0)
      .reduce((sum, val) => sum + val, 0);

    return total === 100 ? null : {percentageSum: true};
  }

  addTier(): void {
    const nextTierNumber = this.payTierRules.length + 1;
    const tierForm = this.fb.group({
      id: [null],
      tierNumber: [nextTierNumber, Validators.required],
      tierPercent: [0, [Validators.required, Validators.min(0), Validators.max(100)]],
      payCodeId: [null, Validators.required]
    });
    this.payTierRules.push(tierForm);
  }

  deleteTier(index: number): void {
    this.payTierRules.removeAt(index);
    // Renumber remaining tiers
    this.payTierRules.controls.forEach((control, idx) => {
      control.get('tierNumber')?.setValue(idx + 1);
    });
  }

  getTierPercent(index: number): number {
    return this.payTierRules.at(index).get('tierPercent')?.value || 0;
  }

  getTierNumber(index: number): number {
    return this.payTierRules.at(index).get('tierNumber')?.value || 0;
  }

  getPayCodeId(index: number): number | null {
    return this.payTierRules.at(index).get('payCodeId')?.value;
  }

  getTierFormGroup(index: number): FormGroup {
    return this.payTierRules.at(index) as FormGroup;
  }

  get totalPercent(): number {
    return this.payTierRules.controls
      .map(c => c.get('tierPercent')?.value || 0)
      .reduce((sum, val) => sum + val, 0);
  }
}
