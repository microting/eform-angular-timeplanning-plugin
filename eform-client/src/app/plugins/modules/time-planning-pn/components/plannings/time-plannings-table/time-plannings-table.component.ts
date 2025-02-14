import {Component, EventEmitter, Input, OnInit, Output, TemplateRef, ViewChild, ViewEncapsulation} from '@angular/core';
import { TableHeaderElementModel } from 'src/app/common/models';
import { TimePlanningModel } from '../../../models';
import {MtxGrid, MtxGridColumn} from '@ng-matero/extensions/grid';
import {TranslateService} from '@ngx-translate/core';
import {FormGroup} from '@angular/forms';
import {DaysOfWeekEnum} from 'src/app/common/const';

@Component({
  selector: 'app-time-plannings-table',
  templateUrl: './time-plannings-table.component.html',
  styleUrls: ['./time-plannings-table.component.scss'],
  encapsulation: ViewEncapsulation.None,
  standalone: false
})
export class TimePlanningsTableComponent implements OnInit {
  @Input() timePlannings: TimePlanningModel[] = [];
  @Output()
  timePlanningChanged: EventEmitter<TimePlanningModel> = new EventEmitter<TimePlanningModel>();
  @Output() sortChanged: EventEmitter<string> = new EventEmitter<string>();
  tableHeaders: MtxGridColumn[] = [];

  @ViewChild('firstColumnTemplate', { static: true }) firstColumnTemplate!: TemplateRef<any>;
  @ViewChild('dayColumnTemplate', { static: true }) dayColumnTemplate!: TemplateRef<any>;
  //tableHeaders: TableHeaderElementModel[] = [
    // { name: 'DayOfWeek', elementId: 'dayOfWeekTableHeader', sortable: true },
    // { name: 'Date', elementId: 'dateTableHeader', sortable: true },
    // { name: 'Plan text', elementId: 'planTextTableHeader', sortable: false },
    // { name: 'Plan hours', elementId: 'planHoursTableHeader', sortable: false },
    // { name: 'Message', elementId: 'messageTableHeader', sortable: false },
  //];

  constructor(
    private translateService: TranslateService,
    ) {}

  ngOnInit(): void {
    this.tableHeaders = [
      {
        cellTemplate: this.firstColumnTemplate,
        header: this.translateService.stream('Name'),
        pinned: 'left',
        field: 'siteName',
        sortable: true,
      },
      {
        cellTemplate: this.dayColumnTemplate,
        header: this.translateService.stream('Monday'),
        field: '0',
        sortable: false,
        class: (row: any) => this.getCellClass(row, '0'),
      },
      {
        cellTemplate: this.dayColumnTemplate,
        header: this.translateService.stream('Tuesday'),
        field: '1',
        sortable: false,
        class: (row: any) => this.getCellClass(row, '1'),
      },
      {
        cellTemplate: this.dayColumnTemplate,
        header: this.translateService.stream('Wednesday'),
        field: '2',
        sortable: false,
        class: (row: any) => this.getCellClass(row, '2'),
      },
      {
        cellTemplate: this.dayColumnTemplate,
        header: this.translateService.stream('Thursday'),
        field: '3',
        sortable: false,
        class: (row: any) => this.getCellClass(row, '3'),
      },
      {
        cellTemplate: this.dayColumnTemplate,
        header: this.translateService.stream('Friday'),
        field: '4',
        sortable: false,
        class: (row: any) => this.getCellClass(row, '4'),
      },
      {
        cellTemplate: this.dayColumnTemplate,
        header: this.translateService.stream('Saturday'),
        field: '5',
        sortable: false,
        class: (row: any) => this.getCellClass(row, '5'),
      },
      {
        cellTemplate: this.dayColumnTemplate,
        header: this.translateService.stream('Sunday'),
        field: '6',
        sortable: false,
        class: (row: any) => this.getCellClass(row, '6'),
      },
    ];
  }

  // sortTable(sort: string) {
  //   this.sortChanged.emit(sort);
  // }
  //
  // onTimePlanningChanged(
  //   planHours: number,
  //   planText: string,
  //   message: number,
  //   timePlanning: TimePlanningModel
  // ) {
  //   this.timePlanningChanged.emit({
  //     ...timePlanning,
  //     planHours: planHours ?? timePlanning.planHours,
  //     message: message ?? timePlanning.message,
  //     planText: planText ?? timePlanning.planText,
  //   });
  // }

  getCellClass(row: any, field: string): string {
    const planHours = row.planningPrDayModels[field]?.planHours;
    const workDayStarted = row.planningPrDayModels[field]?.workDayStarted;
    if (planHours > 0) {
      return workDayStarted ? 'green-background' : 'grey-background';
    }
    return '';
  }

  protected readonly JSON = JSON;
}
