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
    //this.tableHeaders = [];

    this.tableHeaders = [
      { header: this.translateService.stream('Workday shift'), field: 'shift' },
      {
        cellTemplate: this.plannedColumnTemplate,
        header: this.translateService.stream('Planned'),
        field: 'plannedStart',
        sortable: false,
      },
      {
        cellTemplate: this.actualColumnTemplate,
        header: this.translateService.stream('Actual'),
        field: 'actualStart',
        sortable: false,
      },
    ];
  }

  // columns = [
  //   { header: this.translateService.stream('Workday shift'), field: 'shift' },
  //   { header: this.translateService.stream('Planned'), field: 'planned',
  //     cellTemplate: this.plannedColumnTemplate, },
  //   { header: this.translateService.stream('Actual'), field: 'actual',
  //     cellTemplate: this.actualColumnTemplate, },
  // ];


  shift2Data = {
    shift: this.translateService.instant('2nd'),
    plannedStart: this.data.plannedStartOfShift2,
    plannedEnd: this.data.plannedEndOfShift2,
    plannedBreak: this.data.plannedBreakOfShift2,
    actualStart: this.data.start2StartedAt,
    actualEnd: this.data.stop2StoppedAt,
    actualBreak: this.data.break2Shift,
    //planned: this.data.plannedStartOfShift1 !== this.data.plannedEndOfShift1 && this.data.plannedEndOfShift2 !== 0 ? `${this.convertMinutesToTime(this.data.plannedStartOfShift2)} - ${this.convertMinutesToTime(this.data.plannedEndOfShift2)} / ${this.convertMinutesToTime(this.data.plannedBreakOfShift2)}` : '',
    //actual: this.data.start2StartedAt !== null ? `${this.datePipe.transform(this.data.start2StartedAt, 'HH:mm', 'UTC')} - ${this.data.stop2StoppedAt != null ? this.datePipe.transform(this.data.stop2StoppedAt, 'HH:mm', 'UTC') : ''}` : ''
  };

  shift1Data = {
    shift: this.translateService.instant('1st'),
    plannedStart: this.data.plannedStartOfShift1,
    plannedEnd: this.data.plannedEndOfShift1,
    plannedBreak: this.data.plannedBreakOfShift1,
    actualStart: this.data.start1StartedAt,
    actualEnd: this.data.stop1StoppedAt,
    actualBreak: this.data.break1Shift,
    //planned: this.data.plannedStartOfShift1 !== this.data.plannedEndOfShift1 ? `${this.convertMinutesToTime(this.data.plannedStartOfShift1)} - ${this.convertMinutesToTime(this.data.plannedEndOfShift1)} / ${this.convertMinutesToTime(this.data.plannedBreakOfShift1)}` : '',
    //actual: this.data.start1StartedAt !== null ? `${this.datePipe.transform(this.data.start1StartedAt, 'HH:mm', 'UTC')} - ${this.datePipe.transform(this.data.stop1StoppedAt, 'HH:mm', 'UTC')}` : ''
  };

  shiftData = (this.data.isDoubleShift ? [this.shift1Data, this.shift2Data] : [this.shift1Data]);

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
    this.planningsService.updatePlanning(this.data, this.data.id).subscribe();
    this.workdayEntityUpdate.emit(this.data);
  }

  onCancel() {
    this.data.message = this.originalData.message;
    this.enumKeys.forEach(key => {
      this.data[key] = this.originalData[key];
    });
  }
}
