import {Component, OnInit} from '@angular/core';
import {MatDialogRef} from '@angular/material/dialog';
import {TimePlanningPnRegistrationDevicesService} from '../../../../../services/time-planning-pn-registration-devices.service';
import {TimePlanningRegistrationDeviceModel} from '../../../../../../../modules/time-planning-pn/models';

@Component({
  selector: 'app-registration-devices-create-modal',
  templateUrl: './registration-devices-create-modal.component.html'
  // styleUrls: ['./registration-devices-create.component.scss']
})
export class RegistrationDevicesCreateModalComponent implements OnInit {
  selectedRegistrationDevice: TimePlanningRegistrationDeviceModel = new TimePlanningRegistrationDeviceModel();
  constructor(
    private registrationDevicesService: TimePlanningPnRegistrationDevicesService,
    public dialogRef: MatDialogRef<RegistrationDevicesCreateModalComponent>) {
  }

  ngOnInit() {
  }

  createRegistrationDevice() {
    this.registrationDevicesService.createRegistrationDevice(this.selectedRegistrationDevice).subscribe((data) => {
      if (data && data.success) {
        this.hide(true);
      }
    });
  }

  hide(result = false) {
    this.dialogRef.close(result);
  }
}
