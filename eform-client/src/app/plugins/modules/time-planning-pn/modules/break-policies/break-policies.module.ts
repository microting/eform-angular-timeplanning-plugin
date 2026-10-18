import {NgModule} from '@angular/core';
import {BreakPoliciesRouting} from './break-policies.routing';
import {
  BreakPoliciesContainerComponent,
  BreakPoliciesTableComponent,
  BreakPoliciesCreateModalComponent,
  BreakPoliciesEditModalComponent,
  BreakPoliciesDeleteModalComponent,
  BreakPolicyRuleFormComponent,
  BreakPolicyRulesListComponent,
  BreakPolicyRuleDialogComponent,
} from './components';
import {TimePlanningPnBreakPoliciesService} from '../../services';
import {MatDialogModule} from '@angular/material/dialog';

@NgModule({
  imports: [
    BreakPoliciesRouting,
    MatDialogModule,
    BreakPoliciesContainerComponent,
    BreakPoliciesTableComponent,
    BreakPoliciesCreateModalComponent,
    BreakPoliciesEditModalComponent,
    BreakPoliciesDeleteModalComponent,
    BreakPolicyRuleFormComponent,
    BreakPolicyRulesListComponent,
    BreakPolicyRuleDialogComponent,
  ],
  providers: [
    TimePlanningPnBreakPoliciesService,
  ],
})
export class BreakPoliciesModule {}
