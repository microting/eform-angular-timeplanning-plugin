import {
  Component,
  EventEmitter,
  Input,
  OnDestroy,
  OnInit,
  Output,
  inject
} from '@angular/core';
import {TimeFlexesModel} from '../../../../models';
import {
  TimeFlexesCommentOfficeAllUpdateModalComponent,
  TimeFlexesCommentOfficeUpdateModalComponent,
} from '../';
import {TranslateService} from '@ngx-translate/core';
import {MtxGridColumn} from '@ng-matero/extensions/grid';
import {MatDialog} from '@angular/material/dialog';
import {Overlay} from '@angular/cdk/overlay';
import {dialogConfigHelper} from 'src/app/common/helpers';
import {Subscription} from 'rxjs';
import {AutoUnsubscribe} from 'ngx-auto-unsubscribe';

@AutoUnsubscribe()
@Component({
    selector: 'app-time-flexes-table',
    templateUrl: './time-flexes-table.component.html',
    styleUrls: ['./time-flexes-table.component.scss'],
    standalone: false
})
export class TimeFlexesTableComponent implements OnInit, OnDestroy {
  private translateService = inject(TranslateService);
  private dialog = inject(MatDialog);
  private overlay = inject(Overlay);

  @Input() flexPlannings: TimeFlexesModel[] = [];
  @Output()
  flexPlanningChanged: EventEmitter<TimeFlexesModel> = new EventEmitter<TimeFlexesModel>();
  editCommentOfficeModal: TimeFlexesCommentOfficeUpdateModalComponent;

  tableHeaders: MtxGridColumn[] = [
    {header: this.translateService.stream('Date'), field: 'date', type: 'date', typeParameter: {format: 'dd.MM.yyyy'}},
    {
      header: this.translateService.stream('Worker'),
      field: 'worker',
      formatter: (row: TimeFlexesModel) => row.worker ? row.worker.name : '',
    },
    {header: this.translateService.stream('SumFlex'), field: 'sumFlex'},
    {header: this.translateService.stream('Comment office'), field: 'commentOffice'},
  ];

  TimeFlexesCommentOfficeUpdateModalComponentAfterClosedSub$: Subscription;
  TimeFlexesCommentOfficeAllUpdateModalComponentAfterClosedSub$: Subscription;

  

  ngOnInit(): void {
  }

  onFlexPlanningChanged(paidOutFlex: number, flexPlanning: TimeFlexesModel) {
    this.flexPlanningChanged.emit({
      ...flexPlanning,
      paidOutFlex: paidOutFlex ?? flexPlanning.paidOutFlex,
    });
  }

  onCommentOfficeClick(model: TimeFlexesModel) {
    this.TimeFlexesCommentOfficeUpdateModalComponentAfterClosedSub$ = this.dialog
      .open(TimeFlexesCommentOfficeUpdateModalComponent, {...dialogConfigHelper(this.overlay, model)})
      .afterClosed().subscribe(x => x.result ? this.onFlexPlanningChanged(null, x.model) : undefined);
  }

  onCommentOfficeAllClick(model: TimeFlexesModel) {
    this.TimeFlexesCommentOfficeAllUpdateModalComponentAfterClosedSub$ = this.dialog
      .open(TimeFlexesCommentOfficeAllUpdateModalComponent, {...dialogConfigHelper(this.overlay, model)})
      .afterClosed().subscribe(x => x.result ? this.onFlexPlanningChanged(null, x.model) : undefined);
  }

  ngOnDestroy(): void {
  }
}
