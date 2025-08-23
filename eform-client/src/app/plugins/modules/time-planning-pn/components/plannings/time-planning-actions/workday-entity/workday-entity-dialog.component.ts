import { Component, Inject, OnInit, TemplateRef, ViewChild } from '@angular/core';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { TranslateService } from '@ngx-translate/core';
import { DatePipe } from '@angular/common';
import { TimePlanningMessagesEnum } from '../../../../enums';
import { AssignedSiteModel, PlanningPrDayModel } from '../../../../models';
import { MtxGridColumn } from '@ng-matero/extensions/grid';
import { TimePlanningPnPlanningsService } from '../../../../services';

import {
  AbstractControl,
  FormBuilder,
  FormControl,
  FormGroup,
  ValidationErrors,
  Validators,
} from '@angular/forms';

@Component({
  selector: 'app-workday-entity-dialog',
  templateUrl: './workday-entity-dialog.component.html',
  styleUrls: ['./workday-entity-dialog.component.scss'],
  standalone: false
})
export class WorkdayEntityDialogComponent implements OnInit {
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

  @ViewChild('plannedColumnTemplate', { static: true }) plannedColumnTemplate!: TemplateRef<any>;
  @ViewChild('actualColumnTemplate', { static: true }) actualColumnTemplate!: TemplateRef<any>;
  protected readonly JSON = JSON;

  constructor(
    private fb: FormBuilder,
    private planningsService: TimePlanningPnPlanningsService,
    @Inject(MAT_DIALOG_DATA) public data: {
      planningPrDayModels: PlanningPrDayModel,
      assignedSiteModel: AssignedSiteModel
    },
    protected datePipe: DatePipe,
    private translateService: TranslateService,
  ) {}

  ngOnInit(): void {
    // Enum-opsætning
    this.enumKeys = Object.keys(TimePlanningMessagesEnum).filter(key => isNaN(Number(key)));
    this.nettoHoursOverrideActive = this.data.planningPrDayModels.nettoHoursOverrideActive;
    if (this.data.planningPrDayModels.message) {
      this.data.planningPrDayModels[this.enumKeys[this.data.planningPrDayModels.message - 1]] = true;
    }

    // Konverter modelværdier til "HH:mm" strenge
    const plannedStartOfShift1 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedStartOfShift1);
    const plannedEndOfShift1   = this.convertMinutesToTime(this.data.planningPrDayModels.plannedEndOfShift1);
    const plannedBreakOfShift1 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedBreakOfShift1);

    const plannedStartOfShift2 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedStartOfShift2);
    const plannedEndOfShift2   = this.convertMinutesToTime(this.data.planningPrDayModels.plannedEndOfShift2);
    const plannedBreakOfShift2 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedBreakOfShift2);

    const plannedStartOfShift3 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedStartOfShift3);
    const plannedEndOfShift3   = this.convertMinutesToTime(this.data.planningPrDayModels.plannedEndOfShift3);
    const plannedBreakOfShift3 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedBreakOfShift3);

    const plannedStartOfShift4 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedStartOfShift4);
    const plannedEndOfShift4   = this.convertMinutesToTime(this.data.planningPrDayModels.plannedEndOfShift4);
    const plannedBreakOfShift4 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedBreakOfShift4);

    const plannedStartOfShift5 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedStartOfShift5);
    const plannedEndOfShift5   = this.convertMinutesToTime(this.data.planningPrDayModels.plannedEndOfShift5);
    const plannedBreakOfShift5 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedBreakOfShift5);

    const start1StartedAt = this.datePipe.transform(this.data.planningPrDayModels.start1StartedAt, 'HH:mm', 'UTC');
    const stop1StoppedAt  = this.datePipe.transform(this.data.planningPrDayModels.stop1StoppedAt, 'HH:mm', 'UTC');
    const pause1Id        = this.convertMinutesToTime(this.data.planningPrDayModels.pause1Id * 5);

    const start2StartedAt = this.datePipe.transform(this.data.planningPrDayModels.start2StartedAt, 'HH:mm', 'UTC');
    const stop2StoppedAt  = this.datePipe.transform(this.data.planningPrDayModels.stop2StoppedAt, 'HH:mm', 'UTC');
    const pause2Id        = this.convertMinutesToTime(this.data.planningPrDayModels.pause2Id * 5);

    const start3StartedAt = this.datePipe.transform(this.data.planningPrDayModels.start3StartedAt, 'HH:mm', 'UTC');
    const stop3StoppedAt  = this.datePipe.transform(this.data.planningPrDayModels.stop3StoppedAt, 'HH:mm', 'UTC');
    const pause3Id        = this.convertMinutesToTime(this.data.planningPrDayModels.pause3Id * 5);

    const start4StartedAt = this.datePipe.transform(this.data.planningPrDayModels.start4StartedAt, 'HH:mm', 'UTC');
    const stop4StoppedAt  = this.datePipe.transform(this.data.planningPrDayModels.stop4StoppedAt, 'HH:mm', 'UTC');
    const pause4Id        = this.convertMinutesToTime(this.data.planningPrDayModels.pause4Id * 5);

    const start5StartedAt = this.datePipe.transform(this.data.planningPrDayModels.start5StartedAt, 'HH:mm', 'UTC');
    const stop5StoppedAt  = this.datePipe.transform(this.data.planningPrDayModels.stop5StoppedAt, 'HH:mm', 'UTC');
    const pause5Id        = this.convertMinutesToTime(this.data.planningPrDayModels.pause5Id * 5);

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
        shift1: this.fb.group({
          start: new FormControl({ value: plannedStartOfShift1, disabled: false }),
          break: new FormControl({ value: plannedBreakOfShift1, disabled: false }),
          stop:  new FormControl({ value: plannedEndOfShift1,   disabled: false }),
        }),
        shift2: this.fb.group({
          start: new FormControl({ value: plannedStartOfShift2, disabled: false }),
          break: new FormControl({ value: plannedBreakOfShift2, disabled: false }),
          stop:  new FormControl({ value: plannedEndOfShift2,   disabled: false }),
        }),
        shift3: this.fb.group({
          start: new FormControl({ value: plannedStartOfShift3, disabled: false }),
          break: new FormControl({ value: plannedBreakOfShift3, disabled: false }),
          stop:  new FormControl({ value: plannedEndOfShift3,   disabled: false }),
        }),
        shift4: this.fb.group({
          start: new FormControl({ value: plannedStartOfShift4, disabled: false }),
          break: new FormControl({ value: plannedBreakOfShift4, disabled: false }),
          stop:  new FormControl({ value: plannedEndOfShift4,   disabled: false }),
        }),
        shift5: this.fb.group({
          start: new FormControl({ value: plannedStartOfShift5, disabled: false }),
          break: new FormControl({ value: plannedBreakOfShift5, disabled: false }),
          stop:  new FormControl({ value: plannedEndOfShift5,   disabled: false }),
        }),
      }),
      actual: this.fb.group({
        shift1: this.fb.group({
          start: new FormControl({ value: start1StartedAt, disabled: this.isInTheFuture }),
          pause: new FormControl({ value: pause1Id,       disabled: this.isInTheFuture }),
          stop:  new FormControl({ value: stop1StoppedAt, disabled: this.isInTheFuture }),
        }),
        shift2: this.fb.group({
          start: new FormControl({ value: start2StartedAt, disabled: this.isInTheFuture }),
          pause: new FormControl({ value: pause2Id,        disabled: this.isInTheFuture }),
          stop:  new FormControl({ value: stop2StoppedAt,  disabled: this.isInTheFuture }),
        },
          { validators: [this.stopAfterStartValidator('start', 'stop', 'stop2AfterStart')]}
        ),
        shift3: this.fb.group({
          start: new FormControl({ value: start3StartedAt, disabled: this.isInTheFuture }),
          pause: new FormControl({ value: pause3Id,        disabled: this.isInTheFuture }),
          stop:  new FormControl({ value: stop3StoppedAt,  disabled: this.isInTheFuture }),
        },
          { validators: [this.stopAfterStartValidator('start', 'stop', 'stop3AfterStart')]}
        ),
        shift4: this.fb.group({
          start: new FormControl({ value: start4StartedAt, disabled: this.isInTheFuture }),
          pause: new FormControl({ value: pause4Id,        disabled: this.isInTheFuture }),
          stop:  new FormControl({ value: stop4StoppedAt,  disabled: this.isInTheFuture }),
        },
          { validators: [this.stopAfterStartValidator('start', 'stop', 'stop4AfterStart')]}
        ),
        shift5: this.fb.group({
          start: new FormControl({ value: start5StartedAt, disabled: this.isInTheFuture }),
          pause: new FormControl({ value: pause5Id,        disabled: this.isInTheFuture }),
          stop:  new FormControl({ value: stop5StoppedAt,  disabled: this.isInTheFuture }),
        },
          { validators: [this.stopAfterStartValidator('start', 'stop', 'stop5AfterStart')]}
        ),
      }),
      planHours: new FormControl({value: this.data.planningPrDayModels.planHours ?? null, disabled: this.isInTheFuture}),
      nettoHoursOverride: new FormControl(this.data.planningPrDayModels.nettoHoursOverride ?? null),
      paidOutFlex: new FormControl(this.data.planningPrDayModels.paidOutFlex ?? null),
      commentOffice: new FormControl(this.data.planningPrDayModels.commentOffice ?? null),


      flags: this.fb.group(flagsGroup),
    });

    // Opdatér fejltekst for stop2 ved ændringer
    this.workdayForm.get('actual.shift2')?.statusChanges.subscribe(() => {
      this.stop2Error = this.workdayForm.get('actual.shift2')?.hasError('stop2AfterStart')
        ? this.translateService.instant('Stop time must be after start time')
        : null;
    });

    // Tabellens kolonner
    this.tableHeaders = this.data.assignedSiteModel.useOnlyPlanHours ? [
      { header: this.translateService.stream('Shift'), field: 'shift', pinned: 'left' },
      { cellTemplate: this.actualColumnTemplate, header: this.translateService.stream('Registered'), field: 'actualStart', sortable: false },
    ] : this.isInTheFuture ? [
      { header: this.translateService.stream('Shift'), field: 'shift', pinned: 'left' },
      { cellTemplate: this.plannedColumnTemplate, header: this.translateService.stream('Planned'), field: 'plannedStart', sortable: false },
    ] : [
      { header: this.translateService.stream('Shift'), field: 'shift', pinned: 'left' },
      { cellTemplate: this.plannedColumnTemplate, header: this.translateService.stream('Planned'), field: 'plannedStart', sortable: false },
      { cellTemplate: this.actualColumnTemplate, header: this.translateService.stream('Registered'), field: 'actualStart', sortable: false },
    ];

    // Byg shiftData fra formværdier
    const get = (path: string) => this.workdayForm.get(path)?.value || null;

    const shift1Data = {
      shiftId: '1',
      shift: this.translateService.instant('1st'),
      plannedStart: get('planned.shift1.start'),
      plannedEnd:   get('planned.shift1.stop'),
      plannedBreak: get('planned.shift1.break'),
      actualStart:  get('actual.shift1.start'),
      actualEnd:    get('actual.shift1.stop'),
      actualBreak:  get('actual.shift1.pause'),
    };
    const shift2Data = {
      shiftId: '2',
      shift: this.translateService.instant('2nd'),
      plannedStart: get('planned.shift2.start'),
      plannedEnd:   get('planned.shift2.stop'),
      plannedBreak: get('planned.shift2.break'),
      actualStart:  get('actual.shift2.start'),
      actualEnd:    get('actual.shift2.stop'),
      actualBreak:  get('actual.shift2.pause'),
    };
    const shift3Data = {
      shiftId: '3',
      shift: this.translateService.instant('3rd'),
      plannedStart: get('planned.shift3.start'),
      plannedEnd:   get('planned.shift3.stop'),
      plannedBreak: get('planned.shift3.break'),
      actualStart:  get('actual.shift3.start'),
      actualEnd:    get('actual.shift3.stop'),
      actualBreak:  get('actual.shift3.pause'),
    };
    const shift4Data = {
      shiftId: '4',
      shift: this.translateService.instant('4th'),
      plannedStart: get('planned.shift4.start'),
      plannedEnd:   get('planned.shift4.stop'),
      plannedBreak: get('planned.shift4.break'),
      actualStart:  get('actual.shift4.start'),
      actualEnd:    get('actual.shift4.stop'),
      actualBreak:  get('actual.shift4.pause'),
    };
    const shift5Data = {
      shiftId: '5',
      shift: this.translateService.instant('5th'),
      plannedStart: get('planned.shift5.start'),
      plannedEnd:   get('planned.shift5.stop'),
      plannedBreak: get('planned.shift5.break'),
      actualStart:  get('actual.shift5.start'),
      actualEnd:    get('actual.shift5.stop'),
      actualBreak:  get('actual.shift5.pause'),
    };

    this.shiftData = [shift1Data, shift2Data];
    if (this.data.assignedSiteModel.thirdShiftActive)  {this.shiftData.push(shift3Data);}
    if (this.data.assignedSiteModel.fourthShiftActive) {this.shiftData.push(shift4Data);}
    if (this.data.assignedSiteModel.fifthShiftActive)  {this.shiftData.push(shift5Data);}

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

  private updateDisabledStates() {
    // ---- PLANNED, SHIFT 1 ----
    const p1Start = this.getCtrl('planned.shift1.start').value as string | null;
    const p1Stop  = this.getCtrl('planned.shift1.stop').value as string | null;
    const p2Start = this.getCtrl('planned.shift2.start').value as string | null;
    const p2Stop  = this.getCtrl('planned.shift2.stop').value as string | null;

    if (p1Start === '00:00' && p1Stop === '00:00') {
      this.setDisabled('planned.shift1.break', true);
      this.setDisabled('planned.shift1.stop', false);
      this.setDisabled('planned.shift2.start', true);
      this.setDisabled('planned.shift2.break', true);
      this.setDisabled('planned.shift2.stop', true);
      this.setDisabled('planned.shift3.start', true);
      this.setDisabled('planned.shift3.break', true);
      this.setDisabled('planned.shift3.stop', true);
      this.setDisabled('planned.shift4.start', true);
      this.setDisabled('planned.shift4.break', true);
      this.setDisabled('planned.shift4.stop', true);
      this.setDisabled('planned.shift5.start', true);
      this.setDisabled('planned.shift5.break', true);
      this.setDisabled('planned.shift5.stop', true);
    } else {
      this.setDisabled('planHours', true);
      this.setDisabled('planned.shift1.break', false);
      this.setDisabled('planned.shift1.stop', false);
      this.setDisabled('planned.shift2.start', false);
    }

    if (p2Start !== '00:00') {
      // this.setDisabled('planned.shift2.break', false);
      this.setDisabled('planned.shift2.stop', false);
      this.setDisabled('planned.shift3.start', false);
    }
    if (p2Stop !== '00:00') {
      this.setDisabled('planned.shift2.break', false);
    }

    if (this.data.assignedSiteModel.thirdShiftActive) {
      const p3Start = this.getCtrl('planned.shift3.start').value as string | null;
      const p3Stop  = this.getCtrl('planned.shift3.stop').value as string | null;
      if (p3Start !== '00:00') {
        this.setDisabled('planned.shift3.stop', false);
        this.setDisabled('planned.shift4.start', false);
      }
      if (p3Stop !== '00:00') {
        this.setDisabled('planned.shift3.break', false);
      }
    }

    if (this.data.assignedSiteModel.fourthShiftActive) {
      const p4Start = this.getCtrl('planned.shift4.start').value as string | null;
      const p4Stop  = this.getCtrl('planned.shift4.stop').value as string | null;
      if (p4Start !== '00:00') {
        this.setDisabled('planned.shift4.stop', false);
        this.setDisabled('planned.shift5.start', false);
      }
      if (p4Stop !== '00:00') {
        this.setDisabled('planned.shift4.break', false);
      }
    }

    if (this.data.assignedSiteModel.fifthShiftActive) {
      const p5Start = this.getCtrl('planned.shift5.start').value as string | null;
      const p5Stop  = this.getCtrl('planned.shift5.stop').value as string | null;
      if (p5Start !== '00:00') {
        this.setDisabled('planned.shift5.stop', false);
      }
      if (p5Stop !== '00:00') {
        this.setDisabled('planned.shift5.break', false);
      }
    }

    const a1Start = this.getCtrl('actual.shift1.start').value as string | null;
    const a1Stop  = this.getCtrl('actual.shift1.stop').value as string | null;

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
    const a2Stop  = this.getCtrl('actual.shift2.stop').value as string | null;

    if (a2Start) {
      // this.setDisabled('actual.shift2.pause', false);
      this.setDisabled('actual.shift2.stop', false);
      // this.setDisabled('actual.shift3.start', false);
    }

    if (a2Stop) {
      this.setDisabled('actual.shift2.pause', false);
      this.setDisabled('actual.shift3.start', false);
    }

    if (this.data.assignedSiteModel.thirdShiftActive) {
      const a3Start = this.getCtrl('actual.shift3.start').value as string | null;
      const a3Stop  = this.getCtrl('actual.shift3.stop').value as string | null;
      if (a3Start) {
        // this.setDisabled('actual.shift3.pause', false);
        this.setDisabled('actual.shift3.stop', false);
        // this.setDisabled('actual.shift4.start', false);
      }
      if (a3Stop) {
        this.setDisabled('actual.shift3.pause', false);
        this.setDisabled('actual.shift4.start', false);
      }
    }

    if (this.data.assignedSiteModel.fourthShiftActive) {
      const a4Start = this.getCtrl('actual.shift4.start').value as string | null;
      const a4Stop  = this.getCtrl('actual.shift4.stop').value as string | null;
      if (a4Start) {
        // this.setDisabled('actual.shift4.pause', false);
        this.setDisabled('actual.shift4.stop', false);
        // this.setDisabled('actual.shift5.start', false);
      }
      if (a4Stop) {
        this.setDisabled('actual.shift4.pause', false);
        this.setDisabled('actual.shift5.start', false);
      }
    }

    if (this.data.assignedSiteModel.fifthShiftActive) {
      const a5Start = this.getCtrl('actual.shift5.start').value as string | null;
      const a5Stop  = this.getCtrl('actual.shift5.stop').value as string | null;
      if (a5Start) {
        // this.setDisabled('actual.shift5.pause', false);
        this.setDisabled('actual.shift5.stop', false);
      }
      if (a5Stop) {
        this.setDisabled('actual.shift5.pause', false);
      }
    }

  }

  // ===== Validators =====
  // Gruppevalidator: kræv at stop > start (tillad across-midnight ved at lægge 1440 til).
  private stopAfterStartValidator(startKey: string, stopKey: string, errorName: string) {
    return (group: AbstractControl): ValidationErrors | null => {
      const start = (group.get(startKey)?.value as string | null) ?? null;
      const stop  = (group.get(stopKey)?.value as string | null) ?? null;
      const s = this.toMinutes(start);
      const e0 = this.toMinutes(stop);
      if (s == null || e0 == null) {return null;} // andre validators håndterer tomme
      let e = e0;
      if (e < s) {e += 1440;}
      return e > s ? null : { [errorName]: true };
    };
  }

  // ===== UI-hjælpere (samme logik som tidligere, men brugt af form) =====
  convertMinutesToTime(minutes: number): string {
    if (minutes == null) {return null;}
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return `${this.padZero(hours)}:${this.padZero(mins)}`;
  }

convertTimeToDateTimeOfToday(hourMinutes: string): string {
  if (hourMinutes === '' || hourMinutes === null || hourMinutes === undefined) {
    return null;
  }
  const today = new Date();
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
    if (!hhmm) {return null;}
    const m = hhmm.match(/^(\d{1,2}):(\d{2})$/);
    if (!m) {return null;}
    const h = Number(m[1]);
    const mi = Number(m[2]);
    if (h === 24 && mi === 0) {return 1440;}
    if (h < 0 || h > 24 || mi < 0 || mi > 59) {return null;}
    if (h === 24 && mi !== 0) {return null;}
    return h * 60 + mi;
  }

  convertHoursToTime(hours: number): string {
    const isNegative = hours < 0;
    if (hours < 0) {hours = Math.abs(hours);}
    const totalMinutes = Math.round(hours * 60);
    const hrs = Math.floor(totalMinutes / 60);
    const mins = totalMinutes % 60;
    if (isNegative) {
      return `-${hrs}:${this.padZero(mins)}`;
    }
    return `${this.padZero(hrs)}:${this.padZero(mins)}`;
  }

  private padZero(num: number): string {
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
          flags.get(k)?.setValue(false, { emitEvent: false });
        }
      });
      // Update model message to selected option
      this.data.planningPrDayModels.message = TimePlanningMessagesEnum[changedKey as keyof typeof TimePlanningMessagesEnum];

      // Your original “DayOff” logic preserved
      if (changedKey !== 'DayOff') {
        this.data.planningPrDayModels.nettoHoursOverrideActive = true;
        this.data.planningPrDayModels.nettoHoursOverride = this.data.planningPrDayModels.planHours;
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
    switch (number) {
      case 1:
        s1.patchValue({ start: '00:00', break: '00:00', stop: '00:00' });
        s2.patchValue({ start: '00:00', break: '00:00', stop: '00:00' });
        break;
      case 2:
        s1.patchValue({ break: '00:00' });
        break;
      case 3:
        s1.patchValue({ break: '00:00', stop: '00:00' });
        s2.patchValue({ start: '00:00', break: '00:00', stop: '00:00' });
        break;
      case 4:
        s2.patchValue({ start: '00:00', break: '00:00', stop: '00:00' });
        break;
      case 5:
        s2.patchValue({ break: '00:00' });
        break;
      case 6:
        s2.patchValue({ break: '00:00', stop: '00:00' });
        break;
    }
    this.calculatePlanHours();
    this.updateDisabledStates();
  }

  // ===== Nulstil registreret =====
  resetActualTimes(number: number) {
    const a1 = this.workdayForm.get('actual.shift1') as FormGroup;
    const a2 = this.workdayForm.get('actual.shift2') as FormGroup;
    switch (number) {
      case 1:
        a1.patchValue({ start: null, pause: null, stop: null });
        a2.patchValue({ start: null, pause: null, stop: null });
        break;
      case 2:
        a1.patchValue({ pause: null });
        break;
      case 3:
        a1.patchValue({ pause: null, stop: null });
        a2.patchValue({ start: null, pause: null, stop: null });
        break;
      case 4:
        a2.patchValue({ start: null, pause: null, stop: null });
        break;
      case 5:
        a2.patchValue({ pause: null });
        break;
      case 6:
        a2.patchValue({ pause: null, stop: null });
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

    this.data.planningPrDayModels.plannedStartOfShift1 = this.convertTimeToMinutes(p1?.start);
    this.data.planningPrDayModels.plannedEndOfShift1   = this.convertTimeToMinutes(p1?.stop);
    this.data.planningPrDayModels.plannedBreakOfShift1 = this.convertTimeToMinutes(p1?.break ?? '00:00');

    this.data.planningPrDayModels.plannedStartOfShift2 = this.convertTimeToMinutes(p2?.start);
    this.data.planningPrDayModels.plannedEndOfShift2   = this.convertTimeToMinutes(p2?.stop);
    this.data.planningPrDayModels.plannedBreakOfShift2 = this.convertTimeToMinutes(p2?.break);

    this.data.planningPrDayModels.plannedStartOfShift3 = this.convertTimeToMinutes(p3?.start);
    this.data.planningPrDayModels.plannedEndOfShift3   = this.convertTimeToMinutes(p3?.stop);
    this.data.planningPrDayModels.plannedBreakOfShift3 = this.convertTimeToMinutes(p3?.break);

    this.data.planningPrDayModels.plannedStartOfShift4 = this.convertTimeToMinutes(p4?.start);
    this.data.planningPrDayModels.plannedEndOfShift4   = this.convertTimeToMinutes(p4?.stop);
    this.data.planningPrDayModels.plannedBreakOfShift4 = this.convertTimeToMinutes(p4?.break);

    this.data.planningPrDayModels.plannedStartOfShift5 = this.convertTimeToMinutes(p5?.start);
    this.data.planningPrDayModels.plannedEndOfShift5   = this.convertTimeToMinutes(p5?.stop);
    this.data.planningPrDayModels.plannedBreakOfShift5 = this.convertTimeToMinutes(p5?.break);

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
    this.data.planningPrDayModels.stop1Id  = this.convertTimeToMinutes(a1?.stop,  true, true);
    this.data.planningPrDayModels.stop1StoppedAt = this.convertTimeToDateTimeOfToday(a1?.stop === '00:00' ? '24:00' : a1?.stop);

    this.data.planningPrDayModels.start2Id = this.convertTimeToMinutes(a2?.start, true);
    this.data.planningPrDayModels.start2StartedAt = this.convertTimeToDateTimeOfToday(a2?.start);
    // eslint-disable-next-line max-len
    this.data.planningPrDayModels.pause2Id = this.convertTimeToMinutes(a2?.pause, true) === 0 ? null : this.convertTimeToMinutes(a2?.pause, true);
    this.data.planningPrDayModels.stop2Id  = this.convertTimeToMinutes(a2?.stop,  true, true);
    this.data.planningPrDayModels.stop2StoppedAt = this.convertTimeToDateTimeOfToday(a2?.stop === '00:00' ? '24:00' : a2?.stop);

    this.data.planningPrDayModels.start3Id = this.convertTimeToMinutes(a3?.start, true);
    this.data.planningPrDayModels.start3StartedAt = this.convertTimeToDateTimeOfToday(a3?.start);
    // eslint-disable-next-line max-len
    this.data.planningPrDayModels.pause3Id = this.convertTimeToMinutes(a3?.pause, true) === 0 ? null : this.convertTimeToMinutes(a3?.pause, true);
    this.data.planningPrDayModels.stop3Id  = this.convertTimeToMinutes(a3?.stop,  true, true);
    this.data.planningPrDayModels.stop3StoppedAt = this.convertTimeToDateTimeOfToday(a3?.stop === '00:00' ? '24:00' : a3?.stop);

    this.data.planningPrDayModels.start4Id = this.convertTimeToMinutes(a4?.start, true);
    this.data.planningPrDayModels.start4StartedAt = this.convertTimeToDateTimeOfToday(a4?.start);
    // eslint-disable-next-line max-len
    this.data.planningPrDayModels.pause4Id = this.convertTimeToMinutes(a4?.pause, true) === 0 ? null : this.convertTimeToMinutes(a4?.pause, true);
    this.data.planningPrDayModels.stop4Id  = this.convertTimeToMinutes(a4?.stop,  true, true);
    this.data.planningPrDayModels.stop4StoppedAt = this.convertTimeToDateTimeOfToday(a4?.stop === '00:00' ? '24:00' : a4?.stop);

    this.data.planningPrDayModels.start5Id = this.convertTimeToMinutes(a5?.start, true);
    this.data.planningPrDayModels.start5StartedAt = this.convertTimeToDateTimeOfToday(a5?.start);
    // eslint-disable-next-line max-len
    this.data.planningPrDayModels.pause5Id = this.convertTimeToMinutes(a5?.pause, true) === 0 ? null : this.convertTimeToMinutes(a5?.pause, true);
    this.data.planningPrDayModels.stop5Id  = this.convertTimeToMinutes(a5?.stop,  true, true);
    this.data.planningPrDayModels.stop5StoppedAt = this.convertTimeToDateTimeOfToday(a5?.stop === '00:00' ? '24:00' : a5?.stop);

    this.data.planningPrDayModels.planHours = this.workdayForm.get('planHours')?.value;
    this.data.planningPrDayModels.paidOutFlex = this.workdayForm.get('paidOutFlex')?.value;

    // Rens paidOutFlex
    this.data.planningPrDayModels.paidOutFlex =
      this.data.planningPrDayModels.paidOutFlex === null ? 0 : this.data.planningPrDayModels.paidOutFlex;
  }

  // ===== Genberegn plan/actual/todaysFlex og sumFlexEnd (samme logik som før, men baseret på form) =====
  calculatePlanHours() {
    this.updateDisabledStates?.();
    const ok = this.runAllValidators();
    if (!ok) {
      this.focusFirstError(); // valgfri
      return;                 // 2) Stop beregninger når der er fejl
    }
    this.onUpdateWorkDayEntity();

    let plannedTimeInMinutes = 0;
    if (this.data.planningPrDayModels.plannedEndOfShift1 !== 0) {
      plannedTimeInMinutes =
        this.data.planningPrDayModels.plannedEndOfShift1
        - this.data.planningPrDayModels.plannedStartOfShift1
        - this.data.planningPrDayModels.plannedBreakOfShift1;
    }
    if (this.data.planningPrDayModels.plannedEndOfShift2 !== 0) {
      const timeInMinutes2NdShift =
        this.data.planningPrDayModels.plannedEndOfShift2
        - this.data.planningPrDayModels.plannedStartOfShift2
        - this.data.planningPrDayModels.plannedBreakOfShift2;
      plannedTimeInMinutes += timeInMinutes2NdShift;
    }
    if (this.data.planningPrDayModels.plannedEndOfShift3 !== 0) {
      const timeInMinutes3RdShift =
        this.data.planningPrDayModels.plannedEndOfShift3
        - this.data.planningPrDayModels.plannedStartOfShift3
        - this.data.planningPrDayModels.plannedBreakOfShift3;
      plannedTimeInMinutes += timeInMinutes3RdShift;
    }
    if (this.data.planningPrDayModels.plannedEndOfShift4 !== 0) {
      const timeInMinutes4ThShift =
        this.data.planningPrDayModels.plannedEndOfShift4
        - this.data.planningPrDayModels.plannedStartOfShift4
        - this.data.planningPrDayModels.plannedBreakOfShift4;
      plannedTimeInMinutes += timeInMinutes4ThShift;
    }
    if (this.data.planningPrDayModels.plannedEndOfShift5 !== 0) {
      const timeInMinutes5ThShift =
        this.data.planningPrDayModels.plannedEndOfShift5
        - this.data.planningPrDayModels.plannedStartOfShift5
        - this.data.planningPrDayModels.plannedBreakOfShift5;
      plannedTimeInMinutes += timeInMinutes5ThShift;
    }

    if (this.data.planningPrDayModels.message === null) {
      if (plannedTimeInMinutes !== 0) {
        this.data.planningPrDayModels.planHours = plannedTimeInMinutes / 60;
        this.workdayForm.get('planHours')?.setValue(this.data.planningPrDayModels.planHours, { emitEvent: false });
      }
    }

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
  }

// Kør ALLE validators og vis fejl i UI
  private runAllValidators(): boolean {
    // Sørg for at Material viser <mat-error>
    this.workdayForm.markAllAsTouched();
    // Trig alle control- og gruppevalidators (inkl. cross-field)
    this.workdayForm.updateValueAndValidity({ onlySelf: false, emitEvent: false });

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
    if (!firstInvalid) {return;}
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
        if (found) {return found;}
      }
      return null;
    }
    // FormControl
    if (group?.invalid) {
      return [path[path.length - 1] ?? '', group];
    }
    return null;
  }



  onCancel() {}
}


// import {Component, Inject, OnInit, TemplateRef, ViewChild} from '@angular/core';
// import {
//   MAT_DIALOG_DATA
// } from '@angular/material/dialog';
// import {TranslateService} from '@ngx-translate/core';
// import {DatePipe} from '@angular/common';
// import {TimePlanningMessagesEnum} from '../../../../enums';
// import {
//   AssignedSiteModel,
//   PlanningPrDayModel,
// } from '../../../../models';
// import {MtxGridColumn} from '@ng-matero/extensions/grid';
// import {TimePlanningPnPlanningsService} from '../../../../services';
//
// @Component({
//   selector: 'app-workday-entity-dialog',
//   templateUrl: './workday-entity-dialog.component.html',
//   styleUrls: ['./workday-entity-dialog.component.scss'],
//   standalone: false
// })
// export class WorkdayEntityDialogComponent implements OnInit {
//   TimePlanningMessagesEnum = TimePlanningMessagesEnum;
//   enumKeys: string[];
//   tableHeaders: MtxGridColumn[] = [];
//   shiftData: any[] = [];
//   plannedStartOfShift1: string;
//   plannedEndOfShift1: string;
//   plannedBreakOfShift1: string;
//   plannedStartOfShift2: string;
//   plannedEndOfShift2: string;
//   plannedBreakOfShift2: string;
//   start1StartedAt: string;
//   stop1StoppedAt: string;
//   pause1Id: string;
//   start2StartedAt: string;
//   stop2StoppedAt: string;
//   pause2Id: string;
//   start3StartedAt: string;
//   stop3StoppedAt: string;
//   pause3Id: string;
//   start4StartedAt: string;
//   stop4StoppedAt: string;
//   pause4Id: string;
//   start5StartedAt: string;
//   stop5StoppedAt: string;
//   pause5Id: string;
//   isInTheFuture: boolean = false;
//   maxPause1Id: number = 0;
//   maxPause2Id: number = 0;
//   todaysFlex: number = 0;
//   nettoHoursOverrideActive: boolean = false;
//   date: any;
//   @ViewChild('plannedColumnTemplate', {static: true}) plannedColumnTemplate!: TemplateRef<any>;
//   @ViewChild('actualColumnTemplate', {static: true}) actualColumnTemplate!: TemplateRef<any>;
//   protected readonly JSON = JSON;
//
//   constructor(
//     private planningsService: TimePlanningPnPlanningsService,
//     @Inject(MAT_DIALOG_DATA) public data: {
//       planningPrDayModels: PlanningPrDayModel,
//       assignedSiteModel: AssignedSiteModel
//     },
//     protected datePipe: DatePipe,
//     private translateService: TranslateService,
//   ) {
//   }
//
//   ngOnInit(): void {
//     this.enumKeys = Object.keys(TimePlanningMessagesEnum).filter(key => isNaN(Number(key)));
//     this.nettoHoursOverrideActive = this.data.planningPrDayModels.nettoHoursOverrideActive;
//     this.data.planningPrDayModels[this.enumKeys[this.data.planningPrDayModels.message - 1]] = true;
//     this.plannedStartOfShift1 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedStartOfShift1);
//     this.plannedEndOfShift1 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedEndOfShift1);
//     this.plannedBreakOfShift1 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedBreakOfShift1);
//     this.plannedStartOfShift2 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedStartOfShift2);
//     this.plannedEndOfShift2 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedEndOfShift2);
//     this.plannedBreakOfShift2 = this.convertMinutesToTime(this.data.planningPrDayModels.plannedBreakOfShift2);
//     this.start1StartedAt = this.datePipe.transform(this.data.planningPrDayModels.start1StartedAt, 'HH:mm', 'UTC')
//     this.stop1StoppedAt = this.datePipe.transform(this.data.planningPrDayModels.stop1StoppedAt, 'HH:mm', 'UTC');
//     this.pause1Id = this.convertMinutesToTime(this.data.planningPrDayModels.pause1Id * 5);
//     this.start2StartedAt = this.datePipe.transform(this.data.planningPrDayModels.start2StartedAt, 'HH:mm', 'UTC');
//     this.stop2StoppedAt = this.datePipe.transform(this.data.planningPrDayModels.stop2StoppedAt, 'HH:mm', 'UTC');
//     this.pause2Id = this.convertMinutesToTime(this.data.planningPrDayModels.pause2Id * 5);
//     this.start3StartedAt = this.datePipe.transform(this.data.planningPrDayModels.start3StartedAt, 'HH:mm', 'UTC');
//     this.stop3StoppedAt = this.datePipe.transform(this.data.planningPrDayModels.stop3StoppedAt, 'HH:mm', 'UTC');
//     this.pause3Id = this.convertMinutesToTime(this.data.planningPrDayModels.pause3Id * 5);
//     this.start4StartedAt = this.datePipe.transform(this.data.planningPrDayModels.start4StartedAt, 'HH:mm', 'UTC');
//     this.stop4StoppedAt = this.datePipe.transform(this.data.planningPrDayModels.stop4StoppedAt, 'HH:mm', 'UTC');
//     this.pause4Id = this.convertMinutesToTime(this.data.planningPrDayModels.pause4Id * 5);
//     this.start5StartedAt = this.datePipe.transform(this.data.planningPrDayModels.start5StartedAt, 'HH:mm', 'UTC');
//     this.stop5StoppedAt = this.datePipe.transform(this.data.planningPrDayModels.stop5StoppedAt, 'HH:mm', 'UTC');
//     this.pause5Id = this.convertMinutesToTime(this.data.planningPrDayModels.pause5Id * 5);
//     this.isInTheFuture = Date.parse(this.data.planningPrDayModels.date) > Date.now();
//     this.todaysFlex = this.data.planningPrDayModels.actualHours - this.data.planningPrDayModels.planHours;
//     this.date = Date.parse(this.data.planningPrDayModels.date);
//
//     this.tableHeaders = this.data.assignedSiteModel.useOnlyPlanHours ? [
//       {
//         header: this.translateService.stream('Shift'), field: 'shift',
//         pinned: 'left'
//       },
//       {
//         cellTemplate: this.actualColumnTemplate,
//         header: this.translateService.stream('Registered'),
//         field: 'actualStart',
//         sortable: false,
//       },
//     ]: this.isInTheFuture ? [
//       {
//         header: this.translateService.stream('Shift'), field: 'shift',
//         pinned: 'left'
//       },
//       {
//         cellTemplate: this.plannedColumnTemplate,
//         header: this.translateService.stream('Planned'),
//         field: 'plannedStart',
//         sortable: false,
//       },
//     ] : [
//       {
//         header: this.translateService.stream('Shift'), field: 'shift',
//         pinned: 'left'
//       },
//       {
//         cellTemplate: this.plannedColumnTemplate,
//         header: this.translateService.stream('Planned'),
//         field: 'plannedStart',
//         sortable: false,
//       },
//       {
//         cellTemplate: this.actualColumnTemplate,
//         header: this.translateService.stream('Registered'),
//         field: 'actualStart',
//         sortable: false,
//       },
//     ];
//
//     let shift2Data = {
//       shiftId: '2',
//       shift: this.translateService.instant('2nd'),
//       plannedStart: this.plannedStartOfShift2,
//       plannedEnd: this.plannedEndOfShift2,
//       plannedBreak: this.plannedBreakOfShift2,
//       actualStart: this.start2StartedAt,
//       actualEnd: this.stop2StoppedAt,
//       actualBreak: this.pause2Id,
//     };
//
//     let shift1Data = {
//       shiftId: '1',
//       shift: this.translateService.instant('1st'),
//       plannedStart: this.plannedStartOfShift1,
//       plannedEnd: this.plannedEndOfShift1,
//       plannedBreak: this.plannedBreakOfShift1,
//       actualStart: this.start1StartedAt,
//       actualEnd: this.stop1StoppedAt,
//       actualBreak: this.pause1Id,
//     };
//
//     let shift3Data = {
//       shiftId: '3',
//       shift: this.translateService.instant('3rd'),
//       plannedStart: this.plannedStartOfShift1,
//       plannedEnd: this.plannedEndOfShift1,
//       plannedBreak: this.plannedBreakOfShift1,
//       actualStart: this.start3StartedAt,
//       actualEnd: this.stop3StoppedAt,
//       actualBreak: this.pause3Id,
//     }
//
//     let shift4Data = {
//       shiftId: '4',
//       shift: this.translateService.instant('4th'),
//       plannedStart: this.plannedStartOfShift2,
//       plannedEnd: this.plannedEndOfShift2,
//       plannedBreak: this.plannedBreakOfShift2,
//       actualStart: this.start4StartedAt,
//       actualEnd: this.stop4StoppedAt,
//       actualBreak: this.pause4Id,
//     }
//
//     let shift5Data = {
//       shiftId: '5',
//       shift: this.translateService.instant('5th'),
//       plannedStart: this.plannedStartOfShift1,
//       plannedEnd: this.plannedEndOfShift1,
//       plannedBreak: this.plannedBreakOfShift1,
//       actualStart: this.start5StartedAt,
//       actualEnd: this.stop5StoppedAt,
//       actualBreak: this.pause5Id,
//     }
//
//     this.shiftData = [shift1Data, shift2Data];
//     if (this.data.assignedSiteModel.thirdShiftActive) {
//       this.shiftData.push(shift3Data);
//     }
//     if (this.data.assignedSiteModel.fourthShiftActive) {
//       this.shiftData.push(shift4Data);
//     }
//     if (this.data.assignedSiteModel.fifthShiftActive) {
//       this.shiftData.push(shift5Data);
//     }
//   }
//
//   convertMinutesToTime(minutes: number): string {
//     const hours = Math.floor(minutes / 60);
//     const mins = minutes % 60;
//     return `${this.padZero(hours)}:${this.padZero(mins)}`;
//   }
//
//   onCheckboxChange(selectedOption: TimePlanningMessagesEnum): void {
//     if (selectedOption !== this.data.planningPrDayModels.message) {
//       if (selectedOption !== TimePlanningMessagesEnum.DayOff) {
//         this.data.planningPrDayModels.nettoHoursOverrideActive = true;
//         this.data.planningPrDayModels.nettoHoursOverride = this.data.planningPrDayModels.planHours;
//       } else {
//         this.data.planningPrDayModels.nettoHoursOverrideActive = false;
//       }
//       this.data.planningPrDayModels.message = selectedOption;
//       this.enumKeys.forEach(key => {
//         this.data.planningPrDayModels[key] = selectedOption
//           === TimePlanningMessagesEnum[key as keyof typeof TimePlanningMessagesEnum];
//       });
//     }
//     else {
//       this.data.planningPrDayModels.nettoHoursOverrideActive = false;
//       this.data.planningPrDayModels.message = null;
//       // this.calculatePlanHours();
//     }
//     this.calculatePlanHours();
//   }
//
//   resetPlannedTimes(number: number) {
//     switch (number) {
//       case 1:
//         this.plannedStartOfShift1 = '00:00';
//         this.plannedBreakOfShift1 = '00:00';
//         this.plannedEndOfShift1 = '00:00';
//         this.plannedStartOfShift2 = '00:00';
//         this.plannedBreakOfShift2 = '00:00';
//         this.plannedEndOfShift2 = '00:00';
//         break;
//       case 2:
//         this.plannedBreakOfShift1 = '00:00';
//         break;
//       case 3:
//         this.plannedBreakOfShift1 = '00:00';
//         this.plannedEndOfShift1 = '00:00';
//         this.plannedStartOfShift2 = '00:00';
//         this.plannedBreakOfShift2 = '00:00';
//         this.plannedEndOfShift2 = '00:00';
//         break;
//       case 4:
//         this.plannedStartOfShift2 = '00:00';
//         this.plannedBreakOfShift2 = '00:00';
//         this.plannedEndOfShift2 = '00:00';
//         break;
//       case 5:
//         this.plannedBreakOfShift2 = '00:00';
//         break;
//       case 6:
//         this.plannedBreakOfShift2 = '00:00';
//         this.plannedEndOfShift2 = '00:00';
//         break;
//     }
//     this.calculatePlanHours();
//   }
//
//   resetActualTimes(number: number) {
//     switch (number) {
//       case 1:
//         this.start1StartedAt = null;
//         this.pause1Id = null;
//         this.stop1StoppedAt = null;
//         this.start2StartedAt = null;
//         this.pause2Id = null;
//         this.stop2StoppedAt = null;
//         break;
//       case 2:
//         this.pause1Id = null;
//         break;
//       case 3:
//         this.pause1Id = null;
//         this.stop1StoppedAt = null;
//         this.start2StartedAt = null;
//         this.pause2Id = null;
//         this.stop2StoppedAt = null;
//         break;
//       case 4:
//         this.start2StartedAt = null;
//         this.pause2Id = null;
//         this.stop2StoppedAt = null;
//         break;
//       case 5:
//         this.pause2Id = null;
//         break;
//       case 6:
//         this.pause2Id = null;
//         this.stop2StoppedAt = null;
//         break;
//     }
//     this.calculatePlanHours();
//   }
//
//   onUpdateWorkDayEntity() {
//     this.data.planningPrDayModels.plannedStartOfShift1 = this.convertTimeToMinutes(this.plannedStartOfShift1);
//     this.data.planningPrDayModels.plannedEndOfShift1 = this.convertTimeToMinutes(this.plannedEndOfShift1);
//     this.data.planningPrDayModels.plannedBreakOfShift1 = this.convertTimeToMinutes(this.plannedBreakOfShift1);
//     this.data.planningPrDayModels.plannedStartOfShift2 = this.convertTimeToMinutes(this.plannedStartOfShift2);
//     this.data.planningPrDayModels.plannedEndOfShift2 = this.convertTimeToMinutes(this.plannedEndOfShift2);
//     this.data.planningPrDayModels.plannedBreakOfShift2 = this.convertTimeToMinutes(this.plannedBreakOfShift2);
//     this.data.planningPrDayModels.start1Id = this.convertTimeToMinutes(this.start1StartedAt, true);
//     this.data.planningPrDayModels.pause1Id = this.convertTimeToMinutes(this.pause1Id, true);
//     this.data.planningPrDayModels.start2Id = this.convertTimeToMinutes(this.start2StartedAt, true);
//     this.data.planningPrDayModels.stop1Id = this.convertTimeToMinutes(this.stop1StoppedAt, true);
//     this.data.planningPrDayModels.pause2Id = this.convertTimeToMinutes(this.pause2Id, true);
//     this.data.planningPrDayModels.stop2Id = this.convertTimeToMinutes(this.stop2StoppedAt, true);
//     this.data.planningPrDayModels.start3Id = this.convertTimeToMinutes(this.start3StartedAt, true);
//     this.data.planningPrDayModels.stop3Id = this.convertTimeToMinutes(this.stop3StoppedAt, true);
//     this.data.planningPrDayModels.pause3Id = this.convertTimeToMinutes(this.pause3Id, true);
//     this.data.planningPrDayModels.start4Id = this.convertTimeToMinutes(this.start4StartedAt, true);
//     this.data.planningPrDayModels.stop4Id = this.convertTimeToMinutes(this.stop4StoppedAt, true);
//     this.data.planningPrDayModels.pause4Id = this.convertTimeToMinutes(this.pause4Id, true);
//     this.data.planningPrDayModels.start5Id = this.convertTimeToMinutes(this.start5StartedAt, true);
//     this.data.planningPrDayModels.stop5Id = this.convertTimeToMinutes(this.stop5StoppedAt, true);
//     this.data.planningPrDayModels.pause5Id = this.convertTimeToMinutes(this.pause5Id, true);
//     this.data.planningPrDayModels.paidOutFlex = this.data.planningPrDayModels.paidOutFlex
//     === null ? 0 : this.data.planningPrDayModels.paidOutFlex;
//   }
//
//   getMaxDifference(start: string, end: string): string {
//     const startTime = this.convertTimeToMinutes(start);
//     const endTime = this.convertTimeToMinutes(end);
//     const diff = endTime - startTime;
//     if (diff < 0) {
//       return '00:00';
//     }
//     const hours = Math.floor(diff / 60);
//     const minutes = diff % 60;
//     return `${hours}:${minutes}`;
//   }
//
//   convertTimeToDateTimeOfToday(hourMinutes: string): string {
//     const today = new Date();
//     const [hours, minutes] = hourMinutes.split(':');
//     today.setHours(parseInt(hours, 10), parseInt(minutes, 10), 0, 0);
//     return today.toISOString();
//   }
//
//   convertTimeToMinutes(timeStamp: string, isFiveNumberIntervals: boolean = false): number {
//     if (timeStamp === '' || timeStamp === null) {
//       return null;
//     }
//     const parts = timeStamp.split(':');
//     const hours = parseInt(parts[0], 10);
//     const minutes = parseInt(parts[1], 10);
//     if (isFiveNumberIntervals) {
//       const result = ((hours * 60 + minutes) / 5);
//       if (result !== 0) {
//         return result + 1
//       }
//       return 0;
//     }
//     return hours * 60 + minutes;
//   }
//
//   convertHoursToTime(hours: number): string {
//     const isNegative = hours < 0;
//     if (hours < 0) {
//       hours = Math.abs(hours);
//     }
//     const totalMinutes = Math.round(hours * 60)
//     const hrs = Math.floor(totalMinutes / 60);
//     let mins = totalMinutes % 60;
//     if (isNegative) {
//       // return '${padZero(hrs)}:${padZero(60 - mins)}';
//       return `-${hrs}:${this.padZero(mins)}`;
//     }
//     return `${this.padZero(hrs)}:${this.padZero(mins)}`;
//   }
//
//   onCancel() {
//   }
//
//   calculatePlanHours() {
//     this.data.planningPrDayModels.plannedStartOfShift1 = this.convertTimeToMinutes(this.plannedStartOfShift1);
//     this.data.planningPrDayModels.plannedEndOfShift1 = this.convertTimeToMinutes(this.plannedEndOfShift1);
//     this.data.planningPrDayModels.plannedBreakOfShift1 = this.convertTimeToMinutes(this.plannedBreakOfShift1);
//     this.data.planningPrDayModels.plannedStartOfShift2 = this.convertTimeToMinutes(this.plannedStartOfShift2);
//     this.data.planningPrDayModels.plannedEndOfShift2 = this.convertTimeToMinutes(this.plannedEndOfShift2);
//     this.data.planningPrDayModels.plannedBreakOfShift2 = this.convertTimeToMinutes(this.plannedBreakOfShift2);
//     let plannedTimeInMinutes = 0;
//     if (this.data.planningPrDayModels.plannedEndOfShift1 !== 0) {
//       plannedTimeInMinutes = this.data.planningPrDayModels.plannedEndOfShift1
//         - this.data.planningPrDayModels.plannedStartOfShift1
//         - this.data.planningPrDayModels.plannedBreakOfShift1;
//     }
//     if (this.data.planningPrDayModels.plannedEndOfShift2 !== 0) {
//       let timeInMinutes2NdShift = this.data.planningPrDayModels.plannedEndOfShift2
//         - this.data.planningPrDayModels.plannedStartOfShift2
//         - this.data.planningPrDayModels.plannedBreakOfShift2;
//       plannedTimeInMinutes += timeInMinutes2NdShift;
//     }
//     if (this.data.planningPrDayModels.message === null) {
//       if (plannedTimeInMinutes !== 0) {
//         this.data.planningPrDayModels.planHours = plannedTimeInMinutes / 60;
//       }
//     }
//
//     this.data.planningPrDayModels.start1Id = this.convertTimeToMinutes(this.start1StartedAt, true);
//     this.data.planningPrDayModels.stop1Id = this.convertTimeToMinutes(this.stop1StoppedAt, true);
//     this.data.planningPrDayModels.pause1Id = this.convertTimeToMinutes(
//       this.pause1Id,
//       true) === 0 ? null : this.convertTimeToMinutes(this.pause1Id, true);
//     if (this.data.planningPrDayModels.pause1Id > 0) {
//       this.data.planningPrDayModels.pause1Id -= 1;
//     }
//
//     this.data.planningPrDayModels.start2Id = this.convertTimeToMinutes(this.start2StartedAt, true);
//     this.data.planningPrDayModels.stop2Id = this.convertTimeToMinutes(this.stop2StoppedAt, true);
//     this.data.planningPrDayModels.pause2Id = this.convertTimeToMinutes(
//       this.pause2Id,
//       true) === 0 ? null : this.convertTimeToMinutes(this.pause2Id, true);
//     if (this.data.planningPrDayModels.pause2Id > 0) {
//       this.data.planningPrDayModels.pause2Id -= 1;
//     }
//
//     this.data.planningPrDayModels.start3Id = this.convertTimeToMinutes(this.start3StartedAt, true);
//     this.data.planningPrDayModels.stop3Id = this.convertTimeToMinutes(this.stop3StoppedAt, true);
//     this.data.planningPrDayModels.pause3Id = this.convertTimeToMinutes(
//       this.pause3Id,
//       true) === 0 ? null : this.convertTimeToMinutes(this.pause3Id, true);
//     if (this.data.planningPrDayModels.pause3Id > 0) {
//       this.data.planningPrDayModels.pause3Id -= 1;
//     }
//
//     this.data.planningPrDayModels.start4Id = this.convertTimeToMinutes(this.start4StartedAt, true);
//     this.data.planningPrDayModels.stop4Id = this.convertTimeToMinutes(this.stop4StoppedAt, true);
//     this.data.planningPrDayModels.pause4Id = this.convertTimeToMinutes(
//       this.pause4Id,
//       true) === 0 ? null : this.convertTimeToMinutes(this.pause4Id, true);
//     if (this.data.planningPrDayModels.pause4Id > 0) {
//       this.data.planningPrDayModels.pause4Id -= 1;
//     }
//
//     this.data.planningPrDayModels.start5Id = this.convertTimeToMinutes(this.start5StartedAt, true);
//     this.data.planningPrDayModels.stop5Id = this.convertTimeToMinutes(this.stop5StoppedAt, true);
//     this.data.planningPrDayModels.pause5Id = this.convertTimeToMinutes(
//       this.pause5Id,
//       true) === 0 ? null : this.convertTimeToMinutes(this.pause5Id, true);
//     if (this.data.planningPrDayModels.pause5Id > 0) {
//       this.data.planningPrDayModels.pause5Id -= 1;
//     }
//
//     let actualTimeInMinutes = 0;
//     if (this.data.planningPrDayModels.stop1Id !== null) {
//       actualTimeInMinutes = this.data.planningPrDayModels.stop1Id
//         - this.data.planningPrDayModels.pause1Id
//         - this.data.planningPrDayModels.start1Id;
//     }
//
//     if (this.data.planningPrDayModels.stop2Id !== null) {
//       let timeInMinutes2NdShift = this.data.planningPrDayModels.stop2Id
//         - this.data.planningPrDayModels.pause2Id
//         - this.data.planningPrDayModels.start2Id;
//       actualTimeInMinutes += timeInMinutes2NdShift;
//     }
//
//     if (this.data.planningPrDayModels.stop3Id !== null) {
//       let timeInMinutes3RdShift = this.data.planningPrDayModels.stop3Id
//         - this.data.planningPrDayModels.pause3Id
//         - this.data.planningPrDayModels.start3Id;
//       actualTimeInMinutes += timeInMinutes3RdShift;
//     }
//
//     if (this.data.planningPrDayModels.stop4Id !== null) {
//       let timeInMinutes4ThShift = this.data.planningPrDayModels.stop4Id
//         - this.data.planningPrDayModels.pause4Id
//         - this.data.planningPrDayModels.start4Id;
//       actualTimeInMinutes += timeInMinutes4ThShift;
//     }
//
//     if (this.data.planningPrDayModels.stop5Id !== null) {
//       let timeInMinutes5ThShift = this.data.planningPrDayModels.stop5Id
//         - this.data.planningPrDayModels.pause5Id
//         - this.data.planningPrDayModels.start5Id;
//       actualTimeInMinutes += timeInMinutes5ThShift;
//     }
//
//     if (actualTimeInMinutes !== 0) {
//       actualTimeInMinutes *= 5;
//     }
//     this.data.planningPrDayModels.actualHours = actualTimeInMinutes / 60;
//
//     if (this.data.planningPrDayModels.nettoHoursOverrideActive) {
//       this.todaysFlex = this.data.planningPrDayModels.nettoHoursOverride - this.data.planningPrDayModels.planHours;
//     } else {
//       this.todaysFlex = this.data.planningPrDayModels.actualHours - this.data.planningPrDayModels.planHours;
//     }
//
//     if (this.data.planningPrDayModels.paidOutFlex !== null) {
//       let paidOutFlex = this.data.planningPrDayModels.paidOutFlex.toString();
//       if (paidOutFlex.includes(',')) {
//         paidOutFlex = paidOutFlex.replace(',', '.');
//       }
//       // check if the string is a valid number
//       if (!isNaN(Number(paidOutFlex))) {
//         this.data.planningPrDayModels.paidOutFlex = Number(paidOutFlex);
//         if (this.data.planningPrDayModels.nettoHoursOverrideActive) {
//           this.data.planningPrDayModels.sumFlexEnd = this.data.planningPrDayModels.sumFlexStart
//             + this.data.planningPrDayModels.nettoHoursOverride
//             - this.data.planningPrDayModels.planHours
//             - this.data.planningPrDayModels.paidOutFlex;
//         } else {
//           this.data.planningPrDayModels.sumFlexEnd = this.data.planningPrDayModels.sumFlexStart
//             + this.data.planningPrDayModels.actualHours
//             - this.data.planningPrDayModels.planHours
//             - this.data.planningPrDayModels.paidOutFlex;
//         }
//       } else {
//         if (this.data.planningPrDayModels.nettoHoursOverrideActive) {
//           this.data.planningPrDayModels.sumFlexEnd = this.data.planningPrDayModels.sumFlexStart
//             + this.data.planningPrDayModels.nettoHoursOverride
//             - this.data.planningPrDayModels.planHours;
//         }
//         else {
//           this.data.planningPrDayModels.sumFlexEnd = this.data.planningPrDayModels.sumFlexStart
//             + this.data.planningPrDayModels.actualHours
//             - this.data.planningPrDayModels.planHours;
//         }
//       }
//     } else {
//       if (this.data.planningPrDayModels.nettoHoursOverrideActive) {
//         debugger;
//         this.data.planningPrDayModels.sumFlexEnd = this.data.planningPrDayModels.sumFlexStart
//           + this.data.planningPrDayModels.nettoHoursOverride
//           - this.data.planningPrDayModels.planHours;
//       }
//       else {
//         this.data.planningPrDayModels.sumFlexEnd = this.data.planningPrDayModels.sumFlexStart
//           + this.data.planningPrDayModels.actualHours
//           - this.data.planningPrDayModels.planHours;
//       }
//     }
//   }
//
//   private padZero(num: number): string {
//     return num < 10 ? '0' + num : num.toString();
//   }
// }
