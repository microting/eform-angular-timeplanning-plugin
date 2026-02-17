import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef } from '@angular/material/dialog';
import { ToastrService } from 'ngx-toastr';
import { BreakPolicyCreateModel } from '../../../../models';
import { TimePlanningPnBreakPoliciesService } from '../../../../services';

@Component({
  selector: 'app-break-policies-create-modal',
  templateUrl: './break-policies-create-modal.component.html',
  styleUrls: ['./break-policies-create-modal.component.scss'],
  standalone: false
})
export class BreakPoliciesCreateModalComponent implements OnInit {
  private breakPoliciesService = inject(TimePlanningPnBreakPoliciesService);
  private toastrService = inject(ToastrService);
  private fb = inject(FormBuilder);
  public dialogRef = inject(MatDialogRef<BreakPoliciesCreateModalComponent>);

  breakPolicyForm: FormGroup;

  ngOnInit() {
    this.initForm();
  }

  initForm() {
    this.breakPolicyForm = this.fb.group({
      name: ['', Validators.required],
    });
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
        this.hide(true);
      } else {
        this.toastrService.error('Failed to create break policy');
      }
    });
  }

  hide(result = false) {
    this.dialogRef.close(result);
  }
}
