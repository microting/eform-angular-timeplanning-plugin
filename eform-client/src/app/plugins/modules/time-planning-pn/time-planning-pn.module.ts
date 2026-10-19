import {NgModule} from '@angular/core';
import {MatDialogModule} from '@angular/material/dialog';
import {MAT_FORM_FIELD_DEFAULT_OPTIONS} from '@angular/material/form-field';
import {TimePlanningPnRouting} from './time-planning-pn.routing';
import {TimePlanningPnLayoutComponent} from './layouts';
import {
  TimePlanningPnPlanningsService, TimePlanningPnRegistrationDevicesService,
  TimePlanningPnSettingsService,
  TimePlanningPnFlexesService,
  TimePlanningPnPayRuleSetsService,
  TimePlanningPnPayrollExportService
} from './services';
import {
  TimePlanningsContainerComponent, TimePlanningSettingsComponent, TimePlanningsTableComponent,
  AssignedSiteDialogComponent,
  WorkdayEntityDialogComponent, DownloadExcelDialogComponent, VersionHistoryModalComponent,
  PayrollExportDialogComponent
} from './components';
import {HelpIconComponent} from './help/components/help-icon/help-icon.component';
import {HelpHintComponent} from './help/components/help-hint/help-hint.component';
import {HelpPanelComponent} from './help/components/help-panel/help-panel.component';
import {HelpTourComponent} from './help/components/help-tour/help-tour.component';

@NgModule({
  imports: [
    MatDialogModule,
    TimePlanningPnRouting,
    TimePlanningPnLayoutComponent,
    TimePlanningSettingsComponent,
    WorkdayEntityDialogComponent,
    AssignedSiteDialogComponent,
    DownloadExcelDialogComponent,
    VersionHistoryModalComponent,
    TimePlanningsTableComponent,
    TimePlanningsContainerComponent,
    PayrollExportDialogComponent,
    HelpIconComponent,
    HelpHintComponent,
    HelpPanelComponent,
    HelpTourComponent,
  ],
  providers: [
    TimePlanningPnSettingsService,
    TimePlanningPnPlanningsService,
    TimePlanningPnRegistrationDevicesService,
    TimePlanningPnFlexesService,
    TimePlanningPnPayRuleSetsService,
    TimePlanningPnPayrollExportService,
    { provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { subscriptSizing: 'dynamic' } }
  ],
})
export class TimePlanningPnModule {
}
