import {Component, EventEmitter, Input, OnInit, Output} from '@angular/core';
import {format} from 'date-fns';
import {ExcelIcon, PARSING_DATE_FORMAT} from 'src/app/common/const';
import {SiteDto} from 'src/app/common/models';
import {TimePlanningsReportAllWorkersDownloadRequestModel, TimePlanningsRequestModel} from '../../../../models';
import {saveAs} from 'file-saver';
import {ToastrService} from 'ngx-toastr';
import {TimePlanningPnWorkingHoursService} from '../../../../services';
import {Subscription} from 'rxjs';
import {MatIconRegistry} from '@angular/material/icon';
import {DomSanitizer} from '@angular/platform-browser';
import {catchError} from 'rxjs/operators';
import {MatDatepickerInputEvent} from '@angular/material/datepicker';

@Component({
  selector: 'app-working-hours-header',
  templateUrl: './working-hours-header.component.html',
  styleUrls: ['./working-hours-header.component.scss'],
})
export class WorkingHoursHeaderComponent implements OnInit {
  @Input()
  workingHoursRequest: TimePlanningsRequestModel = new TimePlanningsRequestModel();
  @Input() availableSites: SiteDto[] = [];
  @Input() tainted = false;
  @Output()
  filtersChanged: EventEmitter<TimePlanningsRequestModel> = new EventEmitter<TimePlanningsRequestModel>();
  @Output() updateWorkingHours: EventEmitter<void> = new EventEmitter<void>();

  siteId: number;

  dateFrom: Date = null;
  dateTo: Date = null;
  downloadReportSub$: Subscription;

  constructor(
    private toastrService: ToastrService,
    private workingHoursService: TimePlanningPnWorkingHoursService,
    iconRegistry: MatIconRegistry,
    sanitizer: DomSanitizer,
  ) {
    iconRegistry.addSvgIconLiteral('file-excel', sanitizer.bypassSecurityTrustHtml(ExcelIcon));
  }

  ngOnInit(): void {
  }

  onSiteChanged(siteId: number) {
    this.siteId = siteId;
    this.filtersChangedEmmit();
  }

  onSaveWorkingHours() {
    this.updateWorkingHours.emit();
  }

  onDownloadExcelReport() {
    const model: TimePlanningsRequestModel = {
      dateFrom: format(this.dateFrom, 'yyyy-MM-dd'),
      dateTo: format(this.dateTo, 'yyyy-MM-dd'),
      siteId: this.siteId,
    };
    this.downloadReportSub$ = this.workingHoursService
      .downloadReport(model)
      .pipe(catchError(
        (error, caught) => {
          this.toastrService.error('Error downloading report');
          return caught;
        }))
      .subscribe(
        (data) => {
          saveAs(data, model.dateFrom + '_' + model.dateTo + '_report.xlsx');
        },
      );
  }

  filtersChangedEmmit(): void {
    if (this.dateFrom && this.dateTo && this.siteId) {
      this.filtersChanged.emit({
        siteId: this.siteId,
        dateFrom: format(this.dateFrom, PARSING_DATE_FORMAT),
        dateTo: format(this.dateTo, PARSING_DATE_FORMAT),
      });
    }
  }

  updateDateFrom(dateFrom: MatDatepickerInputEvent<any, any>) {
    this.dateFrom = dateFrom.value;
  }

  updateDateTo(dateTo: MatDatepickerInputEvent<any, any>) {
    this.dateTo = dateTo.value;
    this.filtersChangedEmmit();
  }

  onDownloadExcelReportAllWorkers() {
    const model: TimePlanningsReportAllWorkersDownloadRequestModel = {
      dateFrom: format(this.dateFrom, 'yyyy-MM-dd'),
      dateTo: format(this.dateTo, 'yyyy-MM-dd'),
    };
    this.downloadReportSub$ = this.workingHoursService
      .downloadReportAllWorkers(model)
      .pipe(catchError(
        (caught) => {
          this.toastrService.error('Error downloading report');
          return caught;
        }))
      .subscribe(
        (data) => {
          saveAs(data, model.dateFrom + '_' + model.dateTo + '_AllWorkersReport.xlsx');
        },
      );
  }
}
