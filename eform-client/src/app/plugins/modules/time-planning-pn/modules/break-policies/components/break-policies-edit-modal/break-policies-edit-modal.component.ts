import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { ToastrService } from 'ngx-toastr';
import { BreakPolicyModel, BreakPolicyUpdateModel } from '../../../../models';
import { TimePlanningPnBreakPoliciesService } from '../../../../services';

@Component({
  selector: 'app-break-policies-edit-modal',
  templateUrl: './break-policies-edit-modal.component.html',
  styleUrls: ['./break-policies-edit-modal.component.scss'],
  standalone: false
})
export class BreakPoliciesEditModalComponent implements OnInit {
  private breakPoliciesService = inject(TimePlanningPnBreakPoliciesService);
  private toastrService = inject(ToastrService);
  private fb = inject(FormBuilder);
  public dialogRef = inject(MatDialogRef<BreakPoliciesEditModalComponent>);
  private model = inject<{ selectedBreakPolicy: BreakPolicyModel }>(MAT_DIALOG_DATA);

  breakPolicyForm: FormGroup;
  selectedBreakPolicy: BreakPolicyModel;

  ngOnInit() {
    this.selectedBreakPolicy = { ...this.model.selectedBreakPolicy };
    this.initForm();
  }

  initForm() {
    this.breakPolicyForm = this.fb.group({
      name: [this.selectedBreakPolicy.name || '', Validators.required],
    });
  }

  updateBreakPolicy() {
    if (this.breakPolicyForm.invalid) return;

    const model: BreakPolicyUpdateModel = {
      id: this.selectedBreakPolicy.id,
      name: this.breakPolicyForm.value.name,
      rules: this.selectedBreakPolicy.rules || [],
    };

    this.breakPoliciesService.updateBreakPolicy(model).subscribe((result) => {
      if (result.success) {
        this.toastrService.success('Break policy updated successfully');
        this.hide(true);
      } else {
        this.toastrService.error('Failed to update break policy');
      }
    });
  }

  hide(result = false) {
    this.dialogRef.close(result);
  }
}
