import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { AuthGuard, PermissionGuard } from 'src/app/common/guards';
import {TimePlanningPnClaims} from 'src/app/plugins/modules/time-planning-pn/enums';
import {MobileWorkingHoursComponent, WorkingHoursContainerComponent} from './components';

export const routes: Routes = [
  {
    path: '',
    canActivate: [PermissionGuard],
    component: WorkingHoursContainerComponent,
    data: {
      requiredPermission: TimePlanningPnClaims.accessTimePlanningPlugin,
    },
  },
  {
    path: 'mobile-working-hours',
    canActivate: [AuthGuard],
    component: MobileWorkingHoursComponent,
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class WorkingHoursRouting {}
