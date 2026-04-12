import { Component, Inject, OnInit, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TimePlanningPnPayrollExportService } from '../../../../services';
import { ToastrService } from 'ngx-toastr';
import { saveAs } from 'file-saver';
import { format } from 'date-fns';

export interface PayrollExportDialogData {
  cutoffDay: number;
  payrollSystem: number;
}

@Component({
  selector: 'app-payroll-export-dialog',
  templateUrl: './payroll-export-dialog.component.html',
  standalone: false,
})
export class PayrollExportDialogComponent implements OnInit {
  private payrollExportService = inject(TimePlanningPnPayrollExportService);
  private toastrService = inject(ToastrService);

  periodStart: Date;
  periodEnd: Date;
  preview: any = null;
  loading = false;
  exporting = false;
  alreadyExported = false;
  alreadyExportedDate: string = '';

  constructor(
    public dialogRef: MatDialogRef<PayrollExportDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: PayrollExportDialogData
  ) {}

  ngOnInit(): void {
    this.calculateDefaultPeriod();
    this.loadPreview();
  }

  private calculateDefaultPeriod(): void {
    const now = new Date();
    const cutoff = this.data.cutoffDay || 19;
    const currentMonth = now.getMonth();
    const currentYear = now.getFullYear();

    // Current cutoff date
    const currentCutoff = new Date(currentYear, currentMonth, cutoff);

    if (now > currentCutoff) {
      // We're past the cutoff this month: period is cutoff+1 of this month to cutoff of next month
      this.periodStart = new Date(currentYear, currentMonth, cutoff + 1);
      const nextMonth = currentMonth + 1;
      this.periodEnd = new Date(currentYear, nextMonth, cutoff);
    } else {
      // We're before the cutoff: period is cutoff+1 of last month to cutoff of this month
      const prevMonth = currentMonth - 1;
      this.periodStart = new Date(currentYear, prevMonth, cutoff + 1);
      this.periodEnd = new Date(currentYear, currentMonth, cutoff);
    }
  }

  loadPreview(): void {
    this.loading = true;
    const start = format(this.periodStart, 'yyyy-MM-dd');
    const end = format(this.periodEnd, 'yyyy-MM-dd');
    this.payrollExportService.preview(start, end).subscribe({
      next: (data) => {
        this.loading = false;
        if (data && data.success && data.model) {
          this.preview = data.model;
          this.alreadyExported = !!data.model.alreadyExportedAt;
          this.alreadyExportedDate = data.model.alreadyExportedAt || '';
        }
      },
      error: () => {
        this.loading = false;
      }
    });
  }

  onExport(): void {
    this.exporting = true;
    const start = format(this.periodStart, 'yyyy-MM-dd');
    const end = format(this.periodEnd, 'yyyy-MM-dd');
    this.payrollExportService.exportPayroll(start, end).subscribe({
      next: (response) => {
        this.exporting = false;
        if (response.body) {
          const contentDisposition = response.headers.get('content-disposition');
          let filename = `payroll_${start}_${end}.csv`;
          if (contentDisposition) {
            const match = contentDisposition.match(/filename="?([^";\s]+)"?/);
            if (match) {
              filename = match[1];
            }
          }
          saveAs(response.body, filename);
          this.toastrService.success('Payroll export completed');
          this.dialogRef.close(true);
        }
      },
      error: () => {
        this.exporting = false;
        this.toastrService.error('Error exporting payroll');
      }
    });
  }

  onCancel(): void {
    this.dialogRef.close();
  }
}
