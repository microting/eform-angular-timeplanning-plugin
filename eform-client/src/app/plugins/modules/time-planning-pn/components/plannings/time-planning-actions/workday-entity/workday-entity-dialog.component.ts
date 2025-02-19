// src/app/plugins/modules/time-planning-pn/components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component.ts
import {Component, EventEmitter, Inject, OnInit} from '@angular/core';
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
import {PlanningPrDayModel} from 'src/app/plugins/modules/time-planning-pn/models';
import {MatCheckbox} from '@angular/material/checkbox';
import {FormsModule} from '@angular/forms';
import {TimePlanningMessagesEnum} from 'src/app/plugins/modules/time-planning-pn/enums';
import {
  PlanningPrDayUpdateModel
} from "src/app/plugins/modules/time-planning-pn/models/plannings/planning-pr-day-update.model";
import {MtxGrid} from "@ng-matero/extensions/grid";

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
    MtxGrid
  ],
  styleUrls: ['./workday-entity-dialog.component.scss']
})
export class WorkdayEntityDialogComponent implements OnInit {
  TimePlanningMessagesEnum = TimePlanningMessagesEnum;
  workdayEntityUpdate: EventEmitter<PlanningPrDayUpdateModel> = new EventEmitter<PlanningPrDayUpdateModel>();
  enumKeys: string[];
  constructor(
    @Inject(MAT_DIALOG_DATA) public data: PlanningPrDayModel,
    protected datePipe: DatePipe,
    private translateService: TranslateService,
  ) {}

  protected readonly JSON = JSON;

  ngOnInit(): void {
    this.enumKeys = Object.keys(TimePlanningMessagesEnum).filter(key => isNaN(Number(key)));
  }

  columns = [
    { header: this.translateService.stream('Shift'), field: 'shift' },
    { header: this.translateService.stream('Planned'), field: 'planned' },
    { header: this.translateService.stream('Actual'), field: 'actual' }
  ];

  shift1Data = {
    shift: this.translateService.instant('1st'),
    planned: `${this.convertMinutesToTime(this.data.plannedStartOfShift1)} - ${this.convertMinutesToTime(this.data.plannedEndOfShift1)} / ${this.convertMinutesToTime(this.data.plannedBreakOfShift1)}`,
    actual: this.data.start1StartedAt !== null ? `${this.datePipe.transform(this.data.start1StartedAt, 'HH:mm', 'UTC')} - ${this.datePipe.transform(this.data.stop1StoppedAt, 'HH:mm', 'UTC')}` : ''
  };

  shift2Data = {
    shift: this.translateService.instant('2nd'),
    planned: `${this.convertMinutesToTime(this.data.plannedStartOfShift2)} - ${this.convertMinutesToTime(this.data.plannedEndOfShift2)} / ${this.convertMinutesToTime(this.data.plannedBreakOfShift2)}`,
    actual: this.data.start2StartedAt !== null ? `${this.datePipe.transform(this.data.start2StartedAt, 'HH:mm', 'UTC')} - ${this.datePipe.transform(this.data.stop2StoppedAt, 'HH:mm', 'UTC')}` : ''
  };

  convertMinutesToTime(minutes: number): string {
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return `${this.padZero(hours)}:${this.padZero(mins)}`;
  }

  private padZero(num: number): string {
    return num < 10 ? '0' + num : num.toString();
  }

  onCheckboxChange(selectedOption: TimePlanningMessagesEnum): void {
    this.data.message = selectedOption;
    this.enumKeys.forEach(key => {
      this.data[key] = selectedOption === TimePlanningMessagesEnum[key as keyof typeof TimePlanningMessagesEnum];
    });
  }

  onUpdateWorkDayEntity() {

  }
}
