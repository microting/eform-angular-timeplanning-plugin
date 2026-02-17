import {Component, OnDestroy, OnInit} from '@angular/core';
import {AutoUnsubscribe} from 'ngx-auto-unsubscribe';
import {PayRuleSetSimpleModel, PayRuleSetsRequestModel} from '../../../../models';
import {MatDialog} from '@angular/material/dialog';
import {PayRuleSetsDeleteModalComponent} from '../pay-rule-sets-delete-modal/pay-rule-sets-delete-modal.component';
import {TimePlanningPnPayRuleSetsService} from '../../../../services';
import {Subscription} from 'rxjs';

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
    private payRuleSetsService: TimePlanningPnPayRuleSetsService
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
    // TODO: Open create modal
    console.log('Create clicked');
  }

  onEditClicked(payRuleSet: PayRuleSetSimpleModel): void {
    // TODO: Open edit modal
    console.log('Edit clicked', payRuleSet);
  }

  onDeleteClicked(payRuleSet: PayRuleSetSimpleModel): void {
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
