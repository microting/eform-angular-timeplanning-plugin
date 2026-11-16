import {NgModule} from '@angular/core';
import {MatDialogModule} from '@angular/material/dialog';
import {PlannerRouting} from './planner.routing';
import {PlannerContainerComponent} from './components';

/**
 * The web planner, "Planlægning (beta)" (#5489), lazy-loaded at
 * /plugins/time-planning-pn/planner behind the time_planning_planner claim.
 */
@NgModule({
  imports: [MatDialogModule, PlannerRouting, PlannerContainerComponent],
})
export class PlannerModule {}
