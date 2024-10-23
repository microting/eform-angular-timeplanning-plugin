import {Component, OnDestroy, OnInit} from '@angular/core';
import {WorkingHourModel} from 'src/app/plugins/modules/time-planning-pn/models';
import {TimePlanningPnWorkingHoursService} from 'src/app/plugins/modules/time-planning-pn/services';
import {format} from 'date-fns';
import {PARSING_DATE_FORMAT} from 'src/app/common/const';
import {take} from "rxjs";
import {TitleService} from "src/app/common/services";
import {TranslateService} from "@ngx-translate/core";
@Component({
  selector: 'app-mobile-working-hours',
  templateUrl: './mobile-working-hours.component.html',
  styleUrls: ['./mobile-working-hours.component.scss'],
})
export class MobileWorkingHoursComponent implements OnInit, OnDestroy {
  displayedColumns: string[] = ['property', 'value'];

  //workingHourModel: WorkingHourModel;
  workingHourModel: WorkingHourModel;
  selectedDate: Date = new Date();
  yesterday: Date = new Date(this.selectedDate.setDate(this.selectedDate.getDate() - 1));

  constructor(
    private titleService: TitleService,
    private translateService: TranslateService,
    private workingHoursService: TimePlanningPnWorkingHoursService) {
  }

  ngOnDestroy(): void {
  }

  ngOnInit(): void {
    this.workingHoursService.getWorkingHourReadSimple(format(this.selectedDate, PARSING_DATE_FORMAT))
      .subscribe((data) => {
      if (data && data.success) {
        this.workingHourModel = data.model;
      }
    });
    this.translateService.get('Timer').pipe(take(1)).subscribe(translate => this.titleService.setTitle(translate));
  }

  goBackward() {
    this.selectedDate = new Date(this.selectedDate.setDate(this.selectedDate.getDate() - 1));
    this.workingHoursService.getWorkingHourReadSimple(format(this.selectedDate, PARSING_DATE_FORMAT))
      .subscribe((data) => {
        if (data && data.success) {
          this.workingHourModel = data.model;
        }
      });
  }

  goForward() {
    this.selectedDate = new Date(this.selectedDate.setDate(this.selectedDate.getDate() + 1));
    this.workingHoursService.getWorkingHourReadSimple(format(this.selectedDate, PARSING_DATE_FORMAT))
      .subscribe((data) => {
        if (data && data.success) {
          this.workingHourModel = data.model;
        }
      });
  }


  isToday(): boolean {
    const today = new Date();
    return this.selectedDate.toDateString() === today.toDateString();
  }

  get workingHourData() {
    return Object.entries(this.workingHourModel)
      .filter(([key, value]) => value !== null && value !== undefined && value !== '' && key !== 'date' && key !== 'yesterDay')
      .map(([key, value]) => ({ property: key, value }));
  }


  updateSelectedDate() {
    this.workingHoursService.getWorkingHourReadSimple(format(this.selectedDate, PARSING_DATE_FORMAT))
      .subscribe((data) => {
        if (data && data.success) {
          this.workingHourModel = data.model;
        }
      });
  }
}
