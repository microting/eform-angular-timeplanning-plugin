import {NgModule} from '@angular/core';
import {
  RegistrationDevicesContainerComponent,
  RegistrationDevicesCreateComponent,
  RegistrationDevicesDeleteComponent,
  RegistrationDevicesOtpCodeComponent,
  RegistrationDevicesTableComponent
} from './components';
import {AsyncPipe, NgIf} from '@angular/common';
import {EformSharedModule} from 'src/app/common/modules/eform-shared/eform-shared.module';
import {MatButton} from '@angular/material/button';
import {TranslateModule} from '@ngx-translate/core';
import {
  RegistrationDevicesRouting
} from './registration-devices.routing';
import {MtxGrid} from '@ng-matero/extensions/grid';
import {MatDialogActions, MatDialogContent, MatDialogTitle} from "@angular/material/dialog";

@NgModule({
  imports: [
    AsyncPipe,
    EformSharedModule,
    MatButton,
    NgIf,
    TranslateModule,
    RegistrationDevicesRouting,
    MtxGrid,
    MatDialogTitle,
    MatDialogContent,
    MatDialogActions
  ],
  declarations: [
    RegistrationDevicesTableComponent,
    RegistrationDevicesContainerComponent,
    RegistrationDevicesCreateComponent,
    RegistrationDevicesDeleteComponent,
    RegistrationDevicesOtpCodeComponent
  ],
  providers: [],
})
export class RegistrationDevicesModule {}
