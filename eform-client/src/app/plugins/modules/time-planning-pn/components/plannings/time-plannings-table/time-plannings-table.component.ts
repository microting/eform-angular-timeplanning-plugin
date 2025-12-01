import {
  ChangeDetectorRef, Component, EventEmitter, Input, OnChanges, OnInit, Output,
  SimpleChanges, TemplateRef, ViewChild, ViewEncapsulation,
  inject
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
import {AndroidIcon, iOSIcon} from "src/app/common/const";
import {MatIconRegistry} from "@angular/material/icon";
import {DomSanitizer} from "@angular/platform-browser";

@Component({
  selector: 'app-time-plannings-table',
  templateUrl: './time-plannings-table.component.html',
  styleUrls: ['./time-plannings-table.component.scss'],
  encapsulation: ViewEncapsulation.None,
  standalone: false

})
export class TimePlanningsTableComponent implements OnInit, OnChanges {
  private store = inject(Store);
  private planningsService = inject(TimePlanningPnPlanningsService);
  private timePlanningPnSettingsService = inject(TimePlanningPnSettingsService);
  private dialog = inject(MatDialog);
  private translateService = inject(TranslateService);
  protected datePipe = inject(DatePipe);
  private cdr = inject(ChangeDetectorRef);
  private iconRegistry = inject(MatIconRegistry);
  private sanitizer = inject(DomSanitizer);

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

  ngOnInit(): void {
    this.iconRegistry.addSvgIconLiteral('android-icon', this.sanitizer.bypassSecurityTrustHtml(AndroidIcon));
    this.iconRegistry.addSvgIconLiteral('ios-icon', this.sanitizer.bypassSecurityTrustHtml(iOSIcon));
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
    const tempEndDate = new Date(endDate);
    tempEndDate.setHours(0, 0, 0, 0);
    const diff = (tempEndDate.getTime() - startDate.getTime()) / (1000 * 3600 * 24);
    let daysCount = Math.floor(diff) +1;
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

  normalizeFlex(value: any): string {
    if (value === null || value === undefined || isNaN(value)) {return '0.00';}
    const rounded = Number(value).toFixed(2);
    return rounded === '-0.00' ? '0.00' : rounded;
  }

  getCellClass(row: any, field: string): string {
    // const date = row.planningPrDayModels[field]?.date;
    try {
      const cellData = row.planningPrDayModels[field];
      if (!cellData) {
        return '';
      }
      let isInTheFuture = new Date(row.planningPrDayModels[field]?.date) > new Date();

      const { planHours, nettoHoursOverrideActive, plannedStartOfShift1, message, workerComment, commentOffice, paidOutFlex } = cellData;
      let workDayStarted = cellData.start1StartedAt || cellData.start2StartedAt;
      let workDayEnded = cellData.workDayEnded;

      // If netto hours override is active, use the override value
      if (nettoHoursOverrideActive) {
        workDayStarted = true;
        workDayEnded = true;
      }
      if (isInTheFuture) {
        if (planHours > 0) {
          return 'grey-background';
        } else {
          if (message || workerComment || commentOffice || paidOutFlex) {
            return 'grey-background';
          }
          return 'white-background';
        }
      }

      // Case 1: Has planned hours
      if (planHours > 0) {
        if (workDayStarted) {
          return workDayEnded ? 'green-background' : 'grey-background';
        } else {
          return 'red-background';
        }
      }

      // Case 2: No planned hours
      // eslint-disable-next-line max-len
      return this.getCellClassForNoPlanHours(workDayStarted, workDayEnded, plannedStartOfShift1, message, workerComment, commentOffice, paidOutFlex);
    } catch (e) {
      return '';
    }
  }

  private getCellClassForNoPlanHours(
    workDayStarted: boolean,
    workDayEnded: boolean,
    plannedStarted: any,
    message: any,
    workerComment: any,
    commentOffice: any,
    paidOutFlex: any
  ): string {
    if (workDayStarted) {
      return workDayEnded ? 'green-background' : 'red-background';
    }

    if (plannedStarted) {
      return 'red-background';
    }

    if (message || workerComment || commentOffice || paidOutFlex) {
      return 'grey-background';
    }

    return 'white-background';
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
    if (!startedAt || !stoppedAt) {
      return '';
    }
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
            data.autoBreakSettings.monday.breakMinutesDivider =
              this.convertStringToMinutes(data.autoBreakSettings.monday.breakMinutesDivider as string);
            data.autoBreakSettings.monday.breakMinutesPrDivider =
              this.convertStringToMinutes(data.autoBreakSettings.monday.breakMinutesPrDivider as string);
            data.autoBreakSettings.monday.breakMinutesUpperLimit =
              this.convertStringToMinutes(data.autoBreakSettings.monday.breakMinutesUpperLimit as string);
            data.autoBreakSettings.tuesday.breakMinutesDivider =
              this.convertStringToMinutes(data.autoBreakSettings.tuesday.breakMinutesDivider as string);
            data.autoBreakSettings.tuesday.breakMinutesPrDivider =
              this.convertStringToMinutes(data.autoBreakSettings.tuesday.breakMinutesPrDivider as string);
            data.autoBreakSettings.tuesday.breakMinutesUpperLimit =
              this.convertStringToMinutes(data.autoBreakSettings.tuesday.breakMinutesUpperLimit as string);
            data.autoBreakSettings.wednesday.breakMinutesDivider =
              this.convertStringToMinutes(data.autoBreakSettings.wednesday.breakMinutesDivider as string);
            data.autoBreakSettings.wednesday.breakMinutesPrDivider =
              this.convertStringToMinutes(data.autoBreakSettings.wednesday.breakMinutesPrDivider as string);
            data.autoBreakSettings.wednesday.breakMinutesUpperLimit =
              this.convertStringToMinutes(data.autoBreakSettings.wednesday.breakMinutesUpperLimit as string);
            data.autoBreakSettings.thursday.breakMinutesDivider =
              this.convertStringToMinutes(data.autoBreakSettings.thursday.breakMinutesDivider as string);
            data.autoBreakSettings.thursday.breakMinutesPrDivider =
              this.convertStringToMinutes(data.autoBreakSettings.thursday.breakMinutesPrDivider as string);
            data.autoBreakSettings.thursday.breakMinutesUpperLimit =
              this.convertStringToMinutes(data.autoBreakSettings.thursday.breakMinutesUpperLimit as string);
            data.autoBreakSettings.friday.breakMinutesDivider =
              this.convertStringToMinutes(data.autoBreakSettings.friday.breakMinutesDivider as string);
            data.autoBreakSettings.friday.breakMinutesPrDivider =
              this.convertStringToMinutes(data.autoBreakSettings.friday.breakMinutesPrDivider as string);
            data.autoBreakSettings.friday.breakMinutesUpperLimit =
              this.convertStringToMinutes(data.autoBreakSettings.friday.breakMinutesUpperLimit as string);
            data.autoBreakSettings.saturday.breakMinutesDivider =
              this.convertStringToMinutes(data.autoBreakSettings.saturday.breakMinutesDivider as string);
            data.autoBreakSettings.saturday.breakMinutesPrDivider =
              this.convertStringToMinutes(data.autoBreakSettings.saturday.breakMinutesPrDivider as string);
            data.autoBreakSettings.saturday.breakMinutesUpperLimit =
              this.convertStringToMinutes(data.autoBreakSettings.saturday.breakMinutesUpperLimit as string);
            data.autoBreakSettings.sunday.breakMinutesDivider =
              this.convertStringToMinutes(data.autoBreakSettings.sunday.breakMinutesDivider as string);
            data.autoBreakSettings.sunday.breakMinutesPrDivider =
              this.convertStringToMinutes(data.autoBreakSettings.sunday.breakMinutesPrDivider as string);
            data.autoBreakSettings.sunday.breakMinutesUpperLimit =
              this.convertStringToMinutes(data.autoBreakSettings.sunday.breakMinutesUpperLimit as string);
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

  convertStringToMinutes(time: string): number {
    const [hours, minutes] = time.split(':').map(Number);
    const result = hours * 60 + minutes;
    if (isNaN(result)) {
      return 0;
    }
    return result;
  }

  padZero(num: number): string {
    return num < 10 ? '0' + num : num.toString();
  }

  protected readonly Date = Date;
  protected readonly Math = Math;
}
