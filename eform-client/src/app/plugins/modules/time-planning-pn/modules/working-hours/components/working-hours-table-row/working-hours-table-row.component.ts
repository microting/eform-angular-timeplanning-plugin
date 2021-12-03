import {
  Component,
  Input,
  OnChanges,
  OnDestroy,
  OnInit,
  SimpleChanges,
  ViewChild,
} from '@angular/core';
import { FormGroup } from '@angular/forms';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import { Subscription } from 'rxjs';
import {
  DaysOfWeekEnum,
  HOURS_PICKER_ARRAY,
  STANDARD_DATE_FORMAT,
} from 'src/app/common/const';
import { TranslateService } from '@ngx-translate/core';
import { messages } from '../../../../consts/messages';
import {
  WorkingHoursCommentOfficeAllUpdateModalComponent,
  WorkingHoursCommentOfficeUpdateModalComponent,
} from '../../components';

@AutoUnsubscribe()
@Component({
  // tslint:disable-next-line:component-selector
  selector: '[working-hours-table-row]',
  templateUrl: './working-hours-table-row.component.html',
  styleUrls: ['./working-hours-table-row.component.scss'],
})
export class WorkingHoursTableRowComponent
  implements OnInit, OnDestroy, OnChanges {
  @Input() workingHoursForm: FormGroup;
  @Input() workingHoursFormIndex: number;
  @ViewChild('editCommentOfficeModal')
  editCommentOfficeModal: WorkingHoursCommentOfficeUpdateModalComponent;
  @ViewChild('editCommentOfficeAllModal')
  editCommentOfficeAllModal: WorkingHoursCommentOfficeAllUpdateModalComponent;
  messages: { id: number; value: string }[] = [];

  subs$: Subscription[] = [];

  get hoursPickerArray() {
    return HOURS_PICKER_ARRAY;
  }

  get daysOfWeek() {
    return DaysOfWeekEnum;
  }

  get dateFormat() {
    return STANDARD_DATE_FORMAT;
  }

  constructor(translateService: TranslateService) {
    this.messages = messages(translateService);
  }

  ngOnInit(): void {
    const shouldDisable = this.workingHoursForm.get('isLocked').value;
    if (shouldDisable) {
      this.workingHoursForm.disable();
    }
  }

  ngOnDestroy(): void {
    for (const sub$ of this.subs$) {
      sub$.unsubscribe();
    }
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes && changes.workingHoursForm) {
      // Unsubscribe from previous subs
      for (const sub$ of this.subs$) {
        sub$.unsubscribe();
      }
      this.subs$ = [];

      if (
        changes.workingHoursForm.currentValue &&
        changes.workingHoursForm.currentValue.controls
      ) {
        const shift1StartSub$ = this.workingHoursForm
          .get('shift1Start')
          .valueChanges.subscribe(() => {
            this.workingHoursForm
              .get('nettoHours')
              .setValue(this.calculateNettoHours().formattedHours);
          });

        const shift1StopSub$ = this.workingHoursForm
          .get('shift1Stop')
          .valueChanges.subscribe(() => {
            this.workingHoursForm
              .get('nettoHours')
              .setValue(this.calculateNettoHours().formattedHours);
          });

        const shift1PauseSub$ = this.workingHoursForm
          .get('shift1Pause')
          .valueChanges.subscribe(() => {
            this.workingHoursForm
              .get('nettoHours')
              .setValue(this.calculateNettoHours().formattedHours);
          });

        const shift2StartSub$ = this.workingHoursForm
          .get('shift2Start')
          .valueChanges.subscribe(() => {
            this.workingHoursForm
              .get('nettoHours')
              .setValue(this.calculateNettoHours().formattedHours);
          });

        const shift2StopSub$ = this.workingHoursForm
          .get('shift2Stop')
          .valueChanges.subscribe(() => {
            this.workingHoursForm
              .get('nettoHours')
              .setValue(this.calculateNettoHours().formattedHours);
          });

        const shift2PauseSub$ = this.workingHoursForm
          .get('shift2Pause')
          .valueChanges.subscribe(() => {
            this.workingHoursForm
              .get('nettoHours')
              .setValue(this.calculateNettoHours().formattedHours);
          });

        const flexHoursSub$ = this.workingHoursForm
          .get('nettoHours')
          .valueChanges.subscribe(() => {
            this.workingHoursForm
              .get('flexHours')
              .setValue(this.calculateFlexHours());
          });

        const planHoursSub$ = this.workingHoursForm
          .get('planHours')
          .valueChanges.subscribe(() => {
            this.workingHoursForm
              .get('flexHours')
              .setValue(this.calculateFlexHours());
          });

        this.subs$ = [
          shift1StartSub$,
          shift1StopSub$,
          shift1PauseSub$,
          shift2StartSub$,
          shift2StopSub$,
          shift2PauseSub$,
          flexHoursSub$,
          planHoursSub$,
        ];
      }
    }
  }

  calculateNettoHours(): { formattedHours: number; rawMinutes: number } {
    const shift1Start = this.workingHoursForm.get('shift1Start').value;
    const shift1Stop = this.workingHoursForm.get('shift1Stop').value;
    const shift1Pause = this.workingHoursForm.get('shift1Pause').value;
    const shift2Start = this.workingHoursForm.get('shift2Start').value;
    const shift2Stop = this.workingHoursForm.get('shift2Stop').value;
    const shift2Pause = this.workingHoursForm.get('shift2Pause').value;

    const offset = 1;
    const minutesMultiplier = 5;

    let nettoMinutes = 0;

    if (shift1Start && shift1Stop) {
      nettoMinutes = shift1Stop - shift1Start;
      if (shift1Pause) {
        nettoMinutes = nettoMinutes - shift1Pause + offset;
      }
    }

    if (shift2Start && shift2Stop) {
      nettoMinutes = nettoMinutes + shift2Stop - shift2Start;
      if (shift2Pause) {
        nettoMinutes = nettoMinutes - shift2Pause + offset;
      }
    }

    nettoMinutes = nettoMinutes * minutesMultiplier;

    const hours = +(nettoMinutes / 60).toFixed(2);
    // const minutes = nettoMinutes % 60;
    // const formattedMinutes = minutes.toLocaleString('en-US', {
    //   minimumIntegerDigits: 2,
    //   useGrouping: false,
    // });

    // const formattedHours = hours.toLocaleString('en-US', {
    //   minimumIntegerDigits: 2,
    //   minimumFractionDigits: 2,
    //   useGrouping: false,
    // });

    return {
      formattedHours: hours,
      rawMinutes: nettoMinutes,
    };
  }

  calculateFlexHours() {
    const nettoHours = this.workingHoursForm.get('nettoHours').value;
    const planHours = this.workingHoursForm.get('planHours').value;
    return +(nettoHours - planHours).toFixed(2);
  }

  get commentWorker(): string {
    return this.workingHoursForm.get('commentWorker').value;
  }

  get commentOffice(): string {
    return this.workingHoursForm.get('commentOffice').value;
  }

  get commentOfficeAll(): string {
    return this.workingHoursForm.get('commentOfficeAll').value;
  }

  openEditCommentOfficeModal() {
    this.editCommentOfficeModal.show();
  }

  openEditCommentOfficeAllModal() {
    this.editCommentOfficeAllModal.show();
  }
}
