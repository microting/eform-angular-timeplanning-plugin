import { Component, OnDestroy, OnInit } from '@angular/core';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import { Subscription } from 'rxjs';
import { SiteDto } from 'src/app/common/models';
import { TimePlanningModel, TimePlanningsRequestModel } from '../../../models';
import {
  TimePlanningPnPlanningsService,
  TimePlanningPnSettingsService,
} from '../../../services';
import {startOfWeek, endOfWeek, format} from 'date-fns';
import {PARSING_DATE_FORMAT} from 'src/app/common/const';

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
  dateTo: Date

  getTimePlannings$: Subscription;
  updateTimePlanning$: Subscription;
  getAvailableSites$: Subscription;

  constructor(
    private planningsService: TimePlanningPnPlanningsService,
    private settingsService: TimePlanningPnSettingsService
  ) {}

  ngOnInit(): void {
    this.getPlannings();
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

  goForward() {
    this.dateFrom = new Date(this.dateFrom.setDate(this.dateFrom.getDate() + 7));
    this.dateTo = new Date(this.dateTo.setDate(this.dateTo.getDate() + 7));
    this.getPlannings();
  }

  formatDateRange(): string {
    const options = { year: 'numeric', month: 'long', day: 'numeric' } as const;
    const from = this.dateFrom.toLocaleDateString(undefined, options);
    const to = this.dateTo.toLocaleDateString(undefined, options);
    return `${from} - ${to}`;
  }
}
