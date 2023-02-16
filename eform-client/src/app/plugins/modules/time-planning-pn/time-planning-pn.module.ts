import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  OwlDateTimeModule,
  OwlMomentDateTimeModule,
  OWL_DATE_TIME_FORMATS,
} from '@danielmoncada/angular-datetime-picker';
import { NgSelectModule } from '@ng-select/ng-select';
import { TranslateModule } from '@ngx-translate/core';
import { NgxMaskModule } from 'ngx-mask';
import { EformSharedModule } from 'src/app/common/modules/eform-shared/eform-shared.module';
import { TimePlanningPnRouting } from './time-planning-pn.routing';
import {
  TimePlanningsContainerComponent,
  TimePlanningsHeaderComponent,
  TimePlanningsTableComponent,
  TimePlanningTableRowComponent,
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
    TranslateModule,
    FormsModule,
    NgSelectModule,
    EformSharedModule,
    RouterModule,
    TimePlanningPnRouting,
    ReactiveFormsModule,
    NgxMaskModule,
    OwlDateTimeModule,
    OwlMomentDateTimeModule,
  ],
  declarations: [
    TimePlanningPnLayoutComponent,
    TimePlanningTableRowComponent,
    TimePlanningsTableComponent,
    TimePlanningsContainerComponent,
    TimePlanningsHeaderComponent,
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
