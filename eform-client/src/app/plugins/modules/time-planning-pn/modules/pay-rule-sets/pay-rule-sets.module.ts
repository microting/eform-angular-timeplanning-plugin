import {NgModule} from '@angular/core';
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
import {HhMmSecondsDirective} from './directives/hh-mm-seconds.directive';
import {TimePlanningPnPayRuleSetsService} from '../../services';
import {MatDialogModule} from '@angular/material/dialog';

@NgModule({
  imports: [
    PayRuleSetsRouting,
    MatDialogModule,
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
    HhMmSecondsDirective,
  ],
  providers: [
    TimePlanningPnPayRuleSetsService,
  ],
})
export class PayRuleSetsModule {}
