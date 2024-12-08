import {CommonModule} from '@angular/common';
import {NgModule} from '@angular/core';
import {FormsModule, ReactiveFormsModule} from '@angular/forms';
import {RouterModule} from '@angular/router';
import {NgSelectModule} from '@ng-select/ng-select';
import {TranslateModule} from '@ngx-translate/core';
import {EformSharedModule} from 'src/app/common/modules/eform-shared/eform-shared.module';
import {TimePlanningPnRouting} from './time-planning-pn.routing';
// import {
//   TimePlanningsContainerComponent,
//   TimePlanningsHeaderComponent,
//   TimePlanningsTableComponent,
//   TimePlanningTableRowComponent,
// } from './components';
import {TimePlanningPnLayoutComponent} from './layouts';
import {
  TimePlanningPnPlanningsService, TimePlanningPnRegistrationDevicesService,
  TimePlanningPnSettingsService,
} from './services';
import {TimePlanningSettingsComponent} from './components';
import {MatCard, MatCardContent, MatCardHeader} from "@angular/material/card";
import {MatFormField} from "@angular/material/form-field";
import {MatInput} from "@angular/material/input";
import {MatButton} from "@angular/material/button";

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
    MatCardHeader,
    MatCard,
    MatCardContent,
    MatFormField,
    MatInput,
    MatButton,
  ],
  declarations: [
    TimePlanningPnLayoutComponent,
    TimePlanningSettingsComponent
    // TimePlanningTableRowComponent,
    // TimePlanningsTableComponent,
    // TimePlanningsContainerComponent,
    // TimePlanningsHeaderComponent,
  ],
  providers: [
    TimePlanningPnSettingsService,
    TimePlanningPnPlanningsService,
    TimePlanningPnRegistrationDevicesService
  ],
})
export class TimePlanningPnModule {
}
