import { Component, Inject, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { ToastrService } from 'ngx-toastr';
import {
  BreakPolicyModel,
  BreakPolicyCreateModel,
  BreakPolicyUpdateModel,
} from '../../../../models';
import { TimePlanningPnBreakPoliciesService } from '../../../../services';

@Component({
  selector: 'app-break-policies-actions',
  templateUrl: './break-policies-actions.component.html',
  styleUrls: ['./break-policies-actions.component.scss'],
  standalone: false
})
export class BreakPoliciesActionsComponent implements OnInit {
  private breakPoliciesService = inject(TimePlanningPnBreakPoliciesService);
  private toastrService = inject(ToastrService);
  private fb = inject(FormBuilder);

  breakPolicyForm: FormGroup;
  mode: 'create' | 'edit' | 'delete';
  breakPolicy: BreakPolicyModel;

  constructor(
    public dialogRef: MatDialogRef<BreakPoliciesActionsComponent>,
    @Inject(MAT_DIALOG_DATA) public data: any
  ) {
    this.mode = data.mode;
    this.breakPolicy = data.breakPolicy;
  }

  ngOnInit() {
    if (this.mode !== 'delete') {
      this.initForm();
    }
  }

  initForm() {
    this.breakPolicyForm = this.fb.group({
      name: [this.breakPolicy?.name || '', Validators.required],
    });
  }

  onSubmit() {
    if (this.mode === 'create') {
      this.createBreakPolicy();
    } else if (this.mode === 'edit') {
      this.updateBreakPolicy();
    } else if (this.mode === 'delete') {
      this.deleteBreakPolicy();
    }
  }

  createBreakPolicy() {
    if (this.breakPolicyForm.invalid) return;

    const model: BreakPolicyCreateModel = {
      name: this.breakPolicyForm.value.name,
      rules: [],
    };

    this.breakPoliciesService.createBreakPolicy(model).subscribe((result) => {
      if (result.success) {
        this.toastrService.success('Break policy created successfully');
        this.dialogRef.close(true);
      } else {
        this.toastrService.error('Failed to create break policy');
      }
    });
  }

  updateBreakPolicy() {
    if (this.breakPolicyForm.invalid) return;

    const model: BreakPolicyUpdateModel = {
      id: this.breakPolicy.id,
      name: this.breakPolicyForm.value.name,
      rules: this.breakPolicy.rules || [],
    };

    this.breakPoliciesService.updateBreakPolicy(model).subscribe((result) => {
      if (result.success) {
        this.toastrService.success('Break policy updated successfully');
        this.dialogRef.close(true);
      } else {
        this.toastrService.error('Failed to update break policy');
      }
    });
  }

  deleteBreakPolicy() {
    this.breakPoliciesService
      .deleteBreakPolicy(this.breakPolicy.id)
      .subscribe((result) => {
        if (result.success) {
          this.toastrService.success('Break policy deleted successfully');
          this.dialogRef.close(true);
        } else {
          this.toastrService.error('Failed to delete break policy');
        }
      });
  }

  onCancel() {
    this.dialogRef.close();
  }
}
