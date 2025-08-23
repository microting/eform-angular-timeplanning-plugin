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
      }),
      actual: this.fb.group({
        shift1: this.fb.group({
          start: new FormControl<string | null>(start1StartedAt),
          pause: new FormControl<string | null>(pause1Id),
          stop:  new FormControl<string | null>(stop1StoppedAt),
        }),
        shift2: this.fb.group({
          start: new FormControl<string | null>(start2StartedAt),
          pause: new FormControl<string | null>(pause2Id),
          stop:  new FormControl<string | null>(stop2StoppedAt),
        }, { validators: [this.stopAfterStartValidator('start', 'stop', 'stop2AfterStart')] }),
        shift3: this.fb.group({
          start: new FormControl<string | null>(start3StartedAt),
          pause: new FormControl<string | null>(pause3Id),
          stop:  new FormControl<string | null>(stop3StoppedAt),
        }),
        shift4: this.fb.group({
          start: new FormControl<string | null>(start4StartedAt),
          pause: new FormControl<string | null>(pause4Id),
          stop:  new FormControl<string | null>(stop4StoppedAt),
        }),
        shift5: this.fb.group({
          start: new FormControl<string | null>(start5StartedAt),
          pause: new FormControl<string | null>(pause5Id),
          stop:  new FormControl<string | null>(stop5StoppedAt),
        }),
      }),
      planHours: new FormControl<number | null>(this.data.planningPrDayModels.planHours ?? null),
      nettoHoursOverride: new FormControl<number | null>(this.data.planningPrDayModels.nettoHoursOverride ?? null),
      paidOutFlex: new FormControl<number | null>(this.data.planningPrDayModels.paidOutFlex ?? null),
      commentOffice: new FormControl<string | null>(this.data.planningPrDayModels.commentOffice ?? null),

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
      plannedStart: get('planned.shift1.start'),
      plannedEnd:   get('planned.shift1.stop'),
      plannedBreak: get('planned.shift1.break'),
      actualStart:  get('actual.shift3.start'),
      actualEnd:    get('actual.shift3.stop'),
      actualBreak:  get('actual.shift3.pause'),
    };
    const shift4Data = {
      shiftId: '4',
      shift: this.translateService.instant('4th'),
      plannedStart: get('planned.shift2.start'),
      plannedEnd:   get('planned.shift2.stop'),
      plannedBreak: get('planned.shift2.break'),
      actualStart:  get('actual.shift4.start'),
      actualEnd:    get('actual.shift4.stop'),
      actualBreak:  get('actual.shift4.pause'),
    };
    const shift5Data = {
      shiftId: '5',
      shift: this.translateService.instant('5th'),
      plannedStart: get('planned.shift1.start'),
      plannedEnd:   get('planned.shift1.stop'),
      plannedBreak: get('planned.shift1.break'),
      actualStart:  get('actual.shift5.start'),
      actualEnd:    get('actual.shift5.stop'),
      actualBreak:  get('actual.shift5.pause'),
    };

    this.shiftData = [shift1Data, shift2Data];
    if (this.data.assignedSiteModel.thirdShiftActive)  this.shiftData.push(shift3Data);
    if (this.data.assignedSiteModel.fourthShiftActive) this.shiftData.push(shift4Data);
    if (this.data.assignedSiteModel.fifthShiftActive)  this.shiftData.push(shift5Data);
  }

  // ===== Validators =====
  // Gruppevalidator: kræv at stop > start (tillad across-midnight ved at lægge 1440 til).
  private stopAfterStartValidator(startKey: string, stopKey: string, errorName: string) {
    return (group: AbstractControl): ValidationErrors | null => {
      const start = (group.get(startKey)?.value as string | null) ?? null;
      const stop  = (group.get(stopKey)?.value as string | null) ?? null;
      const s = this.toMinutes(start);
      const e0 = this.toMinutes(stop);
      if (s == null || e0 == null) return null; // andre validators håndterer tomme
      let e = e0;
      if (e < s) e += 1440;
      return e > s ? null : { [errorName]: true };
    };
  }

  // ===== UI-hjælpere (samme logik som tidligere, men brugt af form) =====
  convertMinutesToTime(minutes: number): string {
    if (minutes == null) return null;
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return `${this.padZero(hours)}:${this.padZero(mins)}`;
  }

  convertTimeToDateTimeOfToday(hourMinutes: string): string {
    const today = new Date();
    const [hours, minutes] = hourMinutes.split(':');
    today.setHours(parseInt(hours, 10), parseInt(minutes, 10), 0, 0);
    return today.toISOString();
  }

  // Bevarer din specielle 5-minutters opskalering, når isFiveNumberIntervals=true
  convertTimeToMinutes(timeStamp: string, isFiveNumberIntervals: boolean = false): number {
    if (timeStamp === '' || timeStamp === null) {
      return null;
    }
    const parts = timeStamp.split(':');
    const hours = parseInt(parts[0], 10);
    const minutes = parseInt(parts[1], 10);
    if (isFiveNumberIntervals) {
      const result = ((hours * 60 + minutes) / 5);
      if (result !== 0) {
        return result + 1;
      }
      return 0;
    }
    return hours * 60 + minutes;
  }

  private toMinutes(hhmm: string | null): number | null {
    if (!hhmm) return null;
    const m = hhmm.match(/^(\d{1,2}):(\d{2})$/);
    if (!m) return null;
    const h = Number(m[1]);
    const mi = Number(m[2]);
    if (h === 24 && mi === 0) return 1440;
    if (h < 0 || h > 24 || mi < 0 || mi > 59) return null;
    if (h === 24 && mi !== 0) return null;
    return h * 60 + mi;
  }

  convertHoursToTime(hours: number): string {
    const isNegative = hours < 0;
    if (hours < 0) hours = Math.abs(hours);
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


  // // ===== Checkbox enum-ændring (uændret, men triggrer beregning) =====
  // onCheckboxChange(selectedOption: TimePlanningMessagesEnum): void {
  //   if (selectedOption !== this.data.planningPrDayModels.message) {
  //     if (selectedOption !== TimePlanningMessagesEnum.DayOff) {
  //       this.data.planningPrDayModels.nettoHoursOverrideActive = true;
  //       this.data.planningPrDayModels.nettoHoursOverride = this.data.planningPrDayModels.planHours;
  //     } else {
  //       this.data.planningPrDayModels.nettoHoursOverrideActive = false;
  //     }
  //     this.data.planningPrDayModels.message = selectedOption;
  //     this.enumKeys.forEach(key => {
  //       this.data.planningPrDayModels[key] =
  //         selectedOption === TimePlanningMessagesEnum[key as keyof typeof TimePlanningMessagesEnum];
  //     });
  //   } else {
  //     this.data.planningPrDayModels.nettoHoursOverrideActive = false;
  //     this.data.planningPrDayModels.message = null;
  //   }
  //   this.calculatePlanHours();
  // }

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
  }

  // ===== Gem til model (kaldes fra Save) =====
  onUpdateWorkDayEntity() {
    // Læs plan fra form
    const p1 = this.workdayForm.get('planned.shift1')?.value as { start: string; break: string; stop: string };
    const p2 = this.workdayForm.get('planned.shift2')?.value as { start: string; break: string; stop: string };

    this.data.planningPrDayModels.plannedStartOfShift1 = this.convertTimeToMinutes(p1?.start);
    this.data.planningPrDayModels.plannedEndOfShift1   = this.convertTimeToMinutes(p1?.stop);
    this.data.planningPrDayModels.plannedBreakOfShift1 = this.convertTimeToMinutes(p1?.break);

    this.data.planningPrDayModels.plannedStartOfShift2 = this.convertTimeToMinutes(p2?.start);
    this.data.planningPrDayModels.plannedEndOfShift2   = this.convertTimeToMinutes(p2?.stop);
    this.data.planningPrDayModels.plannedBreakOfShift2 = this.convertTimeToMinutes(p2?.break);

    // Læs actual fra form (med 5-min intervaller)
    const a1 = this.workdayForm.get('actual.shift1')?.value as { start: string; pause: string; stop: string };
    const a2 = this.workdayForm.get('actual.shift2')?.value as { start: string; pause: string; stop: string };
    const a3 = this.workdayForm.get('actual.shift3')?.value as { start: string; pause: string; stop: string };
    const a4 = this.workdayForm.get('actual.shift4')?.value as { start: string; pause: string; stop: string };
    const a5 = this.workdayForm.get('actual.shift5')?.value as { start: string; pause: string; stop: string };

    this.data.planningPrDayModels.start1Id = this.convertTimeToMinutes(a1?.start, true);
    this.data.planningPrDayModels.pause1Id = this.convertTimeToMinutes(a1?.pause, true);
    this.data.planningPrDayModels.stop1Id  = this.convertTimeToMinutes(a1?.stop,  true);

    this.data.planningPrDayModels.start2Id = this.convertTimeToMinutes(a2?.start, true);
    this.data.planningPrDayModels.pause2Id = this.convertTimeToMinutes(a2?.pause, true);
    this.data.planningPrDayModels.stop2Id  = this.convertTimeToMinutes(a2?.stop,  true);

    this.data.planningPrDayModels.start3Id = this.convertTimeToMinutes(a3?.start, true);
    this.data.planningPrDayModels.pause3Id = this.convertTimeToMinutes(a3?.pause, true);
    this.data.planningPrDayModels.stop3Id  = this.convertTimeToMinutes(a3?.stop,  true);

    this.data.planningPrDayModels.start4Id = this.convertTimeToMinutes(a4?.start, true);
    this.data.planningPrDayModels.pause4Id = this.convertTimeToMinutes(a4?.pause, true);
    this.data.planningPrDayModels.stop4Id  = this.convertTimeToMinutes(a4?.stop,  true);

    this.data.planningPrDayModels.start5Id = this.convertTimeToMinutes(a5?.start, true);
    this.data.planningPrDayModels.pause5Id = this.convertTimeToMinutes(a5?.pause, true);
    this.data.planningPrDayModels.stop5Id  = this.convertTimeToMinutes(a5?.stop,  true);

    // Rens paidOutFlex
    this.data.planningPrDayModels.paidOutFlex =
      this.data.planningPrDayModels.paidOutFlex === null ? 0 : this.data.planningPrDayModels.paidOutFlex;
  }

  // ===== Genberegn plan/actual/todaysFlex og sumFlexEnd (samme logik som før, men baseret på form) =====
  calculatePlanHours(field: string = '') {
    const p1 = this.workdayForm.get('planned.shift1')?.value as { start: string; break: string; stop: string };
    const p2 = this.workdayForm.get('planned.shift2')?.value as { start: string; break: string; stop: string };

    this.data.planningPrDayModels.plannedStartOfShift1 = this.convertTimeToMinutes(p1?.start);
    this.data.planningPrDayModels.plannedEndOfShift1   = this.convertTimeToMinutes(p1?.stop);
    this.data.planningPrDayModels.plannedBreakOfShift1 = this.convertTimeToMinutes(p1?.break);

    this.data.planningPrDayModels.plannedStartOfShift2 = this.convertTimeToMinutes(p2?.start);
    this.data.planningPrDayModels.plannedEndOfShift2   = this.convertTimeToMinutes(p2?.stop);
    this.data.planningPrDayModels.plannedBreakOfShift2 = this.convertTimeToMinutes(p2?.break);

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
    if (this.data.planningPrDayModels.message === null) {
      if (plannedTimeInMinutes !== 0) {
        this.data.planningPrDayModels.planHours = plannedTimeInMinutes / 60;
        this.workdayForm.get('planHours')?.setValue(this.data.planningPrDayModels.planHours, { emitEvent: false });
      }
    }

    // Actual fra form (med 5-min intervaller)
    const a1 = this.workdayForm.get('actual.shift1')?.value as { start: string; pause: string; stop: string };
    const a2 = this.workdayForm.get('actual.shift2')?.value as { start: string; pause: string; stop: string };
    const a3 = this.workdayForm.get('actual.shift3')?.value as { start: string; pause: string; stop: string };
    const a4 = this.workdayForm.get('actual.shift4')?.value as { start: string; pause: string; stop: string };
    const a5 = this.workdayForm.get('actual.shift5')?.value as { start: string; pause: string; stop: string };

    this.data.planningPrDayModels.start1Id = this.convertTimeToMinutes(a1?.start, true);
    this.data.planningPrDayModels.stop1Id  = this.convertTimeToMinutes(a1?.stop,  true);
    this.data.planningPrDayModels.pause1Id = this.convertTimeToMinutes(a1?.pause, true) === 0 ? null : this.convertTimeToMinutes(a1?.pause, true);
    if (this.data.planningPrDayModels.pause1Id > 0) this.data.planningPrDayModels.pause1Id -= 1;

    this.data.planningPrDayModels.start2Id = this.convertTimeToMinutes(a2?.start, true);
    this.data.planningPrDayModels.stop2Id  = this.convertTimeToMinutes(a2?.stop,  true);
    this.data.planningPrDayModels.pause2Id = this.convertTimeToMinutes(a2?.pause, true) === 0 ? null : this.convertTimeToMinutes(a2?.pause, true);
    if (this.data.planningPrDayModels.pause2Id > 0) this.data.planningPrDayModels.pause2Id -= 1;

    this.data.planningPrDayModels.start3Id = this.convertTimeToMinutes(a3?.start, true);
    this.data.planningPrDayModels.stop3Id  = this.convertTimeToMinutes(a3?.stop,  true);
    this.data.planningPrDayModels.pause3Id = this.convertTimeToMinutes(a3?.pause, true) === 0 ? null : this.convertTimeToMinutes(a3?.pause, true);
    if (this.data.planningPrDayModels.pause3Id > 0) this.data.planningPrDayModels.pause3Id -= 1;

    this.data.planningPrDayModels.start4Id = this.convertTimeToMinutes(a4?.start, true);
    this.data.planningPrDayModels.stop4Id  = this.convertTimeToMinutes(a4?.stop,  true);
    this.data.planningPrDayModels.pause4Id = this.convertTimeToMinutes(a4?.pause, true) === 0 ? null : this.convertTimeToMinutes(a4?.pause, true);
    if (this.data.planningPrDayModels.pause4Id > 0) this.data.planningPrDayModels.pause4Id -= 1;

    this.data.planningPrDayModels.start5Id = this.convertTimeToMinutes(a5?.start, true);
    this.data.planningPrDayModels.stop5Id  = this.convertTimeToMinutes(a5?.stop,  true);
    this.data.planningPrDayModels.pause5Id = this.convertTimeToMinutes(a5?.pause, true) === 0 ? null : this.convertTimeToMinutes(a5?.pause, true);
    if (this.data.planningPrDayModels.pause5Id > 0) this.data.planningPrDayModels.pause5Id -= 1;

    // Summer actual
    let actualTimeInMinutes = 0;
    if (this.data.planningPrDayModels.stop1Id !== null) {
      actualTimeInMinutes =
        this.data.planningPrDayModels.stop1Id
        - this.data.planningPrDayModels.pause1Id
        - this.data.planningPrDayModels.start1Id;
    }
    if (this.data.planningPrDayModels.stop2Id !== null) {
      actualTimeInMinutes +=
        this.data.planningPrDayModels.stop2Id
        - this.data.planningPrDayModels.pause2Id
        - this.data.planningPrDayModels.start2Id;
    }
    if (this.data.planningPrDayModels.stop3Id !== null) {
      actualTimeInMinutes +=
        this.data.planningPrDayModels.stop3Id
        - this.data.planningPrDayModels.pause3Id
        - this.data.planningPrDayModels.start3Id;
    }
    if (this.data.planningPrDayModels.stop4Id !== null) {
      actualTimeInMinutes +=
        this.data.planningPrDayModels.stop4Id
        - this.data.planningPrDayModels.pause4Id
        - this.data.planningPrDayModels.start4Id;
    }
    if (this.data.planningPrDayModels.stop5Id !== null) {
      actualTimeInMinutes +=
        this.data.planningPrDayModels.stop5Id
        - this.data.planningPrDayModels.pause5Id
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
