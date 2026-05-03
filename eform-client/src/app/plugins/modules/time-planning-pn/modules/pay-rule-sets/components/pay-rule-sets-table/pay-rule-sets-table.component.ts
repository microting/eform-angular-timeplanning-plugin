import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MtxGridColumn } from '@ng-matero/extensions/grid';
import { TranslateService } from '@ngx-translate/core';
import { PayRuleSetSimpleModel, PAY_RULE_SET_PRESETS } from '../../../../models';

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

  /**
   * True when the row's name matches a preset entry flagged as locked
   * (e.g. GLS-A / 3F overenskomster). Locked rule sets are read-only:
   * the edit and delete row actions are disabled, and the edit modal
   * renders a summary view instead of the form.
   */
  isLockedPreset(row: PayRuleSetSimpleModel): boolean {
    return PAY_RULE_SET_PRESETS.some(p => p.locked && p.name === row.name);
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
