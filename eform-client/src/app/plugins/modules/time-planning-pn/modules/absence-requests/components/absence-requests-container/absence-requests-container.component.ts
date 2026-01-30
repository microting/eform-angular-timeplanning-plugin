import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { TimePlanningPnAbsenceRequestsService } from '../../../../services';
import { AbsenceRequestModel } from '../../../../models';
import { Subscription } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';
import { Overlay } from '@angular/cdk/overlay';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import { ToastrService } from 'ngx-toastr';
import { TranslateService } from '@ngx-translate/core';

@AutoUnsubscribe()
@Component({
  selector: 'app-absence-requests-container',
  templateUrl: './absence-requests-container.component.html',
  standalone: false
})
export class AbsenceRequestsContainerComponent implements OnInit, OnDestroy {
  public dialog = inject(MatDialog);
  private overlay = inject(Overlay);
  private absenceRequestsService = inject(TimePlanningPnAbsenceRequestsService);
  private toastrService = inject(ToastrService);
  private translateService = inject(TranslateService);

  absenceRequests: AbsenceRequestModel[] = [];
  getAbsenceRequests$: Subscription;
  currentView: 'inbox' | 'mine' = 'inbox';
  managerSdkSitId: number = 1; // TODO: Get from user context
  requestedBySdkSitId: number = 1; // TODO: Get from user context

  ngOnInit(): void {
    this.loadAbsenceRequests();
  }

  ngOnDestroy(): void {}

  switchView(view: 'inbox' | 'mine') {
    this.currentView = view;
    this.loadAbsenceRequests();
  }

  onUpdateAbsenceRequests() {
    this.loadAbsenceRequests();
  }

  loadAbsenceRequests() {
    if (this.currentView === 'inbox') {
      this.getAbsenceRequests$ = this.absenceRequestsService
        .getInbox(this.managerSdkSitId)
        .subscribe({
          next: (data) => {
            if (data && data.success) {
              this.absenceRequests = data.model;
            }
          },
          error: () => {
            this.toastrService.error(
              this.translateService.instant('Error loading absence requests')
            );
          }
        });
    } else {
      this.getAbsenceRequests$ = this.absenceRequestsService
        .getMine(this.requestedBySdkSitId)
        .subscribe({
          next: (data) => {
            if (data && data.success) {
              this.absenceRequests = data.model;
            }
          },
          error: () => {
            this.toastrService.error(
              this.translateService.instant('Error loading absence requests')
            );
          }
        });
    }
  }
}
