import {Component, OnDestroy, OnInit,
  inject
} from '@angular/core';
import {EMPTY, Subscription} from 'rxjs';
import {MatDatepickerInputEvent} from '@angular/material/datepicker';
import {SiteDto} from 'src/app/common/models';
import {
  AssignedSiteModel,
  TimePlanningsReportAllWorkersDownloadRequestModel,
  WorkingHourRequestModel
} from 'src/app/plugins/modules/time-planning-pn/models';
import {format} from 'date-fns';
import {catchError} from 'rxjs/operators';
import {saveAs} from 'file-saver';
import {TimePlanningPnWorkingHoursService} from 'src/app/plugins/modules/time-planning-pn/services';
import {ToastrService} from 'ngx-toastr';
import {MAT_DIALOG_DATA} from '@angular/material/dialog';

@Component({
  selector: 'app-download-excel-dialog',
  templateUrl: './download-excel-dialog.component.html',
  styleUrls: ['./download-excel-dialog.component.scss'],
  standalone: false,
})
export class DownloadExcelDialogComponent implements OnInit, OnDestroy {
  public availableSites = inject<SiteDto[]>(MAT_DIALOG_DATA);
  private toastrService = inject(ToastrService);
  private workingHoursService = inject(TimePlanningPnWorkingHoursService);

  siteId: number;

  dateFrom: Date = null;
  dateTo: Date = null;
  downloadReportSub$: Subscription;

  

  ngOnInit(): void {
    // this.getAvailableSites();
    // this.store.dispatch(new PlanningActions.GetPlanning(this.planningId));
  }

  ngOnDestroy(): void {
    // this.store.dispatch(new PlanningActions.ResetPlanning());
  }

  onSiteChanged(siteId: number) {
    this.siteId = siteId;
  }

  updateDateFrom(dateFrom: MatDatepickerInputEvent<any, any>) {
    this.dateFrom = dateFrom.value;
  }

  updateDateTo(dateTo: MatDatepickerInputEvent<any, any>) {
    this.dateTo = dateTo.value;
  }

  onDownloadExcelReport() {
    const model: WorkingHourRequestModel = {
      dateFrom: format(this.dateFrom, 'yyyy-MM-dd'),
      dateTo: format(this.dateTo, 'yyyy-MM-dd'),
      siteId: this.siteId,
    };
    this.downloadReportSub$ = this.workingHoursService
      .downloadReport(model)
      .pipe(catchError(
        (error) => {
          this.toastrService.error('Error downloading report');
          return EMPTY;
        }))
      .subscribe(
        (data) => {
          saveAs(data, model.dateFrom + '_' + model.dateTo + '_report.xlsx');
        },
      );
  }

  onDownloadExcelReportAllWorkers() {
    const model: TimePlanningsReportAllWorkersDownloadRequestModel = {
      dateFrom: format(this.dateFrom, 'yyyy-MM-dd'),
      dateTo: format(this.dateTo, 'yyyy-MM-dd'),
    };
    this.downloadReportSub$ = this.workingHoursService
      .downloadReportAllWorkers(model)
      .pipe(catchError(
        (error) => {
          this.toastrService.error('Error downloading report');
          return EMPTY;
        }))
      .subscribe(
        (data) => {
          saveAs(data, model.dateFrom + '_' + model.dateTo + '_AllWorkersReport.xlsx');
        },
      );
  }

  onCancel() {

  }
}
