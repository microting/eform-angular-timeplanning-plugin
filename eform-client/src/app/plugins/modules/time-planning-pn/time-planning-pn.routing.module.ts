import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { AuthGuard, PermissionGuard } from 'src/app/common/guards';
import { TimePlanningSettingsComponent } from './components';
// import { PlanningsContainerComponent } from './components';
import { TimePlanningPnClaims } from './enums';
import { TimePlanningPnLayoutComponent } from './layouts';

export const routes: Routes = [
  {
    path: '',
    component: TimePlanningPnLayoutComponent,
    canActivate: [PermissionGuard],
    data: {
      requiredPermission: TimePlanningPnClaims.accessTimePlanningPlugin,
    },
    children: [
      // {
      //   path: 'plannings',
      //   canActivate: [PermissionGuard],
      //   data: {
      //     requiredPermission: TimePlanningPnClaims.getPlannings,
      //   },
      //   component: PlanningsContainerComponent,
      // },
      // {
      //   path: 'working-hours',
      //   canActivate: [AuthGuard],
      //   loadChildren: () =>
      //     import('./modules/property-workers/property-workers.module').then(
      //       (m) => m.PropertyWorkersModule
      //     ),
      // },
      // {
      //   path: 'flex',
      //   canActivate: [AuthGuard],
      //   loadChildren: () =>
      //     import('./modules/area-rules/area-rules.module').then(
      //       (m) => m.AreaRulesModule
      //     ),
      // },
      {
        path: 'settings',
        canActivate: [AuthGuard],
        component: TimePlanningSettingsComponent,
      },
    ],
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class TimePlanningPnRouting {}
