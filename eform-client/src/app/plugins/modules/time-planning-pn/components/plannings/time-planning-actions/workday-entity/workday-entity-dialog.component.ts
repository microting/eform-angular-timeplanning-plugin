// src/app/plugins/modules/time-planning-pn/components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component.ts
import {Component, EventEmitter, Inject, OnInit, TemplateRef, ViewChild} from '@angular/core';
import {
  MAT_DIALOG_DATA,
  MatDialogActions,
  MatDialogClose,
  MatDialogContent,
  MatDialogTitle
} from '@angular/material/dialog';
import {MatButton, MatIconButton} from '@angular/material/button';
import {TranslatePipe, TranslateService} from '@ngx-translate/core';
import {DatePipe, NgForOf, NgIf} from '@angular/common';
import {MatCheckbox} from '@angular/material/checkbox';
import {FormsModule} from '@angular/forms';
import {TimePlanningMessagesEnum} from '../../../../enums';
import {
  PlanningPrDayModel,
  PlanningPrDayUpdateModel
} from '../../../../models';
import {MtxGrid, MtxGridColumn} from '@ng-matero/extensions/grid';
import {TimePlanningPnPlanningsService} from '../../../../services';
import * as R from 'ramda';
import {MatFormField, MatLabel} from '@angular/material/form-field';
import {MatInput} from '@angular/material/input';
import {NgxMaterialTimepickerModule} from 'ngx-material-timepicker';
import {MatIcon} from "@angular/material/icon";

@Component({
  selector: 'app-workday-entity-dialog',
  templateUrl: './workday-entity-dialog.component.html',
  imports: [
    MatButton,
    MatDialogActions,
    MatDialogClose,
    TranslatePipe,
    MatDialogTitle,
    MatDialogContent,
    MatCheckbox,
    FormsModule,
    NgForOf,
    NgIf,
    MtxGrid,
    MatFormField,
    MatInput,
    MatLabel,
    NgxMaterialTimepickerModule,
    MatIconButton,
    MatIcon
  ],
  styleUrls: ['./workday-entity-dialog.component.scss']
})
export class WorkdayEntityDialogComponent implements OnInit {
  TimePlanningMessagesEnum = TimePlanningMessagesEnum;
  workdayEntityUpdate: EventEmitter<PlanningPrDayUpdateModel> = new EventEmitter<PlanningPrDayUpdateModel>();
  enumKeys: string[];
  originalData: PlanningPrDayModel;
  tableHeaders: MtxGridColumn[] = [];
  shiftData: any[] = [];
  plannedStartOfShift1: string;
  plannedEndOfShift1: string;
  plannedBreakOfShift1: string;
  plannedStartOfShift2: string;
  plannedEndOfShift2: string;
  plannedBreakOfShift2: string;
  start1StartedAt: string;
  stop1StoppedAt: string;
  pause1Id: string;
  start2StartedAt: string;
  stop2StoppedAt: string;
  pause2Id: string;
  isInTheFuture: boolean = false;
  maxPause1Id: number = 0;
  maxPause2Id: number = 0;
  todaysFlex: number = 0;
  date: any;
  @ViewChild('plannedColumnTemplate', { static: true }) plannedColumnTemplate!: TemplateRef<any>;
  @ViewChild('actualColumnTemplate', { static: true }) actualColumnTemplate!: TemplateRef<any>;
  constructor(
    private planningsService: TimePlanningPnPlanningsService,
    @Inject(MAT_DIALOG_DATA) public data: PlanningPrDayModel,
    protected datePipe: DatePipe,
    private translateService: TranslateService,
  ) {}

  protected readonly JSON = JSON;

  ngOnInit(): void {
    this.originalData = R.clone(this.data);
    this.enumKeys = Object.keys(TimePlanningMessagesEnum).filter(key => isNaN(Number(key)));
    this.data[this.enumKeys[this.data.message - 1]] = true;
    this.plannedStartOfShift1 = this.convertMinutesToTime(this.data.plannedStartOfShift1);
    this.plannedEndOfShift1 = this.convertMinutesToTime(this.data.plannedEndOfShift1);
    this.plannedBreakOfShift1 = this.convertMinutesToTime(this.data.plannedBreakOfShift1);
    this.plannedStartOfShift2 = this.convertMinutesToTime(this.data.plannedStartOfShift2);
    this.plannedEndOfShift2 = this.convertMinutesToTime(this.data.plannedEndOfShift2);
    this.plannedBreakOfShift2 = this.convertMinutesToTime(this.data.plannedBreakOfShift2);
    this.start1StartedAt = this.datePipe.transform(this.data.start1StartedAt, 'HH:mm', 'UTC')
    this.stop1StoppedAt =  this.datePipe.transform(this.data.stop1StoppedAt, 'HH:mm', 'UTC');
    this.pause1Id = this.convertMinutesToTime(this.data.pause1Id * 5);
    this.start2StartedAt = this.datePipe.transform(this.data.start2StartedAt, 'HH:mm', 'UTC');
    this.stop2StoppedAt = this.datePipe.transform(this.data.stop2StoppedAt, 'HH:mm', 'UTC');
    this.pause2Id = this.convertMinutesToTime(this.data.pause2Id * 5);
    this.isInTheFuture = Date.parse(this.data.date) > Date.now();
    this.todaysFlex = this.data.actualHours - this.data.planHours;
    this.date = Date.parse(this.data.date);
    //this.tableHeaders = [];

    this.tableHeaders = [
      { header: this.translateService.stream('Shift'), field: 'shift',
        pinned: 'left'},
      {
        cellTemplate: this.plannedColumnTemplate,
        header: this.translateService.stream('Planned'),
        field: 'plannedStart',
        sortable: false,
      },
      {
        cellTemplate: this.actualColumnTemplate,
        header: this.translateService.stream('Registered'),
        field: 'actualStart',
        sortable: false,
      },
    ];



    let shift2Data = {
      shift: this.translateService.instant('2nd'),
      plannedStart: this.plannedStartOfShift2,
      plannedEnd: this.plannedEndOfShift2,
      plannedBreak: this.plannedBreakOfShift2,
      actualStart: this.start2StartedAt,
      actualEnd: this.stop2StoppedAt,
      actualBreak: this.pause2Id,
      // eslint-disable-next-line max-len
      //planned: this.data.plannedStartOfShift1 !== this.data.plannedEndOfShift1 && this.data.plannedEndOfShift2 !== 0 ? `${this.convertMinutesToTime(this.data.plannedStartOfShift2)} - ${this.convertMinutesToTime(this.data.plannedEndOfShift2)} / ${this.convertMinutesToTime(this.data.plannedBreakOfShift2)}` : '',
      // eslint-disable-next-line max-len
      //actual: this.data.start2StartedAt !== null ? `${this.datePipe.transform(this.data.start2StartedAt, 'HH:mm', 'UTC')} - ${this.data.stop2StoppedAt != null ? this.datePipe.transform(this.data.stop2StoppedAt, 'HH:mm', 'UTC') : ''}` : ''
    };

    let shift1Data = {
      shift: this.translateService.instant('1st'),
      plannedStart: this.plannedStartOfShift1,
      plannedEnd: this.plannedEndOfShift1,
      plannedBreak: this.plannedBreakOfShift1,
      actualStart: this.start1StartedAt,
      actualEnd: this.stop1StoppedAt,
      actualBreak: this.pause1Id,
      // eslint-disable-next-line max-len
      //planned: this.data.plannedStartOfShift1 !== this.data.plannedEndOfShift1 ? `${this.convertMinutesToTime(this.data.plannedStartOfShift1)} - ${this.convertMinutesToTime(this.data.plannedEndOfShift1)} / ${this.convertMinutesToTime(this.data.plannedBreakOfShift1)}` : '',
      // eslint-disable-next-line max-len
      //actual: this.data.start1StartedAt !== null ? `${this.datePipe.transform(this.data.start1StartedAt, 'HH:mm', 'UTC')} - ${this.datePipe.transform(this.data.stop1StoppedAt, 'HH:mm', 'UTC')}` : ''
    };

    // this.shiftData = (this.data.isDoubleShift ? [shift1Data, shift2Data] : [shift1Data]);
    this.shiftData = [shift1Data, shift2Data];
  }

  // columns = [
  //   { header: this.translateService.stream('Workday shift'), field: 'shift' },
  //   { header: this.translateService.stream('Planned'), field: 'planned',
  //     cellTemplate: this.plannedColumnTemplate, },
  //   { header: this.translateService.stream('Actual'), field: 'actual',
  //     cellTemplate: this.actualColumnTemplate, },
  // ];

  convertMinutesToTime(minutes: number): string {
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return `${this.padZero(hours)}:${this.padZero(mins)}`;
  }

  private padZero(num: number): string {
    return num < 10 ? '0' + num : num.toString();
  }

  onCheckboxChange(selectedOption: TimePlanningMessagesEnum): void {
    if (selectedOption !== this.data.message) {
      this.data.message = selectedOption;
      this.enumKeys.forEach(key => {
        this.data[key] = selectedOption === TimePlanningMessagesEnum[key as keyof typeof TimePlanningMessagesEnum];
      });
    }
    else {
      this.data.message = null;
      this.calculatePlanHours();
    }
  }

  resetPlannedTimes(number: number) {
    switch (number) {
      case 1:
        this.plannedStartOfShift1 = '00:00';
        this.plannedBreakOfShift1 = '00:00';
        this.plannedEndOfShift1= '00:00';
        this.plannedStartOfShift2= '00:00';
        this.plannedBreakOfShift2= '00:00';
        this.plannedEndOfShift2= '00:00';
        break;
      case 2:
        this.plannedBreakOfShift1= '00:00';
        break;
      case 3:
        this.plannedBreakOfShift1= '00:00';
        this.plannedEndOfShift1= '00:00';
        this.plannedStartOfShift2= '00:00';
        this.plannedBreakOfShift2= '00:00';
        this.plannedEndOfShift2= '00:00';
        break;
      case 4:
        this.plannedStartOfShift2= '00:00';
        this.plannedBreakOfShift2= '00:00';
        this.plannedEndOfShift2= '00:00';
        break;
      case 5:
        this.plannedBreakOfShift2= '00:00';
        break;
      case 6:
        this.plannedBreakOfShift2= '00:00';
        this.plannedEndOfShift2= '00:00';
        break;
    }
    this.calculatePlanHours();
  }

  resetActualTimes(number: number) {
    switch (number) {
      case 1:
        this.start1StartedAt  = null;
        this.pause1Id  = null;
        this.stop1StoppedAt  = null;
        this.start2StartedAt  = null;
        this.pause2Id  = null;
        this.stop2StoppedAt  = null;
        break;
      case 2:
        this.pause1Id  = null;
        break;
      case 3:
        this.pause1Id  = null;
        this.stop1StoppedAt  = null;
        this.start2StartedAt  = null;
        this.pause2Id  = null;
        this.stop2StoppedAt  = null;
        break;
      case 4:
        this.start2StartedAt  = null;
        this.pause2Id  = null;
        this.stop2StoppedAt  = null;
        break;
      case 5:
        this.pause2Id  = null;
        break;
      case 6:
        this.pause2Id  = null;
        this.stop2StoppedAt = null;
        break;
    }
    this.calculatePlanHours();
  }

  onUpdateWorkDayEntity() {
    this.data.plannedStartOfShift1 = this.convertTimeToMinutes(this.plannedStartOfShift1);
    this.data.plannedEndOfShift1 = this.convertTimeToMinutes(this.plannedEndOfShift1);
    this.data.plannedBreakOfShift1 = this.convertTimeToMinutes(this.plannedBreakOfShift1);
    this.data.plannedStartOfShift2 = this.convertTimeToMinutes(this.plannedStartOfShift2);
    this.data.plannedEndOfShift2 = this.convertTimeToMinutes(this.plannedEndOfShift2);
    this.data.plannedBreakOfShift2 = this.convertTimeToMinutes(this.plannedBreakOfShift2);
    this.data.start1Id = this.convertTimeToMinutes(this.start1StartedAt, true);
    this.data.pause1Id = this.convertTimeToMinutes(this.pause1Id, true);
    this.data.start2Id = this.convertTimeToMinutes(this.start2StartedAt, true);
    this.data.stop1Id = this.convertTimeToMinutes(this.stop1StoppedAt, true);
    this.data.pause2Id = this.convertTimeToMinutes(this.pause2Id, true);
    this.data.stop2Id = this.convertTimeToMinutes(this.stop2StoppedAt, true);
    this.data.paidOutFlex = this.data.paidOutFlex === null ? 0 : this.data.paidOutFlex;
    // this.data.start1StartedAt = this.convertTimeToDateTimeOfToday(this.start1StartedAt);
    // this.data.stop1StoppedAt = this.convertTimeToDateTimeOfToday(this.stop1StoppedAt);
    // this.data.break1Shift = this.convertTimeToMinutes(this.break1Shift);
    // this.data.start2StartedAt = this.convertTimeToDateTimeOfToday(this.start2StartedAt);
    // this.data.stop2StoppedAt = this.convertTimeToDateTimeOfToday(this.stop2StoppedAt);
    this.planningsService.updatePlanning(this.data, this.data.id).subscribe(
      () => {
        this.workdayEntityUpdate.emit(this.data);
      }
    );
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

  convertTimeToDateTimeOfToday(hourMinutes: string): string {
    const today = new Date();
    const [hours, minutes] = hourMinutes.split(':');
    today.setHours(parseInt(hours, 10), parseInt(minutes, 10), 0, 0);
    return today.toISOString();
    }

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
        return result + 1
      }
      return 0;
    }
    return hours * 60 + minutes;
  }

  convertHoursToTime(hours: number): string {
    const isNegative = hours < 0;
    if (hours < 0) {
      hours = Math.abs(hours);
    }
    const totalMinutes = Math.floor(hours * 60)
    const hrs = Math.floor(totalMinutes / 60);
    let mins = totalMinutes % 60;
    if (isNegative) {
      // return '${padZero(hrs)}:${padZero(60 - mins)}';
      return `-${hrs}:${this.padZero(mins)}`;
    }
    return `${this.padZero(hrs)}:${this.padZero(mins)}`;
  }

  onCancel() {
    this.data.message = this.originalData.message;
    this.enumKeys.forEach(key => {
      this.data[key] = this.originalData[key];
    });
  }

  calculatePlanHours() {
    this.data.plannedStartOfShift1 = this.convertTimeToMinutes(this.plannedStartOfShift1);
    this.data.plannedEndOfShift1 = this.convertTimeToMinutes(this.plannedEndOfShift1);
    this.data.plannedBreakOfShift1 = this.convertTimeToMinutes(this.plannedBreakOfShift1);
    this.data.plannedStartOfShift2 = this.convertTimeToMinutes(this.plannedStartOfShift2);
    this.data.plannedEndOfShift2 = this.convertTimeToMinutes(this.plannedEndOfShift2);
    this.data.plannedBreakOfShift2 = this.convertTimeToMinutes(this.plannedBreakOfShift2);
    let plannedTimeInMinutes = 0;
    if (this.data.plannedEndOfShift1 !== 0) {
      plannedTimeInMinutes = this.data.plannedEndOfShift1 - this.data.plannedStartOfShift1 - this.data.plannedBreakOfShift1;
    }
    if (this.data.plannedEndOfShift2 !== 0) {
      let timeInMinutes2NdShift = this.data.plannedEndOfShift2 - this.data.plannedStartOfShift2 - this.data.plannedBreakOfShift2;
      plannedTimeInMinutes += timeInMinutes2NdShift;
    }
    if (this.data.message === null) {
      this.data.planHours = plannedTimeInMinutes / 60;
    }

    this.data.start1Id = this.convertTimeToMinutes(this.start1StartedAt, true);
    this.data.pause1Id = this.convertTimeToMinutes(this.pause1Id, true) === 0 ? null : this.convertTimeToMinutes(this.pause1Id, true);
    if (this.data.pause1Id > 0) {
      this.data.pause1Id -= 1;
    }
    this.data.start2Id = this.convertTimeToMinutes(this.start2StartedAt, true);
    this.data.stop1Id = this.convertTimeToMinutes(this.stop1StoppedAt, true);
    this.data.pause2Id = this.convertTimeToMinutes(this.pause2Id, true) === 0 ? null : this.convertTimeToMinutes(this.pause2Id, true);
    if (this.data.pause2Id > 0) {
      this.data.pause2Id -= 1;
    }
    this.data.stop2Id = this.convertTimeToMinutes(this.stop2StoppedAt, true);

    let actualTimeInMinutes = 0;
    if (this.data.stop1Id !== null) {
      actualTimeInMinutes = this.data.stop1Id - this.data.pause1Id - this.data.start1Id;
    }
    if (this.data.stop2Id !== null) {
      let timeInMinutes2NdShift = this.data.stop2Id - this.data.pause2Id - this.data.start2Id;
      actualTimeInMinutes += timeInMinutes2NdShift;
    }
    if (actualTimeInMinutes !== 0) {
      // actualTimeInMinutes += 1;
      actualTimeInMinutes *= 5;
    }
    this.data.actualHours = actualTimeInMinutes / 60;

    this.todaysFlex = this.data.actualHours - this.data.planHours;
    this.data.sumFlexEnd = this.data.sumFlexStart + this.data.actualHours - this.data.planHours - this.data.paidOutFlex;
  }
}
