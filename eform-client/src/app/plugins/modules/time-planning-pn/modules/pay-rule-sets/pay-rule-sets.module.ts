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
  PayDayRuleFormComponent,
  PayDayRuleListComponent,
} from './components';
import {TimePlanningPnPayRuleSetsService} from '../../services';
import {MtxGridModule} from '@ng-matero/extensions/grid';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import {MatButtonModule} from '@angular/material/button';
import {MatIconModule} from '@angular/material/icon';
import {MatDialogModule} from '@angular/material/dialog';
import {MatTooltipModule} from '@angular/material/tooltip';
import {MatSelectModule} from '@angular/material/select';
import {MatMenuModule} from '@angular/material/menu';
import {MatTableModule} from '@angular/material/table';

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
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    MatTooltipModule,
    MatSelectModule,
    MatMenuModule,
    MatTableModule,
  ],
  declarations: [
    PayRuleSetsContainerComponent,
    PayRuleSetsTableComponent,
    PayRuleSetsDeleteModalComponent,
    PayDayRuleFormComponent,
    PayDayRuleListComponent,
  ],
  providers: [
    TimePlanningPnPayRuleSetsService,
  ],
})
export class PayRuleSetsModule {}
