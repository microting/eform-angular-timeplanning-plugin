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
import { TimePlanningPnRouting } from './time-planning-pn.routing.module';
import {
  TimeFlexesContainerComponent,
  TimeFlexesTableComponent,
  TimeFlexesTableRowComponent,
  TimePlanningsContainerComponent,
  TimePlanningSettingsAddSiteModalComponent,
  TimePlanningSettingsComponent,
  TimePlanningSettingsFoldersModalComponent,
  TimePlanningSettingsRemoveSiteModalComponent,
  TimePlanningsHeaderComponent,
  TimePlanningsTableComponent,
  TimePlanningTableRowComponent,
  TimeFlexesCommentOfficeUpdateModalComponent,
  TimeFlexesCommentOfficeAllUpdateModalComponent,
} from './components';
import { TimePlanningPnLayoutComponent } from './layouts';
import {
  TimePlanningPnPlanningsService,
  TimePlanningPnSettingsService,
} from './services';
import { MY_MOMENT_FORMATS_FOR_TIME_PLANNING } from './consts/custom-date-time-adapter';

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
    TimePlanningPnRouting,
    ReactiveFormsModule,
    NgxMaskModule,
    OwlDateTimeModule,
    OwlMomentDateTimeModule,
  ],
  declarations: [
    TimePlanningPnLayoutComponent,
    TimePlanningSettingsComponent,
    TimePlanningSettingsAddSiteModalComponent,
    TimePlanningSettingsFoldersModalComponent,
    TimePlanningSettingsRemoveSiteModalComponent,
    TimePlanningTableRowComponent,
    TimePlanningsTableComponent,
    TimePlanningsContainerComponent,
    TimePlanningsHeaderComponent,
    TimeFlexesTableComponent,
    TimeFlexesTableRowComponent,
    TimeFlexesContainerComponent,
    TimeFlexesCommentOfficeUpdateModalComponent,
    TimeFlexesCommentOfficeAllUpdateModalComponent,
  ],
  providers: [
    TimePlanningPnSettingsService,
    TimePlanningPnPlanningsService,
    {
      provide: OWL_DATE_TIME_FORMATS,
      useValue: MY_MOMENT_FORMATS_FOR_TIME_PLANNING,
    },
  ],
})
export class TimePlanningPnModule {}
