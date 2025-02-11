import {Component, Inject, OnInit} from '@angular/core';
import {MAT_DIALOG_DATA, MatDialogRef} from '@angular/material/dialog';
import {TimePlanningRegistrationDeviceModel} from '../../../../../models';
import {TimePlanningPnRegistrationDevicesService} from '../../../../../services';



@Component({
    selector: 'app-registration-devices-otp-code',
    templateUrl: './registration-devices-otp-code.component.html',
    standalone: false
})
export class RegistrationDevicesOtpCodeComponent implements OnInit {
  constructor(
    private timePlanningPnRegistrationDevicesService: TimePlanningPnRegistrationDevicesService,
    public dialogRef: MatDialogRef<RegistrationDevicesOtpCodeComponent>,
    @Inject(MAT_DIALOG_DATA) public selectedRegistrationDevice: TimePlanningRegistrationDeviceModel =
      new TimePlanningRegistrationDeviceModel(),
  ) {
  }

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
