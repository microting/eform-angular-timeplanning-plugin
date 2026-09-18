import {Component, OnDestroy, OnInit,
  inject
} from '@angular/core';
import {EMPTY, Subscription} from 'rxjs';
import {MatDatepickerInputEvent} from '@angular/material/datepicker';
import {SiteDto} from 'src/app/common/models';
import {
  CommonTagModel,
  SiteTagsModel,
  TimePlanningsReportAllWorkersDownloadRequestModel,
  WorkingHourRequestModel
} from 'src/app/plugins/modules/time-planning-pn/models';
import {differenceInCalendarDays, format} from 'date-fns';
import {catchError, take} from 'rxjs/operators';
import {saveAs} from 'file-saver';
import {
  TimePlanningPnPlanningsService,
  TimePlanningPnWorkingHoursService
} from 'src/app/plugins/modules/time-planning-pn/services';
import {ToastrService} from 'ngx-toastr';
import {MAT_DIALOG_DATA} from '@angular/material/dialog';

/**
 * The filters the planning page has on screen when the dialog is opened. The export
 * starts from what the user is looking at; the dialog then works on its own copies, so
 * narrowing an export never moves the page behind it.
 */
export interface DownloadExcelDialogData {
  availableSites: SiteDto[];
  availableTags: CommonTagModel[];
  dateFrom: Date;
  dateTo: Date;
  selectedTagIds: number[];
  siteId: number | null;
}

@Component({
  selector: 'app-download-excel-dialog',
  templateUrl: './download-excel-dialog.component.html',
  styleUrls: ['./download-excel-dialog.component.scss'],
  standalone: false,
})
export class DownloadExcelDialogComponent implements OnInit, OnDestroy {
  private data = inject<DownloadExcelDialogData>(MAT_DIALOG_DATA);
  private toastrService = inject(ToastrService);
  private workingHoursService = inject(TimePlanningPnWorkingHoursService);
  private planningsService = inject(TimePlanningPnPlanningsService);

  availableSites: SiteDto[] = this.data.availableSites;
  availableTags: CommonTagModel[] = this.data.availableTags;

  siteId: number = this.data.siteId;
  // Copies, not the page's own array and Date instances. See DownloadExcelDialogData.
  selectedTagIds: number[] = [...this.data.selectedTagIds];
  dateFrom: Date = this.data.dateFrom ? new Date(this.data.dateFrom) : null;
  dateTo: Date = this.data.dateTo ? new Date(this.data.dateTo) : null;

  /** null hides the count line: without the tag map there is no honest number to show. */
  workerCount: number | null = null;
  dayCount = 0;

  private siteTags: SiteTagsModel[] | null = null;
  downloadReportSub$: Subscription;
  getSiteTags$: Subscription;

  ngOnInit(): void {
    this.recomputeScope();
    // Fetched once for the life of the dialog, so the count follows every tag, period or
    // worker change without a round trip. A failure only costs the count line — the
    // export itself does not depend on it.
    this.getSiteTags$ = this.planningsService.getSiteTags()
      .pipe(take(1), catchError(() => EMPTY))
      .subscribe((result) => {
        if (result && result.success) {
          this.siteTags = result.model || [];
          this.recomputeScope();
        }
      });
  }

  ngOnDestroy(): void {
    // Closing the dialog abandons both: an export still running would otherwise drop a
    // file on the user long after they left, and the tag fetch has nothing left to count.
    this.downloadReportSub$?.unsubscribe();
    this.getSiteTags$?.unsubscribe();
  }

  onSiteChanged(siteId: number) {
    this.siteId = siteId;
    this.recomputeScope();
  }

  onTagsChanged(tagIds: number[]) {
    this.selectedTagIds = tagIds || [];
    this.recomputeScope();
  }

  updateDateFrom(dateFrom: MatDatepickerInputEvent<any, any>) {
    this.dateFrom = dateFrom.value;
    this.recomputeScope();
  }

  updateDateTo(dateTo: MatDatepickerInputEvent<any, any>) {
    this.dateTo = dateTo.value;
    this.recomputeScope();
  }

  /** Keeps the "N workers · M days" line in step with the fields above it. */
  private recomputeScope(): void {
    this.dayCount = this.dateFrom && this.dateTo
      ? Math.max(differenceInCalendarDays(this.dateTo, this.dateFrom) + 1, 0)
      : 0;
    this.workerCount = this.countWorkers();
  }

  private countWorkers(): number | null {
    if (this.siteId) {
      // A single worker's report covers that one person, whatever the tags say.
      return 1;
    }
    if (!this.siteTags) {
      return null;
    }
    return this.siteTags.filter(row =>
      // Never resigned workers, whatever the page's "Show resigned" toggle says: the
      // all-workers export leaves them out, and a count that promised them would name
      // workers the workbook does not contain.
      !row.resigned
      // Any-of, the same rule the dashboard's own tag filter applies.
      && (this.selectedTagIds.length === 0
        || row.tagIds.some(tagId => this.selectedTagIds.includes(tagId)))
    ).length;
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
    if (this.selectedTagIds.length > 0) {
      model.tagIds = this.selectedTagIds;
    }
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
