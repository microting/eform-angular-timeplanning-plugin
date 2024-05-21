import {NgModule} from '@angular/core';
import {
  RegistrationDevicesContainerComponent,
  RegistrationDevicesTableComponent
} from './components';
import {AsyncPipe, NgIf} from '@angular/common';
import {EformSharedModule} from 'src/app/common/modules/eform-shared/eform-shared.module';
import {MatButton} from '@angular/material/button';
import {TranslateModule} from '@ngx-translate/core';
import {
  RegistrationDevicesRouting
} from './registration-devices.routing';
import {MtxGrid} from "@ng-matero/extensions/grid";

@NgModule({
  imports: [
    AsyncPipe,
    EformSharedModule,
    MatButton,
    NgIf,
    TranslateModule,
    RegistrationDevicesRouting,
    MtxGrid
  ],
  declarations: [
    RegistrationDevicesTableComponent,
    RegistrationDevicesContainerComponent
  ],
  providers: [],
})
export class RegistrationDevicesModule {}
