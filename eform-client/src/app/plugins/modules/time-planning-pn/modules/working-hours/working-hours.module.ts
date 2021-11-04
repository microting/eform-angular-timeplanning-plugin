import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { OwlDateTimeModule } from '@danielmoncada/angular-datetime-picker';
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
  WorkingHoursTableComponent,
} from './components';
import { WorkingHoursTableRowComponent } from './components/working-hours-table-row/working-hours-table-row.component';

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
    OwlDateTimeModule,
    NgxMaskModule,
    WorkingHoursRouting,
  ],
  declarations: [
    WorkingHoursContainerComponent,
    WorkingHoursHeaderComponent,
    WorkingHoursTableComponent,
    WorkingHoursTableRowComponent,
  ],
})
export class WorkingHoursModule {}
