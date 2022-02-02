import {
  Component,
  EventEmitter,
  Input,
  OnChanges,
  OnInit,
  Output,
  SimpleChanges,
} from '@angular/core';
import {AbstractControl, FormArray, FormControl, FormGroup} from '@angular/forms';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import { Subscription } from 'rxjs';
import { HOURS_PICKER_ARRAY } from 'src/app/common/const';
import { TableHeaderElementModel } from 'src/app/common/models';
import { TimePlanningModel } from '../../../../models';

@Component({
  selector: 'app-working-hours-table',
  templateUrl: './working-hours-table.component.html',
  styleUrls: ['./working-hours-table.component.scss'],
})
export class WorkingHoursTableComponent implements OnInit, OnChanges {
  @Input() workingHours: TimePlanningModel[] = [];
  @Input() workingHoursFormArray: FormArray = new FormArray([]);
  @Input() timePlannings: TimePlanningModel[] = [];
  @Output()
  timePlanningChanged: EventEmitter<TimePlanningModel> = new EventEmitter<TimePlanningModel>();
  @Output() sortChanged: EventEmitter<string> = new EventEmitter<string>();

  sub$: Subscription;

  tableHeaders: TableHeaderElementModel[] = [
    { name: 'DayOfWeek', elementId: 'dayOfWeekTableHeader', sortable: false },
    { name: 'Date', elementId: 'dateTableHeader', sortable: false },
    { name: 'Plan text', elementId: 'planTextTableHeader', sortable: false },
    { name: 'Plan hours', elementId: 'planHoursTableHeader', sortable: false },
    {
      name: 'Shift 1: Start',
      elementId: 'shift1StartTableHeader',
      sortable: false,
    },
    {
      name: 'Shift 1: Stop',
      elementId: 'shift1StopTableHeader',
      sortable: false,
    },
    {
      name: 'Shift 1: Pause',
      elementId: 'shift1PauseTableHeader',
      sortable: false,
    },
    {
      name: 'Shift 2: Start',
      elementId: 'shift2StartTableHeader',
      sortable: false,
    },
    {
      name: 'Shift 2: Stop',
      elementId: 'shift2StopTableHeader',
      sortable: false,
    },
    {
      name: 'Shift 2: Pause',
      elementId: 'shift2PauseTableHeader',
      sortable: false,
    },
    { name: 'NettoHours', elementId: 'nettoHoursTableHeader', sortable: false },
    { name: 'Flex', elementId: 'flexTableHeader', sortable: false },
    { name: 'SumFlex', elementId: 'flexTableHeader', sortable: false },
    { name: 'PaidOutFlex', elementId: 'flexTableHeader', sortable: false },
    { name: 'Message', elementId: 'messageTableHeader', sortable: false },
    { name: 'CommentWorker', elementId: 'flexTableHeader', sortable: false },
    { name: 'CommentOffice', elementId: 'flexTableHeader', sortable: false },
    // { name: 'CommentOfficeAll', elementId: 'flexTableHeader', sortable: false },
  ];

  constructor() {}

  getIsWeekend(workingHoursModel: AbstractControl): boolean {
    if (workingHoursModel != null) {
      return workingHoursModel.get('isWeekend').value;
    }
  }

  getIsLocked(workingHoursModel: AbstractControl): boolean {
    if (workingHoursModel != null) {
      return workingHoursModel.disabled;
    }
  }

  ngOnInit(): void {
    // if (this.sub$) {
    //   this.sub$.unsubscribe();
    // }
    // this.sub$ = this.workingHoursFormArray.valueChanges.subscribe(
    //   (selectedValue) => {
    //     // this.recalculateSumFlex();
    //   }
    // );
  }

  ngOnChanges(changes: SimpleChanges): void {
    // if (changes && changes.timePlannings) {
    //   if (this.sub$) {
    //     this.sub$.unsubscribe();
    //   }
    //   this.sub$ = this.workingHoursFormArray.valueChanges.subscribe(
    //     (selectedValue) => {
    //       this.recalculateSumFlex();
    //     }
    //   );
    // }
  }

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
}
