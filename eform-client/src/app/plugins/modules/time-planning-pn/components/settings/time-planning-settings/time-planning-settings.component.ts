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

  constructor(private timePlanningPnSettingsService: TimePlanningPnSettingsService) {
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

  updateGoogleSheetSettings() {
    this.timePlanningPnSettingsService.updateSettings(this.settingsModel).subscribe((data) => {
      if (data && data.success) {
        this.getSettings();
      }
    });
  }
}
