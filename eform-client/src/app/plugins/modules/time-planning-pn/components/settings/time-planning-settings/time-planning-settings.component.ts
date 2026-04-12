import {Component, OnDestroy, OnInit,
  inject
} from '@angular/core';
import {Subscription} from 'rxjs';
import {TimePlanningPnSettingsService} from '../../../services';
import {TimePlanningSettingsModel} from '../../../models';
import {selectCurrentUserIsFirstUser} from 'src/app/state';
import {Store} from '@ngrx/store';

@Component({
    selector: 'app-time-planning-settings',
    templateUrl: './time-planning-settings.component.html',
    styleUrls: ['./time-planning-settings.component.scss'],
    standalone: false
})
export class TimePlanningSettingsComponent implements OnInit, OnDestroy {
  private timePlanningPnSettingsService = inject(TimePlanningPnSettingsService);
  private store = inject(Store);

  getSettings$: Subscription;
  settingsModel: TimePlanningSettingsModel = new TimePlanningSettingsModel();
  previousData: TimePlanningSettingsModel = new TimePlanningSettingsModel();
  public selectCurrentUserIsFirstUser$ = this.store.select(selectCurrentUserIsFirstUser);

  payrollSettings: { payrollSystem: number; cutoffDay: number } = { payrollSystem: 0, cutoffDay: 19 };
  payrollSystemOptions = [
    { value: 0, label: 'None' },
    { value: 1, label: 'DanLøn' },
    { value: 2, label: 'DataLøn' }
  ];

  

  ngOnInit() {
    this.previousData = {...this.settingsModel};
    this.getSettings();
    this.getPayrollSettings();
  }

  ngOnDestroy() {
  }

  getSettings() {
    this.getSettings$ = this.timePlanningPnSettingsService.getAllSettings().subscribe((data) => {
      if (data && data.success) {
        this.settingsModel = data.model;
      }
    });
  }


  getConvertedValue(minutes: number, compareMinutes?: number): string {
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    let result = `${this.padZero(hours)}:${this.padZero(mins)}`;
    if (result === '00:00' && (compareMinutes === 0 || compareMinutes === undefined || compareMinutes === null)) {
      result = '';
    }
    return result;
  }

  setMinutes(event: any, field: string): void {
    const [hours, mins] = event.split(':').map(Number);
    this.settingsModel[field] = (hours * 60) + mins;
    // this.calculateHours();
    this.previousData = {...this.settingsModel};
  }

  private padZero(num: number): string {
    return num < 10 ? `0${num}` : `${num}`;
  }

  updateGoogleSheetSettings() {
    if (this.settingsModel.dayOfPayment > 28) {
      this.settingsModel.dayOfPayment = 28;
    }
    this.timePlanningPnSettingsService.updateSettings(this.settingsModel).subscribe((data) => {
      if (data && data.success) {
        this.getSettings();
      }
    });
  }

  resetGlobalAutoBreakCalculationSettings() {
    this.timePlanningPnSettingsService.resetGlobalAutoBreakCalculationSettings().subscribe((data) => {
      if (data && data.success) {
        this.getSettings();
      }
    })
  }

  getPayrollSettings() {
    this.timePlanningPnSettingsService.getPayrollSettings().subscribe((data) => {
      if (data && data.success && data.model) {
        this.payrollSettings = data.model;
      }
    });
  }

  updatePayrollSettings() {
    if (this.payrollSettings.cutoffDay < 1) {
      this.payrollSettings.cutoffDay = 1;
    }
    if (this.payrollSettings.cutoffDay > 28) {
      this.payrollSettings.cutoffDay = 28;
    }
    this.timePlanningPnSettingsService.updatePayrollSettings(this.payrollSettings).subscribe((data) => {
      if (data && data.success) {
        this.getPayrollSettings();
      }
    });
  }
}
