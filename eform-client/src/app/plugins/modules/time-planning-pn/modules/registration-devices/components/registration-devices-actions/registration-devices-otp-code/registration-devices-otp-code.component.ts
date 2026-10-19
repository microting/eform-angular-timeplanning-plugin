import {Component, OnInit,
  inject
} from '@angular/core';
import {MAT_DIALOG_DATA, MatDialogRef, MatDialogTitle, MatDialogContent, MatDialogActions} from '@angular/material/dialog';
import {TimePlanningRegistrationDeviceModel} from '../../../../../models';
import {TimePlanningPnRegistrationDevicesService} from '../../../../../services';
import {TranslatePipe} from '@ngx-translate/core';



@Component({
    selector: 'app-registration-devices-otp-code',
    imports: [TranslatePipe, MatDialogTitle, MatDialogContent, MatDialogActions],
    templateUrl: './registration-devices-otp-code.component.html',
})
export class RegistrationDevicesOtpCodeComponent implements OnInit {
  private timePlanningPnRegistrationDevicesService = inject(TimePlanningPnRegistrationDevicesService);
  public dialogRef = inject(MatDialogRef<RegistrationDevicesOtpCodeComponent>);
  public selectedRegistrationDevice = inject<TimePlanningRegistrationDeviceModel>(MAT_DIALOG_DATA);

  

  ngOnInit() {
  }

  hide(result = false) {
    this.dialogRef.close(result);
  }

  requestOtp() {
    this.timePlanningPnRegistrationDevicesService.requestOtp(this.selectedRegistrationDevice.id).subscribe(operation => {
      if (operation && operation.success) {
        this.hide(true);
      }
    });
  }
}
