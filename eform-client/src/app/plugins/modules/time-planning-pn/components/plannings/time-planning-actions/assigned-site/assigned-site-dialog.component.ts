import {Component, DoCheck, Inject, OnInit} from '@angular/core';
import {
  MAT_DIALOG_DATA
} from '@angular/material/dialog';
import {AssignedSiteModel, GlobalAutoBreakSettingsModel} from '../../../../models';
import {selectCurrentUserIsAdmin, selectCurrentUserIsFirstUser} from 'src/app/state';
import {Store} from '@ngrx/store';
import {TimePlanningPnSettingsService} from 'src/app/plugins/modules/time-planning-pn/services';

@Component({
  selector: 'app-assigned-site-dialog',
  templateUrl: './assigned-site-dialog.component.html',
  styleUrls: ['./assigned-site-dialog.component.scss'],
  standalone: false
})
export class AssignedSiteDialogComponent implements DoCheck, OnInit {
  public selectCurrentUserIsAdmin$ = this.store.select(selectCurrentUserIsAdmin);
  public selectCurrentUserIsFirstUser$ = this.store.select(selectCurrentUserIsFirstUser);
  private previousData: AssignedSiteModel;
  private globalAutoBreakSettings: GlobalAutoBreakSettingsModel;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: AssignedSiteModel,
    private timePlanningPnSettingsService: TimePlanningPnSettingsService,
    private store: Store
  ) {
    this.previousData = {...data};
    this.calculateHours();
  }

  ngDoCheck(): void {
    if (this.hasDataChanged()) {
      // this.calculateHours();
      // this.previousData = { ...this.data };
    }
  }

  ngOnInit(): void {
    this.timePlanningPnSettingsService.getGlobalAutoBreakCalculationSettings().subscribe(result => {
      if (result && result.success) {
        this.globalAutoBreakSettings = result.model;
      }
    });
    if (!this.data.resigned) {
      const today = new Date();
      today.setHours(0, 0, 0, 0);
      this.data.resignedAtDate = today.toISOString();
    }
  }

  hasDataChanged(): boolean {
    return JSON.stringify(this.data) !== JSON.stringify(this.previousData);
  }

  calculateHours(): void {
    this.data.mondayCalculatedHours = this.calculateDayHours(
      this.data.startMonday,
      this.data.endMonday,
      this.data.breakMonday,
      this.data.startMonday2NdShift,
      this.data.endMonday2NdShift,
      this.data.breakMonday2NdShift);
    this.data.tuesdayCalculatedHours = this.calculateDayHours(
      this.data.startTuesday,
      this.data.endTuesday,
      this.data.breakTuesday,
      this.data.startTuesday2NdShift,
      this.data.endTuesday2NdShift,
      this.data.breakTuesday2NdShift);
    this.data.wednesdayCalculatedHours = this.calculateDayHours(
      this.data.startWednesday,
      this.data.endWednesday,
      this.data.breakWednesday,
      this.data.startWednesday2NdShift,
      this.data.endWednesday2NdShift,
      this.data.breakWednesday2NdShift);
    this.data.thursdayCalculatedHours = this.calculateDayHours(
      this.data.startThursday,
      this.data.endThursday,
      this.data.breakThursday,
      this.data.startThursday2NdShift,
      this.data.endThursday2NdShift,
      this.data.breakThursday2NdShift);
    this.data.fridayCalculatedHours = this.calculateDayHours(
      this.data.startFriday,
      this.data.endFriday,
      this.data.breakFriday,
      this.data.startFriday2NdShift,
      this.data.endFriday2NdShift,
      this.data.breakFriday2NdShift);
    this.data.saturdayCalculatedHours = this.calculateDayHours(
      this.data.startSaturday,
      this.data.endSaturday,
      this.data.breakSaturday,
      this.data.startSaturday2NdShift,
      this.data.endSaturday2NdShift,
      this.data.breakSaturday2NdShift);
    this.data.sundayCalculatedHours = this.calculateDayHours(
      this.data.startSunday,
      this.data.endSunday,
      this.data.breakSunday,
      this.data.startSunday2NdShift,
      this.data.endSunday2NdShift,
      this.data.breakSunday2NdShift);
  }

  calculateDayHours(
    start: number,
    end: number,
    breakTime: number,
    start2NdShift: number,
    end2NdShift: number,
    break2NdShift: number
  ): string {
    let timeInMinutes = (end - start - breakTime) / 60;
    let timeInMinutes2NdShift = (end2NdShift - start2NdShift - break2NdShift) / 60;
    timeInMinutes += timeInMinutes2NdShift;
    const hours = Math.floor(timeInMinutes);
    const minutes = Math.round((timeInMinutes - hours) * 60);
    return `${hours}:${minutes}`;
  }

  onTimeChange(event: any, field: string): void {
    const time = event.split(':');
    const minutes = (+time[0] * 60) + (+time[1]);
    this.data[field] = minutes;
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
    if (event === null || event === undefined || event === '') {
      const [hours, mins] = event.target.value.split(':').map(Number);
      this.data[field] = (hours * 60) + mins;
      this.calculateHours();
      this.previousData = {...this.data};
    } else {
      const [hours, mins] = event.split(':').map(Number);
      this.data[field] = (hours * 60) + mins;
      this.calculateHours();
      this.previousData = {...this.data};
    }
  }

  updateAssignedSite() {
    this.data.mondayPlanHours = this.data.startMonday && this.data.endMonday
      ? this.data.endMonday - this.data.startMonday - this.data.breakMonday
      : 0;
    this.data.mondayPlanHours += this.data.startMonday2NdShift && this.data.endMonday2NdShift
      ? this.data.endMonday2NdShift - this.data.startMonday2NdShift - this.data.breakMonday2NdShift
      : 0;
    this.data.mondayPlanHours += this.data.startMonday3RdShift && this.data.endMonday3RdShift
      ? this.data.endMonday3RdShift - this.data.startMonday3RdShift - this.data.breakMonday3RdShift
      : 0;
    this.data.mondayPlanHours += this.data.startMonday4ThShift && this.data.endMonday4ThShift
      ? this.data.endMonday4ThShift - this.data.startMonday4ThShift - this.data.breakMonday4ThShift
      : 0;
    this.data.mondayPlanHours += this.data.startMonday5ThShift && this.data.endMonday5ThShift
      ? this.data.endMonday5ThShift - this.data.startMonday5ThShift - this.data.breakMonday5ThShift
      : 0;
    this.data.tuesdayPlanHours = this.data.startTuesday && this.data.endTuesday
      ? this.data.endTuesday - this.data.startTuesday - this.data.breakTuesday
      : 0;
    this.data.tuesdayPlanHours += this.data.startTuesday2NdShift && this.data.endTuesday2NdShift
      ? this.data.endTuesday2NdShift - this.data.startTuesday2NdShift - this.data.breakTuesday2NdShift
      : 0;
    this.data.tuesdayPlanHours += this.data.startTuesday3RdShift && this.data.endTuesday3RdShift
      ? this.data.endTuesday3RdShift - this.data.startTuesday3RdShift - this.data.breakTuesday3RdShift
      : 0;
    this.data.tuesdayPlanHours += this.data.startTuesday4ThShift && this.data.endTuesday4ThShift
      ? this.data.endTuesday4ThShift - this.data.startTuesday4ThShift - this.data.breakTuesday4ThShift
      : 0;
    this.data.tuesdayPlanHours += this.data.startTuesday5ThShift && this.data.endTuesday5ThShift
      ? this.data.endTuesday5ThShift - this.data.startTuesday5ThShift - this.data.breakTuesday5ThShift
      : 0;
    this.data.wednesdayPlanHours = this.data.startWednesday && this.data.endWednesday
      ? this.data.endWednesday - this.data.startWednesday - this.data.breakWednesday
      : 0;
    this.data.wednesdayPlanHours += this.data.startWednesday2NdShift && this.data.endWednesday2NdShift
      ? this.data.endWednesday2NdShift - this.data.startWednesday2NdShift - this.data.breakWednesday2NdShift
      : 0;
    this.data.wednesdayPlanHours += this.data.startWednesday3RdShift && this.data.endWednesday3RdShift
      ? this.data.endWednesday3RdShift - this.data.startWednesday3RdShift - this.data.breakWednesday3RdShift
      : 0;
    this.data.wednesdayPlanHours += this.data.startWednesday4ThShift && this.data.endWednesday4ThShift
      ? this.data.endWednesday4ThShift - this.data.startWednesday4ThShift - this.data.breakWednesday4ThShift
      : 0;
    this.data.wednesdayPlanHours += this.data.startWednesday5ThShift && this.data.endWednesday5ThShift
      ? this.data.endWednesday5ThShift - this.data.startWednesday5ThShift - this.data.breakWednesday5ThShift
      : 0;
    this.data.thursdayPlanHours = this.data.startThursday && this.data.endThursday
      ? this.data.endThursday - this.data.startThursday - this.data.breakThursday
      : 0;
    this.data.thursdayPlanHours += this.data.startThursday2NdShift && this.data.endThursday2NdShift
      ? this.data.endThursday2NdShift - this.data.startThursday2NdShift - this.data.breakThursday2NdShift
      : 0;
    this.data.thursdayPlanHours += this.data.startThursday3RdShift && this.data.endThursday3RdShift
      ? this.data.endThursday3RdShift - this.data.startThursday3RdShift - this.data.breakThursday3RdShift
      : 0;
    this.data.thursdayPlanHours += this.data.startThursday4ThShift && this.data.endThursday4ThShift
      ? this.data.endThursday4ThShift - this.data.startThursday4ThShift - this.data.breakThursday4ThShift
      : 0;
    this.data.thursdayPlanHours += this.data.startThursday5ThShift && this.data.endThursday5ThShift
      ? this.data.endThursday5ThShift - this.data.startThursday5ThShift - this.data.breakThursday5ThShift
      : 0;
    this.data.fridayPlanHours = this.data.startFriday && this.data.endFriday
      ? this.data.endFriday - this.data.startFriday - this.data.breakFriday
      : 0;
    this.data.fridayPlanHours += this.data.startFriday2NdShift && this.data.endFriday2NdShift
      ? this.data.endFriday2NdShift - this.data.startFriday2NdShift - this.data.breakFriday2NdShift
      : 0;
    this.data.fridayPlanHours += this.data.startFriday3RdShift && this.data.endFriday3RdShift
      ? this.data.endFriday3RdShift - this.data.startFriday3RdShift - this.data.breakFriday3RdShift
      : 0;
    this.data.fridayPlanHours += this.data.startFriday4ThShift && this.data.endFriday4ThShift
      ? this.data.endFriday4ThShift - this.data.startFriday4ThShift - this.data.breakFriday4ThShift
      : 0;
    this.data.fridayPlanHours += this.data.startFriday5ThShift && this.data.endFriday5ThShift
      ? this.data.endFriday5ThShift - this.data.startFriday5ThShift - this.data.breakFriday5ThShift
      : 0;
    this.data.saturdayPlanHours = this.data.startSaturday && this.data.endSaturday
      ? this.data.endSaturday - this.data.startSaturday - this.data.breakSaturday
      : 0;
    this.data.saturdayPlanHours += this.data.startSaturday2NdShift && this.data.endSaturday2NdShift
      ? this.data.endSaturday2NdShift - this.data.startSaturday2NdShift - this.data.breakSaturday2NdShift
      : 0;
    this.data.saturdayPlanHours += this.data.startSaturday3RdShift && this.data.endSaturday3RdShift
      ? this.data.endSaturday3RdShift - this.data.startSaturday3RdShift - this.data.breakSaturday3RdShift
      : 0;
    this.data.saturdayPlanHours += this.data.startSaturday4ThShift && this.data.endSaturday4ThShift
      ? this.data.endSaturday4ThShift - this.data.startSaturday4ThShift - this.data.breakSaturday4ThShift
      : 0;
    this.data.sundayPlanHours = this.data.startSunday && this.data.endSunday
      ? this.data.endSunday - this.data.startSunday - this.data.breakSunday
      : 0;
    this.data.sundayPlanHours += this.data.startSunday2NdShift && this.data.endSunday2NdShift
      ? this.data.endSunday2NdShift - this.data.startSunday2NdShift - this.data.breakSunday2NdShift
      : 0;
    this.data.sundayPlanHours += this.data.startSunday3RdShift && this.data.endSunday3RdShift
      ? this.data.endSunday3RdShift - this.data.startSunday3RdShift - this.data.breakSunday3RdShift
      : 0;
    this.data.sundayPlanHours += this.data.startSunday4ThShift && this.data.endSunday4ThShift
      ? this.data.endSunday4ThShift - this.data.startSunday4ThShift - this.data.breakSunday4ThShift
      : 0;
    this.data.sundayPlanHours += this.data.startSunday5ThShift && this.data.endSunday5ThShift
      ? this.data.endSunday5ThShift - this.data.startSunday5ThShift - this.data.breakSunday5ThShift
      : 0;
  }

  private padZero(num: number): string {
    return num < 10 ? `0${num}` : `${num}`;
  }

  copyBreakSettings(day: string) {
    switch (day) {
      case 'monday':
        this.data.mondayBreakMinutesDivider = this.globalAutoBreakSettings.mondayBreakMinutesDivider;
        this.data.mondayBreakMinutesPrDivider = this.globalAutoBreakSettings.mondayBreakMinutesPrDivider;
        this.data.mondayBreakMinutesUpperLimit = this.globalAutoBreakSettings.mondayBreakMinutesUpperLimit;
        break;
      case 'tuesday':
        this.data.tuesdayBreakMinutesDivider = this.globalAutoBreakSettings.tuesdayBreakMinutesDivider;
        this.data.tuesdayBreakMinutesPrDivider = this.globalAutoBreakSettings.tuesdayBreakMinutesPrDivider;
        this.data.tuesdayBreakMinutesUpperLimit = this.globalAutoBreakSettings.tuesdayBreakMinutesUpperLimit;
        break;
      case 'wednesday':
        this.data.wednesdayBreakMinutesDivider = this.globalAutoBreakSettings.wednesdayBreakMinutesDivider;
        this.data.wednesdayBreakMinutesPrDivider = this.globalAutoBreakSettings.wednesdayBreakMinutesPrDivider;
        this.data.wednesdayBreakMinutesUpperLimit = this.globalAutoBreakSettings.wednesdayBreakMinutesUpperLimit;
        break;
      case 'thursday':
        this.data.thursdayBreakMinutesDivider = this.globalAutoBreakSettings.thursdayBreakMinutesDivider;
        this.data.thursdayBreakMinutesPrDivider = this.globalAutoBreakSettings.thursdayBreakMinutesPrDivider;
        this.data.thursdayBreakMinutesUpperLimit = this.globalAutoBreakSettings.thursdayBreakMinutesUpperLimit;
        break;
      case 'friday':
        this.data.fridayBreakMinutesDivider = this.globalAutoBreakSettings.fridayBreakMinutesDivider;
        this.data.fridayBreakMinutesPrDivider = this.globalAutoBreakSettings.fridayBreakMinutesPrDivider;
        this.data.fridayBreakMinutesUpperLimit = this.globalAutoBreakSettings.fridayBreakMinutesUpperLimit;
        break;
      case 'saturday':
        this.data.saturdayBreakMinutesDivider = this.globalAutoBreakSettings.saturdayBreakMinutesDivider;
        this.data.saturdayBreakMinutesPrDivider = this.globalAutoBreakSettings.saturdayBreakMinutesPrDivider;
        this.data.saturdayBreakMinutesUpperLimit = this.globalAutoBreakSettings.saturdayBreakMinutesUpperLimit;
        break;
      case 'sunday':
        this.data.sundayBreakMinutesDivider = this.globalAutoBreakSettings.sundayBreakMinutesDivider;
        this.data.sundayBreakMinutesPrDivider = this.globalAutoBreakSettings.sundayBreakMinutesPrDivider;
        this.data.sundayBreakMinutesUpperLimit = this.globalAutoBreakSettings.sundayBreakMinutesUpperLimit;
        break;
    }
  }
}
