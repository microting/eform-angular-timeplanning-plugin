import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MtxGridColumn } from '@ng-matero/extensions/grid';
import { PayRuleSetSimpleModel } from '../../../../models';

@Component({
  selector: 'app-pay-rule-sets-table',
  templateUrl: './pay-rule-sets-table.component.html',
  styleUrls: ['./pay-rule-sets-table.component.scss'],
  standalone: false
})
export class PayRuleSetsTableComponent {
  private dialog = inject(MatDialog);

  @Input() payRuleSets: PayRuleSetSimpleModel[] = [];
  @Output() createClicked = new EventEmitter<void>();
  @Output() editClicked = new EventEmitter<PayRuleSetSimpleModel>();
  @Output() deleteClicked = new EventEmitter<PayRuleSetSimpleModel>();

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

  openEditModal(payRuleSet: PayRuleSetSimpleModel) {
    this.editClicked.emit(payRuleSet);
  }

  openDeleteModal(payRuleSet: PayRuleSetSimpleModel) {
    this.deleteClicked.emit(payRuleSet);
  }
}
