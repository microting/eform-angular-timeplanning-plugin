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
  TimePlanningPnFlexesService
} from './services';
import {TimePlanningsContainerComponent, TimePlanningSettingsComponent, TimePlanningsTableComponent} from './components';
import {MatCard, MatCardContent, MatCardHeader} from '@angular/material/card';
import {MAT_FORM_FIELD_DEFAULT_OPTIONS, MatFormField, MatLabel, MatSuffix} from '@angular/material/form-field';
import {MatInput} from '@angular/material/input';
import {MatButton} from '@angular/material/button';
import {MatSlideToggle} from '@angular/material/slide-toggle';
import {MatDatepicker, MatDatepickerInput, MatDatepickerToggle} from '@angular/material/datepicker';
import {MtxGrid} from '@ng-matero/extensions/grid';
import {NgxChartsModule} from '@swimlane/ngx-charts';
import {MatIcon} from '@angular/material/icon';

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
        MatSlideToggle,
        MatLabel,
        MatDatepicker,
        MatDatepickerInput,
        MatDatepickerToggle,
        MatSuffix,
        MtxGrid,
        NgxChartsModule,
        MatIcon
    ],
  declarations: [
    TimePlanningPnLayoutComponent,
    TimePlanningSettingsComponent,
    // TimePlanningTableRowComponent,
    TimePlanningsTableComponent,
    TimePlanningsContainerComponent,
    // TimePlanningsHeaderComponent,
  ],
  providers: [
    TimePlanningPnSettingsService,
    TimePlanningPnPlanningsService,
    TimePlanningPnRegistrationDevicesService,
    TimePlanningPnFlexesService,
    { provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { subscriptSizing: 'dynamic' } }
  ],
})
export class TimePlanningPnModule {
}
