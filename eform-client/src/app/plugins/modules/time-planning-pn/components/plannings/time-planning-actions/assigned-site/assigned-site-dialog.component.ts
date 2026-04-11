import {Component, DoCheck, OnInit,
  inject
} from '@angular/core';
import {
  MAT_DIALOG_DATA
} from '@angular/material/dialog';
import {AssignedSiteModel, CommonTagModel, GlobalAutoBreakSettingsModel, PayRuleSetSimpleModel} from '../../../../models';
import {selectCurrentUserIsAdmin, selectCurrentUserIsFirstUser} from 'src/app/state';
import {Store} from '@ngrx/store';
import {TimePlanningPnSettingsService, TimePlanningPnPayRuleSetsService} from 'src/app/plugins/modules/time-planning-pn/services';
import {
  AbstractControl,
  FormBuilder,
  FormGroup,
  ValidationErrors,
  Validators,
  ReactiveFormsModule,
  FormControl,
} from '@angular/forms';


@Component({
  selector: 'app-assigned-site-dialog',
  templateUrl: './assigned-site-dialog.component.html',
  styleUrls: ['./assigned-site-dialog.component.scss'],
  standalone: false
})
export class AssignedSiteDialogComponent implements DoCheck, OnInit {
  private fb = inject(FormBuilder);
  public data = inject<AssignedSiteModel>(MAT_DIALOG_DATA);
  private timePlanningPnSettingsService = inject(TimePlanningPnSettingsService);
  private payRuleSetsService = inject(TimePlanningPnPayRuleSetsService);
  private store = inject(Store);

  assignedSiteForm!: FormGroup;

  public selectCurrentUserIsAdmin$ = this.store.select(selectCurrentUserIsAdmin);
  public selectCurrentUserIsFirstUser$ = this.store.select(selectCurrentUserIsFirstUser);
  private previousData: AssignedSiteModel;
  private globalAutoBreakSettings: GlobalAutoBreakSettingsModel;
  public availableTags: CommonTagModel[] = [];
  public availablePayRuleSets: PayRuleSetSimpleModel[] = [];

  ngDoCheck(): void {
    if (this.hasDataChanged()) {
      // this.calculateHours();
      // this.previousData = { ...this.data };
    }
  }

  ngOnInit(): void {
    this.previousData = {...this.data};
    // this.calculateHours();
    this.timePlanningPnSettingsService.getGlobalAutoBreakCalculationSettings().subscribe(result => {
      if (result && result.success) {
        this.globalAutoBreakSettings = result.model;
      }
    });

    // Load available tags from eForm core API via service
    this.loadAvailableTags();

    // Load available pay rule sets
    this.loadPayRuleSets();

    if (!this.data.resigned) {
      const today = new Date();
      today.setHours(0, 0, 0, 0);
      this.data.resignedAtDate = today.toISOString();
    }

    const days = ['monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday', 'sunday'];

    // autoBreakSettings group
    const autoBreakGroup = days.reduce((acc, day) => {
      acc[day] = this.fb.group({
        breakMinutesDivider: new FormControl(this.getConvertedValue(this.data[`${day}BreakMinutesDivider`]) ?? null),
        breakMinutesPrDivider: new FormControl(this.getConvertedValue(this.data[`${day}BreakMinutesPrDivider`]) ?? null),
        breakMinutesUpperLimit: new FormControl(this.getConvertedValue(this.data[`${day}BreakMinutesUpperLimit`]) ?? null),
      });
      return acc;
    }, {} as { [key: string]: FormGroup });

    // planHours group
    const planHoursGroup = days.reduce((acc, day) => {
      acc[day] = new FormControl(this.data[`${day}PlanHours`] ?? null);
      return acc;
    }, {} as { [key: string]: FormControl });

    // 1st Shift
    const firstShiftGroup = days.reduce((acc, day) => {
      const startRaw = this.data[`start${this.capitalize(day)}`] ?? null;
      const endRaw = this.data[`end${this.capitalize(day)}`] ?? null;
      const breakRaw = this.data[`break${this.capitalize(day)}`] ?? null;

      acc[day] = this.fb.group({
        start: new FormControl(this.getConvertedValue(startRaw, endRaw)),
        end: new FormControl(this.getConvertedValue(endRaw)),
        break: new FormControl(this.getConvertedValue(breakRaw)),
        calculatedHours: new FormControl({ value: this.data[`${day.toLowerCase()}CalculatedHours`] ?? null, disabled: true }),
      });
      return acc;
    }, {} as { [key: string]: FormGroup });

    // 2nd Shift
    const secondShiftGroup = days.reduce((acc, day) => {
      const startRaw = this.data[`start${this.capitalize(day)}2NdShift`] ?? null;
      const endRaw = this.data[`end${this.capitalize(day)}2NdShift`] ?? null;
      const breakRaw = this.data[`break${this.capitalize(day)}2NdShift`] ?? null;

      acc[day.toLowerCase()] = this.fb.group({
        start: new FormControl(this.getConvertedValue(startRaw, endRaw)),
        end: new FormControl(this.getConvertedValue(endRaw)),
        break: new FormControl(this.getConvertedValue(breakRaw)),
        calculatedHours: new FormControl({ value: this.data[`${day.toLowerCase()}CalculatedHours`] ?? null, disabled: true }),
      });
      return acc;
    }, {} as { [key: string]: FormGroup });

    // 3rd Shift
    const thirdShiftGroup = days.reduce((acc, day) => {
      const startRaw = this.data[`start${this.capitalize(day)}3RdShift`] ?? null;
      const endRaw = this.data[`end${this.capitalize(day)}3RdShift`] ?? null;
      const breakRaw = this.data[`break${this.capitalize(day)}3RdShift`] ?? null;

      acc[day.toLowerCase()] = this.fb.group({
        start: new FormControl(this.getConvertedValue(startRaw, endRaw)),
        end: new FormControl(this.getConvertedValue(endRaw)),
        break: new FormControl(this.getConvertedValue(breakRaw)),
        calculatedHours: new FormControl({ value: this.data[`${day.toLowerCase()}CalculatedHours`] ?? null, disabled: true }),
      });
      return acc;
    }, {} as { [key: string]: FormGroup });

    // 4th Shift
    const fourthShiftGroup = days.reduce((acc, day) => {
      const startRaw = this.data[`start${this.capitalize(day)}4ThShift`] ?? null;
      const endRaw = this.data[`end${this.capitalize(day)}4ThShift`] ?? null;
      const breakRaw = this.data[`break${this.capitalize(day)}4ThShift`] ?? null;

      acc[day.toLowerCase()] = this.fb.group({
        start: new FormControl(this.getConvertedValue(startRaw, endRaw)),
        end: new FormControl(this.getConvertedValue(endRaw)),
        break: new FormControl(this.getConvertedValue(breakRaw)),
        calculatedHours: new FormControl({ value: this.data[`${day.toLowerCase()}CalculatedHours`] ?? null, disabled: true }),
      });
      return acc;
    }, {} as { [key: string]: FormGroup });

    // 5th Shift
    const fifthShiftGroup = days.reduce((acc, day) => {
      const startRaw = this.data[`start${this.capitalize(day)}5ThShift`] ?? null;
      const endRaw = this.data[`end${this.capitalize(day)}5ThShift`] ?? null;
      const breakRaw = this.data[`break${this.capitalize(day)}5ThShift`] ?? null;

      acc[day.toLowerCase()] = this.fb.group({
        start: new FormControl(this.getConvertedValue(startRaw, endRaw)),
        end: new FormControl(this.getConvertedValue(endRaw)),
        break: new FormControl(this.getConvertedValue(breakRaw)),
        // calculatedHours: new FormControl(this.data[`${day.toLowerCase()}CalculatedHours`] ?? null),
        calculatedHours: new FormControl({ value: this.data[`${day.toLowerCase()}CalculatedHours`] ?? null, disabled: true }),
      });
      return acc;
    }, {} as { [key: string]: FormGroup });

    this.assignedSiteForm = this.fb.group({
      useGoogleSheetAsDefault: new FormControl(this.data.useGoogleSheetAsDefault),
      useOnlyPlanHours: new FormControl(this.data.useOnlyPlanHours),
      autoBreakCalculationActive: new FormControl(this.data.autoBreakCalculationActive),
      allowPersonalTimeRegistration: new FormControl(this.data.allowPersonalTimeRegistration),
      allowEditOfRegistrations: new FormControl(this.data.allowEditOfRegistrations),
      usePunchClock: new FormControl(this.data.usePunchClock),
      usePunchClockWithAllowRegisteringInHistory: new FormControl(this.data.usePunchClockWithAllowRegisteringInHistory),
      allowAcceptOfPlannedHours: new FormControl(this.data.allowAcceptOfPlannedHours),
      daysBackInTimeAllowedEditingEnabled: new FormControl(this.data.daysBackInTimeAllowedEditingEnabled),
      thirdShiftActive: new FormControl(this.data.thirdShiftActive),
      fourthShiftActive: new FormControl(this.data.fourthShiftActive),
      fifthShiftActive: new FormControl(this.data.fifthShiftActive),
      resigned: new FormControl(this.data.resigned),
      resignedAtDate: new FormControl(
        this.data.resigned ? new Date(this.data.resignedAtDate) : new Date(),
        this.data.resigned ? Validators.required : null
      ),
      isManager: new FormControl(this.data.isManager ?? false),
      managingTagIds: new FormControl(this.data.managingTagIds ?? []),
      payRuleSetId: new FormControl(this.data.payRuleSetId ?? null),
      planHours: this.fb.group(planHoursGroup),
      autoBreakSettings: this.fb.group(autoBreakGroup),
      firstShift: this.fb.group(firstShiftGroup),
      secondShift: this.fb.group(secondShiftGroup),
      thirdShift: this.fb.group(thirdShiftGroup),
      fourthShift: this.fb.group(fourthShiftGroup),
      fifthShift: this.fb.group(fifthShiftGroup),
    }, {
      validators: [
      ],
    },);

    this.assignedSiteForm.valueChanges.subscribe(formValue => {
      Object.assign(this.data, formValue);
    });

    // Normalize mutually-exclusive flag combinations that old flat-checkbox
    // data might contain (e.g. both usePunchClock and allowAcceptOfPlannedHours
    // set to true). Derives the current radio value and writes it back so the
    // stored flags match the displayed selection exactly.
    this.onEntryMethodChange(this.entryMethod);
    this.onEditingPolicyChange(this.editingPolicy);

    this.calculateHours();

    // Re-baseline after normalization: the setValue calls above fired
    // valueChanges, which Object.assigns the whole form back into `this.data`
    // (including form controls initialised from a fresh Date object). That
    // counts as a difference from the previousData snapshot captured before
    // the form was built, so hasDataChanged() would return true immediately
    // after ngOnInit. Capture again here so it reflects the true baseline.
    this.previousData = {...this.data};
  }

  setAutoBreakValue(day: string, control: string, value: string) {
    const fg = this.assignedSiteForm.get('autoBreakSettings') as FormGroup;
    fg.get(day)?.get(control)?.setValue(value, { emitEvent: true });
  }

  private capitalize(str: string) {
    return str.charAt(0).toUpperCase() + str.slice(1);
  }

  hasDataChanged(): boolean {
    return JSON.stringify(this.data) !== JSON.stringify(this.previousData);
  }

  calculateHours(): void {
    const f = this.assignedSiteForm;

    f.get('mondayCalculatedHours')?.setValue(
      this.calculateDayHours(
        f.get('startMonday')?.value,
        f.get('endMonday')?.value,
        f.get('breakMonday')?.value,
        f.get('startMonday2NdShift')?.value,
        f.get('endMonday2NdShift')?.value,
        f.get('breakMonday2NdShift')?.value
      )
    );

    f.get('tuesdayCalculatedHours')?.setValue(
      this.calculateDayHours(
        f.get('startTuesday')?.value,
        f.get('endTuesday')?.value,
        f.get('breakTuesday')?.value,
        f.get('startTuesday2NdShift')?.value,
        f.get('endTuesday2NdShift')?.value,
        f.get('breakTuesday2NdShift')?.value
      )
    );

    f.get('wednesdayCalculatedHours')?.setValue(
      this.calculateDayHours(
        f.get('startWednesday')?.value,
        f.get('endWednesday')?.value,
        f.get('breakWednesday')?.value,
        f.get('startWednesday2NdShift')?.value,
        f.get('endWednesday2NdShift')?.value,
        f.get('breakWednesday2NdShift')?.value
      )
    );

    f.get('thursdayCalculatedHours')?.setValue(
      this.calculateDayHours(
        f.get('startThursday')?.value,
        f.get('endThursday')?.value,
        f.get('breakThursday')?.value,
        f.get('startThursday2NdShift')?.value,
        f.get('endThursday2NdShift')?.value,
        f.get('breakThursday2NdShift')?.value
      )
    );

    f.get('fridayCalculatedHours')?.setValue(
      this.calculateDayHours(
        f.get('startFriday')?.value,
        f.get('endFriday')?.value,
        f.get('breakFriday')?.value,
        f.get('startFriday2NdShift')?.value,
        f.get('endFriday2NdShift')?.value,
        f.get('breakFriday2NdShift')?.value
      )
    );

    f.get('saturdayCalculatedHours')?.setValue(
      this.calculateDayHours(
        f.get('startSaturday')?.value,
        f.get('endSaturday')?.value,
        f.get('breakSaturday')?.value,
        f.get('startSaturday2NdShift')?.value,
        f.get('endSaturday2NdShift')?.value,
        f.get('breakSaturday2NdShift')?.value
      )
    );

    f.get('sundayCalculatedHours')?.setValue(
      this.calculateDayHours(
        f.get('startSunday')?.value,
        f.get('endSunday')?.value,
        f.get('breakSunday')?.value,
        f.get('startSunday2NdShift')?.value,
        f.get('endSunday2NdShift')?.value,
        f.get('breakSunday2NdShift')?.value
      )
    );
  }

  calculateDayHours(
    start: number,
    end: number,
    breakTime: number,
    start2NdShift: number,
    end2NdShift: number,
    break2NdShift: number
  ): string {
    const firstShiftMinutes = this.calculateShiftMinutes(start, end, breakTime);
    const secondShiftMinutes = this.calculateShiftMinutes(start2NdShift, end2NdShift, break2NdShift);
    const totalMinutes = firstShiftMinutes + secondShiftMinutes;

    return this.formatMinutesAsTime(totalMinutes);
  }

  private calculateShiftMinutes(start: number, end: number, breakTime: number): number {
    if (!start || !end) {
      return 0;
    }
    return (end - start - (breakTime || 0)) / 60;
  }

  private formatMinutesAsTime(totalMinutes: number): string {
    const hours = Math.floor(totalMinutes);
    const minutes = Math.round((totalMinutes - hours) * 60);
    return `${hours}:${minutes}`;
  }

  onTimeChange(event: any, field: string): void {
    const time = event.split(':');
    const minutes = (+time[0] * 60) + (+time[1]);
    this.data[field] = minutes;
  }

  getConvertedValue(minutes: number, compareMinutes?: number): string {
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    let result = `${this.padZero(hours)}:${this.padZero(mins)}`;
    if (result === '00:00' && (compareMinutes === 0 || compareMinutes === undefined || compareMinutes === null)) {
      result = '';
    }
    return result;
  }

  setMinutes(event: any, field: string): void {
    let value: string;
    if (event && event.target) {
      value = (event.target as HTMLInputElement).value;
    } else {
      value = event;
    }
    if (!value) {
      this.data[field] = 0;
    } else {
      const [hours, mins] = value.split(':').map(Number);
      this.data[field] = (hours * 60) + mins;
    }
    this.calculateHours();
    this.previousData = { ...this.data };
  }

  updateAssignedSite() {
    const f = this.assignedSiteForm;

    f.get('mondayPlanHours')?.setValue(
      (f.get('startMonday')?.value && f.get('endMonday')?.value
        ? f.get('endMonday')?.value - f.get('startMonday')?.value - f.get('breakMonday')?.value
        : 0) +
      (f.get('startMonday2NdShift')?.value && f.get('endMonday2NdShift')?.value
        ? f.get('endMonday2NdShift')?.value - f.get('startMonday2NdShift')?.value - f.get('breakMonday2NdShift')?.value
        : 0) +
      (f.get('startMonday3RdShift')?.value && f.get('endMonday3RdShift')?.value
        ? f.get('endMonday3RdShift')?.value - f.get('startMonday3RdShift')?.value - f.get('breakMonday3RdShift')?.value
        : 0) +
      (f.get('startMonday4ThShift')?.value && f.get('endMonday4ThShift')?.value
        ? f.get('endMonday4ThShift')?.value - f.get('startMonday4ThShift')?.value - f.get('breakMonday4ThShift')?.value
        : 0) +
      (f.get('startMonday5ThShift')?.value && f.get('endMonday5ThShift')?.value
        ? f.get('endMonday5ThShift')?.value - f.get('startMonday5ThShift')?.value - f.get('breakMonday5ThShift')?.value
        : 0)
    );

    f.get('tuesdayPlanHours')?.setValue(
      (f.get('startTuesday')?.value && f.get('endTuesday')?.value
        ? f.get('endTuesday')?.value - f.get('startTuesday')?.value - f.get('breakTuesday')?.value
        : 0) +
      (f.get('startTuesday2NdShift')?.value && f.get('endTuesday2NdShift')?.value
        ? f.get('endTuesday2NdShift')?.value - f.get('startTuesday2NdShift')?.value - f.get('breakTuesday2NdShift')?.value
        : 0) +
      (f.get('startTuesday3RdShift')?.value && f.get('endTuesday3RdShift')?.value
        ? f.get('endTuesday3RdShift')?.value - f.get('startTuesday3RdShift')?.value - f.get('breakTuesday3RdShift')?.value
        : 0) +
      (f.get('startTuesday4ThShift')?.value && f.get('endTuesday4ThShift')?.value
        ? f.get('endTuesday4ThShift')?.value - f.get('startTuesday4ThShift')?.value - f.get('breakTuesday4ThShift')?.value
        : 0) +
      (f.get('startTuesday5ThShift')?.value && f.get('endTuesday5ThShift')?.value
        ? f.get('endTuesday5ThShift')?.value - f.get('startTuesday5ThShift')?.value - f.get('breakTuesday5ThShift')?.value
        : 0)
    );

    f.get('wednesdayPlanHours')?.setValue(
      (f.get('startWednesday')?.value && f.get('endWednesday')?.value
        ? f.get('endWednesday')?.value - f.get('startWednesday')?.value - f.get('breakWednesday')?.value
        : 0) +
      (f.get('startWednesday2NdShift')?.value && f.get('endWednesday2NdShift')?.value
        ? f.get('endWednesday2NdShift')?.value - f.get('startWednesday2NdShift')?.value - f.get('breakWednesday2NdShift')?.value
        : 0) +
      (f.get('startWednesday3RdShift')?.value && f.get('endWednesday3RdShift')?.value
        ? f.get('endWednesday3RdShift')?.value - f.get('startWednesday3RdShift')?.value - f.get('breakWednesday3RdShift')?.value
        : 0) +
      (f.get('startWednesday4ThShift')?.value && f.get('endWednesday4ThShift')?.value
        ? f.get('endWednesday4ThShift')?.value - f.get('startWednesday4ThShift')?.value - f.get('breakWednesday4ThShift')?.value
        : 0) +
      (f.get('startWednesday5ThShift')?.value && f.get('endWednesday5ThShift')?.value
        ? f.get('endWednesday5ThShift')?.value - f.get('startWednesday5ThShift')?.value - f.get('breakWednesday5ThShift')?.value
        : 0)
    );

    f.get('thursdayPlanHours')?.setValue(
      (f.get('startThursday')?.value && f.get('endThursday')?.value
        ? f.get('endThursday')?.value - f.get('startThursday')?.value - f.get('breakThursday')?.value
        : 0) +
      (f.get('startThursday2NdShift')?.value && f.get('endThursday2NdShift')?.value
        ? f.get('endThursday2NdShift')?.value - f.get('startThursday2NdShift')?.value - f.get('breakThursday2NdShift')?.value
        : 0) +
      (f.get('startThursday3RdShift')?.value && f.get('endThursday3RdShift')?.value
        ? f.get('endThursday3RdShift')?.value - f.get('startThursday3RdShift')?.value - f.get('breakThursday3RdShift')?.value
        : 0) +
      (f.get('startThursday4ThShift')?.value && f.get('endThursday4ThShift')?.value
        ? f.get('endThursday4ThShift')?.value - f.get('startThursday4ThShift')?.value - f.get('breakThursday4ThShift')?.value
        : 0) +
      (f.get('startThursday5ThShift')?.value && f.get('endThursday5ThShift')?.value
        ? f.get('endThursday5ThShift')?.value - f.get('startThursday5ThShift')?.value - f.get('breakThursday5ThShift')?.value
        : 0)
    );

    f.get('fridayPlanHours')?.setValue(
      (f.get('startFriday')?.value && f.get('endFriday')?.value
        ? f.get('endFriday')?.value - f.get('startFriday')?.value - f.get('breakFriday')?.value
        : 0) +
      (f.get('startFriday2NdShift')?.value && f.get('endFriday2NdShift')?.value
        ? f.get('endFriday2NdShift')?.value - f.get('startFriday2NdShift')?.value - f.get('breakFriday2NdShift')?.value
        : 0) +
      (f.get('startFriday3RdShift')?.value && f.get('endFriday3RdShift')?.value
        ? f.get('endFriday3RdShift')?.value - f.get('startFriday3RdShift')?.value - f.get('breakFriday3RdShift')?.value
        : 0) +
      (f.get('startFriday4ThShift')?.value && f.get('endFriday4ThShift')?.value
        ? f.get('endFriday4ThShift')?.value - f.get('startFriday4ThShift')?.value - f.get('breakFriday4ThShift')?.value
        : 0) +
      (f.get('startFriday5ThShift')?.value && f.get('endFriday5ThShift')?.value
        ? f.get('endFriday5ThShift')?.value - f.get('startFriday5ThShift')?.value - f.get('breakFriday5ThShift')?.value
        : 0)
    );

    f.get('saturdayPlanHours')?.setValue(
      (f.get('startSaturday')?.value && f.get('endSaturday')?.value
        ? f.get('endSaturday')?.value - f.get('startSaturday')?.value - f.get('breakSaturday')?.value
        : 0) +
      (f.get('startSaturday2NdShift')?.value && f.get('endSaturday2NdShift')?.value
        ? f.get('endSaturday2NdShift')?.value - f.get('startSaturday2NdShift')?.value - f.get('breakSaturday2NdShift')?.value
        : 0) +
      (f.get('startSaturday3RdShift')?.value && f.get('endSaturday3RdShift')?.value
        ? f.get('endSaturday3RdShift')?.value - f.get('startSaturday3RdShift')?.value - f.get('breakSaturday3RdShift')?.value
        : 0) +
      (f.get('startSaturday4ThShift')?.value && f.get('endSaturday4ThShift')?.value
        ? f.get('endSaturday4ThShift')?.value - f.get('startSaturday4ThShift')?.value - f.get('breakSaturday4ThShift')?.value
        : 0)
    );

    f.get('sundayPlanHours')?.setValue(
      (f.get('startSunday')?.value && f.get('endSunday')?.value
        ? f.get('endSunday')?.value - f.get('startSunday')?.value - f.get('breakSunday')?.value
        : 0) +
      (f.get('startSunday2NdShift')?.value && f.get('endSunday2NdShift')?.value
        ? f.get('endSunday2NdShift')?.value - f.get('startSunday2NdShift')?.value - f.get('breakSunday2NdShift')?.value
        : 0) +
      (f.get('startSunday3RdShift')?.value && f.get('endSunday3RdShift')?.value
        ? f.get('endSunday3RdShift')?.value - f.get('startSunday3RdShift')?.value - f.get('breakSunday3RdShift')?.value
        : 0) +
      (f.get('startSunday4ThShift')?.value && f.get('endSunday4ThShift')?.value
        ? f.get('endSunday4ThShift')?.value - f.get('startSunday4ThShift')?.value - f.get('breakSunday4ThShift')?.value
        : 0) +
      (f.get('startSunday5ThShift')?.value && f.get('endSunday5ThShift')?.value
        ? f.get('endSunday5ThShift')?.value - f.get('startSunday5ThShift')?.value - f.get('breakSunday5ThShift')?.value
        : 0)
    );
  }

  padZero(num: number): string {
    return num < 10 ? `0${num}` : `${num}`;
  }

  copyBreakSettings(day: string) {
    if (!this.globalAutoBreakSettings) {return;}

    const fg = this.assignedSiteForm.get('autoBreakSettings') as FormGroup;
    const dayGroup = fg.get(day) as FormGroup;

    switch (day) {
      case 'monday':
        dayGroup.patchValue({
          breakMinutesDivider: this.getConvertedValue(this.globalAutoBreakSettings.mondayBreakMinutesDivider),
          breakMinutesPrDivider: this.getConvertedValue(this.globalAutoBreakSettings.mondayBreakMinutesPrDivider),
          breakMinutesUpperLimit: this.getConvertedValue(this.globalAutoBreakSettings.mondayBreakMinutesUpperLimit),
        });
        break;
      case 'tuesday':
        dayGroup.patchValue({
          breakMinutesDivider: this.getConvertedValue(this.globalAutoBreakSettings.tuesdayBreakMinutesDivider),
          breakMinutesPrDivider: this.getConvertedValue(this.globalAutoBreakSettings.tuesdayBreakMinutesPrDivider),
          breakMinutesUpperLimit: this.getConvertedValue(this.globalAutoBreakSettings.tuesdayBreakMinutesUpperLimit),
        });
        break;
      case 'wednesday':
        dayGroup.patchValue({
          breakMinutesDivider: this.getConvertedValue(this.globalAutoBreakSettings.wednesdayBreakMinutesDivider),
          breakMinutesPrDivider: this.getConvertedValue(this.globalAutoBreakSettings.wednesdayBreakMinutesPrDivider),
          breakMinutesUpperLimit: this.getConvertedValue(this.globalAutoBreakSettings.wednesdayBreakMinutesUpperLimit),
        });
        break;
      case 'thursday':
        dayGroup.patchValue({
          breakMinutesDivider: this.getConvertedValue(this.globalAutoBreakSettings.thursdayBreakMinutesDivider),
          breakMinutesPrDivider: this.getConvertedValue(this.globalAutoBreakSettings.thursdayBreakMinutesPrDivider),
          breakMinutesUpperLimit: this.getConvertedValue(this.globalAutoBreakSettings.thursdayBreakMinutesUpperLimit),
        });
        break;
      case 'friday':
        dayGroup.patchValue({
          breakMinutesDivider: this.getConvertedValue(this.globalAutoBreakSettings.fridayBreakMinutesDivider),
          breakMinutesPrDivider: this.getConvertedValue(this.globalAutoBreakSettings.fridayBreakMinutesPrDivider),
          breakMinutesUpperLimit: this.getConvertedValue(this.globalAutoBreakSettings.fridayBreakMinutesUpperLimit),
        });
        break;
      case 'saturday':
        dayGroup.patchValue({
          breakMinutesDivider: this.getConvertedValue(this.globalAutoBreakSettings.saturdayBreakMinutesDivider),
          breakMinutesPrDivider: this.getConvertedValue(this.globalAutoBreakSettings.saturdayBreakMinutesPrDivider),
          breakMinutesUpperLimit: this.getConvertedValue(this.globalAutoBreakSettings.saturdayBreakMinutesUpperLimit),
        });
        break;
      case 'sunday':
        dayGroup.patchValue({
          breakMinutesDivider: this.getConvertedValue(this.globalAutoBreakSettings.sundayBreakMinutesDivider),
          breakMinutesPrDivider: this.getConvertedValue(this.globalAutoBreakSettings.sundayBreakMinutesPrDivider),
          breakMinutesUpperLimit: this.getConvertedValue(this.globalAutoBreakSettings.sundayBreakMinutesUpperLimit),
        });
        break;
    }
  }

  getPlanHoursFormGroup(): FormGroup {
    return this.assignedSiteForm.get('planHours') as FormGroup;
  }

  getAutoBreakSettingsFormGroup(): FormGroup {
    return this.assignedSiteForm.get('autoBreakSettings') as FormGroup;
  }

  getFirstShiftFormGroup(): FormGroup {
    return this.assignedSiteForm.get('firstShift') as FormGroup;
  }

  getSecondShiftFormGroup(): FormGroup {
    return this.assignedSiteForm.get('secondShift') as FormGroup;
  }

  getThirdShiftFormGroup(): FormGroup {
    return this.assignedSiteForm.get('thirdShift') as FormGroup;
  }

  getFourthShiftFormGroup(): FormGroup {
    return this.assignedSiteForm.get('fourthShift') as FormGroup;
  }

  getFifthShiftFormGroup(): FormGroup {
    return this.assignedSiteForm.get('fifthShift') as FormGroup;
  }

  /**
   * Axis 1 — how working time is captured.
   * Maps the two underlying flags (usePunchClock, allowAcceptOfPlannedHours)
   * onto a single 3-value radio control. These flags are mutually exclusive
   * in the UI by construction.
   */
  get entryMethod(): 'manual' | 'punchClock' | 'acceptPlanned' {
    if (this.data.usePunchClock) {
      return 'punchClock';
    }
    if (this.data.allowAcceptOfPlannedHours) {
      return 'acceptPlanned';
    }
    return 'manual';
  }

  onEntryMethodChange(value: 'manual' | 'punchClock' | 'acceptPlanned'): void {
    const f = this.assignedSiteForm;
    const punch = value === 'punchClock';
    const accept = value === 'acceptPlanned';
    f.get('usePunchClock')?.setValue(punch);
    f.get('allowAcceptOfPlannedHours')?.setValue(accept);
    // Mirror onto this.data immediately so template *ngIf bindings that read
    // from data (instead of form) react in the same change-detection tick.
    this.data.usePunchClock = punch;
    this.data.allowAcceptOfPlannedHours = accept;
    // The "allow entry of forgotten days" sub-option only makes sense under punch clock
    if (!punch) {
      f.get('usePunchClockWithAllowRegisteringInHistory')?.setValue(false);
      this.data.usePunchClockWithAllowRegisteringInHistory = false;
    }
  }

  /**
   * Axis 2 — editing policy for past registrations.
   * Maps two boolean flags (allowEditOfRegistrations, daysBackInTimeAllowedEditingEnabled)
   * onto a single 3-value radio control:
   *   locked          → both false
   *   untilPayroll    → allowEditOfRegistrations=true, daysBack=false
   *   twoDaysRolling  → both true
   */
  get editingPolicy(): 'locked' | 'untilPayroll' | 'twoDaysRolling' {
    if (this.data.daysBackInTimeAllowedEditingEnabled) {
      return 'twoDaysRolling';
    }
    if (this.data.allowEditOfRegistrations) {
      return 'untilPayroll';
    }
    return 'locked';
  }

  onEditingPolicyChange(value: 'locked' | 'untilPayroll' | 'twoDaysRolling'): void {
    const f = this.assignedSiteForm;
    const allowEdit = value !== 'locked';
    const daysBack = value === 'twoDaysRolling';
    f.get('allowEditOfRegistrations')?.setValue(allowEdit);
    f.get('daysBackInTimeAllowedEditingEnabled')?.setValue(daysBack);
    this.data.allowEditOfRegistrations = allowEdit;
    this.data.daysBackInTimeAllowedEditingEnabled = daysBack;
  }

  loadPayRuleSets(): void {
    this.payRuleSetsService.getPayRuleSets({offset: 0, pageSize: 1000}).subscribe({
      next: (result) => {
        if (result && result.success) {
          this.availablePayRuleSets = result.model?.payRuleSets || [];
        }
      },
      error: (error) => {
        console.error('Error loading pay rule sets:', error);
        this.availablePayRuleSets = [];
      }
    });
  }

  loadAvailableTags(): void {
    this.timePlanningPnSettingsService.getAvailableTags().subscribe({
      next: (result) => {
        if (result && result.success) {
          this.availableTags = result.model || [];
        }
      },
      error: (error) => {
        console.error('Error loading tags:', error);
        this.availableTags = [];
      }
    });
  }
}
