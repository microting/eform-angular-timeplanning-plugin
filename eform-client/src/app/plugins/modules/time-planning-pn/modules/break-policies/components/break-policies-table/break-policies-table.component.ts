import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import {MtxGridColumn, MtxGrid} from '@ng-matero/extensions/grid';
import { BreakPolicySimpleModel } from '../../../../models';
import {TranslatePipe} from '@ngx-translate/core';
import {MatIcon} from '@angular/material/icon';
import {MatIconButton, MatButton} from '@angular/material/button';
import {MatTooltip} from '@angular/material/tooltip';
import {MatMenu, MatMenuItem, MatMenuTrigger} from '@angular/material/menu';

@Component({
  selector: 'app-break-policies-table',
  imports: [TranslatePipe, MatIcon, MatIconButton, MatButton, MtxGrid, MatTooltip, MatMenu, MatMenuItem, MatMenuTrigger],
  templateUrl: './break-policies-table.component.html',
  styleUrls: ['./break-policies-table.component.scss'],
})
export class BreakPoliciesTableComponent {
  private dialog = inject(MatDialog);

  @Input() breakPolicies: BreakPolicySimpleModel[] = [];
  @Output() createClicked = new EventEmitter<void>();
  @Output() editClicked = new EventEmitter<BreakPolicySimpleModel>();
  @Output() deleteClicked = new EventEmitter<BreakPolicySimpleModel>();

  tableHeaders: MtxGridColumn[] = [
    { header: 'ID', field: 'id', sortable: true },
    { header: 'Name', field: 'name', sortable: true },
    {
      header: 'Actions',
      field: 'actions',
      width: '120px',
      pinned: 'right',
      type: 'button',
    },
  ];

  openCreateModal() {
    this.createClicked.emit();
  }

  openEditModal(breakPolicy: BreakPolicySimpleModel) {
    this.editClicked.emit(breakPolicy);
  }

  openDeleteModal(breakPolicy: BreakPolicySimpleModel) {
    this.deleteClicked.emit(breakPolicy);
  }
}
