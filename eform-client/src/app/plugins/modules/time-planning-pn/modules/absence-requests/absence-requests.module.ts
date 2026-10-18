import { NgModule } from '@angular/core';
import { AbsenceRequestsRouting } from './absence-requests.routing';
import {
  AbsenceRequestsContainerComponent,
  AbsenceRequestsTableComponent,
  AbsenceRequestsApproveModalComponent,
  AbsenceRequestsRejectModalComponent
} from './components';

@NgModule({
  imports: [
    AbsenceRequestsRouting,
    AbsenceRequestsContainerComponent,
    AbsenceRequestsTableComponent,
    AbsenceRequestsApproveModalComponent,
    AbsenceRequestsRejectModalComponent
  ],
  providers: [],
})
export class AbsenceRequestsModule {}
