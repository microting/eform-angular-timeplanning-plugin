import { Component, OnDestroy, OnInit } from '@angular/core';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import { Subscription } from 'rxjs';
import { SiteDto } from 'src/app/common/models';
import { TimePlanningsStateService } from 'src/app/plugins/modules/time-planning-pn/components/plannings/store';
import { TimePlanningModel, TimePlanningsRequestModel } from '../../../models';
import {
  TimePlanningPnPlanningsService,
  TimePlanningPnSettingsService,
} from '../../../services';

@AutoUnsubscribe()
@Component({
  selector: 'app-time-plannings-container',
  templateUrl: './time-plannings-container.component.html',
  styleUrls: ['./time-plannings-container.component.scss'],
})
export class TimePlanningsContainerComponent implements OnInit, OnDestroy {
  timePlanningsRequest: TimePlanningsRequestModel;
  availableSites: SiteDto[] = [];
  timePlannings: TimePlanningModel[] = [];

  getTimePlannings$: Subscription;
  updateTimePlanning$: Subscription;
  getAvailableSites$: Subscription;

  constructor(
    private planningsService: TimePlanningPnPlanningsService,
    private planningsStateService: TimePlanningsStateService,
    private settingsService: TimePlanningPnSettingsService
  ) {}

  ngOnInit(): void {
    this.getAvailableSites();
  }

  getAvailableSites() {
    this.getAvailableSites$ = this.settingsService
      .getAvailableSites()
      .subscribe((data) => {
        if (data && data.success) {
          this.availableSites = data.model;
        }
      });
  }

  getPlannings(model: TimePlanningsRequestModel) {
    this.getTimePlannings$ = this.planningsStateService
      .getPlannings(model)
      .subscribe((data) => {
        if (data && data.success) {
          this.timePlannings = data.model;
        }
      });
  }

  onTimePlanningsFiltersChanged(model: TimePlanningsRequestModel) {
    this.timePlanningsRequest = { ...model };
    this.getPlannings(model);
  }

  onUpdateTimePlanning(model: TimePlanningModel) {
    this.updateTimePlanning$ = this.planningsService
      .updatePlanning({
        siteId: this.timePlanningsRequest.siteId,
        date: model.date,
        planText: model.planText,
        message: model.message,
        planHours: model.planHours,
      })
      .subscribe((data) => {
        if (data && data.success) {
          this.getPlannings(this.timePlanningsRequest);
        }
      });
  }

  ngOnDestroy(): void {}

  onSortChanged(sort: string) {
    this.planningsStateService.onSortTable(sort);
    if (this.timePlanningsRequest) {
      this.getPlannings(this.timePlanningsRequest);
    }
  }
}
