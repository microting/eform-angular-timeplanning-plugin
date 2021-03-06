import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { format } from 'date-fns';
import { PARSING_DATE_FORMAT } from 'src/app/common/const';
import { SiteDto } from 'src/app/common/models';
import { TimePlanningsRequestModel } from '../../../../models';
import {saveAs} from 'file-saver';
import {ToastrService} from 'ngx-toastr';
import {TimePlanningPnWorkingHoursService} from 'src/app/plugins/modules/time-planning-pn/services';
import {Subscription} from 'rxjs';

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
    private workingHoursService: TimePlanningPnWorkingHoursService) {}

  ngOnInit(): void {}

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
    model.dateFrom = format(this.dateRange[0]._d, PARSING_DATE_FORMAT);
    model.dateTo = format(this.dateRange[1]._d, PARSING_DATE_FORMAT);
    model.siteId = this.siteId;
    this.downloadReportSub$ = this.workingHoursService
      .downloadReport(model)
      .subscribe(
        (data) => {
          saveAs(data, model.dateFrom + '_' + model.dateTo + '_report.xlsx');
        },
        (_) => {
          this.toastrService.error('Error downloading report');
        }
      );
  }

  filtersChangedEmmit(): void {
    if (this.dateRange && this.siteId) {
      this.filtersChanged.emit({
        siteId: this.siteId,
        dateFrom: format(this.dateRange[0]._d, PARSING_DATE_FORMAT),
        dateTo: format(this.dateRange[1]._d, PARSING_DATE_FORMAT),
      });
    }
  }
}
