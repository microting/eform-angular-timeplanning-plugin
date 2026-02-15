import { Component, EventEmitter, Input, Output, ViewChild, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MtxGridColumn } from '@ng-matero/extensions/grid';
import { BreakPolicyModel } from '../../../../models';
import { BreakPoliciesActionsComponent } from '../break-policies-actions/break-policies-actions.component';

@Component({
  selector: 'app-break-policies-table',
  templateUrl: './break-policies-table.component.html',
  styleUrls: ['./break-policies-table.component.scss'],
  standalone: false
})
export class BreakPoliciesTableComponent {
  private dialog = inject(MatDialog);

  @Input() breakPolicies: BreakPolicyModel[] = [];
  @Input() totalBreakPolicies = 0;
  @Output() pageChanged = new EventEmitter<number>();
  @Output() breakPolicyCreated = new EventEmitter<void>();
  @Output() breakPolicyUpdated = new EventEmitter<void>();
  @Output() breakPolicyDeleted = new EventEmitter<void>();

  columns: MtxGridColumn[] = [
    { header: 'ID', field: 'id', sortable: true },
    { header: 'Name', field: 'name', sortable: true },
    {
      header: 'Actions',
      field: 'actions',
      type: 'button',
      buttons: [
        {
          type: 'icon',
          icon: 'edit',
          tooltip: 'Edit',
          click: (record: BreakPolicyModel) => this.openEditModal(record),
        },
        {
          type: 'icon',
          icon: 'delete',
          tooltip: 'Delete',
          color: 'warn',
          click: (record: BreakPolicyModel) => this.openDeleteModal(record),
        },
      ],
    },
  ];

  openCreateModal() {
    const dialogRef = this.dialog.open(BreakPoliciesActionsComponent, {
      width: '600px',
      data: { mode: 'create' },
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.breakPolicyCreated.emit();
      }
    });
  }

  openEditModal(breakPolicy: BreakPolicyModel) {
    const dialogRef = this.dialog.open(BreakPoliciesActionsComponent, {
      width: '600px',
      data: { mode: 'edit', breakPolicy },
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.breakPolicyUpdated.emit();
      }
    });
  }

  openDeleteModal(breakPolicy: BreakPolicyModel) {
    const dialogRef = this.dialog.open(BreakPoliciesActionsComponent, {
      width: '400px',
      data: { mode: 'delete', breakPolicy },
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.breakPolicyDeleted.emit();
      }
    });
  }

  onPaginateChange(event: any) {
    this.pageChanged.emit(event.pageIndex * event.pageSize);
  }
}
