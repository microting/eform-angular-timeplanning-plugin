import {
  Component,
  EventEmitter,
  Input,
  OnInit,
  Output,
  ViewChild,
} from '@angular/core';
import { TableHeaderElementModel } from 'src/app/common/models';
import { TimeFlexesModel, TimeFlexesUpdateModel } from '../../../models';
import { TimeFlexesCommentOfficeUpdateModalComponent } from '../';

@Component({
  selector: 'app-time-flexes-table',
  templateUrl: './time-flexes-table.component.html',
  styleUrls: ['./time-flexes-table.component.scss'],
})
export class TimeFlexesTableComponent implements OnInit {
  @Input() flexPlannings: TimeFlexesModel[] = [];
  @Output()
  flexPlanningChanged: EventEmitter<TimeFlexesModel> = new EventEmitter<TimeFlexesModel>();
  @ViewChild('editCommentOffice', { static: false })
  editCommentOfficeModal: TimeFlexesCommentOfficeUpdateModalComponent;

  tableHeaders: TableHeaderElementModel[] = [
    { name: 'Date', sortable: false },
    { name: 'Worker', sortable: false },
    { name: 'Sum flex', sortable: false },
    { name: 'Paid out flex', sortable: false },
    { name: 'Comment worker', sortable: false },
    { name: 'Comment office', sortable: false },
    { name: 'Comment office all', sortable: false },
  ];

  constructor() {}

  ngOnInit(): void {}

  onFlexPlanningChanged(paidOutFlex: number, flexPlanning: TimeFlexesModel) {
    this.flexPlanningChanged.emit({
      ...flexPlanning,
      paidOutFlex: paidOutFlex ?? flexPlanning.paidOutFlex,
    });
  }

  onOpenEditCommentOffice(model: TimeFlexesModel) {
    this.editCommentOfficeModal.show(model);
  }
}
