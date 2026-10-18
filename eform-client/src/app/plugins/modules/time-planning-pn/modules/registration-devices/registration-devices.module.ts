import {NgModule} from '@angular/core';
import {
  RegistrationDevicesContainerComponent,
  RegistrationDevicesCreateModalComponent,
  RegistrationDevicesDeleteModalComponent, RegistrationDevicesEditModalComponent,
  RegistrationDevicesOtpCodeComponent,
  RegistrationDevicesTableComponent
} from './components';
import {
  RegistrationDevicesRouting
} from './registration-devices.routing';

@NgModule({
  imports: [
    RegistrationDevicesRouting,
    RegistrationDevicesTableComponent,
    RegistrationDevicesContainerComponent,
    RegistrationDevicesCreateModalComponent,
    RegistrationDevicesEditModalComponent,
    RegistrationDevicesDeleteModalComponent,
    RegistrationDevicesOtpCodeComponent
  ],
  providers: [],
})
export class RegistrationDevicesModule {}
