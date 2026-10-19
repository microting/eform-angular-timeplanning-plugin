import { NgModule } from '@angular/core';
import {MatDialogModule} from '@angular/material/dialog';
import { AbsenceRequestsRouting } from './absence-requests.routing';
import {
  AbsenceRequestsContainerComponent,
  AbsenceRequestsTableComponent,
  AbsenceRequestsApproveModalComponent,
  AbsenceRequestsRejectModalComponent
} from './components';

@NgModule({
  imports: [
    MatDialogModule,
    AbsenceRequestsRouting,
    AbsenceRequestsContainerComponent,
    AbsenceRequestsTableComponent,
    AbsenceRequestsApproveModalComponent,
    AbsenceRequestsRejectModalComponent
  ],
  providers: [],
})
export class AbsenceRequestsModule {}
