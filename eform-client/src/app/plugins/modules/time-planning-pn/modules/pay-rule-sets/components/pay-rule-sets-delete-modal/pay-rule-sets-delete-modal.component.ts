import { Component, OnInit, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { ToastrService } from 'ngx-toastr';
import { TranslateService } from '@ngx-translate/core';
import { PayRuleSetModel } from '../../../../models';
import { TimePlanningPnPayRuleSetsService } from '../../../../services';

@Component({
  selector: 'app-pay-rule-sets-delete-modal',
  templateUrl: './pay-rule-sets-delete-modal.component.html',
  styleUrls: ['./pay-rule-sets-delete-modal.component.scss'],
  standalone: false
})
export class PayRuleSetsDeleteModalComponent implements OnInit {
  private payRuleSetsService = inject(TimePlanningPnPayRuleSetsService);
  private toastrService = inject(ToastrService);
  private translateService = inject(TranslateService);
  public dialogRef = inject(MatDialogRef<PayRuleSetsDeleteModalComponent>);
  private model = inject<{ selectedPayRuleSet: PayRuleSetModel }>(MAT_DIALOG_DATA);

  selectedPayRuleSet: PayRuleSetModel;

  ngOnInit() {
    this.selectedPayRuleSet = { ...this.model.selectedPayRuleSet };
  }

  deleteSingle() {
    this.payRuleSetsService
      .deletePayRuleSet(this.selectedPayRuleSet.id)
      .subscribe((result) => {
        if (result.success) {
          this.toastrService.success(this.translateService.instant('Pay rule set deleted successfully'));
          this.hide(true);
        } else {
          this.toastrService.error(this.translateService.instant('Failed to delete pay rule set'));
        }
      });
  }

  hide(result = false) {
    this.dialogRef.close(result);
  }
}
