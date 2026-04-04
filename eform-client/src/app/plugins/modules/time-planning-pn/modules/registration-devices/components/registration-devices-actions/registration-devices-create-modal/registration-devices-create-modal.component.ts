import {Component, OnInit,
  inject
} from '@angular/core';
import {MatDialogRef} from '@angular/material/dialog';
import {TimePlanningPnRegistrationDevicesService} from '../../../../../services/time-planning-pn-registration-devices.service';
import {TimePlanningRegistrationDeviceModel} from '../../../../../../../modules/time-planning-pn/models';

@Component({
    selector: 'app-registration-devices-create-modal',
    templateUrl: './registration-devices-create-modal.component.html'
    // styleUrls: ['./registration-devices-create.component.scss']
    ,
    standalone: false
})
export class RegistrationDevicesCreateModalComponent implements OnInit {
  private registrationDevicesService = inject(TimePlanningPnRegistrationDevicesService);
  public dialogRef = inject(MatDialogRef<RegistrationDevicesCreateModalComponent>);

  selectedRegistrationDevice: TimePlanningRegistrationDeviceModel = new TimePlanningRegistrationDeviceModel();
  

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
