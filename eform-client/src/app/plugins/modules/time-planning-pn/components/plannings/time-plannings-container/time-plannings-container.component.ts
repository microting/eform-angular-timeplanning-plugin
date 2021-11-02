import { Component, OnDestroy, OnInit } from '@angular/core';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import { Subscription } from 'rxjs';
import { SiteDto } from 'src/app/common/models';
import {
  TimePlanningPnPlanningsService,
  TimePlanningPnSettingsService,
} from '../../../services';
import {
  TimePlanningModel,
  TimePlanningsRequestModel,
  TimePlanningUpdateModel,
} from '../../../models';

@AutoUnsubscribe()
@Component({
  selector: 'app-time-plannings-container',
  templateUrl: './time-plannings-container.component.html',
  styleUrls: ['./time-plannings-container.component.scss'],
})
export class TimePlanningsContainerComponent implements OnInit, OnDestroy {
  timePlanningsRequest: TimePlanningsRequestModel =
    new TimePlanningsRequestModel();
  availableWorkers: SiteDto[] = [];
  timePlannings: TimePlanningModel[] = [];

  getSimpleTimePlannings$: Subscription;
  updateTimePlanning$: Subscription;
  getAvailableSites$: Subscription;

  constructor(
    private planningsService: TimePlanningPnPlanningsService,
    private settingsService: TimePlanningPnSettingsService
  ) {}

  ngOnInit(): void {}

  getAvailableSites() {
    this.getAvailableSites$ = this.settingsService
      .getAvailableSites()
      .subscribe((data) => {
        if (data && data.success) {
          this.availableWorkers = data.model;
        }
      });
  }

  onTimePlanningsFiltersChanged(model: TimePlanningsRequestModel) {
    this.getSimpleTimePlannings$ = this.planningsService
      .getSimplePlannings(model)
      .subscribe((data) => {
        if (data && data.success) {
          this.timePlannings = data.model;
        }
      });
  }

  onUpdateTimePlanning(model: TimePlanningUpdateModel) {
    this.updateTimePlanning$ = this.planningsService
      .updateSinglePlanning(model)
      .subscribe((data) => {});
  }

  ngOnDestroy(): void {}
}
