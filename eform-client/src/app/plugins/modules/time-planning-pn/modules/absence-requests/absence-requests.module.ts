import { NgModule } from '@angular/core';
import { CommonModule, NgIf, AsyncPipe, DatePipe } from '@angular/common';
import { EformSharedModule } from 'src/app/common/modules/eform-shared/eform-shared.module';
import { MatButton, MatIconButton } from '@angular/material/button';
import { TranslateModule } from '@ngx-translate/core';
import { AbsenceRequestsRouting } from './absence-requests.routing';
import { MtxGrid } from '@ng-matero/extensions/grid';
import { MatDialogActions, MatDialogContent, MatDialogTitle } from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { MatTooltip } from '@angular/material/tooltip';
import { FormsModule } from '@angular/forms';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import {
  AbsenceRequestsContainerComponent,
  AbsenceRequestsTableComponent,
  AbsenceRequestsApproveModalComponent,
  AbsenceRequestsRejectModalComponent
} from './components';

@NgModule({
  imports: [
    CommonModule,
    AsyncPipe,
    DatePipe,
    NgIf,
    EformSharedModule,
    MatButton,
    TranslateModule,
    AbsenceRequestsRouting,
    MtxGrid,
    MatDialogTitle,
    MatDialogContent,
    MatDialogActions,
    MatIcon,
    MatIconButton,
    MatTooltip,
    FormsModule,
    MatFormField,
    MatInput,
    MatLabel
  ],
  declarations: [
    AbsenceRequestsContainerComponent,
    AbsenceRequestsTableComponent,
    AbsenceRequestsApproveModalComponent,
    AbsenceRequestsRejectModalComponent
  ],
  providers: [],
})
export class AbsenceRequestsModule {}
