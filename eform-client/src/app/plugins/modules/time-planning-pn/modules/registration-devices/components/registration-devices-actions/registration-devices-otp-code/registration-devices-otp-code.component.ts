import {Component, OnInit,
  inject
} from '@angular/core';
import {MAT_DIALOG_DATA, MatDialogRef} from '@angular/material/dialog';
import {TimePlanningRegistrationDeviceModel} from '../../../../../models';
import {TimePlanningPnRegistrationDevicesService} from '../../../../../services';



@Component({
    selector: 'app-registration-devices-otp-code',
    templateUrl: './registration-devices-otp-code.component.html',
    standalone: false
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
