// src/app/plugins/modules/time-planning-pn/components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component.ts
import {Component, EventEmitter, Inject, OnInit, TemplateRef, ViewChild} from '@angular/core';
import {
  MAT_DIALOG_DATA,
  MatDialogActions,
  MatDialogClose,
  MatDialogContent,
  MatDialogTitle
} from '@angular/material/dialog';
import {MatButton} from '@angular/material/button';
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
import {NgxMaterialTimepickerModule} from "ngx-material-timepicker";

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
    NgxMaterialTimepickerModule
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
    //this.tableHeaders = [];

    this.tableHeaders = [
      { header: this.translateService.stream('Workday shift'), field: 'shift' },
      {
        cellTemplate: this.plannedColumnTemplate,
        header: this.translateService.stream('Planned working hours'),
        field: 'plannedStart',
        sortable: false,
      },
      {
        cellTemplate: this.actualColumnTemplate,
        header: this.translateService.stream('Working hours'),
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

    this.shiftData = (this.data.isDoubleShift ? [shift1Data, shift2Data] : [shift1Data]);
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
    }
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
    // this.data.start1StartedAt = this.convertTimeToDateTimeOfToday(this.start1StartedAt);
    // this.data.stop1StoppedAt = this.convertTimeToDateTimeOfToday(this.stop1StoppedAt);
    // this.data.break1Shift = this.convertTimeToMinutes(this.break1Shift);
    // this.data.start2StartedAt = this.convertTimeToDateTimeOfToday(this.start2StartedAt);
    // this.data.stop2StoppedAt = this.convertTimeToDateTimeOfToday(this.stop2StoppedAt);
    this.planningsService.updatePlanning(this.data, this.data.id).subscribe();
    this.workdayEntityUpdate.emit(this.data);
  }

  convertTimeToDateTimeOfToday(hourMinutes: string): string {
    const today = new Date();
    const [hours, minutes] = hourMinutes.split(':');
    today.setHours(parseInt(hours, 10), parseInt(minutes, 10), 0, 0);
    return today.toISOString();
    }

  convertTimeToMinutes(plannedStartOfShift1: string, isFiveNumberIntervals: boolean = false): number {
    const parts = plannedStartOfShift1.split(':');
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

  onCancel() {
    this.data.message = this.originalData.message;
    this.enumKeys.forEach(key => {
      this.data[key] = this.originalData[key];
    });
  }
}
