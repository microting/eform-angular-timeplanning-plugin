import { Component, OnInit, inject } from '@angular/core';
import { TimePlanningPnAbsenceRequestsService } from '../../../../services';
import { AbsenceRequestModel } from '../../../../models';
import { Subscription } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';
import { Overlay } from '@angular/cdk/overlay';

@Component({
  selector: 'app-absence-requests-container',
  templateUrl: './absence-requests-container.component.html',
  standalone: false
})
export class AbsenceRequestsContainerComponent implements OnInit {
  public dialog = inject(MatDialog);
  private overlay = inject(Overlay);
  private absenceRequestsService = inject(TimePlanningPnAbsenceRequestsService);

  absenceRequests: AbsenceRequestModel[] = [];
  getAbsenceRequests$: Subscription;
  currentView: 'inbox' | 'mine' = 'inbox';
  managerSdkSitId: number = 1; // TODO: Get from user context
  requestedBySdkSitId: number = 1; // TODO: Get from user context

  ngOnInit(): void {
    this.loadAbsenceRequests();
  }

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
        .subscribe((data) => {
          if (data && data.success) {
            this.absenceRequests = data.model;
          }
        });
    } else {
      this.getAbsenceRequests$ = this.absenceRequestsService
        .getMine(this.requestedBySdkSitId)
        .subscribe((data) => {
          if (data && data.success) {
            this.absenceRequests = data.model;
          }
        });
    }
  }
}
