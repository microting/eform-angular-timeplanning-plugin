import {Component, OnDestroy, OnInit} from '@angular/core';
import {Subscription} from 'rxjs';
import {TimePlanningPnSettingsService} from '../../../services';
import {TimePlanningSettingsModel} from '../../../models';

@Component({
    selector: 'app-time-planning-settings',
    templateUrl: './time-planning-settings.component.html',
    styleUrls: ['./time-planning-settings.component.scss'],
    standalone: false
})
export class TimePlanningSettingsComponent implements OnInit, OnDestroy {
  getSettings$: Subscription;
  settingsModel: TimePlanningSettingsModel = new TimePlanningSettingsModel();
  previousData: TimePlanningSettingsModel = new TimePlanningSettingsModel();

  constructor(private timePlanningPnSettingsService: TimePlanningPnSettingsService) {
    this.previousData = {...this.settingsModel};
  }
  ngOnInit() {
    this.getSettings();
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
}
