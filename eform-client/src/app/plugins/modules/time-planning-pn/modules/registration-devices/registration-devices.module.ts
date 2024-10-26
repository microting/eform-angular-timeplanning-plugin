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
import {MatButton, MatIconButton} from '@angular/material/button';
import {TranslateModule} from '@ngx-translate/core';
import {
  RegistrationDevicesRouting
} from './registration-devices.routing';
import {MtxGrid} from '@ng-matero/extensions/grid';
import {MatDialogActions, MatDialogContent, MatDialogTitle} from "@angular/material/dialog";
import {MatIcon} from "@angular/material/icon";
import {MatTooltip} from "@angular/material/tooltip";

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
        MatDialogActions,
        MatIcon,
        MatIconButton,
        MatTooltip
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
