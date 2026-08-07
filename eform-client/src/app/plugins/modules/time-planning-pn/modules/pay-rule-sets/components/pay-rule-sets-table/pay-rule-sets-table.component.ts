import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MtxGridColumn } from '@ng-matero/extensions/grid';
import { TranslateService } from '@ngx-translate/core';
import { Store } from '@ngrx/store';
import { selectCurrentUserIsAdmin } from 'src/app/state';
import { PayRuleSetSimpleModel } from '../../../../models';
import { isLockedPresetName } from '../../pay-rule-lock.util';

@Component({
  selector: 'app-pay-rule-sets-table',
  templateUrl: './pay-rule-sets-table.component.html',
  styleUrls: ['./pay-rule-sets-table.component.scss'],
  standalone: false
})
export class PayRuleSetsTableComponent implements OnInit {
  private dialog = inject(MatDialog);
  private translateService = inject(TranslateService);
  private store = inject(Store);

  // The list endpoint is open to any authenticated user, but create/update/delete
  // are still admin-only on the server and a 403 is escalated into a forced
  // logout by the global HttpErrorInterceptor. Hide the mutating actions rather
  // than let a non-admin trigger them.
  public selectCurrentUserIsAdmin$ = this.store.select(selectCurrentUserIsAdmin);

  @Input() payRuleSets: PayRuleSetSimpleModel[] = [];
  @Input() loading = false;
  @Output() createClicked = new EventEmitter<void>();
  @Output() editClicked = new EventEmitter<PayRuleSetSimpleModel>();
  @Output() deleteClicked = new EventEmitter<PayRuleSetSimpleModel>();
  @Output() viewClicked = new EventEmitter<PayRuleSetSimpleModel>();

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
   *
   * The comparison ignores the trailing validity period so rows stored
   * under an earlier agreement period stay locked after a catalogue rename.
   */
  isLockedPreset(row: PayRuleSetSimpleModel): boolean {
    return isLockedPresetName(row.name);
  }

  openCreateModal() {
    this.createClicked.emit();
  }

  openEditModal(payRuleSet: PayRuleSetSimpleModel) {
    this.editClicked.emit(payRuleSet);
  }

  openViewModal(payRuleSet: PayRuleSetSimpleModel) {
    this.viewClicked.emit(payRuleSet);
  }

  openDeleteModal(payRuleSet: PayRuleSetSimpleModel) {
    this.deleteClicked.emit(payRuleSet);
  }
}
