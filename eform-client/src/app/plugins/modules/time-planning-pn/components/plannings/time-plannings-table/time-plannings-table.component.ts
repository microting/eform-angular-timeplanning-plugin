import {
  ChangeDetectorRef, Component, EventEmitter, Input, OnChanges, OnInit, Output,
  SimpleChanges, TemplateRef, ViewChild, ViewEncapsulation
} from '@angular/core';
import {AssignedSiteModel, TimePlanningModel} from '../../../models';
import {MtxGridColumn} from '@ng-matero/extensions/grid';
import {TranslateService} from '@ngx-translate/core';
import {TimePlanningPnPlanningsService, TimePlanningPnSettingsService} from '../../../services';
import {MatDialog} from '@angular/material/dialog';
import {AssignedSiteDialogComponent, WorkdayEntityDialogComponent} from '../';
import {DatePipe} from '@angular/common';
import * as R from 'ramda';
import {TimePlanningMessagesEnum} from '../../../enums';
import {Store} from "@ngrx/store";
import {selectAuthIsAdmin, selectCurrentUserIsFirstUser} from "src/app/state";

@Component({
  selector: 'app-time-plannings-table',
  templateUrl: './time-plannings-table.component.html',
  styleUrls: ['./time-plannings-table.component.scss'],
  encapsulation: ViewEncapsulation.None,
  standalone: false

})
export class TimePlanningsTableComponent implements OnInit, OnChanges {
  @Input() timePlannings: TimePlanningModel[] = [];
  @Input() dateFrom!: Date;
  @Input() dateTo!: Date;
  @Output() timePlanningChanged: EventEmitter<any> = new EventEmitter<any>();
  @Output() assignedSiteChanged: EventEmitter<any> = new EventEmitter<any>();
  @Output() sortChanged: EventEmitter<string> = new EventEmitter<string>();
  tableHeaders: MtxGridColumn[] = [];
  enumKeys: string[];
  currentLocale: string = 'da';

  @ViewChild('firstColumnTemplate', {static: true}) firstColumnTemplate!: TemplateRef<any>;
  @ViewChild('dayColumnTemplate', {static: true}) dayColumnTemplate!: TemplateRef<any>;
  protected selectAuthIsAdmin$ = this.store.select(selectAuthIsAdmin);
  public selectCurrentUserIsFirstUser$ = this.store.select(selectCurrentUserIsFirstUser);

  constructor(
    private store: Store,
    private planningsService: TimePlanningPnPlanningsService,
    private timePlanningPnSettingsService: TimePlanningPnSettingsService,
    private dialog: MatDialog,
    private translateService: TranslateService,
    protected datePipe: DatePipe,
    private cdr: ChangeDetectorRef
  ) {
  }

  ngOnInit(): void {
    this.enumKeys = Object.keys(TimePlanningMessagesEnum).filter(key => isNaN(Number(key)));
    this.updateTableHeaders();
    this.translateService.onLangChange.subscribe((lang) => {
      this.currentLocale = lang.lang;
      this.updateTableHeaders();
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes.dateFrom || changes.dateTo) {
      if (changes.dateFrom !== undefined) {
        this.dateFrom = changes.dateFrom.currentValue;
      }
      if (changes.dateTo !== undefined) {
        this.dateTo = changes.dateTo.currentValue;
        this.updateTableHeaders();
      }
    }
  }

  private updateTableHeaders(): void {
    this.tableHeaders = [];
    this.cdr.detectChanges();
    const startDate = new Date(this.dateFrom);
    const endDate = new Date(this.dateTo);
    const today = new Date();
    const daysCount = Math.ceil((endDate.getTime() - startDate.getTime()) / (1000 * 3600 * 24));
    let todayTranslated = this.translateService.stream('Today');

    this.tableHeaders = [
      {
        cellTemplate: this.firstColumnTemplate,
        header: this.translateService.stream('Name'),
        pinned: 'left',
        field: 'siteName',
        sortable: true,
      },
      ...Array.from({length: daysCount}).map((_, index) => {
        const currentDate = new Date(startDate);
        currentDate.setDate(startDate.getDate() + index);
        const isToday = currentDate.toDateString() === today.toDateString();
        const formattedDate = isToday
          ? todayTranslated
          : this.datePipe.transform(currentDate, 'E dd/MM', undefined, this.currentLocale) || '';
        return {
          cellTemplate: this.dayColumnTemplate,
          header: formattedDate,
          field: index.toString(),
          sortable: false,
          class: (row: any) => this.getCellClass(row, index.toString()),
        };
      }),
    ];
    this.cdr.detectChanges();
  }

  // sortTable(sort: string) {
  //   this.sortChanged.emit(sort);
  // }
  //
  // onTimePlanningChanged(
  //   planHours: number,
  //   planText: string,
  //   message: number,
  //   timePlanning: TimePlanningModel
  // ) {
  //   this.timePlanningChanged.emit({
  //     ...timePlanning,
  //     planHours: planHours ?? timePlanning.planHours,
  //     message: message ?? timePlanning.message,
  //     planText: planText ?? timePlanning.planText,
  //   });
  // }

  getCellClass(row: any, field: string): string {
    try {
      const cellData = row.planningPrDayModels[field];
      if (!cellData) {
        return '';
      }

      const { planHours, nettoHoursOverrideActive, plannedStartOfShift1, message, workerComment } = cellData;
      let workDayStarted = cellData.start1StartedAt || cellData.start2StartedAt;
      let workDayEnded = cellData.workDayEnded;

      // If netto hours override is active, use the override value
      if (nettoHoursOverrideActive) {
        workDayStarted = true;
        workDayEnded = true;
      }

      // Case 1: Has planned hours
      if (planHours > 0) {
        if (workDayStarted) {
          return workDayEnded ? 'green-background' : 'grey-background';
        } else {
          return 'grey-background';
        }
      }

      // Case 2: No planned hours
      return this.getCellClassForNoPlanHours(workDayStarted, workDayEnded, plannedStartOfShift1, message, workerComment);
    } catch (e) {
      return '';
    }
  }

  private getCellClassForNoPlanHours(
    workDayStarted: boolean,
    workDayEnded: boolean,
    plannedStarted: any,
    message: any,
    workerComment: any
  ): string {
    if (workDayStarted) {
      return workDayEnded ? 'green-background' : 'red-background';
    }
    
    if (plannedStarted) {
      return 'grey-background';
    }
    
    if (message || workerComment) {
      return 'grey-background';
    }
    
    return 'white-background';
  }

  getCellTextColor(row: any, field: string): string {
    const cellData = row.planningPrDayModels[field];
    if (!cellData) {
      return 'black-text';
    }

    const { planHours, nettoHoursOverrideActive, plannedStartOfShift1, message, workerComment, date } = cellData;
    let workDayStarted = cellData.start1StartedAt || cellData.start2StartedAt;
    let workDayEnded = cellData.workDayEnded;

    // If netto hours override is active, use the override value
    if (nettoHoursOverrideActive) {
      workDayStarted = true;
      workDayEnded = true;
    }

    const isInOlderThanToday = new Date(date) < new Date();

    // Case 1: Has planned hours
    if (planHours > 0) {
      if (workDayStarted) {
        return workDayEnded ? 'white-text' : 'red-text';
      } else {
        return isInOlderThanToday ? 'red-text' : 'black-text';
      }
    }

    // Case 2: No planned hours
    return this.getCellTextColorForNoPlanHours(
      workDayStarted,
      workDayEnded,
      plannedStartOfShift1,
      message,
      workerComment
    );
  }

  private getCellTextColorForNoPlanHours(
    workDayStarted: boolean,
    workDayEnded: boolean,
    plannedStarted: any,
    message: any,
    workerComment: any
  ): string {
    if (workDayStarted) {
      return workDayEnded ? 'black-text' : 'white-text';
    }

    if (plannedStarted) {
      return (message || workerComment) ? 'black-text' : 'white-text';
    }

    return (message || workerComment) ? 'black-text' : 'white-text';
  }

  getCellTextColorForDay(row: any, field: string): string {
    const sumFlexEnd = row.planningPrDayModels[field]?.sumFlexEnd;
    const workDayStarted = row.planningPrDayModels[field]?.workDayStarted;
    const workDayEnded = row.planningPrDayModels[field]?.workDayEnded;
    const isInOlderThanToday = new Date(row.planningPrDayModels[field]?.date) < new Date();
    if (sumFlexEnd >= 0) {
      if (workDayStarted) {
        //console.log('getCellTextColorForDay', row, field, planHours, workDayStarted, workDayEnded);
        return workDayEnded ? 'black-text' : 'black-text';
      }
      else {
        return isInOlderThanToday ? 'red-text' : 'black-text';
      }
    }
    return 'red-text';
  }

  protected readonly JSON = JSON;

  isInOlderThanToday(date: Date): boolean {
    // take the midnight of the date to compare ot today's midnight
    if (!date) {
      return false;
    }
    // Convert the date to a string to ensure it's in a valid format
    if (typeof date === 'string') {
      date = new Date(date);
    }
    // Compare the date with today's date
    if (isNaN(date.getTime())) {
      console.error('Invalid date:', date);
      return false; // or handle the error as needed
    }
    // Create a new Date object for today at midnight
    const todayMidnight = new Date();
    todayMidnight.setHours(0, 0, 0, 0); // Set to midnight
    const dateMidnight = new Date(date);
    dateMidnight.setHours(0, 0, 0, 0); // Set to midnight
    return dateMidnight <todayMidnight;
  }

  getStopTimeDisplay(startedAt: string | null, stoppedAt: string | null): string {
    if (!startedAt || !stoppedAt) return '';
    const startDate = new Date(startedAt);
    const stopDate = new Date(stoppedAt);
    if (
      startDate.getUTCFullYear() !== stopDate.getUTCFullYear() ||
      startDate.getUTCMonth() !== stopDate.getUTCMonth() ||
      startDate.getUTCDate() !== stopDate.getUTCDate()
    ) {
      return '24:00';
    }
    return this.datePipe.transform(stoppedAt, 'HH:mm', 'UTC') ?? '';
  }

  onFirstColumnClick(row: any): void {
    const siteId = row.siteId; // Adjust this according to your data structure
    this.timePlanningPnSettingsService.getAssignedSite(siteId).subscribe(result => {
      if (result && result.success) {
        this.dialog.open(AssignedSiteDialogComponent, {
          data: result.model,
          minWidth: '50%',
        })
          .afterClosed().subscribe((data: any) => {
          if (data !== '' && data !== undefined) {
            this.timePlanningPnSettingsService.updateAssignedSite(data).subscribe(result => {
              if (result && result.success) {
                this.assignedSiteChanged.emit(data);
              }
            });
          }
        });
      }
    });
  }

  onDayColumnClick(row: any, field: string): void {
    const siteId = row.siteId;
    const cellData = R.clone(row.planningPrDayModels[field]);
    this.timePlanningPnSettingsService.getAssignedSite(siteId).subscribe(result => {
      if (result && result.success) {
        this.dialog.open(WorkdayEntityDialogComponent, {
          data: {planningPrDayModels: cellData, assignedSiteModel: result.model},
        })
          .afterClosed().subscribe((data: any) => {
          if (data !== '' && data !== undefined) {
            this.planningsService.updatePlanning(data.planningPrDayModels, data.planningPrDayModels.id).subscribe(result => {
              if (result && result.success) {
                this.timePlanningChanged.emit(data);
              }
            });
          }
        });
      }
    });
  }

  convertMinutesToTime(minutes: number): string {
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return `${this.padZero(hours)}:${this.padZero(mins)}`;
  }

  convertHoursToTime(hours: number): string {
    const isNegative = hours < 0;
    if (hours < 0) {
      hours = Math.abs(hours);
    }
    const totalMinutes = Math.round(hours * 60)
    const hrs = Math.floor(totalMinutes / 60);
    let mins = totalMinutes % 60;
    if (isNegative) {
      // return '${padZero(hrs)}:${padZero(60 - mins)}';
      return `-${hrs}:${this.padZero(mins)}`;
    }
    return `${this.padZero(hrs)}:${this.padZero(mins)}`;
  }

  convertHoursToTimeWithTranslations(hours: number): string {
    const totalMinutes = Math.floor(hours * 60)
    const hrs = Math.floor(totalMinutes / 60);
    const mins = totalMinutes % 60;
    return `${this.padZero(hrs)} ${this.translateService.instant('hours')} ${this.padZero(mins)} ${this.translateService.instant(
      'minutes')}`;
  }

  padZero(num: number): string {
    return num < 10 ? '0' + num : num.toString();
  }

  protected readonly Date = Date;
  protected readonly Math = Math;
}
