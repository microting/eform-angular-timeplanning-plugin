import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import { Subscription } from 'rxjs';
import {
  BreakPolicySimpleModel,
  BreakPoliciesRequestModel,
} from '../../../../models';
import { TimePlanningPnBreakPoliciesService } from '../../../../services';
import { BreakPoliciesCreateModalComponent } from '../break-policies-create-modal/break-policies-create-modal.component';
import { BreakPoliciesEditModalComponent } from '../break-policies-edit-modal/break-policies-edit-modal.component';
import { BreakPoliciesDeleteModalComponent } from '../break-policies-delete-modal/break-policies-delete-modal.component';

@AutoUnsubscribe()
@Component({
  selector: 'app-break-policies-container',
  templateUrl: './break-policies-container.component.html',
  styleUrls: ['./break-policies-container.component.scss'],
  standalone: false
})
export class BreakPoliciesContainerComponent implements OnInit, OnDestroy {
  private breakPoliciesService = inject(TimePlanningPnBreakPoliciesService);
  private dialog = inject(MatDialog);

  breakPoliciesRequest: BreakPoliciesRequestModel = {
    offset: 0,
    pageSize: 10,
  };
  breakPolicies: BreakPolicySimpleModel[] = [];
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

  onCreateClicked() {
    const dialogRef = this.dialog.open(BreakPoliciesCreateModalComponent, {
      width: '600px',
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.onBreakPolicyCreated();
      }
    });
  }

  onEditClicked(breakPolicy: BreakPolicySimpleModel) {
    // First fetch the full break policy details
    this.breakPoliciesService.getBreakPolicy(breakPolicy.id).subscribe((data) => {
      if (data && data.success) {
        const dialogRef = this.dialog.open(BreakPoliciesEditModalComponent, {
          width: '600px',
          data: { selectedBreakPolicy: data.model },
        });

        dialogRef.afterClosed().subscribe((result) => {
          if (result) {
            this.onBreakPolicyUpdated();
          }
        });
      }
    });
  }

  onDeleteClicked(breakPolicy: BreakPolicySimpleModel) {
    // First fetch the full break policy details
    this.breakPoliciesService.getBreakPolicy(breakPolicy.id).subscribe((data) => {
      if (data && data.success) {
        const dialogRef = this.dialog.open(BreakPoliciesDeleteModalComponent, {
          width: '400px',
          data: { selectedBreakPolicy: data.model },
        });

        dialogRef.afterClosed().subscribe((result) => {
          if (result) {
            this.onBreakPolicyDeleted();
          }
        });
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

