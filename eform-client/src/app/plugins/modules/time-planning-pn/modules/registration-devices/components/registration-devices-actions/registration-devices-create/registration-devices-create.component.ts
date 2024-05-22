import {Component, OnInit} from '@angular/core';
import {MatDialogRef} from '@angular/material/dialog';
import {TimePlanningPnRegistrationDevicesService} from '../../../../../services/time-planning-pn-registration-devices.service';

@Component({
  selector: 'app-registration-devices-create',
  templateUrl: './registration-devices-create.component.html'
  // styleUrls: ['./registration-devices-create.component.scss']
})
export class RegistrationDevicesCreateComponent implements OnInit {
  constructor(
    private registrationDevicesService: TimePlanningPnRegistrationDevicesService,
    public dialogRef: MatDialogRef<RegistrationDevicesCreateComponent>) {
  }

  ngOnInit() {
  }

  createRegistrationDevice() {
    this.registrationDevicesService.createRegistrationDevice({}).subscribe((data) => {
      if (data && data.success) {
        this.hide(true);
      }
    });
  }

  hide(result = false) {
    this.dialogRef.close(result);
  }
}
