import {CommonModule} from '@angular/common';
import {NgModule} from '@angular/core';
import {FormsModule, ReactiveFormsModule} from '@angular/forms';
import {RouterModule} from '@angular/router';
import {NgSelectModule} from '@ng-select/ng-select';
import {TranslateModule} from '@ngx-translate/core';
import {EformSharedModule} from 'src/app/common/modules/eform-shared/eform-shared.module';
import {TimePlanningPnRouting} from './time-planning-pn.routing';
import {TimePlanningPnLayoutComponent} from './layouts';
import {
  TimePlanningPnPlanningsService, TimePlanningPnRegistrationDevicesService,
  TimePlanningPnSettingsService,
  TimePlanningPnFlexesService
} from './services';
import {
  TimePlanningsContainerComponent, TimePlanningSettingsComponent, TimePlanningsTableComponent,
  AssignedSiteDialogComponent,
  WorkdayEntityDialogComponent, DownloadExcelDialogComponent, VersionHistoryModalComponent
} from './components';
import {MatCard, MatCardContent, MatCardHeader} from '@angular/material/card';
import {
  MAT_FORM_FIELD_DEFAULT_OPTIONS, MatError,
  MatFormField,
  MatLabel,
  MatPrefix,
  MatSuffix
} from '@angular/material/form-field';
import {MatInput} from '@angular/material/input';
import {MatButton, MatIconButton} from '@angular/material/button';
import {MatSlideToggle} from '@angular/material/slide-toggle';
import {
  MatDatepicker,
  MatDatepickerInput,
  MatDatepickerToggle,
  MatDateRangeInput,
  MatDateRangePicker, MatEndDate, MatStartDate
} from '@angular/material/datepicker';
import {MtxGrid} from '@ng-matero/extensions/grid';
import {NgxChartsModule} from '@swimlane/ngx-charts';
import {MatIcon} from '@angular/material/icon';
import {MatTooltip} from '@angular/material/tooltip';
import {NgxMaskDirective} from 'ngx-mask';
import {NgxMaterialTimepickerModule} from 'ngx-material-timepicker';
import {MatTab, MatTabGroup} from '@angular/material/tabs';
import {MatCheckbox} from '@angular/material/checkbox';
import {MatDialogActions, MatDialogClose, MatDialogContent, MatDialogTitle} from '@angular/material/dialog';
import {MtxSelect} from '@ng-matero/extensions/select';

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
    MatTab,
    MatDialogActions,
    MatDialogTitle,
    MatDialogContent,
    MatDialogClose,
    MtxSelect,
    MatCard,
    MatCheckbox,
    MatCardContent,
    MatIconButton,
    MatFormField,
    MatInput,
    MatButton,
    MatTabGroup,
    MatSlideToggle,
    MatLabel,
    MatDatepicker,
    MatDatepickerInput,
    MatDatepickerToggle,
    MatSuffix,
    MtxGrid,
    NgxChartsModule,
    MatIcon,
    MatTooltip,
    NgxMaskDirective,
    NgxMaterialTimepickerModule,
    MatDateRangeInput,
    MatDateRangePicker,
    MatStartDate,
    MatEndDate,
    MatPrefix,
    MatError
  ],
  declarations: [
    TimePlanningPnLayoutComponent,
    TimePlanningSettingsComponent,
    WorkdayEntityDialogComponent,
    AssignedSiteDialogComponent,
    DownloadExcelDialogComponent,
    VersionHistoryModalComponent,
    TimePlanningsTableComponent,
    TimePlanningsContainerComponent,
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
