import {CommonModule} from '@angular/common';
import {NgModule} from '@angular/core';
import {FormsModule, ReactiveFormsModule} from '@angular/forms';
import {RouterModule} from '@angular/router';
import {TranslateModule} from '@ngx-translate/core';
import {EformSharedModule} from 'src/app/common/modules/eform-shared/eform-shared.module';
import {PayRuleSetsRouting} from './pay-rule-sets.routing';
import {
  PayRuleSetsContainerComponent,
  PayRuleSetsTableComponent,
  PayRuleSetsDeleteModalComponent,
  PayRuleSetsCreateModalComponent,
  PayRuleSetsEditModalComponent,
  PayDayRuleFormComponent,
  PayDayRuleListComponent,
  PayDayRuleDialogComponent,
  DayTypeRuleListComponent,
  DayTypeRuleDialogComponent,
} from './components';
import {TimePlanningPnPayRuleSetsService} from '../../services';
import {MtxGridModule} from '@ng-matero/extensions/grid';
import {MtxSelectModule} from '@ng-matero/extensions/select';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import {MatButtonModule} from '@angular/material/button';
import {MatIconModule} from '@angular/material/icon';
import {MatDialogModule} from '@angular/material/dialog';
import {MatTooltipModule} from '@angular/material/tooltip';
import {MatSelectModule} from '@angular/material/select';
import {MatMenuModule} from '@angular/material/menu';
import {MatTableModule} from '@angular/material/table';
import {NgxMaterialTimepickerModule} from 'ngx-material-timepicker';

@NgModule({
  imports: [
    CommonModule,
    TranslateModule,
    FormsModule,
    EformSharedModule,
    RouterModule,
    ReactiveFormsModule,
    PayRuleSetsRouting,
    MtxGridModule,
    MtxSelectModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    MatTooltipModule,
    MatSelectModule,
    MatMenuModule,
    MatTableModule,
    NgxMaterialTimepickerModule,
  ],
  declarations: [
    PayRuleSetsContainerComponent,
    PayRuleSetsTableComponent,
    PayRuleSetsDeleteModalComponent,
    PayRuleSetsCreateModalComponent,
    PayRuleSetsEditModalComponent,
    PayDayRuleFormComponent,
    PayDayRuleListComponent,
    PayDayRuleDialogComponent,
    DayTypeRuleListComponent,
    DayTypeRuleDialogComponent,
  ],
  providers: [
    TimePlanningPnPayRuleSetsService,
  ],
})
export class PayRuleSetsModule {}
