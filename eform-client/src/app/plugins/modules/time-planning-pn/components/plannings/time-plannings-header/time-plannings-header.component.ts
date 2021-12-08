import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { format } from 'date-fns';
import { PARSING_DATE_FORMAT } from 'src/app/common/const';
import { SiteDto } from 'src/app/common/models';
import { TimePlanningsRequestModel } from '../../../models';

@Component({
  selector: 'app-time-plannings-header',
  templateUrl: './time-plannings-header.component.html',
  styleUrls: ['./time-plannings-header.component.scss'],
})
export class TimePlanningsHeaderComponent implements OnInit {
  @Input()
  timePlanningsRequest: TimePlanningsRequestModel = new TimePlanningsRequestModel();
  @Input() availableSites: SiteDto[] = [];
  @Output()
  filtersChanged: EventEmitter<TimePlanningsRequestModel> = new EventEmitter<TimePlanningsRequestModel>();

  dateRange: any;
  siteId: number;

  constructor() {}

  ngOnInit(): void {}

  updateDateRange(range: any[]) {
    this.dateRange = range;
    if (this.siteId) {
      this.filtersChanged.emit({
        siteId: this.siteId,
        dateFrom: format(range[0]._d, PARSING_DATE_FORMAT),
        dateTo: format(range[1]._d, PARSING_DATE_FORMAT),
      });
    }
  }

  onSiteChanged(siteId: number) {
    this.siteId = siteId;
    if (this.dateRange) {
      this.filtersChanged.emit({
        siteId,
        dateFrom: format(this.dateRange[0]._d, PARSING_DATE_FORMAT),
        dateTo: format(this.dateRange[1]._d, PARSING_DATE_FORMAT),
      });
    }
  }
}
