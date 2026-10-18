import { Component, OnInit, inject } from '@angular/core';
import {MAT_DIALOG_DATA, MatDialogRef, MatDialogTitle, MatDialogContent, MatDialogActions} from '@angular/material/dialog';
import { ToastrService } from 'ngx-toastr';
import { BreakPolicyModel } from '../../../../models';
import { TimePlanningPnBreakPoliciesService } from '../../../../services';
import {TranslatePipe} from '@ngx-translate/core';

@Component({
  selector: 'app-break-policies-delete-modal',
  imports: [TranslatePipe, MatDialogTitle, MatDialogContent, MatDialogActions],
  templateUrl: './break-policies-delete-modal.component.html',
  styleUrls: ['./break-policies-delete-modal.component.scss'],
})
export class BreakPoliciesDeleteModalComponent implements OnInit {
  private breakPoliciesService = inject(TimePlanningPnBreakPoliciesService);
  private toastrService = inject(ToastrService);
  public dialogRef = inject(MatDialogRef<BreakPoliciesDeleteModalComponent>);
  private model = inject<{ selectedBreakPolicy: BreakPolicyModel }>(MAT_DIALOG_DATA);

  selectedBreakPolicy: BreakPolicyModel;

  ngOnInit() {
    this.selectedBreakPolicy = { ...this.model.selectedBreakPolicy };
  }

  deleteSingle() {
    this.breakPoliciesService
      .deleteBreakPolicy(this.selectedBreakPolicy.id)
      .subscribe((result) => {
        if (result.success) {
          this.toastrService.success('Break policy deleted successfully');
          this.hide(true);
        } else {
          this.toastrService.error('Failed to delete break policy');
        }
      });
  }

  hide(result = false) {
    this.dialogRef.close(result);
  }
}
