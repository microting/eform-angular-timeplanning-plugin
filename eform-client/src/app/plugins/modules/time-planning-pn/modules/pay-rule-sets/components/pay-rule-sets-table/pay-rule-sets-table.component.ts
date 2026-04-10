import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MtxGridColumn } from '@ng-matero/extensions/grid';
import { TranslateService } from '@ngx-translate/core';
import { PayRuleSetSimpleModel } from '../../../../models';

@Component({
  selector: 'app-pay-rule-sets-table',
  templateUrl: './pay-rule-sets-table.component.html',
  styleUrls: ['./pay-rule-sets-table.component.scss'],
  standalone: false
})
export class PayRuleSetsTableComponent implements OnInit {
  private dialog = inject(MatDialog);
  private translateService = inject(TranslateService);

  @Input() payRuleSets: PayRuleSetSimpleModel[] = [];
  @Input() loading = false;
  @Output() createClicked = new EventEmitter<void>();
  @Output() editClicked = new EventEmitter<PayRuleSetSimpleModel>();
  @Output() deleteClicked = new EventEmitter<PayRuleSetSimpleModel>();

  tableHeaders: MtxGridColumn[] = [];

  ngOnInit(): void {
    this.tableHeaders = [
      { header: this.translateService.instant('ID'), field: 'id', sortable: true },
      { header: this.translateService.instant('Name'), field: 'name', sortable: true },
      {
        header: this.translateService.instant('Actions'),
        field: 'actions',
        width: '120px',
        pinned: 'right',
        type: 'button',
      },
    ];
  }

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
