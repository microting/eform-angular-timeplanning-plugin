import {CommonModule} from '@angular/common';
import {NgModule} from '@angular/core';
import {FormsModule, ReactiveFormsModule} from '@angular/forms';
import {RouterModule} from '@angular/router';
import {
  OwlDateTimeModule,
  OWL_DATE_TIME_FORMATS,
} from '@danielmoncada/angular-datetime-picker';
import {TranslateModule} from '@ngx-translate/core';
import {NgxMaskModule} from 'ngx-mask';
import {EformSharedModule} from 'src/app/common/modules/eform-shared/eform-shared.module';
import {WorkingHoursRouting} from './working-hours.routing';
import {
  WorkingHoursContainerComponent,
  WorkingHoursHeaderComponent,
  WorkingHoursTableComponent,
  WorkingHoursCommentOfficeUpdateModalComponent,
  WorkingHoursCommentOfficeAllUpdateModalComponent,
} from './components';
import {MY_MOMENT_FORMATS_FOR_WORKING_HOURS} from '../../consts/custom-date-time-adapter-for-working-hours';
import {MtxGridModule} from '@ng-matero/extensions/grid';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import {MtxSelectModule} from '@ng-matero/extensions/select';
import {MatButtonModule} from '@angular/material/button';
import {MatIconModule} from '@angular/material/icon';
import {MatTableModule} from '@angular/material/table';
import {MatDialogModule} from '@angular/material/dialog';

@NgModule({
  imports: [
    CommonModule,
    TranslateModule,
    FormsModule,
    EformSharedModule,
    RouterModule,
    ReactiveFormsModule,
    NgxMaskModule,
    WorkingHoursRouting,
    OwlDateTimeModule,
    MtxGridModule,
    MatFormFieldModule,
    MatInputModule,
    MtxSelectModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatDialogModule,
  ],
  declarations: [
    WorkingHoursContainerComponent,
    WorkingHoursHeaderComponent,
    WorkingHoursTableComponent,
    WorkingHoursCommentOfficeUpdateModalComponent,
    WorkingHoursCommentOfficeAllUpdateModalComponent,
  ],
  providers: [
    {
      provide: OWL_DATE_TIME_FORMATS,
      useValue: MY_MOMENT_FORMATS_FOR_WORKING_HOURS,
    },
  ],
})
export class WorkingHoursModule {
}
