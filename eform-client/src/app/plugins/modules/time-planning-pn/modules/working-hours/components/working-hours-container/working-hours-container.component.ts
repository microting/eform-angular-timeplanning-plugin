import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormArray, FormControl, FormGroup } from '@angular/forms';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import { Subscription } from 'rxjs';
import { SiteDto } from 'src/app/common/models';
import { TimePlanningsStateService } from 'src/app/plugins/modules/time-planning-pn/components/plannings/store';
import { TimePlanningMessagesEnum } from 'src/app/plugins/modules/time-planning-pn/enums';
import {
  TimePlanningModel,
  TimePlanningsRequestModel,
} from 'src/app/plugins/modules/time-planning-pn/models';
import {
  TimePlanningPnPlanningsService,
  TimePlanningPnSettingsService,
  TimePlanningPnWorkingHoursService,
} from '../../../../services';

@AutoUnsubscribe()
@Component({
  selector: 'app-working-hours-container',
  templateUrl: './working-hours-container.component.html',
  styleUrls: ['./working-hours-container.component.scss'],
})
export class WorkingHoursContainerComponent implements OnInit, OnDestroy {
  workingHoursFormArray: FormArray = new FormArray([]);
  workingHoursRequest: TimePlanningsRequestModel;
  availableSites: SiteDto[] = [];
  workingHours: TimePlanningModel[] = [];

  getWorkingHours$: Subscription;
  updateWorkingHours$: Subscription;
  getAvailableSites$: Subscription;

  constructor(
    private workingHoursService: TimePlanningPnWorkingHoursService,
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

  getWorkingHours(model: TimePlanningsRequestModel) {
    this.workingHoursFormArray.clear();
    this.getWorkingHours$ = this.workingHoursService
      .getWorkingHours(model)
      .subscribe((data) => {
        if (data && data.success) {
          this.workingHours = data.model;
          this.initializeWorkingHoursFormArray(data.model);
        }
      });
  }

  initializeWorkingHoursFormArray(workingHours: TimePlanningModel[]) {
    workingHours.map((x) => {
      this.workingHoursFormArray.push(
        new FormGroup({
          weekDay: new FormControl(x.weekDay),
          date: new FormControl(x.date),
          planText: new FormControl(x.planText ? x.planText : null),
          planHours: new FormControl(x.planHours ? x.planHours : 0),
          message: new FormControl(x.message ? x.message : null),
          shift1Start: new FormControl(x.shift1Start ? x.shift1Start : null),
          shift1Pause: new FormControl(x.shift1Pause ? x.shift1Pause : null),
          shift1Stop: new FormControl(x.shift1Stop ? x.shift1Stop : null),
          shift2Start: new FormControl(x.shift2Start ? x.shift2Start : null),
          shift2Pause: new FormControl(x.shift2Pause ? x.shift2Pause : null),
          shift2Stop: new FormControl(x.shift2Stop ? x.shift2Stop : null),
          nettoHours: new FormControl(x.nettoHours ? x.nettoHours : 0),
          flexHours: new FormControl(x.flexHours ? x.flexHours : 0),
          sumFlex: new FormControl(x.sumFlex ? x.sumFlex : 0),
          paidOutFlex: new FormControl(x.paidOutFlex ? x.paidOutFlex : 0),
          commentWorker: new FormControl(x.commentWorker ? x.commentWorker : null),
          commentOffice: new FormControl(x.commentOffice ? x.commentOffice : null),
          commentOfficeAll: new FormControl(x.commentOfficeAll ? x.commentOfficeAll : null),
        })
      );
    });
  }

  onWorkingHoursFiltersChanged(model: TimePlanningsRequestModel) {
    this.workingHoursRequest = { ...model };
    this.getWorkingHours(model);
  }

  onUpdateWorkingHours() {
    this.updateWorkingHours$ = this.workingHoursService
      .updateWorkingHours({
        siteId: this.workingHoursRequest.siteId,
        plannings: this.workingHoursFormArray.getRawValue(),
      })
      .subscribe((data) => {
        // TODO: REMOVE
        // if (data && data.success) {
        //   this.getWorkingHours(this.workingHoursRequest);
        // }
      });
  }

  ngOnDestroy(): void {}
}
