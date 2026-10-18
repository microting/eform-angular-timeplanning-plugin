import { Component, EventEmitter, Input, OnInit, OnDestroy, Output, inject } from '@angular/core';
import {MtxGridColumn, MtxGrid} from '@ng-matero/extensions/grid';
import {TranslateService, TranslatePipe} from '@ngx-translate/core';
import { AbsenceRequestModel } from '../../../../models';
import { Subscription } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';
import { dialogConfigHelper } from 'src/app/common/helpers';
import { Overlay } from '@angular/cdk/overlay';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import {
  AbsenceRequestsApproveModalComponent,
  AbsenceRequestsRejectModalComponent
} from '../absence-requests-actions';
import {NgIf, NgClass} from '@angular/common';
import {MatIcon} from '@angular/material/icon';
import {MatIconButton} from '@angular/material/button';
import {MatTooltip} from '@angular/material/tooltip';

@AutoUnsubscribe()
@Component({
  selector: 'app-absence-requests-table',
  imports: [NgIf, NgClass, TranslatePipe, MatIcon, MatIconButton, MtxGrid, MatTooltip],
  templateUrl: './absence-requests-table.component.html',
})
export class AbsenceRequestsTableComponent implements OnInit, OnDestroy {
  private dialog = inject(MatDialog);
  private overlay = inject(Overlay);
  private translateService = inject(TranslateService);

  @Input() absenceRequests: AbsenceRequestModel[] = [];
  @Input() currentView: 'inbox' | 'mine' = 'inbox';
  @Output() updateAbsenceRequests = new EventEmitter<void>();
  
  absenceRequestApproveModalComponentAfterClosedSub$: Subscription;
  absenceRequestRejectModalComponentAfterClosedSub$: Subscription;
  
  private _tableHeaders: MtxGridColumn[];

  get tableHeaders(): MtxGridColumn[] {
    return this._tableHeaders;
  }

  ngOnInit(): void {
    this._tableHeaders = [
      { header: this.translateService.stream('Id'), field: 'id', width: '80px' },
      { header: this.translateService.stream('Requested By'), field: 'requestedBySdkSitId', width: '120px' },
      { header: this.translateService.stream('Date From'), field: 'dateFrom', type: 'date', typeParameter: { format: 'dd.MM.y' } },
      { header: this.translateService.stream('Date To'), field: 'dateTo', type: 'date', typeParameter: { format: 'dd.MM.y' } },
      { header: this.translateService.stream('Status'), field: 'status' },
      { header: this.translateService.stream('Requested At'), field: 'requestedAtUtc', type: 'date', typeParameter: { format: 'dd.MM.y HH:mm' } },
      { header: this.translateService.stream('Request Comment'), field: 'requestComment' },
      {
        width: '200px',
        pinned: 'right',
        header: this.translateService.stream('Actions'),
        field: 'actions'
      },
    ];
  }

  ngOnDestroy(): void {}

  openApproveModal(row: AbsenceRequestModel) {
    const selectedAbsenceRequest = { ...row };
    this.absenceRequestApproveModalComponentAfterClosedSub$ = this.dialog
      .open(AbsenceRequestsApproveModalComponent, {
        ...dialogConfigHelper(this.overlay, {
          selectedAbsenceRequest: selectedAbsenceRequest
        })
      })
      .afterClosed()
      .subscribe(data => data ? this.updateAbsenceRequests.emit() : undefined);
  }

  openRejectModal(row: AbsenceRequestModel) {
    const selectedAbsenceRequest = { ...row };
    this.absenceRequestRejectModalComponentAfterClosedSub$ = this.dialog
      .open(AbsenceRequestsRejectModalComponent, {
        ...dialogConfigHelper(this.overlay, {
          selectedAbsenceRequest: selectedAbsenceRequest
        })
      })
      .afterClosed()
      .subscribe(data => data ? this.updateAbsenceRequests.emit() : undefined);
  }

  canApproveOrReject(row: AbsenceRequestModel): boolean {
    return this.currentView === 'inbox' && row.status === 'Pending';
  }
}
