import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { TableHeaderElementModel } from 'src/app/common/models';
import { TimePlanningModel } from '../../../models';
import { TimePlanningsStateService } from '../store';

@Component({
  selector: 'app-time-plannings-table',
  templateUrl: './time-plannings-table.component.html',
  styleUrls: ['./time-plannings-table.component.scss'],
})
export class TimePlanningsTableComponent implements OnInit {
  @Input() timePlannings: TimePlanningModel[] = [];
  @Output() timePlanningChanged: EventEmitter<TimePlanningModel> =
    new EventEmitter<TimePlanningModel>();
  @Output() sortChanged: EventEmitter<string> = new EventEmitter<string>();

  tableHeaders: TableHeaderElementModel[] = [
    { name: 'Day of week', elementId: 'dayOfWeekTableHeader', sortable: true },
    { name: 'Date', elementId: 'dateTableHeader', sortable: true },
    { name: 'Plan text', elementId: 'planTextTableHeader', sortable: false },
    { name: 'Plan hours', elementId: 'planHoursTableHeader', sortable: false },
    { name: 'Message', elementId: 'messageTableHeader', sortable: false },
    { name: 'Actions', elementId: '', sortable: false },
  ];

  constructor(public planningsStateService: TimePlanningsStateService) {}

  ngOnInit(): void {}

  sortTable(sort: string) {
    this.sortChanged.emit(sort);
  }

  onTimePlanningChanged(
    planHours: number,
    planText: string,
    message: number,
    timePlanning: TimePlanningModel
  ) {
    this.timePlanningChanged.emit({
      ...timePlanning,
      planHours,
      message,
      planText,
    });
  }
}
