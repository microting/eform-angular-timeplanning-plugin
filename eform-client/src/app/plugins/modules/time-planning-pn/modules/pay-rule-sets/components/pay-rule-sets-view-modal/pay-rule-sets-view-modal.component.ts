import { Component, OnInit, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ToastrService } from 'ngx-toastr';
import { TimePlanningPnPayRuleSetsService } from '../../../../services';
import { PayRuleSetModel } from '../../../../models';
import { formatTierChain, formatTimeBands } from '../../pay-rule-format.util';

export interface PayRuleSetsViewModalData {
  payRuleSetId: number;
}

@Component({
  selector: 'app-pay-rule-sets-view-modal',
  standalone: true,
  imports: [CommonModule, MatDialogModule, TranslateModule],
  templateUrl: './pay-rule-sets-view-modal.component.html',
  styleUrls: ['./pay-rule-sets-view-modal.component.scss']
})
export class PayRuleSetsViewModalComponent implements OnInit {
  payRuleSet: PayRuleSetModel | null = null;
  loading = true;

  constructor(
    private payRuleSetsService: TimePlanningPnPayRuleSetsService,
    private toastrService: ToastrService,
    private translateService: TranslateService,
    public dialogRef: MatDialogRef<PayRuleSetsViewModalComponent>,
    @Inject(MAT_DIALOG_DATA) public data: PayRuleSetsViewModalData
  ) {}

  ngOnInit(): void {
    this.payRuleSetsService.getPayRuleSet(this.data.payRuleSetId).subscribe({
      next: (result) => {
        if (result && result.success && result.model) {
          this.payRuleSet = result.model;
          this.loading = false;
        } else {
          this.failAndClose();
        }
      },
      error: () => this.failAndClose()
    });
  }

  private failAndClose(): void {
    this.toastrService.error(this.translateService.instant('Failed to load pay rule set'));
    this.dialogRef.close();
  }

  formatTierChain = formatTierChain;

  formatDayTypeRule(rule: { defaultPayCode: string; timeBandRules: Array<{ startSecondOfDay: number; endSecondOfDay: number; payCode: string }> }): string {
    const bands = formatTimeBands(rule.timeBandRules || []);
    return [rule.defaultPayCode, bands].filter(part => !!part).join(' | ');
  }
}
