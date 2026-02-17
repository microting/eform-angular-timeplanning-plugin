import {Component, OnDestroy, OnInit} from '@angular/core';
import {AutoUnsubscribe} from 'ngx-auto-unsubscribe';
import {PayRuleSetSimpleModel} from '../../models';

@AutoUnsubscribe()
@Component({
  selector: 'app-pay-rule-sets-container',
  templateUrl: './pay-rule-sets-container.component.html',
  styleUrls: ['./pay-rule-sets-container.component.scss'],
  standalone: false,
})
export class PayRuleSetsContainerComponent implements OnInit, OnDestroy {
  payRuleSets: PayRuleSetSimpleModel[] = [];

  constructor() {}

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
    // TODO: Open delete modal
    console.log('Delete clicked', payRuleSet);
  }

  ngOnDestroy(): void {
    // AutoUnsubscribe handles cleanup
  }
}
