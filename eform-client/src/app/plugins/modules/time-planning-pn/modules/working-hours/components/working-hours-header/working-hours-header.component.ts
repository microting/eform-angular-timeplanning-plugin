import {Component, EventEmitter, Input, OnInit, Output,
  inject
} from '@angular/core';
import {format} from 'date-fns';
import {ExcelIcon, PARSING_DATE_FORMAT} from 'src/app/common/const';
import {SiteDto} from 'src/app/common/models';
import {TimePlanningsReportAllWorkersDownloadRequestModel} from '../../../../models';
import {saveAs} from 'file-saver';
import {ToastrService} from 'ngx-toastr';
import {TimePlanningPnWorkingHoursService} from '../../../../services';
import {EMPTY, Subscription} from 'rxjs';
import {MatIconRegistry} from '@angular/material/icon';
import {DomSanitizer} from '@angular/platform-browser';
import {catchError} from 'rxjs/operators';
import {MatDatepickerInputEvent} from '@angular/material/datepicker';
import {dialogConfigHelper} from 'src/app/common/helpers';
import {MatDialog} from '@angular/material/dialog';
import {Overlay} from '@angular/cdk/overlay';
import {
  WorkingHoursUploadModalComponent
} from 'src/app/plugins/modules/time-planning-pn/modules/working-hours/components';
import {
  WorkingHourRequestModel
} from '../../../../models';

@Component({
    selector: 'app-working-hours-header',
    templateUrl: './working-hours-header.component.html',
    styleUrls: ['./working-hours-header.component.scss'],
    standalone: false
})
export class WorkingHoursHeaderComponent implements OnInit {
  private toastrService = inject(ToastrService);
  private workingHoursService = inject(TimePlanningPnWorkingHoursService);
  private iconRegistry = inject(MatIconRegistry);
  private sanitizer = inject(DomSanitizer);
  public dialog = inject(MatDialog);
  private overlay = inject(Overlay);

  @Input()
  workingHoursRequest: WorkingHourRequestModel = new WorkingHourRequestModel();
  @Input() availableSites: SiteDto[] = [];
  @Input() tainted = false;
  @Output()
  filtersChanged: EventEmitter<WorkingHourRequestModel> = new EventEmitter<WorkingHourRequestModel>();
  @Output() updateWorkingHours: EventEmitter<void> = new EventEmitter<void>();
  eformUploadZipModalComponentAfterClosedSub$: Subscription;

  siteId: number;

  dateFrom: Date = null;
  dateTo: Date = null;
  downloadReportSub$: Subscription;

  

  ngOnInit(): void {
    this.iconRegistry.addSvgIconLiteral('file-excel', this.sanitizer.bypassSecurityTrustHtml(ExcelIcon));
  }

  onSiteChanged(siteId: number) {
    this.siteId = siteId;
    this.filtersChangedEmmit();
  }

  onSaveWorkingHours() {
    this.updateWorkingHours.emit();
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

  filtersChangedEmmit(): void {
    if (this.dateFrom && this.dateTo && this.siteId !== undefined) {
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

  openEformsImportModal() {
    this.eformUploadZipModalComponentAfterClosedSub$ = this.dialog.open(WorkingHoursUploadModalComponent, {
      ...dialogConfigHelper(this.overlay), minWidth: 400,
    }).afterClosed().subscribe(data => data ? undefined : undefined);
    // this.eformsBulkImportModalAfterClosedSub$ = this.dialog.open(EformsBulkImportModalComponent, {
    //   ...dialogConfigHelper(this.overlay, this.availableTags),
    //   minWidth: 400,
    // }).afterClosed().subscribe(data => data ? this.loadAllTags() : undefined);
  }
}
