import {Component, DoCheck, EventEmitter, Inject, OnChanges, SimpleChanges} from '@angular/core';
import {
  MAT_DIALOG_DATA,
  MatDialogActions,
  MatDialogClose,
  MatDialogContent,
  MatDialogTitle
} from '@angular/material/dialog';
import { AssignedSiteModel } from '../../../../models';
import {MatButton} from '@angular/material/button';
import {FormsModule} from '@angular/forms';
import {MatFormField, MatLabel} from '@angular/material/form-field';
import {MatInput} from '@angular/material/input';
import {TranslatePipe} from '@ngx-translate/core';
import {selectCurrentUserIsAdmin} from 'src/app/state';
import {Store} from '@ngrx/store';
import {AsyncPipe, NgForOf, NgIf} from '@angular/common';
import {MatTab, MatTabGroup} from '@angular/material/tabs';
import {NgxMaskDirective} from 'ngx-mask';
import {MatCheckbox} from '@angular/material/checkbox';
import {TimePlanningPnSettingsService} from 'src/app/plugins/modules/time-planning-pn/services';

@Component({
  selector: 'app-assigned-site-dialog',
  templateUrl: './assigned-site-dialog.component.html',
  imports: [
    MatDialogTitle,
    MatDialogContent,
    MatDialogActions,
    MatButton,
    MatDialogClose,
    FormsModule,
    MatFormField,
    MatInput,
    MatLabel,
    TranslatePipe,
    AsyncPipe,
    NgIf,
    MatTab,
    MatTabGroup,
    NgForOf,
    NgxMaskDirective,
    MatCheckbox,
  ],
  styleUrls: ['./assigned-site-dialog.component.scss']
})
export class AssignedSiteDialogComponent implements DoCheck {
  public selectCurrentUserIsAdmin$ = this.authStore.select(selectCurrentUserIsAdmin);
  private previousData: AssignedSiteModel;
  assignedSiteUpdate: EventEmitter<AssignedSiteModel> = new EventEmitter<AssignedSiteModel>();

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: AssignedSiteModel,
    private timePlanningPnSettingsService: TimePlanningPnSettingsService,
    private authStore: Store) {
    this.previousData = { ...data };
    this.calculateHours();
  }

  ngDoCheck(): void {
    if (this.hasDataChanged()) {
      // this.calculateHours();
      // this.previousData = { ...this.data };
    }
  }

  hasDataChanged(): boolean {
    return JSON.stringify(this.data) !== JSON.stringify(this.previousData);
  }

  calculateHours(): void {
    // eslint-disable-next-line max-len
    this.data.mondayCalculatedHours = this.calculateDayHours(this.data.startMonday, this.data.endMonday, this.data.breakMonday, this.data.startMonday2NdShift, this.data.endMonday2NdShift, this.data.breakMonday2NdShift);
    // eslint-disable-next-line max-len
    this.data.tuesdayCalculatedHours = this.calculateDayHours(this.data.startTuesday, this.data.endTuesday, this.data.breakTuesday, this.data.startTuesday2NdShift, this.data.endTuesday2NdShift, this.data.breakTuesday2NdShift);
    // eslint-disable-next-line max-len
    this.data.wednesdayCalculatedHours = this.calculateDayHours(this.data.startWednesday, this.data.endWednesday, this.data.breakWednesday, this.data.startWednesday2NdShift, this.data.endWednesday2NdShift, this.data.breakWednesday2NdShift);
    // eslint-disable-next-line max-len
    this.data.thursdayCalculatedHours = this.calculateDayHours(this.data.startThursday, this.data.endThursday, this.data.breakThursday, this.data.startThursday2NdShift, this.data.endThursday2NdShift, this.data.breakThursday2NdShift);
    // eslint-disable-next-line max-len
    this.data.fridayCalculatedHours = this.calculateDayHours(this.data.startFriday, this.data.endFriday, this.data.breakFriday, this.data.startFriday2NdShift, this.data.endFriday2NdShift, this.data.breakFriday2NdShift);
    // eslint-disable-next-line max-len
    this.data.saturdayCalculatedHours = this.calculateDayHours(this.data.startSaturday, this.data.endSaturday, this.data.breakSaturday, this.data.startSaturday2NdShift, this.data.endSaturday2NdShift, this.data.breakSaturday2NdShift);
    // eslint-disable-next-line max-len
    this.data.sundayCalculatedHours = this.calculateDayHours(this.data.startSunday, this.data.endSunday, this.data.breakSunday, this.data.startSunday2NdShift, this.data.endSunday2NdShift, this.data.breakSunday2NdShift);
  }

  // eslint-disable-next-line max-len
  calculateDayHours(start: number, end: number, breakTime: number, start2NdShift: number, end2NdShift: number, break2NdShift: number): string {
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
    const [hours, mins] = event.target.value.split(':').map(Number);
    this.data[field] = (hours * 60) + mins;
    this.calculateHours();
    this.previousData = { ...this.data };
  }

  private padZero(num: number): string {
    return num < 10 ? `0${num}` : `${num}`;
  }

  updateAssignedSite() {
    this.data.mondayPlanHours = this.data.startMonday && this.data.endMonday
      ? this.data.endMonday - this.data.startMonday - this.data.breakMonday
      : 0;
    this.data.mondayPlanHours += this.data.startMonday2NdShift && this.data.endMonday2NdShift
      ? this.data.endMonday2NdShift - this.data.startMonday2NdShift - this.data.breakMonday2NdShift
      : 0;
    this.data.tuesdayPlanHours = this.data.startTuesday && this.data.endTuesday
      ? this.data.endTuesday - this.data.startTuesday - this.data.breakTuesday
      : 0;
    this.data.tuesdayPlanHours += this.data.startTuesday2NdShift && this.data.endTuesday2NdShift
      ? this.data.endTuesday2NdShift - this.data.startTuesday2NdShift - this.data.breakTuesday2NdShift
      : 0;
    this.data.wednesdayPlanHours = this.data.startWednesday && this.data.endWednesday
      ? this.data.endWednesday - this.data.startWednesday - this.data.breakWednesday
      : 0;
    this.data.wednesdayPlanHours += this.data.startWednesday2NdShift && this.data.endWednesday2NdShift
      ? this.data.endWednesday2NdShift - this.data.startWednesday2NdShift - this.data.breakWednesday2NdShift
      : 0;
    this.data.thursdayPlanHours = this.data.startThursday && this.data.endThursday
      ? this.data.endThursday - this.data.startThursday - this.data.breakThursday
      : 0;
    this.data.thursdayPlanHours += this.data.startThursday2NdShift && this.data.endThursday2NdShift
      ? this.data.endThursday2NdShift - this.data.startThursday2NdShift - this.data.breakThursday2NdShift
      : 0;
    this.data.fridayPlanHours = this.data.startFriday && this.data.endFriday
      ? this.data.endFriday - this.data.startFriday - this.data.breakFriday
      : 0;
    this.data.fridayPlanHours += this.data.startFriday2NdShift && this.data.endFriday2NdShift
      ? this.data.endFriday2NdShift - this.data.startFriday2NdShift - this.data.breakFriday2NdShift
      : 0;
    this.data.saturdayPlanHours = this.data.startSaturday && this.data.endSaturday
      ? this.data.endSaturday - this.data.startSaturday - this.data.breakSaturday
      : 0;
    this.data.saturdayPlanHours += this.data.startSaturday2NdShift && this.data.endSaturday2NdShift
      ? this.data.endSaturday2NdShift - this.data.startSaturday2NdShift - this.data.breakSaturday2NdShift
      : 0;
    this.data.sundayPlanHours = this.data.startSunday && this.data.endSunday
      ? this.data.endSunday - this.data.startSunday - this.data.breakSunday
      : 0;
    this.data.sundayPlanHours += this.data.startSunday2NdShift && this.data.endSunday2NdShift
      ? this.data.endSunday2NdShift - this.data.startSunday2NdShift - this.data.breakSunday2NdShift
      : 0;
    this.timePlanningPnSettingsService.updateAssignedSite(this.data).subscribe(result => {
      if (result && result.success) {
        //this.workdayEntityUpdate.emit(this.data);
      }
    });
  }
}
