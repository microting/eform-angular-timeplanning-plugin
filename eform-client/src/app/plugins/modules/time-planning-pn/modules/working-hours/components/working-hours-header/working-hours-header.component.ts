import {Component, EventEmitter, Input, OnInit, Output} from '@angular/core';
import {format} from 'date-fns';
import {ExcelIcon, PARSING_DATE_FORMAT} from 'src/app/common/const';
import {SiteDto} from 'src/app/common/models';
import {TimePlanningsRequestModel} from '../../../../models';
import {saveAs} from 'file-saver';
import {ToastrService} from 'ngx-toastr';
import {TimePlanningPnWorkingHoursService} from '../../../../services';
import {Subscription} from 'rxjs';
import {MatIconRegistry} from '@angular/material/icon';
import {DomSanitizer} from '@angular/platform-browser';
import {catchError} from 'rxjs/operators';

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

  dateRange: any;
  siteId: number;
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

  updateDateRange(range: Date[]) {
    this.dateRange = range;
    this.filtersChangedEmmit();
  }

  onSiteChanged(siteId: number) {
    this.siteId = siteId;
    this.filtersChangedEmmit();
  }

  onSaveWorkingHours() {
    this.updateWorkingHours.emit();
  }

  onDownloadExcelReport() {
    const model = new TimePlanningsRequestModel();
    //model.dateFrom = format(this.dateRange[0]._d, PARSING_DATE_FORMAT);
    model.dateFrom = this.dateRange[0];
    //model.dateTo = format(this.dateRange[1]._d, PARSING_DATE_FORMAT);
    model.dateTo = this.dateRange[1];

    // @ts-ignore
    model.dateFrom = format(model.dateFrom, 'yyyy-MM-dd');
    // @ts-ignore
    model.dateTo = format(model.dateTo, 'yyyy-MM-dd');
    model.siteId = this.siteId;
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
    if (this.dateRange && this.siteId) {
      this.filtersChanged.emit({
        siteId: this.siteId,
        dateFrom: this.dateRange[0],
        //dateFrom: format(this.dateRange[0]._d, PARSING_DATE_FORMAT),
        dateTo: this.dateRange[1],
        //dateTo: format(this.dateRange[1]._d, PARSING_DATE_FORMAT),
      });
    }
  }
}
