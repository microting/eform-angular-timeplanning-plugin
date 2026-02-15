import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import { Subscription } from 'rxjs';
import {
  BreakPolicyModel,
  BreakPoliciesRequestModel,
} from '../../../../models';
import { TimePlanningPnBreakPoliciesService } from '../../../../services';

@AutoUnsubscribe()
@Component({
  selector: 'app-break-policies-container',
  templateUrl: './break-policies-container.component.html',
  styleUrls: ['./break-policies-container.component.scss'],
  standalone: false
})
export class BreakPoliciesContainerComponent implements OnInit, OnDestroy {
  private breakPoliciesService = inject(TimePlanningPnBreakPoliciesService);

  breakPoliciesRequest: BreakPoliciesRequestModel = {
    offset: 0,
    pageSize: 10,
  };
  breakPolicies: BreakPolicyModel[] = [];
  totalBreakPolicies = 0;

  getBreakPolicies$: Subscription;

  ngOnInit(): void {
    this.getBreakPolicies();
  }

  getBreakPolicies() {
    this.getBreakPolicies$ = this.breakPoliciesService
      .getBreakPolicies(this.breakPoliciesRequest)
      .subscribe((data) => {
        if (data && data.success) {
          this.breakPolicies = data.model.breakPolicies;
          this.totalBreakPolicies = data.model.total;
        }
      });
  }

  onPageChanged(offset: number) {
    this.breakPoliciesRequest.offset = offset;
    this.getBreakPolicies();
  }

  onBreakPolicyCreated() {
    this.breakPoliciesRequest.offset = 0;
    this.getBreakPolicies();
  }

  onBreakPolicyUpdated() {
    this.getBreakPolicies();
  }

  onBreakPolicyDeleted() {
    this.getBreakPolicies();
  }

  ngOnDestroy(): void {}
}
