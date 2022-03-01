import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  OwlDateTimeModule,
  OwlMomentDateTimeModule,
  OWL_DATE_TIME_FORMATS,
} from '@danielmoncada/angular-datetime-picker';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { NgSelectModule } from '@ng-select/ng-select';
import { TranslateModule } from '@ngx-translate/core';
import { MDBBootstrapModule } from 'angular-bootstrap-md';
import { NgxMaskModule } from 'ngx-mask';
import { EformSharedModule } from 'src/app/common/modules/eform-shared/eform-shared.module';
import { WorkingHoursRouting } from './working-hours.routing';
import {
  WorkingHoursContainerComponent,
  WorkingHoursHeaderComponent,
  WorkingHoursTableRowComponent,
  WorkingHoursTableComponent,
  WorkingHoursCommentOfficeUpdateModalComponent,
  WorkingHoursCommentOfficeAllUpdateModalComponent,
} from './components';
import { MY_MOMENT_FORMATS_FOR_WORKING_HOURS } from '../../consts/custom-date-time-adapter-for-working-hours';

@NgModule({
  imports: [
    CommonModule,
    MDBBootstrapModule,
    TranslateModule,
    FormsModule,
    NgSelectModule,
    EformSharedModule,
    FontAwesomeModule,
    RouterModule,
    ReactiveFormsModule,
    NgxMaskModule,
    WorkingHoursRouting,
    OwlDateTimeModule,
    OwlMomentDateTimeModule,
  ],
  declarations: [
    WorkingHoursContainerComponent,
    WorkingHoursHeaderComponent,
    WorkingHoursTableComponent,
    WorkingHoursTableRowComponent,
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
export class WorkingHoursModule {}
