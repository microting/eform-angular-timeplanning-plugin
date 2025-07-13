import { Component, OnDestroy, OnInit } from '@angular/core';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import {Subscription, take} from 'rxjs';
import { SiteDto } from 'src/app/common/models';
import {AssignedSiteModel, TimePlanningModel, TimePlanningsRequestModel} from '../../../models';
import {
  TimePlanningPnPlanningsService,
  TimePlanningPnSettingsService,
} from '../../../services';
import {startOfWeek, endOfWeek, format} from 'date-fns';
import {PARSING_DATE_FORMAT} from 'src/app/common/const';
import {Store} from '@ngrx/store';
import {selectCurrentUserLocale} from 'src/app/state';
import {MatDialog} from '@angular/material/dialog';
import {DownloadExcelDialogComponent} from 'src/app/plugins/modules/time-planning-pn/components';

@AutoUnsubscribe()
@Component({
  selector: 'app-time-plannings-container',
  templateUrl: './time-plannings-container.component.html',
  styleUrls: ['./time-plannings-container.component.scss'],
  standalone: false
})
export class TimePlanningsContainerComponent implements OnInit, OnDestroy {
  timePlanningsRequest: TimePlanningsRequestModel;
  availableSites: SiteDto[] = [];
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

  constructor(
    private store: Store,
    private planningsService: TimePlanningPnPlanningsService,
    private settingsService: TimePlanningPnSettingsService,
    private dialog: MatDialog,
  ) {}

  ngOnInit(): void {

    this.settingsService
      .getAvailableSites()
      .subscribe((data) => {
        if (data && data.success) {
          this.availableSites = data.model;

        }
      });
    this.selectCurrentUserLocale$.pipe(take(1)).subscribe((locale) => {
      this.locale = locale;
      this.getPlannings();
    });
  }

  getPlannings() {
    const now = new Date();
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
    this.dateFrom = new Date(this.dateFrom.setDate(this.dateFrom.getDate() - 7));
    this.dateTo = new Date(this.dateTo.setDate(this.dateTo.getDate() - 7));
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
    this.dateFrom = new Date(this.dateFrom.setDate(this.dateFrom.getDate() + 7));
    this.dateTo = new Date(this.dateTo.setDate(this.dateTo.getDate() + 7));
    this.getPlannings();
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
}
