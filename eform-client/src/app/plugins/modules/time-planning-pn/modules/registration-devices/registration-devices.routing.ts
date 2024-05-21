import {RouterModule, Routes} from '@angular/router';
import {NgModule} from '@angular/core';
import {
  RegistrationDevicesContainerComponent
} from './components';
import {TimePlanningPnClaims} from 'src/app/plugins/modules/time-planning-pn/enums';

export const routes: Routes = [
  {
    path: '',
    component: RegistrationDevicesContainerComponent,
    data: {
      requiredPermission: TimePlanningPnClaims.accessTimePlanningPlugin,
    },
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})

export class RegistrationDevicesRouting {
}
