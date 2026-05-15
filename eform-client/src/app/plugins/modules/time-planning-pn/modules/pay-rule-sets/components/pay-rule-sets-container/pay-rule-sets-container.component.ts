import {Component, OnDestroy, OnInit} from '@angular/core';
import {AutoUnsubscribe} from 'ngx-auto-unsubscribe';
import {PayRuleSetSimpleModel, PayRuleSetsRequestModel, PAY_RULE_SET_PRESETS} from '../../../../models';
import {MatDialog} from '@angular/material/dialog';
import {PayRuleSetsDeleteModalComponent} from '../pay-rule-sets-delete-modal/pay-rule-sets-delete-modal.component';
import {PayRuleSetsCreateModalComponent} from '../pay-rule-sets-create-modal/pay-rule-sets-create-modal.component';
import {PayRuleSetsEditModalComponent} from '../pay-rule-sets-edit-modal/pay-rule-sets-edit-modal.component';
import {TimePlanningPnPayRuleSetsService} from '../../../../services';
import {Subscription} from 'rxjs';
import {ToastrService} from 'ngx-toastr';
import {TranslateService} from '@ngx-translate/core';

@AutoUnsubscribe()
@Component({
  selector: 'app-pay-rule-sets-container',
  templateUrl: './pay-rule-sets-container.component.html',
  styleUrls: ['./pay-rule-sets-container.component.scss'],
  standalone: false,
})
export class PayRuleSetsContainerComponent implements OnInit, OnDestroy {
  payRuleSets: PayRuleSetSimpleModel[] = [];
  totalPayRuleSets = 0;
  loading = false;

  payRuleSetsRequest: PayRuleSetsRequestModel = {
    offset: 0,
    pageSize: 10,
  };

  getPayRuleSets$: Subscription;

  constructor(
    private dialog: MatDialog,
    private payRuleSetsService: TimePlanningPnPayRuleSetsService,
    private toastrService: ToastrService,
    private translateService: TranslateService
  ) {}

  ngOnInit(): void {
    this.getPayRuleSets();
  }

  getPayRuleSets(): void {
    this.loading = true;
    this.getPayRuleSets$ = this.payRuleSetsService
      .getPayRuleSets(this.payRuleSetsRequest)
      .subscribe((data) => {
        if (data && data.success) {
          this.payRuleSets = data.model.payRuleSets;
          this.totalPayRuleSets = data.model.total;
        }
        this.loading = false;
      });
  }

  onCreateClicked(): void {
    // Fetch all pay rule set names (not just the current page) for singleton filtering
    this.payRuleSetsService
      .getPayRuleSets({ offset: 0, pageSize: 10000 })
      .subscribe((allData) => {
        const allNames = allData && allData.success
          ? allData.model.payRuleSets.map(p => p.name)
          : this.payRuleSets.map(p => p.name);

        const dialogRef = this.dialog.open(PayRuleSetsCreateModalComponent, {
          minWidth: 1280,
          maxWidth: 1440,
          data: { existingNames: allNames },
        });

        dialogRef.afterClosed().subscribe((result) => {
          if (result) {
            // Refresh the table after successful create
            this.getPayRuleSets();
          }
        });
      });
  }

  onEditClicked(payRuleSet: PayRuleSetSimpleModel): void {
    // Locked presets (e.g. GLS-A / 3F overenskomster) are read-only. The
    // edit modal still opens but renders a summary view; this guard is a
    // belt-and-braces against direct calls (the table button is also
    // disabled via isLockedPreset).
    const isLockedPreset = PAY_RULE_SET_PRESETS.some(p => p.locked && p.name === payRuleSet.name);

    const dialogRef = this.dialog.open(PayRuleSetsEditModalComponent, {
      data: { payRuleSetId: payRuleSet.id },
      minWidth: 1280,
      maxWidth: 1440,
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result && !isLockedPreset) {
        // Refresh the table after successful edit (skip for locked: read-only)
        this.getPayRuleSets();
      }
    });
  }

  onDeleteClicked(payRuleSet: PayRuleSetSimpleModel): void {
    const isLockedPreset = PAY_RULE_SET_PRESETS.some(p => p.locked && p.name === payRuleSet.name);
    if (isLockedPreset) {
      this.toastrService.error(this.translateService.instant('Cannot delete locked preset'));
      return;
    }

    const dialogRef = this.dialog.open(PayRuleSetsDeleteModalComponent, {
      data: { selectedPayRuleSet: payRuleSet },
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        // Refresh the table after successful delete
        this.getPayRuleSets();
      }
    });
  }

  ngOnDestroy(): void {
    // AutoUnsubscribe handles cleanup
  }
}
