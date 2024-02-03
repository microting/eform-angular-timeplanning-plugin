import {
  Component,
  EventEmitter,
  Input,
  OnChanges, OnDestroy,
  OnInit,
  Output,
  SimpleChanges, TemplateRef, ViewChild, ViewEncapsulation,
} from '@angular/core';
import {AbstractControl, FormArray, FormControl, FormGroup} from '@angular/forms';
import {Subscription} from 'rxjs';
import {SiteDto} from 'src/app/common/models';
import {TimePlanningModel, TimePlanningsRequestModel} from '../../../../models';
import {MtxGridColumn, MtxGridRowClassFormatter} from '@ng-matero/extensions/grid';
import {TranslateService} from '@ngx-translate/core';
import {DaysOfWeekEnum, HOURS_PICKER_ARRAY, STANDARD_DANISH_DATE_FORMAT} from 'src/app/common/const';
import {messages} from '../../../../consts/messages';
import {format} from 'date-fns';
import {MatDialog} from '@angular/material/dialog';
import {Overlay} from '@angular/cdk/overlay';
import {WorkingHoursCommentOfficeUpdateModalComponent} from '../';
import {dialogConfigHelper} from 'src/app/common/helpers';
import {selectCurrentUserLocale} from 'src/app/state';
import {Store} from '@ngrx/store';
import {AutoUnsubscribe} from 'ngx-auto-unsubscribe';

@AutoUnsubscribe()
@Component({
  selector: 'app-working-hours-table',
  templateUrl: './working-hours-table.component.html',
  styleUrls: ['./working-hours-table.component.css'],
  encapsulation: ViewEncapsulation.None,
})
export class WorkingHoursTableComponent implements OnInit, OnChanges, OnDestroy {
  @ViewChild('shiftSelectorTpl', {static: true}) shiftSelectorTpl!: TemplateRef<any>;
  @ViewChild('inputTextTpl', {static: true}) inputTextTpl!: TemplateRef<any>;
  @ViewChild('inputNumberTpl', {static: true}) inputNumberTpl!: TemplateRef<any>;
  @ViewChild('messageSelectorTpl', {static: true}) messageSelectorTpl!: TemplateRef<any>;
  @Input() workingHours: TimePlanningModel[] = [];
  @Input() workingHoursFormArray: FormArray = new FormArray([]);
  @Input() timePlannings: TimePlanningModel[] = [];
  @Input() workingHoursRequest: TimePlanningsRequestModel;
  @Input() tainted: boolean;
  @Input() availableSites: SiteDto[] = [];
  @Output() timePlanningChanged: EventEmitter<TimePlanningModel> = new EventEmitter<TimePlanningModel>();
  @Output() sortChanged: EventEmitter<string> = new EventEmitter<string>();
  @Output() filtersChanged: EventEmitter<TimePlanningsRequestModel> = new EventEmitter<TimePlanningsRequestModel>();
  @Output() updateWorkingHours: EventEmitter<void> = new EventEmitter<void>();
  messages: { id: number; value: string }[] = [];
  private selectCurrentUserLocale$ = this.store.select(selectCurrentUserLocale);
  selectCurrentUserLocaleSub$: Subscription;

  get columns() {
    return this.tableHeaders.map(x => x.field);
  }

  subs$: Subscription[] = [];

  tableHeaders: MtxGridColumn[] = [];

  constructor(
    private translateService: TranslateService,
    private dialog: MatDialog,
    private overlay: Overlay,
    private store: Store,
  ) {
    this.selectCurrentUserLocaleSub$ = this.selectCurrentUserLocale$.subscribe(() => this.messages = messages(translateService));
  }

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

  get hoursPickerArray() {
    return HOURS_PICKER_ARRAY;
  }

  ngOnInit(): void {
    this.tableHeaders = [
      {
        header: this.translateService.stream('DayOfWeek'),
        pinned: 'left',
        field: 'weekDay',
        formatter: (row: FormGroup) => this.translateService.instant(DaysOfWeekEnum[row.get('weekDay').value])
      },
      {
        header: this.translateService.stream('Date'),
        pinned: 'left',
        field: 'date',
        formatter: (row: FormGroup) => `${format(row.get('date').value, STANDARD_DANISH_DATE_FORMAT.replace('YYYY', 'yyyy'))}`,
      },
      {header: this.translateService.stream('Plan text'), field: 'planText', cellTemplate: this.inputTextTpl},
      {header: this.translateService.stream('Plan hours'), field: 'planHours', cellTemplate: this.inputNumberTpl},
      {header: this.translateService.stream('Shift 1: Start'), field: 'shift1Start', cellTemplate: this.shiftSelectorTpl},
      {header: this.translateService.stream('Shift 1: Stop'), field: 'shift1Stop', cellTemplate: this.shiftSelectorTpl},
      {header: this.translateService.stream('Shift 1: Pause'), field: 'shift1Pause', cellTemplate: this.shiftSelectorTpl},
      {header: this.translateService.stream('Shift 2: Start'), field: 'shift2Start', cellTemplate: this.shiftSelectorTpl},
      {header: this.translateService.stream('Shift 2: Stop'), field: 'shift2Stop', cellTemplate: this.shiftSelectorTpl},
      {header: this.translateService.stream('Shift 2: Pause'), field: 'shift2Pause', cellTemplate: this.shiftSelectorTpl},
      {header: this.translateService.stream('NettoHours'), field: 'nettoHours', cellTemplate: this.inputTextTpl},
      {header: this.translateService.stream('Flex'), field: 'flexHours', cellTemplate: this.inputNumberTpl},
      {header: this.translateService.stream('SumFlex'), field: 'sumFlex', cellTemplate: this.inputTextTpl},
      {header: this.translateService.stream('PaidOutFlex'), field: 'paidOutFlex', cellTemplate: this.inputTextTpl},
      {header: this.translateService.stream('Message'), field: 'message', cellTemplate: this.messageSelectorTpl},
      {
        header: this.translateService.stream('CommentWorker'),
        field: 'commentWorker',
        formatter: (row: FormGroup) => row.get('commentWorker').value
      },
      {header: this.translateService.stream('CommentOffice'), field: 'commentOffice', cellTemplate: this.inputTextTpl,},
    ];
  }

  rowClassFormatter: MtxGridRowClassFormatter = {
    'background-powder': (data: FormControl, index) => data.get('isLocked').value === true,
    'background-yellow': (data: FormControl, index) => data.get('isWeekend').value === true,
  };

  ngOnChanges(changes: SimpleChanges): void {
    if (changes && changes.workingHours) {
      // Unsubscribe from previous subs
      for (const sub$ of this.subs$) {
        sub$.unsubscribe();
      }
      this.subs$ = [];
      for (let i = 0; i < this.workingHoursFormArray.length; i++) {
        const workingHoursForm = this.workingHoursFormArray.at(i);
        const shouldDisable = workingHoursForm.get('isLocked').value;
        if (shouldDisable) {
          workingHoursForm.disable();
        }
        const shift1StartSub$ = workingHoursForm
          .get('shift1Start')
          .valueChanges.subscribe(() => {
            workingHoursForm
              .get('nettoHours')
              .setValue(this.calculateNettoHours(workingHoursForm).formattedHours);
          });

        const shift1StopSub$ = workingHoursForm
          .get('shift1Stop')
          .valueChanges.subscribe(() => {
            workingHoursForm
              .get('nettoHours')
              .setValue(this.calculateNettoHours(workingHoursForm).formattedHours);
          });

        const shift1PauseSub$ = workingHoursForm
          .get('shift1Pause')
          .valueChanges.subscribe(() => {
            workingHoursForm
              .get('nettoHours')
              .setValue(this.calculateNettoHours(workingHoursForm).formattedHours);
          });

        const shift2StartSub$ = workingHoursForm
          .get('shift2Start')
          .valueChanges.subscribe(() => {
            workingHoursForm
              .get('nettoHours')
              .setValue(this.calculateNettoHours(workingHoursForm).formattedHours);
          });

        const shift2StopSub$ = workingHoursForm
          .get('shift2Stop')
          .valueChanges.subscribe(() => {
            workingHoursForm
              .get('nettoHours')
              .setValue(this.calculateNettoHours(workingHoursForm).formattedHours);
          });

        const shift2PauseSub$ = workingHoursForm
          .get('shift2Pause')
          .valueChanges.subscribe(() => {
            workingHoursForm
              .get('nettoHours')
              .setValue(this.calculateNettoHours(workingHoursForm).formattedHours);
          });

        const flexHoursSub$ = workingHoursForm
          .get('nettoHours')
          .valueChanges.subscribe(() => {
            workingHoursForm
              .get('flexHours')
              .setValue(this.calculateFlexHours(workingHoursForm));
          });

        const planHoursSub$ = workingHoursForm
          .get('planHours')
          .valueChanges.subscribe(() => {
            workingHoursForm
              .get('flexHours')
              .setValue(this.calculateFlexHours(workingHoursForm));
          });

        const planTextSub$ = workingHoursForm
          .get('planText')
          .valueChanges.subscribe(() => {
            workingHoursForm
              .get('nettoHours')
              .setValue(this.calculateNettoHours(workingHoursForm).formattedHours);
          });

        this.subs$ = [
          ...this.subs$,
          shift1StartSub$,
          shift1StopSub$,
          shift1PauseSub$,
          shift2StartSub$,
          shift2StopSub$,
          shift2PauseSub$,
          flexHoursSub$,
          planHoursSub$,
          planTextSub$
        ];
      }
    }
  }

  openEditCommentOfficeModal(row: FormGroup) {
    this.dialog.open(WorkingHoursCommentOfficeUpdateModalComponent, {...dialogConfigHelper(this.overlay, row)});
  }

  calculateNettoHours(workingHoursForm: AbstractControl): { formattedHours: number; rawMinutes: number } {
    const shift1Start = workingHoursForm.get('shift1Start').value;
    const shift1Stop = workingHoursForm.get('shift1Stop').value;
    const shift1Pause = workingHoursForm.get('shift1Pause').value;
    const shift2Start = workingHoursForm.get('shift2Start').value;
    const shift2Stop = workingHoursForm.get('shift2Stop').value;
    const shift2Pause = workingHoursForm.get('shift2Pause').value;

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

  calculateFlexHours(workingHoursForm: AbstractControl) {
    const nettoHours = workingHoursForm.get('nettoHours').value;
    const planHours = workingHoursForm.get('planHours').value;
    return +(nettoHours - planHours).toFixed(2);
  }

  ngOnDestroy(): void {
    for (const sub$ of this.subs$) {
      sub$.unsubscribe();
    }
  }
}
