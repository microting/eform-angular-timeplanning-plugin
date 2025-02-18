// src/app/plugins/modules/time-planning-pn/components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component.ts
import { Component, Inject } from '@angular/core';
import {
  MAT_DIALOG_DATA,
  MatDialogActions,
  MatDialogClose,
  MatDialogContent,
  MatDialogTitle
} from '@angular/material/dialog';
import {MatButton} from '@angular/material/button';
import {TranslatePipe} from '@ngx-translate/core';
import { DatePipe } from '@angular/common';
import {PlanningPrDayModel} from 'src/app/plugins/modules/time-planning-pn/models';
import {MatCheckbox} from "@angular/material/checkbox";
import {FormsModule} from "@angular/forms";

@Component({
  selector: 'app-workday-entity-dialog',
  templateUrl: './workday-entity-dialog.component.html',
  imports: [
    MatButton,
    MatDialogActions,
    MatDialogClose,
    TranslatePipe,
    MatDialogTitle,
    MatDialogContent,
    MatCheckbox,
    FormsModule
  ],
  styleUrls: ['./workday-entity-dialog.component.scss']
})
export class WorkdayEntityDialogComponent {
  constructor(@Inject(MAT_DIALOG_DATA) public data: PlanningPrDayModel, protected datePipe: DatePipe) {}

  protected readonly JSON = JSON;

  convertMinutesToTime(minutes: number): string {
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return `${this.padZero(hours)}:${this.padZero(mins)}`;
  }

  private padZero(num: number): string {
    return num < 10 ? '0' + num : num.toString();
  }

  onCheckboxChange(selectedOption: string): void {
    this.data.onVacation = selectedOption === 'onVacation';
    this.data.sick = selectedOption === 'sick';
    this.data.otherAllowedAbsence = selectedOption === 'otherAllowedAbsence';
    this.data.absenceWithoutPermission = selectedOption === 'absenceWithoutPermission';
  }
}
