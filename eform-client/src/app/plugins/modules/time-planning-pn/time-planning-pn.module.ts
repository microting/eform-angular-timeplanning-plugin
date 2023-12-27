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
  TimePlanningPnPlanningsService,
  TimePlanningPnSettingsService,
} from './services';

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
  ],
  declarations: [
    TimePlanningPnLayoutComponent,
    // TimePlanningTableRowComponent,
    // TimePlanningsTableComponent,
    // TimePlanningsContainerComponent,
    // TimePlanningsHeaderComponent,
  ],
  providers: [
    TimePlanningPnSettingsService,
    TimePlanningPnPlanningsService,
  ],
})
export class TimePlanningPnModule {
}
