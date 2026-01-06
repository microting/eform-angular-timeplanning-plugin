import { Component, OnDestroy, OnInit,
  inject
} from '@angular/core';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import {Subscription, take} from 'rxjs';
import { SiteDto } from 'src/app/common/models';
import {AssignedSiteModel, TimePlanningModel, TimePlanningsRequestModel} from '../../../models';
import {
  TimePlanningPnPlanningsService,
  TimePlanningPnSettingsService,
} from '../../../services';
import {startOfWeek, endOfWeek, format} from 'date-fns';
import {ExcelIcon, iOSIcon, PARSING_DATE_FORMAT} from 'src/app/common/const';
import {Store} from '@ngrx/store';
import {selectCurrentUserLocale} from 'src/app/state';
import {MatDialog} from '@angular/material/dialog';
import {DownloadExcelDialogComponent} from 'src/app/plugins/modules/time-planning-pn/components';
import {MatDatepickerInputEvent} from '@angular/material/datepicker';

@AutoUnsubscribe()
@Component({
  selector: 'app-time-plannings-container',
  templateUrl: './time-plannings-container.component.html',
  styleUrls: ['./time-plannings-container.component.scss'],
  standalone: false
})
export class TimePlanningsContainerComponent implements OnInit, OnDestroy {
  private store = inject(Store);
  private planningsService = inject(TimePlanningPnPlanningsService);
  private settingsService = inject(TimePlanningPnSettingsService);
  private dialog = inject(MatDialog);

  timePlanningsRequest: TimePlanningsRequestModel;
  availableSites: SiteDto[] = [];
  showResignedSites: boolean = false;
  timePlannings: TimePlanningModel[] = [];
  selectedDate: Date = new Date();
  dateFrom: Date;
  dateTo: Date;
  siteId: number = null; // Default to 0 to get all sites

  getTimePlannings$: Subscription;
  updateTimePlanning$: Subscription;
  getAvailableSites$: Subscription;
  public selectCurrentUserLocale$ = this.store.select(selectCurrentUserLocale);
  locale: string;

  ngOnInit(): void {
    if (!this.showResignedSites) {
      this.settingsService
        .getAvailableSites()
        .subscribe((data) => {
          if (data && data.success) {
            this.availableSites = data.model;

          }
        });
    } else {
      this.getAvailableSites$ = this.settingsService
        .getResignedSites()
        .subscribe((data) => {
          if (data && data.success) {
            this.availableSites = data.model;

          }
        });
    }
    this.selectCurrentUserLocale$.pipe(take(1)).subscribe((locale) => {
      this.locale = locale;
      this.getPlannings();
    });
  }

  getPlannings() {
    const now = new Date();
    now.setHours(0, 0, 0, 0);
    if (!this.dateFrom) {
      this.dateFrom = startOfWeek(now, { weekStartsOn: 1 });
      this.dateTo = endOfWeek(now, { weekStartsOn: 1 });
    }
    //const dateFrom = format(startOfWeek(now, { weekStartsOn: 1 }), PARSING_DATE_FORMAT);
    //const dateTo = format(endOfWeek(now, { weekStartsOn: 1 }), PARSING_DATE_FORMAT);
    this.timePlanningsRequest = {
      dateFrom: format(this.dateFrom, PARSING_DATE_FORMAT),
      dateTo: format(this.dateTo, PARSING_DATE_FORMAT),
      sort: 'Date',
      isSortDsc: true,
      siteId: this.siteId, // Default to 0 to get all sites
      showResignedSites: this.showResignedSites
    }
    this.getTimePlannings$ = this.planningsService
      .getPlannings(this.timePlanningsRequest)
      .subscribe((data) => {
        if (data && data.success) {
          this.timePlannings = data.model;
        }
      });
    }

  ngOnDestroy(): void {
  }

  goBackward() {
    const tempEndDate = new Date(this.dateTo);
    tempEndDate.setHours(0, 0, 0, 0);

    let daysCount = (Math.floor((tempEndDate.getTime() - this.dateFrom.getTime()) / (1000 * 3600 * 24)) * -1) -1;
    this.dateFrom = this.addDays(this.dateFrom, daysCount);
    this.dateTo = this.addDays(this.dateTo, daysCount);
    this.getPlannings();
  }

  openDownloadExcelDialog() {
          const dialogRef = this.dialog.open(DownloadExcelDialogComponent, {
            width: '600px',
            data: this.availableSites,
          });
          dialogRef.afterClosed().subscribe((result) => {
            // if (result) {
            //   this.getPlannings();
            // }
          });
  }

  goForward() {
    const tempEndDate = new Date(this.dateTo);
    tempEndDate.setHours(0, 0, 0, 0);
    let daysCount = Math.floor((tempEndDate.getTime() - this.dateFrom.getTime()) / (1000 * 3600 * 24)) +1;
    this.dateFrom = this.addDays(this.dateFrom, daysCount);
    this.dateTo = this.addDays(this.dateTo, daysCount);
    this.getPlannings();
  }

  private addDays(date: Date, days: number): Date {
    const result = new Date(date);
    result.setDate(result.getDate() + days);
    return result;
  }

  formatDateRange(): string {
    const options = { year: 'numeric', month: 'numeric', day: 'numeric' } as const;
    //const from = this.dateFrom.toLocaleDateString(undefined, options);
    const from = format(this.dateFrom, 'dd.MM.yyyy');
    //const to = this.dateTo.toLocaleDateString(undefined, options);
    const to = format(this.dateTo, 'dd.MM.yyyy');
    return `${from} - ${to}`;
  }

  onTimePlanningChanged($event: any) {
    this.getPlannings();
  }

  onAssignedSiteChanged($event: any) {
    this.getPlannings();
  }

  onSiteChanged($event: any) {
    this.siteId = $event;
    this.getPlannings();
  }

  updateDateFrom(dateFrom: MatDatepickerInputEvent<any, any>) {
    this.dateFrom = dateFrom.value;
  }

  updateDateTo(dateTo: MatDatepickerInputEvent<any, any>) {
    if (dateTo.value) {
      this.dateTo = dateTo.value;
      this.dateTo.setHours(23, 59, 59, 999);
      this.getPlannings();
    }
  }

  onShowResignedSitesChanged($event: any) {
    this.showResignedSites = $event.checked;
    if (!this.showResignedSites) {
      this.settingsService
        .getAvailableSites()
        .subscribe((data) => {
          if (data && data.success) {
            this.availableSites = data.model;

          }
        });
    } else {
      this.getAvailableSites$ = this.settingsService
        .getResignedSites()
        .subscribe((data) => {
          if (data && data.success) {
            this.availableSites = data.model;

          }
        });
    }
    this.getPlannings();
  }
}
