import { RouterModule, Routes } from '@angular/router';
import { NgModule } from '@angular/core';
import { AbsenceRequestsContainerComponent } from './components';
import { TimePlanningPnClaims } from 'src/app/plugins/modules/time-planning-pn/enums';

export const routes: Routes = [
  {
    path: '',
    component: AbsenceRequestsContainerComponent,
    data: {
      requiredPermission: TimePlanningPnClaims.accessTimePlanningPlugin,
    },
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class AbsenceRequestsRouting {}
