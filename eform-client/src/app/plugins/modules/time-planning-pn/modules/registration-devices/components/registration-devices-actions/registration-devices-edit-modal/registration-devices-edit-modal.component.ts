import {Component, OnInit,
  inject
} from '@angular/core';
import {MAT_DIALOG_DATA, MatDialogRef} from '@angular/material/dialog';
import {TimePlanningPnRegistrationDevicesService} from 'src/app/plugins/modules/time-planning-pn/services';
import {TimePlanningRegistrationDeviceModel} from 'src/app/plugins/modules/time-planning-pn/models';

@Component({
    selector: 'app-registration-devices-edit-modal',
    templateUrl: './registration-devices-edit-modal.component.html'
    // styleUrls: ['./registration-devices-create.component.scss']
    ,
    standalone: false
})
export class RegistrationDevicesEditModalComponent implements OnInit {
  private registrationDevicesService = inject(TimePlanningPnRegistrationDevicesService);
  public dialogRef = inject(MatDialogRef<RegistrationDevicesEditModalComponent>);
  private model = inject<{
      selectedRegistrationDevice: TimePlanningRegistrationDeviceModel
    }>(MAT_DIALOG_DATA);

  selectedRegistrationDevice: TimePlanningRegistrationDeviceModel = new TimePlanningRegistrationDeviceModel();
  

  ngOnInit() {
    this.selectedRegistrationDevice = {...this.model.selectedRegistrationDevice};
  }

  updateRegistrationDevice() {
    this.registrationDevicesService.updateRegistrationDevice(this.selectedRegistrationDevice).subscribe((data) => {
      if (data && data.success) {
        this.hide(true);
      }
    });
  }

  hide(result = false) {
    this.dialogRef.close(result);
  }
}
