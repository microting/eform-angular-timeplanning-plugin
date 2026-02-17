import {Component, OnDestroy, OnInit} from '@angular/core';
import {AutoUnsubscribe} from 'ngx-auto-unsubscribe';

@AutoUnsubscribe()
@Component({
  selector: 'app-pay-rule-sets-container',
  templateUrl: './pay-rule-sets-container.component.html',
  styleUrls: ['./pay-rule-sets-container.component.scss'],
  standalone: false,
})
export class PayRuleSetsContainerComponent implements OnInit, OnDestroy {

  constructor() {}

  ngOnInit(): void {
    // TODO: Initialize component
  }

  ngOnDestroy(): void {
    // AutoUnsubscribe handles cleanup
  }
}
