import {NgModule} from '@angular/core';
import {MatDialogModule} from '@angular/material/dialog';
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
    MatDialogModule,
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
