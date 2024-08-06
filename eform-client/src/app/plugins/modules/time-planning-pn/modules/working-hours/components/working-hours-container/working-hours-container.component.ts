import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormArray, FormControl, FormGroup } from '@angular/forms';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import { Subscription } from 'rxjs';
import { SiteDto } from 'src/app/common/models';
import {
  TimePlanningModel,
  TimePlanningsRequestModel,
} from 'src/app/plugins/modules/time-planning-pn/models';
import {
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
  workingHoursGroupSub$: Subscription[] = [];
  tainted = false;

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
    if (model.siteId === undefined || model.siteId === null) {
      this.workingHours = [];
      this.workingHoursFormArray = new FormArray([]);
      //this.initializeWorkingHoursFormArray([]);
    } else {
      this.getWorkingHours$ = this.workingHoursService
        .getWorkingHours(model)
        .subscribe((data) => {
          if (data && data.success) {
            this.workingHours = data.model;
            this.initializeWorkingHoursFormArray(data.model);
          }
        });
    }
  }

  initializeWorkingHoursFormArray(workingHours: TimePlanningModel[]) {
    this.workingHoursFormArray.clear();
    workingHours.map((x) => {
      this.workingHoursFormArray.controls = [
        ...this.workingHoursFormArray.controls,
        new FormGroup({
          isWeekend: new FormControl(x.isWeekend),
          isLocked: new FormControl(x.isLocked),
          workerName: new FormControl(x.workerName),
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
          nettoHours: new FormControl({
            value: x.nettoHours ? x.nettoHours : 0,
            disabled: true,
          }),
          flexHours: new FormControl({
            value: this.calculateFlexHours(x.nettoHours, x.planHours),
            disabled: true,
          }),
          sumFlex: new FormControl({
            value: x.sumFlexEnd ? x.sumFlexEnd : 0,
            disabled: true,
          }),
          paidOutFlex: new FormControl(x.paidOutFlex ? x.paidOutFlex : 0),
          commentWorker: new FormControl({
              value: x.commentWorker ? x.commentWorker : '',
              disabled: true,
            }
          ),
          commentOffice: new FormControl(x.commentOffice ? x.commentOffice : ''),
          commentOfficeAll: new FormControl(x.commentOfficeAll ? x.commentOfficeAll : ''),
        }),
      ];
    });

    if (this.workingHoursGroupSub$.length > 0) {
      for (const sub$ of this.workingHoursGroupSub$) {
        sub$.unsubscribe();
      }
    }
    if (this.workingHoursFormArray.length > 0) {
      this.tainted = false;
      let i = 0;
      for (const group of this.workingHoursFormArray.controls) {
        if (i > 0) {
          this.workingHoursGroupSub$.push(
            group.get('flexHours').valueChanges.subscribe(() => {
              this.recalculateSumFlex();
            })
          );
          this.workingHoursGroupSub$.push(
            group.get('paidOutFlex').valueChanges.subscribe(() => {
              this.recalculateSumFlex();
            })
          );
        }
        i++;
      }
    }
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
        this.tainted = false;
      });
  }

  recalculateSumFlex(initialize = false) {
    this.tainted = true;
    let sumFlex = 0;
    let isFirst = true;
    for (const formGroup of this.workingHoursFormArray.controls) {
      if (!isFirst) {
        const flexHours = formGroup.get('flexHours').value;
        let paidOutFlex = formGroup.get('paidOutFlex').value;
        if (typeof paidOutFlex === 'string') {
          paidOutFlex = paidOutFlex.replace(',', '.');
          //formGroup.setValue({ paidOutFlex: paidOutFlex });
        }
        if (initialize) {
          sumFlex = formGroup.get('sumFlex').value;
        } else {
          sumFlex = sumFlex + (flexHours ? flexHours : 0) - (paidOutFlex ? paidOutFlex : 0);
          if (formGroup.get('sumFlex').value !== sumFlex) {
            formGroup.get('sumFlex').setValue(+sumFlex.toFixed(2));
          } else {
            this.tainted = false;
          }
        }
      } else {
        isFirst = false;
        const paidOutFlex = formGroup.get('paidOutFlex').value;
        sumFlex = formGroup.get('sumFlex').value;
        sumFlex = sumFlex  - (paidOutFlex ? paidOutFlex : 0);
      }
    }
    if (initialize) {
      this.tainted = false;
    }
  }

  calculateFlexHours(nettoHours: number, planHours: number) {
    return +(nettoHours - planHours).toFixed(2);
  }

  ngOnDestroy(): void {
    for (const sub$ of this.workingHoursGroupSub$) {
      sub$.unsubscribe();
    }
  }
}
