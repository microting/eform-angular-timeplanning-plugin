import {Component, OnInit, TemplateRef, ViewChild,
  inject
} from '@angular/core';
import {MAT_DIALOG_DATA, MatDialog} from '@angular/material/dialog';
import {TranslateService} from '@ngx-translate/core';
import {DatePipe} from '@angular/common';
import {TimePlanningMessagesEnum} from '../../../../enums';
import {AssignedSiteModel, PlanningPrDayModel} from '../../../../models';
import {MtxGridColumn} from '@ng-matero/extensions/grid';
import {TimePlanningPnPlanningsService} from '../../../../services';
import {VersionHistoryModalComponent} from '../version-history-modal/version-history-modal.component';
import {Store} from '@ngrx/store';
import {selectCurrentUserIsFirstUser} from 'src/app/state';
import validator from 'validator';

import {
  AbstractControl,
  FormBuilder,
  FormControl,
  FormGroup,
  ValidationErrors,
  Validators,
  ReactiveFormsModule,
  FormArray,
} from '@angular/forms';

@Component({
  selector: 'app-workday-entity-dialog',
  templateUrl: './workday-entity-dialog.component.html',
  styleUrls: ['./workday-entity-dialog.component.scss'],
  standalone: false
})
export class WorkdayEntityDialogComponent implements OnInit {
  private fb = inject(FormBuilder);
  private planningsService = inject(TimePlanningPnPlanningsService);
  private dialog = inject(MatDialog);
  private store = inject(Store);
  public data = inject<{
      planningPrDayModels: PlanningPrDayModel,
      assignedSiteModel: AssignedSiteModel
    }>(MAT_DIALOG_DATA);
  protected datePipe = inject(DatePipe);
  private translateService = inject(TranslateService);

  public selectCurrentUserIsFirstUser$ = this.store.select(selectCurrentUserIsFirstUser);

  TimePlanningMessagesEnum = TimePlanningMessagesEnum;
  enumKeys: string[] = [];
  tableHeaders: MtxGridColumn[] = [];
  shiftData: any[] = [];

  // Reactive form
  workdayForm!: FormGroup;

  // UI / beregningsfelter
  isInTheFuture = false;
  maxPause1Id = 0;
  maxPause2Id = 0;
  todaysFlex = 0;
  nettoHoursOverrideActive = false;
  date: any;

  // fejltekst til stop2 (bruges sammen med gruppevalidator)
  stop2Error: string | null = null;
  stop3Error: string | null = null;
  stop4Error: string | null = null;
  stop5Error: string | null = null;

  @ViewChild('plannedColumnTemplate', {static: true}) plannedColumnTemplate!: TemplateRef<any>;
  @ViewChild('actualColumnTemplate', {static: true}) actualColumnTemplate!: TemplateRef<any>;
  protected readonly JSON = JSON;
  private readonly timeRegex = /^([01]\d|2[0-3]):([0-5]\d)$/;
  inputErrorMessages: Record<string, Record<string, string>> = {};



  ngOnInit(): void {
    // Enum-opsætning
    this.enumKeys = Object.keys(TimePlanningMessagesEnum).filter(key => isNaN(Number(key)));
    this.nettoHoursOverrideActive = this.data.planningPrDayModels.nettoHoursOverrideActive;


    const m = this.data.planningPrDayModels;

    const normalizeTwoDecimals = (value: any) => {
      if (value === null || value === undefined || isNaN(value)) {return 0.00;}
      const rounded = Number(value).toFixed(2);
      return rounded === '-0.00' ? 0.00 : Number(rounded);
    };

    m.sumFlexStart = normalizeTwoDecimals(m.sumFlexStart);
    m.sumFlexEnd = normalizeTwoDecimals(m.sumFlexEnd);
    // m.planHours = normalizeTwoDecimals(m.planHours);
    // m.actualHours = normalizeTwoDecimals(m.actualHours);
    m.paidOutFlex = normalizeTwoDecimals(m.paidOutFlex);

    if (this.data.planningPrDayModels.message) {
      this.data.planningPrDayModels[this.enumKeys[this.data.planningPrDayModels.message - 1]] = true;
    }

    // Konverter modelværdier til "HH:mm" strenge
    const plannedStartOfShift1 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedStartOfShift1);
    const plannedEndOfShift1 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedEndOfShift1);
    const plannedBreakOfShift1 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedBreakOfShift1);

    const plannedStartOfShift2 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedStartOfShift2);
    const plannedEndOfShift2 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedEndOfShift2);
    const plannedBreakOfShift2 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedBreakOfShift2);

    const plannedStartOfShift3 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedStartOfShift3);
    const plannedEndOfShift3 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedEndOfShift3);
    const plannedBreakOfShift3 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedBreakOfShift3);

    const plannedStartOfShift4 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedStartOfShift4);
    const plannedEndOfShift4 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedEndOfShift4);
    const plannedBreakOfShift4 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedBreakOfShift4);

    const plannedStartOfShift5 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedStartOfShift5);
    const plannedEndOfShift5 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedEndOfShift5);
    const plannedBreakOfShift5 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedBreakOfShift5);

    const start1StartedAt = this.datePipe.transform(this.data.planningPrDayModels.start1StartedAt, 'HH:mm', 'UTC');
    const stop1StoppedAt = this.datePipe.transform(this.data.planningPrDayModels.stop1StoppedAt, 'HH:mm', 'UTC');
    const pause1Id = this.convertMinutesToTime(this.data.planningPrDayModels.pause1Id * 5);

    const start2StartedAt = this.datePipe.transform(this.data.planningPrDayModels.start2StartedAt, 'HH:mm', 'UTC');
    const stop2StoppedAt = this.datePipe.transform(this.data.planningPrDayModels.stop2StoppedAt, 'HH:mm', 'UTC');
    const pause2Id = this.convertMinutesToTime(this.data.planningPrDayModels.pause2Id * 5);

    const start3StartedAt = this.datePipe.transform(this.data.planningPrDayModels.start3StartedAt, 'HH:mm', 'UTC');
    const stop3StoppedAt = this.datePipe.transform(this.data.planningPrDayModels.stop3StoppedAt, 'HH:mm', 'UTC');
    const pause3Id = this.convertMinutesToTime(this.data.planningPrDayModels.pause3Id * 5);

    const start4StartedAt = this.datePipe.transform(this.data.planningPrDayModels.start4StartedAt, 'HH:mm', 'UTC');
    const stop4StoppedAt = this.datePipe.transform(this.data.planningPrDayModels.stop4StoppedAt, 'HH:mm', 'UTC');
    const pause4Id = this.convertMinutesToTime(this.data.planningPrDayModels.pause4Id * 5);

    const start5StartedAt = this.datePipe.transform(this.data.planningPrDayModels.start5StartedAt, 'HH:mm', 'UTC');
    const stop5StoppedAt = this.datePipe.transform(this.data.planningPrDayModels.stop5StoppedAt, 'HH:mm', 'UTC');
    const pause5Id = this.convertMinutesToTime(this.data.planningPrDayModels.pause5Id * 5);

    // Er dato i fremtiden?
    this.isInTheFuture = Date.parse(this.data.planningPrDayModels.date) > Date.now();
    this.todaysFlex = this.data.planningPrDayModels.actualHours - this.data.planningPrDayModels.planHours;
    this.date = Date.parse(this.data.planningPrDayModels.date);

    const flagsGroup: { [k: string]: FormControl<boolean> } = {};
    this.enumKeys.forEach(k => {
      if (k !== 'Blank' && k !== 'Care') {
        flagsGroup[k] = new FormControl<boolean>(!!this.data.planningPrDayModels[k]);
      }
    });

    // Byg formstruktur
    this.workdayForm = this.fb.group({
      planned: this.fb.group({
        shift1: this.fb.group(
          {
            start: new FormControl({value: plannedStartOfShift1, disabled: false}, [this.timeValidator]),
            break: new FormControl({value: plannedBreakOfShift1, disabled: false}, [this.timeValidator]),
            stop: new FormControl({value: plannedEndOfShift1, disabled: false}, [this.timeValidator]),
          }, {validators: [this.plannedShiftDurationValidator.bind(this)]}
        ),
        shift2: this.fb.group({
            start: new FormControl({value: plannedStartOfShift2, disabled: false}),
            break: new FormControl({value: plannedBreakOfShift2, disabled: false}),
            stop: new FormControl({value: plannedEndOfShift2, disabled: false}),
          },
          {validators: [this.plannedShiftDurationValidator.bind(this)]},),
        shift3: this.fb.group({
            start: new FormControl({value: plannedStartOfShift3, disabled: false}),
            break: new FormControl({value: plannedBreakOfShift3, disabled: false}),
            stop: new FormControl({value: plannedEndOfShift3, disabled: false}),
          },
          {validators: [this.plannedShiftDurationValidator.bind(this)]},),
        shift4: this.fb.group({
            start: new FormControl({value: plannedStartOfShift4, disabled: false}),
            break: new FormControl({value: plannedBreakOfShift4, disabled: false}),
            stop: new FormControl({value: plannedEndOfShift4, disabled: false}),
          },
          {validators: [this.plannedShiftDurationValidator.bind(this)]},),
        shift5: this.fb.group({
            start: new FormControl({value: plannedStartOfShift5, disabled: false}),
            break: new FormControl({value: plannedBreakOfShift5, disabled: false}),
            stop: new FormControl({value: plannedEndOfShift5, disabled: false}),
          },
          {validators: [this.plannedShiftDurationValidator.bind(this)]},),
      }),
      actual: this.fb.group({
        shift1: this.fb.group({
            start: new FormControl({value: start1StartedAt, disabled: this.isInTheFuture}),
            pause: new FormControl({value: pause1Id, disabled: this.isInTheFuture}),
            stop: new FormControl({value: stop1StoppedAt, disabled: this.isInTheFuture}),
          },
          {validators: [this.actualShiftDurationValidator.bind(this)]},),
        shift2: this.fb.group({
            start: new FormControl({value: start2StartedAt, disabled: this.isInTheFuture}),
            pause: new FormControl({value: pause2Id, disabled: this.isInTheFuture}),
            stop: new FormControl({value: stop2StoppedAt, disabled: this.isInTheFuture}),
          },
          {validators: [this.actualShiftDurationValidator.bind(this)]},
        ),
        shift3: this.fb.group({
            start: new FormControl({value: start3StartedAt, disabled: this.isInTheFuture}),
            pause: new FormControl({value: pause3Id, disabled: this.isInTheFuture}),
            stop: new FormControl({value: stop3StoppedAt, disabled: this.isInTheFuture}),
          },
          {validators: [this.actualShiftDurationValidator.bind(this)]},
        ),
        shift4: this.fb.group({
            start: new FormControl({value: start4StartedAt, disabled: this.isInTheFuture}),
            pause: new FormControl({value: pause4Id, disabled: this.isInTheFuture}),
            stop: new FormControl({value: stop4StoppedAt, disabled: this.isInTheFuture}),
          },
          {validators: [this.actualShiftDurationValidator.bind(this)]},
        ),
        shift5: this.fb.group({
            start: new FormControl({value: start5StartedAt, disabled: this.isInTheFuture}),
            pause: new FormControl({value: pause5Id, disabled: this.isInTheFuture}),
            stop: new FormControl({value: stop5StoppedAt, disabled: this.isInTheFuture}),
          },
          {validators: [this.actualShiftDurationValidator.bind(this)]},
        ),
      }),
      planHours: new FormControl({
        value: this.data.planningPrDayModels.planHours ?? null,
        disabled: this.isInTheFuture
      }, {validators: [this.numberValidator, this.maxPlanHoursValidator]},),
      nettoHoursOverride: new FormControl(this.data.planningPrDayModels.nettoHoursOverride ?? null),
      paidOutFlex: new FormControl(this.data.planningPrDayModels.paidOutFlex ?? null),
      commentOffice: new FormControl(this.data.planningPrDayModels.commentOffice ?? null),

      flags: this.fb.group(flagsGroup),
    }, {
      validators: [
        this.totalHoursValidator.bind(this),
        this.shiftWiseValidator.bind(this),
      ],
    },);

    // Tabellens kolonner
    this.tableHeaders = this.data.assignedSiteModel.useOnlyPlanHours ? [
      {header: this.translateService.stream('Shift'), field: 'shift', pinned: 'left'},
      {
        cellTemplate: this.actualColumnTemplate,
        header: this.translateService.stream('Registered'),
        field: 'actualStart',
        sortable: false
      },
    ] : this.isInTheFuture ? [
      {header: this.translateService.stream('Shift'), field: 'shift', pinned: 'left'},
      {
        cellTemplate: this.plannedColumnTemplate,
        header: this.translateService.stream('Planned'),
        field: 'plannedStart',
        sortable: false
      },
    ] : [
      {header: this.translateService.stream('Shift'), field: 'shift', pinned: 'left'},
      {
        cellTemplate: this.plannedColumnTemplate,
        header: this.translateService.stream('Planned'),
        field: 'plannedStart',
        sortable: false
      },
      {
        cellTemplate: this.actualColumnTemplate,
        header: this.translateService.stream('Registered'),
        field: 'actualStart',
        sortable: false
      },
    ];

    // Byg shiftData fra formværdier
    const get = (path: string) => this.workdayForm.get(path)?.value || null;

    const shift1Data = {
      shiftId: '1',
      shift: this.translateService.instant('1st'),
      plannedStart: get('planned.shift1.start'),
      plannedEnd: get('planned.shift1.stop'),
      plannedBreak: get('planned.shift1.break'),
      actualStart: get('actual.shift1.start'),
      actualEnd: get('actual.shift1.stop'),
      actualBreak: get('actual.shift1.pause'),
    };
    const shift2Data = {
      shiftId: '2',
      shift: this.translateService.instant('2nd'),
      plannedStart: get('planned.shift2.start'),
      plannedEnd: get('planned.shift2.stop'),
      plannedBreak: get('planned.shift2.break'),
      actualStart: get('actual.shift2.start'),
      actualEnd: get('actual.shift2.stop'),
      actualBreak: get('actual.shift2.pause'),
    };
    const shift3Data = {
      shiftId: '3',
      shift: this.translateService.instant('3rd'),
      plannedStart: get('planned.shift3.start'),
      plannedEnd: get('planned.shift3.stop'),
      plannedBreak: get('planned.shift3.break'),
      actualStart: get('actual.shift3.start'),
      actualEnd: get('actual.shift3.stop'),
      actualBreak: get('actual.shift3.pause'),
    };
    const shift4Data = {
      shiftId: '4',
      shift: this.translateService.instant('4th'),
      plannedStart: get('planned.shift4.start'),
      plannedEnd: get('planned.shift4.stop'),
      plannedBreak: get('planned.shift4.break'),
      actualStart: get('actual.shift4.start'),
      actualEnd: get('actual.shift4.stop'),
      actualBreak: get('actual.shift4.pause'),
    };
    const shift5Data = {
      shiftId: '5',
      shift: this.translateService.instant('5th'),
      plannedStart: get('planned.shift5.start'),
      plannedEnd: get('planned.shift5.stop'),
      plannedBreak: get('planned.shift5.break'),
      actualStart: get('actual.shift5.start'),
      actualEnd: get('actual.shift5.stop'),
      actualBreak: get('actual.shift5.pause'),
    };

    this.shiftData = [shift1Data, shift2Data];
    if (this.data.assignedSiteModel.thirdShiftActive) {
      this.shiftData.push(shift3Data);
    }
    if (this.data.assignedSiteModel.fourthShiftActive) {
      this.shiftData.push(shift4Data);
    }
    if (this.data.assignedSiteModel.fifthShiftActive) {
      this.shiftData.push(shift5Data);
    }

    this.updateDisabledStates();
  }

  // inside class:
  private getCtrl(path: string): FormControl {
    return this.workdayForm.get(path) as FormControl;
  }

  private setDisabled(path: string, disabled: boolean) {
    const c = this.getCtrl(path);
    if (!c) {
      return;
    }
    if (disabled && c.enabled) {
      c.disable({emitEvent: false});
    }
    if (!disabled && c.disabled) {
      c.enable({emitEvent: false});
    }
  }

  // validators
  private timeValidator(control: AbstractControl): ValidationErrors | null {
    const value = control.value;
    if (!value || value === '00:00') {
      return null;
    }

    if (!validator.matches(value, /^([01]\d|2[0-3]):([0-5]\d)$/)) {
      return {invalidTime: true};
    }
    return null;
  }

  getMinutes(time: string | null): number {
    if (!time || !validator.matches(time, this.timeRegex)) {
      return 0;
    }
    const [h, m] = time.split(':').map(Number);
    return h * 60 + m;
  }

  private plannedShiftDurationValidator(group: AbstractControl): ValidationErrors | null {
    const startControl = group.get('start');
    const stopControl = group.get('stop');
    const breakControl = group.get('break');

    const start = startControl?.value as string;
    const stop = stopControl?.value as string;
    const brk = breakControl?.value as string;

    if (!start || !stop) {
      return null;
    }

    const startMin = this.getMinutes(start);
    const stopMin = this.getMinutes(stop);
    const breakMin = this.getMinutes(brk);

    const setError = (control: AbstractControl | null, key: string, value: any = true) => {
      if (!control) {
        return;
      }
      const errors = control.errors || {};
      errors[key] = value;
      control.setErrors(errors);
    };

    const removeError = (control: AbstractControl | null, key: string) => {
      if (!control || !control.errors) {
        return;
      }
      const errors = {...control.errors};
      delete errors[key];
      control.setErrors(Object.keys(errors).length ? errors : null);
    };

    if (!start || !stop) {
      if (!start) {
        setError(startControl, 'required', this.translateService.instant('Start time is required'));
      } else {
        removeError(startControl, 'required');
      }
      if (!stop) {
        setError(stopControl, 'required', this.translateService.instant('Stop time is required'));
      } else {
        removeError(stopControl, 'required');
      }
      return null;
    }

    // Validate same start/stop
    if (startMin === stopMin && (startMin !== 0 && stopMin !== 0)) {
      setError(startControl, 'sameStartStop', this.translateService.instant('Start and Stop cannot be the same'));
      setError(stopControl, 'sameStartStop', this.translateService.instant('Start and Stop cannot be the same'));
    } else {
      removeError(startControl, 'sameStartStop');
      removeError(stopControl, 'sameStartStop');
    }

    // Till midnight sameday
    const adjustedStop = stopMin === 0 ? 1440 : stopMin;
    let duration = adjustedStop - startMin;
    if (duration <= 0) {
      duration += 1440;
    }

    // Stop before start
    if (stopMin !== 0 && stopMin < startMin) {
      setError(stopControl, 'invalidRange', this.translateService.instant('Stop time cannot be before start time'));
    } else {
      removeError(stopControl, 'invalidRange');
    }

    // Break time validation
    if (breakMin !== null) {
      if (breakMin < 0) {
        setError(breakControl, 'negativeBreak', this.translateService.instant('Break cannot be negative'));
      } else {
        removeError(breakControl, 'negativeBreak');
      }

      if (breakMin >= duration) {
        setError(
          breakControl,
          'breakTooLong',
          this.translateService.instant('Break cannot be equal or longer than shift duration'),
        );
      } else {
        removeError(breakControl, 'breakTooLong');
      }
    }

    if (duration > 24 * 60) {
      setError(group, 'shiftTooLong', this.translateService.instant('Shift duration cannot exceed 24 hours'));
    } else {
      removeError(group, 'shiftTooLong');
    }

    if (breakMin && breakMin >= duration) {
      setError(breakControl, 'invalidBreak', this.translateService.instant('Break must be shorter than shift duration'));
    } else {
      removeError(breakControl, 'invalidBreak');
    }

    return null;
  }

  private actualShiftDurationValidator(group: AbstractControl): ValidationErrors | null {
    const startControl = group.get('start');
    const stopControl = group.get('stop');
    const breakControl = group.get('pause');

    const start = startControl?.value as string;
    const stop = stopControl?.value as string;
    const brk = breakControl?.value as string;

    if (!start || !stop) {
      return null;
    }

    const startMin = this.getMinutes(start);
    const stopMin = this.getMinutes(stop);
    const breakMin = this.getMinutes(brk);

    const setError = (control: AbstractControl | null, key: string, value: any = true) => {
      if (!control) {
        return;
      }
      const errors = control.errors || {};
      errors[key] = value;
      control.setErrors(errors);
    };

    const removeError = (control: AbstractControl | null, key: string) => {
      if (!control || !control.errors) {
        return;
      }
      const errors = {...control.errors};
      delete errors[key];
      control.setErrors(Object.keys(errors).length ? errors : null);
    };

    if (!start || !stop) {
      if (!start) {
        setError(startControl, 'required', this.translateService.instant('Start time is required'));
      } else {
        removeError(startControl, 'required');
      }
      if (!stop) {
        setError(stopControl, 'required', this.translateService.instant('Stop time is required'));
      } else {
        removeError(stopControl, 'required');
      }
      return null;
    }

    // Validate same start/stop
    if (startMin === stopMin && (startMin !== 0 && stopMin !== 0)) {
      setError(startControl, 'sameStartStop', this.translateService.instant('Start and Stop cannot be the same'));
      setError(stopControl, 'sameStartStop', this.translateService.instant('Start and Stop cannot be the same'));
    } else {
      removeError(startControl, 'sameStartStop');
      removeError(stopControl, 'sameStartStop');
    }

    // Till midnight sameday
    const adjustedStop = stopMin === 0 ? 1440 : stopMin;
    let duration = adjustedStop - startMin;
    if (duration <= 0) {
      duration += 1440;
    }

    // Stop before start
    if (stopMin !== 0 && stopMin < startMin) {
      setError(stopControl, 'invalidRange', this.translateService.instant('Stop time cannot be before start time'));
    } else {
      removeError(stopControl, 'invalidRange');
    }

    // Break time validation
    if (breakMin !== null) {
      if (breakMin < 0) {
        setError(breakControl, 'negativeBreak', this.translateService.instant('Break cannot be negative'));
      } else {
        removeError(breakControl, 'negativeBreak');
      }

      if (breakMin >= duration) {
        setError(
          breakControl,
          'breakTooLong',
          this.translateService.instant('Break cannot be equal or longer than shift duration'),);
      } else {
        removeError(breakControl, 'breakTooLong');
      }
    }

    if (duration > 24 * 60) {
      setError(group, 'shiftTooLong', this.translateService.instant('Shift duration cannot exceed 24 hours'));
    } else {
      removeError(group, 'shiftTooLong');
    }

    if (breakMin && breakMin >= duration) {
      setError(breakControl, 'invalidBreak', this.translateService.instant('Break must be shorter than shift duration'));
    } else {
      removeError(breakControl, 'invalidBreak');
    }

    return null;
  }

  private numberValidator(control: AbstractControl): ValidationErrors | null {
    const value = control.value;
    if (value === null || value === undefined || value === '') {
      return null;
    }
    return isNaN(value) ? {notNumber: true} : null;
  }

  public getGroupErrorMessage(path: string): string | null {
    const group = this.workdayForm.get(path);
    if (!group || !group.errors) {
      return null;
    }
    const firstKey = Object.keys(group.errors)[0];
    return group.errors[firstKey];
  }

  // InputErrorMessages dynamically
  public getInputErrors(controlPath: string): string[] {
    const ctrl = this.workdayForm.get(controlPath);

    if (!ctrl) {
      return [];
    }

    const errors = ctrl.errors ?? {};

    const messages = this.inputErrorMessages[controlPath] || {};

    return Object.keys(errors).map((key) => messages[key] || errors[key]);
  }

  // max plan hour
  private maxPlanHoursValidator = (control: AbstractControl): ValidationErrors | null => {
    const raw = control.value;
    if (raw === null || raw === undefined || raw === '') {
      return null;
    }
    const v = parseFloat(String(raw).replace(',', '.'));
    if (Number.isNaN(v)) {
      return {notNumber: true};
    }
    return v > 24 ? {tooManyHours: true} : null;
  };

  private totalHoursValidator(form: AbstractControl): ValidationErrors | null {
    let totalMinutes = 0;
    for (let i = 1; i <= 5; i++) {
      const shift = form.get(`planned.shift${i}`)?.value;
      if (!shift) {
        continue;
      }
      const start = this.getMinutes(shift.start);
      const stop = this.getMinutes(shift.stop);
      const brk = this.getMinutes(shift.break);
      totalMinutes += this.getPlannedShiftMinutes(start, stop, brk);
    }
    const totalHours = totalMinutes / 60;
    return totalHours > 24 ? {tooManyHours: 'Total planned hours cannot exceed 24'} : null;
  }

  private parseTimeToMinutes(time: string): number | null {
    if (!time) {
      return null;
    }
    const [h, m] = time.split(':').map(Number);
    if (isNaN(h) || isNaN(m) || h < 0 || h > 23 || m < 0 || m > 59) {
      return null;
    }
    return h * 60 + m;
  }

  private shiftWiseValidator(form: AbstractControl): ValidationErrors | null {
    const planned = form.get('planned') as FormGroup;
    const actual = form.get('actual') as FormGroup;
    const shifts = ['shift1', 'shift2', 'shift3', 'shift4', 'shift5'];

    let formError: string | null = null;

    const validateShifts = (group: FormGroup, label: string) => {
      for (let i = 1; i < shifts.length; i++) {
        const prevShift = group.get(shifts[i - 1]);
        const currShift = group.get(shifts[i]);

        if (!prevShift || !currShift) {
          continue;
        }
        const prevEnd = this.parseTimeToMinutes(prevShift.get('stop')?.value);
        const currStart = this.parseTimeToMinutes(currShift.get('start')?.value);

        // Disallow 00:00 as start for shifts > 1
        if (i > 0 && currStart === 0) {
          currShift.get('start')?.setErrors({
            ...(currShift.get('start')?.errors || {}),
            invalidStart: `Start time 00:00 is not allowed for ${label} Shift ${i + 1}`,
          });

          if (!formError) {
            formError = `${label} Shift ${i + 1} cannot start at 00:00`;
          }
        } else {
          const errors = currShift.get('start')?.errors;
          if (errors && errors['invalidStart']) {
            delete errors['invalidStart'];
            currShift.get('start')?.setErrors(Object.keys(errors).length ? errors : null);
          }
        }

        if (prevEnd !== null && (currStart !== null && currStart !== 0)) {
          if (currStart < prevEnd) {
            currShift.get('start')?.setErrors({
              hierarchyError: this.translateService.instant('Start time cannot be earlier than previous shift`s end time'),
            });

            if (!formError) {
              formError = `${label} Shift ${i + 1} cannot start before ${label} Shift ${i} ends`;
            }
          } else {
            const errors = currShift.get('start')?.errors;
            if (errors) {
              delete errors['hierarchyError'];
              currShift.get('start')?.setErrors(Object.keys(errors).length ? errors : null);
            }
          }
        }
      }
    };

    // Validate both planned and actual
    if (planned) {
      validateShifts(planned, 'Planned');
    }
    if (actual) {
      validateShifts(actual, 'Actual');
    }

    return formError ? {shiftOrder: formError} : null;
  }

  private updateDisabledStates() {
    // Planned
    const p1Start = this.getCtrl('planned.shift1.start').value as string | null;
    const p1Stop = this.getCtrl('planned.shift1.stop').value as string | null;
    const p2Start = this.getCtrl('planned.shift2.start').value as string | null;
    const p2Stop = this.getCtrl('planned.shift2.stop').value as string | null;

    const isSet = (time: string | null) => !!time;// && time !== '00:00';

    // Plan hours enabled only if shift 1 start & stop are empty or 00:00
    if ((!p1Start && !p1Stop) || (p1Start === '00:00' && p1Stop === '00:00')) {
      this.setDisabled('planHours', false);
    } else {
      this.setDisabled('planHours', true);
    }

    // Shift 1
    if (isSet(p1Start) || p1Start === '00:00') {
      this.setDisabled('planned.shift1.stop', false);
    } else {
      this.setDisabled('planned.shift1.stop', true);
    }

    if (isSet(p1Stop)) {
      this.setDisabled('planned.shift1.break', false);
      if (p1Stop !== '00:00') {
        this.setDisabled('planned.shift2.start', false);
      } else {
        this.setDisabled('planned.shift2.start', true);
      }
    } else {
      this.setDisabled('planned.shift1.break', true);
      this.setDisabled('planned.shift2.start', true);
    }

    // Shift 2
    if (isSet(p2Start)) {
      this.setDisabled('planned.shift2.stop', false);
    } else {
      this.setDisabled('planned.shift2.stop', true);
    }

    if (isSet(p2Stop)) {
      this.setDisabled('planned.shift2.break', false);
      if (this.data.assignedSiteModel.thirdShiftActive && p2Stop !== '00:00') {
        this.setDisabled('planned.shift3.start', false);
      } else {
        this.setDisabled('planned.shift3.start', true);
      }
    } else {
      this.setDisabled('planned.shift2.break', true);
      if (this.data.assignedSiteModel.thirdShiftActive) {
        this.setDisabled('planned.shift3.start', true);
      }
    }

    // Shift 3
    if (this.data.assignedSiteModel.thirdShiftActive) {
      const p3Start = this.getCtrl('planned.shift3.start').value as string | null;
      const p3Stop = this.getCtrl('planned.shift3.stop').value as string | null;

      if (isSet(p3Start)) {this.setDisabled('planned.shift3.stop', false);}
      else {this.setDisabled('planned.shift3.stop', true);}

      if (isSet(p3Stop)) {
        this.setDisabled('planned.shift3.break', false);
        if (this.data.assignedSiteModel.fourthShiftActive && p3Stop !== '00:00') {
          this.setDisabled('planned.shift4.start', false);
        } else {
          this.setDisabled('planned.shift4.start', true);
        }
      } else {
        this.setDisabled('planned.shift3.break', true);
        if (this.data.assignedSiteModel.fourthShiftActive) {
          this.setDisabled('planned.shift4.start', true);
        }
      }
    }

    // Shift 4
    if (this.data.assignedSiteModel.fourthShiftActive) {
      const p4Start = this.getCtrl('planned.shift4.start').value as string | null;
      const p4Stop = this.getCtrl('planned.shift4.stop').value as string | null;

      if (isSet(p4Start)) {this.setDisabled('planned.shift4.stop', false);}
      else {this.setDisabled('planned.shift4.stop', true);}

      if (isSet(p4Stop)) {
        this.setDisabled('planned.shift4.break', false);
        if (this.data.assignedSiteModel.fifthShiftActive && p4Stop !== '00:00') {
          this.setDisabled('planned.shift5.start', false);
        } else {
          this.setDisabled('planned.shift5.start', true);
        }
      } else {
        this.setDisabled('planned.shift4.break', true);
        if (this.data.assignedSiteModel.fifthShiftActive) {
          this.setDisabled('planned.shift5.start', true);
        }
      }
    }

    // Shift 5
    if (this.data.assignedSiteModel.fifthShiftActive) {
      const p5Start = this.getCtrl('planned.shift5.start').value as string | null;
      const p5Stop = this.getCtrl('planned.shift5.stop').value as string | null;

      if (isSet(p5Start)) {this.setDisabled('planned.shift5.stop', false);}
      else {this.setDisabled('planned.shift5.stop', true);}

      if (isSet(p5Stop)) {this.setDisabled('planned.shift5.break', false);}
      else {this.setDisabled('planned.shift5.break', true);}
    }

    // Actual
    const a1Start = this.getCtrl('actual.shift1.start').value as string | null;
    const a1Stop = this.getCtrl('actual.shift1.stop').value as string | null;

    if (!a1Start) {
      this.setDisabled('actual.shift1.pause', true);
      this.setDisabled('actual.shift1.stop', true);
      this.setDisabled('actual.shift2.start', true);
      this.setDisabled('actual.shift2.pause', true);
      this.setDisabled('actual.shift2.stop', true);
      this.setDisabled('actual.shift3.start', true);
      this.setDisabled('actual.shift3.pause', true);
      this.setDisabled('actual.shift3.stop', true);
      this.setDisabled('actual.shift4.start', true);
      this.setDisabled('actual.shift4.pause', true);
      this.setDisabled('actual.shift4.stop', true);
      this.setDisabled('actual.shift5.start', true);
      this.setDisabled('actual.shift5.pause', true);
      this.setDisabled('actual.shift5.stop', true);
    } else {
      // this.setDisabled('actual.shift1.pause', false);
      this.setDisabled('actual.shift1.stop', false);
      // this.setDisabled('actual.shift2.start', false);
    }

    if (a1Stop) {
      this.setDisabled('actual.shift1.pause', false);
      this.setDisabled('actual.shift2.start', false);
    }

    const a2Start = this.getCtrl('actual.shift2.start').value as string | null;
    const a2Stop = this.getCtrl('actual.shift2.stop').value as string | null;

    if (a2Start) {
      // this.setDisabled('actual.shift2.pause', false);
      this.setDisabled('actual.shift2.stop', false);
      // this.setDisabled('actual.shift3.start', false);
    }

    if (a2Stop) {
      this.setDisabled('actual.shift2.pause', false);
      if (this.data.assignedSiteModel.thirdShiftActive && a2Stop !== '00:00') {
        this.setDisabled('actual.shift3.start', false);
      } else {
        this.setDisabled('actual.shift3.start', true);
      }
    }

    if (this.data.assignedSiteModel.thirdShiftActive) {
      const a3Start = this.getCtrl('actual.shift3.start').value as string | null;
      const a3Stop = this.getCtrl('actual.shift3.stop').value as string | null;
      if (a3Start) {
        // this.setDisabled('actual.shift3.pause', false);
        this.setDisabled('actual.shift3.stop', false);
        // this.setDisabled('actual.shift4.start', false);
      }
      if (a3Stop) {
        this.setDisabled('actual.shift3.pause', false);
        if (this.data.assignedSiteModel.fourthShiftActive && a3Stop !== '00:00') {
          this.setDisabled('actual.shift4.start', false);
        } else {
          this.setDisabled('actual.shift4.start', true);
        }
      }
    }

    if (this.data.assignedSiteModel.fourthShiftActive) {
      const a4Start = this.getCtrl('actual.shift4.start').value as string | null;
      const a4Stop = this.getCtrl('actual.shift4.stop').value as string | null;
      if (a4Start) {
        // this.setDisabled('actual.shift4.pause', false);
        this.setDisabled('actual.shift4.stop', false);
        // this.setDisabled('actual.shift5.start', false);
      }
      if (a4Stop) {
        this.setDisabled('actual.shift4.pause', false);
        if (this.data.assignedSiteModel.fifthShiftActive && a4Stop !== '00:00') {
          this.setDisabled('actual.shift5.start', false);
        } else {
          this.setDisabled('actual.shift5.start', true);
        }
      }
    }

    if (this.data.assignedSiteModel.fifthShiftActive) {
      const a5Start = this.getCtrl('actual.shift5.start').value as string | null;
      const a5Stop = this.getCtrl('actual.shift5.stop').value as string | null;
      if (a5Start) {
        // this.setDisabled('actual.shift5.pause', false);
        this.setDisabled('actual.shift5.stop', false);
      }
      if (a5Stop) {
        this.setDisabled('actual.shift5.pause', false);
      }
    }

  }

  // ===== UI-hjælpere (samme logik som tidligere, men brugt af form) =====
  convertMinutesToTime(minutes?: number | null): string | null {
    if (minutes == null || minutes === 0) {
      return null;
    }
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return `${this.padZero(hours)}:${this.padZero(mins)}`;
  }

  convertTimeToDateTimeOfToday(hourMinutes: string): string {
    if (hourMinutes === '' || hourMinutes === null || hourMinutes === undefined) {
      return null;
    }
    const today= new Date(this.data.planningPrDayModels.date);
    const [hours, minutes] = hourMinutes.split(':');
    const utcDate = new Date(Date.UTC(
      today.getUTCFullYear(),
      today.getUTCMonth(),
      today.getUTCDate(),
      parseInt(hours, 10),
      parseInt(minutes, 10),
      0,
      0
    ));
    return utcDate.toISOString();
  }

  // Bevarer din specielle 5-minutters opskalering, når isFiveNumberIntervals=true
  convertTimeToMinutes(timeStamp: string, isFiveNumberIntervals: boolean = false, isStop: boolean = false): number {
    if (timeStamp === '' || timeStamp === null || timeStamp === undefined) {
      return null;
    }
    const parts = timeStamp.split(':');
    const hours = parseInt(parts[0], 10);
    const minutes = parseInt(parts[1], 10);
    if (isFiveNumberIntervals) {
      const result = ((hours * 60 + minutes) / 5);
      // if (result !== 0) {
      if (isStop && result === 0) {
        return 289; // hvis stop er 00:00, så returner 24*60/5=288
        // return result + 1;
      }
      return result + 1;
    }
    return hours * 60 + minutes;
  }

  private toMinutes(hhmm: string | null): number | null {
    if (!hhmm) {
      return null;
    }
    const m = hhmm.match(/^(\d{1,2}):(\d{2})$/);
    if (!m) {
      return null;
    }
    const h = Number(m[1]);
    const mi = Number(m[2]);
    if (h === 24 && mi === 0) {
      return 1440;
    }
    if (h < 0 || h > 24 || mi < 0 || mi > 59) {
      return null;
    }
    if (h === 24 && mi !== 0) {
      return null;
    }
    return h * 60 + mi;
  }

  convertHoursToTime(hours: number): string {
    const isNegative = hours < 0;
    if (hours < 0) {
      hours = Math.abs(hours);
    }
    const totalMinutes = Math.round(hours * 60);
    const hrs = Math.floor(totalMinutes / 60);
    const mins = totalMinutes % 60;
    if (isNegative) {
      return `-${hrs}:${this.padZero(mins)}`;
    }
    return `${this.padZero(hrs)}:${this.padZero(mins)}`;
  }

  padZero(num: number): string {
    return num < 10 ? '0' + num : num.toString();
  }

  getMaxDifference(start: string, end: string): string {
    const startTime = this.convertTimeToMinutes(start);
    const endTime = this.convertTimeToMinutes(end);
    const diff = endTime - startTime;
    if (diff < 0) {
      if (end === '00:00' && start !== '00:00') {
        // shift ends at midnight, so we count it as 24:00 and recalculate
        const midnight = 24 * 60;
        const adjustedDiff = midnight - startTime;
        const hours = Math.floor(adjustedDiff / 60);
        const minutes = adjustedDiff % 60;
        return `${hours}:${minutes}`;
      }
      return '00:00';
    }
    const hours = Math.floor(diff / 60);
    const minutes = diff % 60;
    return `${hours}:${minutes}`;
  }

  onFlagChange(changedKey: string) {
    const flags = this.workdayForm.get('flags') as FormGroup;

    // Turn off all other flags when one turns on
    const turnedOn = flags.get(changedKey)?.value === true;
    if (turnedOn) {
      Object.keys(flags.controls).forEach(k => {
        if (k !== changedKey && flags.get(k)?.value) {
          flags.get(k)?.setValue(false, {emitEvent: false});
        }
      });
      // Update model message to selected option
      this.data.planningPrDayModels.message = TimePlanningMessagesEnum[changedKey as keyof typeof TimePlanningMessagesEnum];

      // Your original “DayOff” logic preserved
      if (changedKey !== 'DayOff') {
        this.data.planningPrDayModels.nettoHoursOverrideActive = true;
        this.data.planningPrDayModels.nettoHoursOverride = this.data.planningPrDayModels.planHours;
        this.workdayForm.get('nettoHoursOverride')?.setValue(this.data.planningPrDayModels.planHours);
      } else {
        this.data.planningPrDayModels.nettoHoursOverrideActive = false;
      }
    } else {
      // If user unticks the active one, clear message/override
      if (this.data.planningPrDayModels.message === TimePlanningMessagesEnum[changedKey as keyof typeof TimePlanningMessagesEnum]) {
        this.data.planningPrDayModels.message = null;
        this.data.planningPrDayModels.nettoHoursOverrideActive = false;
      }
    }

    this.calculatePlanHours();
  }

  // ===== Nulstil plan =====
  resetPlannedTimes(number: number) {
    const s1 = this.workdayForm.get('planned.shift1') as FormGroup;
    const s2 = this.workdayForm.get('planned.shift2') as FormGroup;
    const s3 = this.workdayForm.get('planned.shift3') as FormGroup;
    const s4 = this.workdayForm.get('planned.shift4') as FormGroup;
    const s5 = this.workdayForm.get('planned.shift5') as FormGroup;
    switch (number) {
      case 1:
        this.workdayForm.get('planHours')?.setValue(0, {emitEvent: false});
        s1.patchValue({start: null, break: null, stop: null});
        s2.patchValue({start: null, break: null, stop: null});
        s3.patchValue({start: null, break: null, stop: null});
        s4.patchValue({start: null, break: null, stop: null});
        s5.patchValue({start: null, break: null, stop: null});
        break;
      case 2:
        s1.patchValue({break: null});
        break;
      case 3:
        s1.patchValue({break: null, stop: null});
        s2.patchValue({start: null, break: null, stop: null});
        s3.patchValue({start: null, break: null, stop: null});
        s4.patchValue({start: null, break: null, stop: null});
        s5.patchValue({start: null, break: null, stop: null});
        break;
      case 4:
        s2.patchValue({start: null, break: null, stop: null});
        s3.patchValue({start: null, break: null, stop: null});
        s4.patchValue({start: null, break: null, stop: null});
        s5.patchValue({start: null, break: null, stop: null});
        break;
      case 5:
        s2.patchValue({break: null});
        break;
      case 6:
        s2.patchValue({break: null, stop: null});
        s3.patchValue({start: null, break: null, stop: null});
        s4.patchValue({start: null, break: null, stop: null});
        s5.patchValue({start: null, break: null, stop: null});
        break;
      case 7:
        s3.patchValue({start: null, break: null, stop: null});
        s4.patchValue({start: null, break: null, stop: null});
        s5.patchValue({start: null, break: null, stop: null});
        break;
      case 8:
        s3.patchValue({break: null});
        break;
      case 9:
        s3.patchValue({break: null, stop: null});
        s4.patchValue({start: null, break: null, stop: null});
        s5.patchValue({start: null, break: null, stop: null});
        break;
      case 10:
        s4.patchValue({start: null, break: null, stop: null});
        s5.patchValue({start: null, break: null, stop: null});
        break;
      case 11:
        s4.patchValue({break: null});
        break;
      case 12:
        s4.patchValue({break: null, stop: null});
        s5.patchValue({start: null, break: null, stop: null});
        break;
      case 13:
        s5.patchValue({start: null, break: null, stop: null});
        break;
      case 14:
        s5.patchValue({break: null});
        break;
      case 15:
        s5.patchValue({break: null, stop: null});
        break;
    }
    this.calculatePlanHours();
    this.updateDisabledStates();
  }

  // ===== Nulstil registreret =====
  resetActualTimes(number: number) {
    const a1 = this.workdayForm.get('actual.shift1') as FormGroup;
    const a2 = this.workdayForm.get('actual.shift2') as FormGroup;
    const a3 = this.workdayForm.get('actual.shift3') as FormGroup;
    const a4 = this.workdayForm.get('actual.shift4') as FormGroup;
    const a5 = this.workdayForm.get('actual.shift5') as FormGroup;
    switch (number) {
      case 1:
        a1.patchValue({start: null, pause: null, stop: null});
        a2.patchValue({start: null, pause: null, stop: null});
        a3.patchValue({start: null, pause: null, stop: null});
        a4.patchValue({start: null, pause: null, stop: null});
        a5.patchValue({start: null, pause: null, stop: null});
        break;
      case 2:
        a1.patchValue({pause: null});
        break;
      case 3:
        a1.patchValue({pause: null, stop: null});
        a2.patchValue({start: null, pause: null, stop: null});
        a3.patchValue({start: null, pause: null, stop: null});
        a4.patchValue({start: null, pause: null, stop: null});
        a5.patchValue({start: null, pause: null, stop: null});
        break;
      case 4:
        a2.patchValue({start: null, pause: null, stop: null});
        a3.patchValue({start: null, pause: null, stop: null});
        a4.patchValue({start: null, pause: null, stop: null});
        a5.patchValue({start: null, pause: null, stop: null});
        break;
      case 5:
        a2.patchValue({pause: null});
        break;
      case 6:
        a2.patchValue({pause: null, stop: null});
        a3.patchValue({start: null, pause: null, stop: null});
        a4.patchValue({start: null, pause: null, stop: null});
        a5.patchValue({start: null, pause: null, stop: null});
        break;
      case 7:
        a3.patchValue({start: null, pause: null, stop: null});
        a4.patchValue({start: null, pause: null, stop: null});
        a5.patchValue({start: null, pause: null, stop: null});
        break;
      case 8:
        a3.patchValue({pause: null});
        break;
      case 9:
        a3.patchValue({pause: null, stop: null});
        a4.patchValue({start: null, pause: null, stop: null});
        a5.patchValue({start: null, pause: null, stop: null});
        break;
      case 10:
        a4.patchValue({start: null, pause: null, stop: null});
        a5.patchValue({start: null, pause: null, stop: null});
        break;
      case 11:
        a4.patchValue({pause: null});
        break;
      case 12:
        a4.patchValue({pause: null, stop: null});
        a5.patchValue({start: null, pause: null, stop: null});
        break;
      case 13:
        a5.patchValue({start: null, pause: null, stop: null});
        break;
      case 14:
        a5.patchValue({pause: null});
        break;
      case 15:
        a5.patchValue({pause: null, stop: null});
        break;
    }
    this.calculatePlanHours();
    this.updateDisabledStates();
  }

  // ===== Gem til model (kaldes fra Save) =====
  onUpdateWorkDayEntity() {
    // Læs plan fra form
    const p1 = this.workdayForm.get('planned.shift1')?.value as { start: string; break: string; stop: string };
    const p2 = this.workdayForm.get('planned.shift2')?.value as { start: string; break: string; stop: string };
    const p3 = this.workdayForm.get('planned.shift3')?.value as { start: string; break: string; stop: string };
    const p4 = this.workdayForm.get('planned.shift4')?.value as { start: string; break: string; stop: string };
    const p5 = this.workdayForm.get('planned.shift5')?.value as { start: string; break: string; stop: string };

    this.data.planningPrDayModels.plannedStartOfShift1 = this.convertTimeToMinutes(p1?.start ?? '00:00');
    this.data.planningPrDayModels.plannedEndOfShift1 = this.convertTimeToMinutes(p1?.stop ?? '00:00');
    this.data.planningPrDayModels.plannedBreakOfShift1 = this.convertTimeToMinutes(p1?.break ?? '00:00');

    this.data.planningPrDayModels.plannedStartOfShift2 = this.convertTimeToMinutes(p2?.start ?? '00:00');
    this.data.planningPrDayModels.plannedEndOfShift2 = this.convertTimeToMinutes(p2?.stop ?? '00:00');
    this.data.planningPrDayModels.plannedBreakOfShift2 = this.convertTimeToMinutes(p2?.break ?? '00:00');

    this.data.planningPrDayModels.plannedStartOfShift3 = this.convertTimeToMinutes(p3?.start ?? '00:00');
    this.data.planningPrDayModels.plannedEndOfShift3 = this.convertTimeToMinutes(p3?.stop ?? '00:00');
    this.data.planningPrDayModels.plannedBreakOfShift3 = this.convertTimeToMinutes(p3?.break ?? '00:00');

    this.data.planningPrDayModels.plannedStartOfShift4 = this.convertTimeToMinutes(p4?.start ?? '00:00');
    this.data.planningPrDayModels.plannedEndOfShift4 = this.convertTimeToMinutes(p4?.stop ?? '00:00');
    this.data.planningPrDayModels.plannedBreakOfShift4 = this.convertTimeToMinutes(p4?.break ?? '00:00');

    this.data.planningPrDayModels.plannedStartOfShift5 = this.convertTimeToMinutes(p5?.start ?? '00:00');
    this.data.planningPrDayModels.plannedEndOfShift5 = this.convertTimeToMinutes(p5?.stop ?? '00:00');
    this.data.planningPrDayModels.plannedBreakOfShift5 = this.convertTimeToMinutes(p5?.break ?? '00:00');


    // Læs actual fra form (med 5-min intervaller)
    const a1 = this.workdayForm.get('actual.shift1')?.value as { start: string; pause: string; stop: string };
    const a2 = this.workdayForm.get('actual.shift2')?.value as { start: string; pause: string; stop: string };
    const a3 = this.workdayForm.get('actual.shift3')?.value as { start: string; pause: string; stop: string };
    const a4 = this.workdayForm.get('actual.shift4')?.value as { start: string; pause: string; stop: string };
    const a5 = this.workdayForm.get('actual.shift5')?.value as { start: string; pause: string; stop: string };

    this.data.planningPrDayModels.start1Id = this.convertTimeToMinutes(a1?.start, true);
    this.data.planningPrDayModels.start1StartedAt = this.convertTimeToDateTimeOfToday(a1?.start);
    // eslint-disable-next-line max-len
    this.data.planningPrDayModels.pause1Id = this.convertTimeToMinutes(a1?.pause, true) === 0 ? null : this.convertTimeToMinutes(a1?.pause, true);
    this.data.planningPrDayModels.stop1Id = this.convertTimeToMinutes(a1?.stop, true, true);
    this.data.planningPrDayModels.stop1StoppedAt = this.convertTimeToDateTimeOfToday(a1?.stop === '00:00' ? '24:00' : a1?.stop);

    this.data.planningPrDayModels.start2Id = this.convertTimeToMinutes(a2?.start, true);
    this.data.planningPrDayModels.start2StartedAt = this.convertTimeToDateTimeOfToday(a2?.start);
    // eslint-disable-next-line max-len
    this.data.planningPrDayModels.pause2Id = this.convertTimeToMinutes(a2?.pause, true) === 0 ? null : this.convertTimeToMinutes(a2?.pause, true);
    this.data.planningPrDayModels.stop2Id = this.convertTimeToMinutes(a2?.stop, true, true);
    this.data.planningPrDayModels.stop2StoppedAt = this.convertTimeToDateTimeOfToday(a2?.stop === '00:00' ? '24:00' : a2?.stop);

    this.data.planningPrDayModels.start3Id = this.convertTimeToMinutes(a3?.start, true);
    this.data.planningPrDayModels.start3StartedAt = this.convertTimeToDateTimeOfToday(a3?.start);
    // eslint-disable-next-line max-len
    this.data.planningPrDayModels.pause3Id = this.convertTimeToMinutes(a3?.pause, true) === 0 ? null : this.convertTimeToMinutes(a3?.pause, true);
    this.data.planningPrDayModels.stop3Id = this.convertTimeToMinutes(a3?.stop, true, true);
    this.data.planningPrDayModels.stop3StoppedAt = this.convertTimeToDateTimeOfToday(a3?.stop === '00:00' ? '24:00' : a3?.stop);

    this.data.planningPrDayModels.start4Id = this.convertTimeToMinutes(a4?.start, true);
    this.data.planningPrDayModels.start4StartedAt = this.convertTimeToDateTimeOfToday(a4?.start);
    // eslint-disable-next-line max-len
    this.data.planningPrDayModels.pause4Id = this.convertTimeToMinutes(a4?.pause, true) === 0 ? null : this.convertTimeToMinutes(a4?.pause, true);
    this.data.planningPrDayModels.stop4Id = this.convertTimeToMinutes(a4?.stop, true, true);
    this.data.planningPrDayModels.stop4StoppedAt = this.convertTimeToDateTimeOfToday(a4?.stop === '00:00' ? '24:00' : a4?.stop);

    this.data.planningPrDayModels.start5Id = this.convertTimeToMinutes(a5?.start, true);
    this.data.planningPrDayModels.start5StartedAt = this.convertTimeToDateTimeOfToday(a5?.start);
    // eslint-disable-next-line max-len
    this.data.planningPrDayModels.pause5Id = this.convertTimeToMinutes(a5?.pause, true) === 0 ? null : this.convertTimeToMinutes(a5?.pause, true);
    this.data.planningPrDayModels.stop5Id = this.convertTimeToMinutes(a5?.stop, true, true);
    this.data.planningPrDayModels.stop5StoppedAt = this.convertTimeToDateTimeOfToday(a5?.stop === '00:00' ? '24:00' : a5?.stop);

    this.data.planningPrDayModels.planHours = this.workdayForm.get('planHours')?.value;
    this.data.planningPrDayModels.paidOutFlex = this.workdayForm.get('paidOutFlex')?.value;

    this.data.planningPrDayModels.nettoHoursOverride = this.workdayForm.get('nettoHoursOverride')?.value;
    this.data.planningPrDayModels.commentOffice = this.workdayForm.get('commentOffice')?.value;
    // Rens paidOutFlex
    this.data.planningPrDayModels.paidOutFlex =
      this.data.planningPrDayModels.paidOutFlex === null ? 0 : this.data.planningPrDayModels.paidOutFlex;

    if (this.data.planningPrDayModels.paidOutFlex.toString().includes(',')) {
      this.data.planningPrDayModels.paidOutFlex = parseFloat(
        this.data.planningPrDayModels.paidOutFlex.toString().replace(',', '.')
      );
    }
  }

  private getPlannedShiftMinutes(
    start: number | null,
    end: number | null,
    breakMinutes: number | null
  ): number {
    if (start === null || end === null || start === end) {
      return 0;
    }

    let duration = end - start;

    if (end <= start) {
      duration = (1440 - start) + end;
    }

    if (breakMinutes) {
      duration -= breakMinutes;
    }

    return Math.max(0, duration);
  }

  markAllAsTouched(control: AbstractControl) {
    if (control instanceof FormControl) {
      control.markAsTouched({ onlySelf: true });
    } else if (control instanceof FormGroup) {
      Object.values(control.controls).forEach((c) => this.markAllAsTouched(c));
      control.markAsTouched({ onlySelf: true });
    } else if (control instanceof FormArray) {
      control.controls.forEach((c) => this.markAllAsTouched(c));
    }
  }

  // ===== Genberegn plan/actual/todaysFlex og sumFlexEnd (samme logik som før, men baseret på form) =====
  calculatePlanHours() {
    this.markAllAsTouched(this.workdayForm);
    this.updateDisabledStates?.();

    const ok = this.runAllValidators();
    if (!ok) {
      this.focusFirstError(); // valgfri
      return;                 // 2) Stop beregninger når der er fejl
    }
    this.onUpdateWorkDayEntity();

    let plannedTimeInMinutes = 0;
    const m = this.data.planningPrDayModels;

    const shifts = [1, 2, 3, 4, 5];
    for (const i of shifts) {
      const start = m[`plannedStartOfShift${i}`];
      const end = m[`plannedEndOfShift${i}`];
      const brk = m[`plannedBreakOfShift${i}`];

      plannedTimeInMinutes += this.getPlannedShiftMinutes(start, end, brk);
    }

    if (plannedTimeInMinutes !== 0) {
      m.planHours = plannedTimeInMinutes / 60;
      this.workdayForm.get('planHours')?.setValue(m.planHours, {emitEvent: false});
    }
    // m.planHours = plannedTimeInMinutes / 60;
    // this.workdayForm.get('planHours')?.setValue(m.planHours, {emitEvent: false});

    // Summer actual
    let actualTimeInMinutes = 0;
    if (this.data.planningPrDayModels.stop1Id !== null) {
      actualTimeInMinutes =
        this.data.planningPrDayModels.stop1Id
        - (this.data.planningPrDayModels.pause1Id > 0 ? this.data.planningPrDayModels.pause1Id - 1 : 0)
        - this.data.planningPrDayModels.start1Id;
    }
    if (this.data.planningPrDayModels.stop2Id !== null) {
      actualTimeInMinutes +=
        this.data.planningPrDayModels.stop2Id
        - (this.data.planningPrDayModels.pause2Id > 0 ? this.data.planningPrDayModels.pause2Id - 1 : 0)
        - this.data.planningPrDayModels.start2Id;
    }
    if (this.data.planningPrDayModels.stop3Id !== null) {
      actualTimeInMinutes +=
        this.data.planningPrDayModels.stop3Id
        - (this.data.planningPrDayModels.pause3Id > 0 ? this.data.planningPrDayModels.pause3Id - 1 : 0)
        - this.data.planningPrDayModels.start3Id;
    }
    if (this.data.planningPrDayModels.stop4Id !== null) {
      actualTimeInMinutes +=
        this.data.planningPrDayModels.stop4Id
        - (this.data.planningPrDayModels.pause4Id > 0 ? this.data.planningPrDayModels.pause4Id - 1 : 0)
        - this.data.planningPrDayModels.start4Id;
    }
    if (this.data.planningPrDayModels.stop5Id !== null) {
      actualTimeInMinutes +=
        this.data.planningPrDayModels.stop5Id
        - (this.data.planningPrDayModels.pause5Id > 0 ? this.data.planningPrDayModels.pause5Id - 1 : 0)
        - this.data.planningPrDayModels.start5Id;
    }
    if (actualTimeInMinutes !== 0) {
      actualTimeInMinutes *= 5;
    }
    this.data.planningPrDayModels.actualHours = actualTimeInMinutes / 60;

    // Flex
    if (this.data.planningPrDayModels.nettoHoursOverrideActive) {
      this.todaysFlex =
        this.data.planningPrDayModels.nettoHoursOverride - this.data.planningPrDayModels.planHours;
    } else {
      this.todaysFlex =
        this.data.planningPrDayModels.actualHours - this.data.planningPrDayModels.planHours;
    }

    // PaidOutFlex & sumFlexEnd
    if (this.data.planningPrDayModels.paidOutFlex !== null) {
      let paidOutFlex = this.data.planningPrDayModels.paidOutFlex.toString();
      if (paidOutFlex.includes(',')) {
        paidOutFlex = paidOutFlex.replace(',', '.');
      }
      if (!isNaN(Number(paidOutFlex))) {
        this.data.planningPrDayModels.paidOutFlex = Number(paidOutFlex);
        if (this.data.planningPrDayModels.nettoHoursOverrideActive) {
          this.data.planningPrDayModels.sumFlexEnd =
            this.data.planningPrDayModels.sumFlexStart
            + this.data.planningPrDayModels.nettoHoursOverride
            - this.data.planningPrDayModels.planHours
            - this.data.planningPrDayModels.paidOutFlex;
        } else {
          this.data.planningPrDayModels.sumFlexEnd =
            this.data.planningPrDayModels.sumFlexStart
            + this.data.planningPrDayModels.actualHours
            - this.data.planningPrDayModels.planHours
            - this.data.planningPrDayModels.paidOutFlex;
        }
      } else {
        if (this.data.planningPrDayModels.nettoHoursOverrideActive) {
          this.data.planningPrDayModels.sumFlexEnd =
            this.data.planningPrDayModels.sumFlexStart
            + this.data.planningPrDayModels.nettoHoursOverride
            - this.data.planningPrDayModels.planHours;
        } else {
          this.data.planningPrDayModels.sumFlexEnd =
            this.data.planningPrDayModels.sumFlexStart
            + this.data.planningPrDayModels.actualHours
            - this.data.planningPrDayModels.planHours;
        }
      }
    } else {
      if (this.data.planningPrDayModels.nettoHoursOverrideActive) {
        this.data.planningPrDayModels.sumFlexEnd =
          this.data.planningPrDayModels.sumFlexStart
          + this.data.planningPrDayModels.nettoHoursOverride
          - this.data.planningPrDayModels.planHours;
      } else {
        this.data.planningPrDayModels.sumFlexEnd =
          this.data.planningPrDayModels.sumFlexStart
          + this.data.planningPrDayModels.actualHours
          - this.data.planningPrDayModels.planHours;
      }
    }

    if (this.data.planningPrDayModels.sumFlexEnd.toFixed(2) === '-0.00') {
      this.data.planningPrDayModels.sumFlexEnd = 0.00;
      this.workdayForm.get('sumFlexEnd')?.setValue(0.00, {emitEvent: false});
    }
  }

// Kør ALLE validators og vis fejl i UI
  private runAllValidators(): boolean {
    // Sørg for at Material viser <mat-error>
    this.workdayForm.markAllAsTouched();
    // Trig alle control- og gruppevalidators (inkl. cross-field)
    this.workdayForm.updateValueAndValidity({onlySelf: false, emitEvent: false});

    // Eksempel: custom fejltekst til actual.shift2 (din cross-field validator)
    this.stop2Error = this.workdayForm.get('actual.shift2').hasError('stop2AfterStart')
      ? this.translateService.instant('Stop time must be after start time')
      : null;
    this.stop3Error = this.workdayForm.get('actual.shift3')?.hasError('stop3AfterStart')
      ? this.translateService.instant('Stop time must be after start time')
      : null;
    this.stop4Error = this.workdayForm.get('actual.shift4')?.hasError('stop4AfterStart')
      ? this.translateService.instant('Stop time must be after start time')
      : null;
    this.stop5Error = this.workdayForm.get('actual.shift5')?.hasError('stop5AfterStart')
      ? this.translateService.instant('Stop time must be after start time')
      : null;

    return this.workdayForm.valid;
  }

// (valgfri) Fokusér første fejl — nice UX
  private focusFirstError(): void {
    const firstInvalid = this.findFirstInvalid(this.workdayForm);
    if (!firstInvalid) {
      return;
    }
    const name = firstInvalid[0]; // fx "stop" eller "start"
    // simpelt bud: find input med formControlName
    const el = document.querySelector(`[formcontrolname="${name}"]`) as HTMLElement | null;
    el?.focus();
  }

// Hjælp til at finde første invalide control (navn, control)
  private findFirstInvalid(group: import('@angular/forms').AbstractControl, path: string[] = []): [string, any] | null {
    // FormGroup
    // @ts-ignore – runtime type check
    if (group?.controls) {
      // @ts-ignore
      for (const key of Object.keys(group.controls)) {
        // @ts-ignore
        const child = group.controls[key];
        const found = this.findFirstInvalid(child, [...path, key]);
        if (found) {
          return found;
        }
      }
      return null;
    }
    // FormControl
    if (group?.invalid) {
      return [path[path.length - 1] ?? '', group];
    }
    return null;
  }

  onCancel() {
  }

  openVersionHistory() {
    this.dialog.open(VersionHistoryModalComponent, {
      data: { planRegistrationId: this.data.planningPrDayModels.id },
      width: '90vw',
      maxWidth: '1400px',
      height: '80vh'
    });
  }
}
