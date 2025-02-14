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
}
