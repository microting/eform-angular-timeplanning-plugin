import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { format } from 'date-fns';
import { PARSING_DATE_FORMAT } from 'src/app/common/const';
import { SiteDto } from 'src/app/common/models';
import { TimePlanningsRequestModel } from '../../../../models';

@Component({
  selector: 'app-working-hours-header',
  templateUrl: './working-hours-header.component.html',
  styleUrls: ['./working-hours-header.component.scss'],
})
export class WorkingHoursHeaderComponent implements OnInit {
  @Input()
  workingHoursRequest: TimePlanningsRequestModel = new TimePlanningsRequestModel();
  @Input() availableSites: SiteDto[] = [];
  @Output()
  filtersChanged: EventEmitter<TimePlanningsRequestModel> = new EventEmitter<TimePlanningsRequestModel>();
  @Output() updateWorkingHours: EventEmitter<void> = new EventEmitter<void>();

  dateRange: any;
  siteId: number;

  constructor() {}

  ngOnInit(): void {}

  updateDateRange(range: Date[]) {
    this.dateRange = range;
    this.filtersChangedEmmit();
  }

  onSiteChanged(siteId: number) {
    this.siteId = siteId;
    this.filtersChangedEmmit();
  }

  onSaveWorkingHours() {
    this.updateWorkingHours.emit();
  }

  filtersChangedEmmit(): void {
    if (this.dateRange && this.siteId) {
      this.filtersChanged.emit({
        siteId: this.siteId,
        dateFrom: format(this.dateRange[0]._d, PARSING_DATE_FORMAT),
        dateTo: format(this.dateRange[1]._d, PARSING_DATE_FORMAT),
      });
    }
  }
}
