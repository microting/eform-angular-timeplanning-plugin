import {Component, Inject, Input, OnInit} from '@angular/core';
import {MAT_DIALOG_DATA, MatDialogRef} from '@angular/material/dialog';
import {TimePlanningPnRegistrationDevicesService} from 'src/app/plugins/modules/time-planning-pn/services';
import {TimePlanningRegistrationDeviceModel} from 'src/app/plugins/modules/time-planning-pn/models';

@Component({
  selector: 'app-registration-devices-delete-modal',
  templateUrl: './registration-devices-delete-modal.component.html'
  // styleUrls: ['./registration-devices-delete.component.scss']
})
export class RegistrationDevicesDeleteModalComponent implements OnInit {
  selectedRegistrationDevice: TimePlanningRegistrationDeviceModel = new TimePlanningRegistrationDeviceModel();
  constructor(
    @Inject(MAT_DIALOG_DATA) model: {
      selectedRegistrationDevice: TimePlanningRegistrationDeviceModel
    },
    private registrationDevicesService: TimePlanningPnRegistrationDevicesService,
    public dialogRef: MatDialogRef<RegistrationDevicesDeleteModalComponent>) {
    this.selectedRegistrationDevice = {...model.selectedRegistrationDevice};
  }

  ngOnInit() {
  }

  hide(result = false) {
    this.dialogRef.close(result);
  }

  deleteSingle() {
    this.registrationDevicesService.deleteRegistrationDevice(this.selectedRegistrationDevice.id).subscribe((data) => {
      if (data && data.success) {
        this.hide(true);
      }
    });
  }
}
