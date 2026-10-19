import {Component, Input, OnInit} from '@angular/core';
import {FormGroup, FormControl, Validators, FormArray, FormBuilder, AbstractControl, ValidationErrors, ReactiveFormsModule} from '@angular/forms';
import {MatTableDataSource, MatTableModule} from '@angular/material/table';
import {TranslateService, TranslatePipe} from '@ngx-translate/core';
import {secondsToHM} from '../../pay-rule-format.util';
import {NgIf, NgFor} from '@angular/common';
import {MatIcon} from '@angular/material/icon';
import {MatIconButton} from '@angular/material/button';
import {MatFormField, MatLabel, MatError, MatHint} from '@angular/material/form-field';
import {MatInput} from '@angular/material/input';
import {MatTooltip} from '@angular/material/tooltip';
import {MatSelect} from '@angular/material/select';
import {MatOption} from '@angular/material/core';
import {HhMmSecondsDirective} from '../../directives/hh-mm-seconds.directive';

@Component({
  selector: 'app-pay-day-rule-form',
  imports: [NgIf, NgFor, ReactiveFormsModule, TranslatePipe, MatIcon, MatIconButton, MatFormField, MatLabel, MatError, MatHint, MatInput, MatTooltip, MatSelect, MatOption, MatTableModule, HhMmSecondsDirective],
  templateUrl: './pay-day-rule-form.component.html',
  styleUrls: ['./pay-day-rule-form.component.scss']
})
export class PayDayRuleFormComponent implements OnInit {
  @Input() payDayRuleForm!: FormGroup;

  dataSource = new MatTableDataSource<AbstractControl>();
  displayedColumns: string[] = ['order', 'upToSeconds', 'payCode', 'payrollCode', 'actions'];

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

  constructor(private fb: FormBuilder, private translateService: TranslateService) {}

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
      payCode: ['', Validators.required],
      payrollCode: ['']
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

  /**
   * Subscript hint for the "Up To" field: the human reading of the stored
   * seconds, or the translated "Unlimited" wording when the tier is unbounded.
   * While the typed text is malformed the hint stays empty — the control still
   * holds its previous value, and any wording there would describe something
   * other than what the field shows.
   */
  getUpToHint(index: number): string {
    if (this.getTierControl(index, 'upToSeconds').hasError('hhMmFormat')) {
      return '';
    }
    const seconds = this.getUpToSeconds(index);
    if (seconds === null || seconds === undefined) {
      return this.translateService.instant('Unlimited');
    }
    return secondsToHM(seconds);
  }
}
