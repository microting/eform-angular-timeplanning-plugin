import {Component, OnDestroy, OnInit} from '@angular/core';
import {AutoUnsubscribe} from 'ngx-auto-unsubscribe';
import {PayRuleSetSimpleModel} from '../../../../models';
import {MatDialog} from '@angular/material/dialog';
import {PayRuleSetsDeleteModalComponent} from '../pay-rule-sets-delete-modal/pay-rule-sets-delete-modal.component';

@AutoUnsubscribe()
@Component({
  selector: 'app-pay-rule-sets-container',
  templateUrl: './pay-rule-sets-container.component.html',
  styleUrls: ['./pay-rule-sets-container.component.scss'],
  standalone: false,
})
export class PayRuleSetsContainerComponent implements OnInit, OnDestroy {
  payRuleSets: PayRuleSetSimpleModel[] = [];

  constructor(private dialog: MatDialog) {}

  ngOnInit(): void {
    // TODO: Load pay rule sets from service
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
        // TODO: Reload data from service
        console.log('Pay rule set deleted successfully');
      }
    });
  }

  ngOnDestroy(): void {
    // AutoUnsubscribe handles cleanup
  }
}
