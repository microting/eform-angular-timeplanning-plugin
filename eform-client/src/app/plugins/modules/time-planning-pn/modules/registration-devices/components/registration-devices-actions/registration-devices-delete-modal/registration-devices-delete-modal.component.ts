import {Component, OnInit,
  inject
} from '@angular/core';
import {MAT_DIALOG_DATA, MatDialogRef, MatDialogTitle, MatDialogContent, MatDialogActions} from '@angular/material/dialog';
import {TimePlanningPnRegistrationDevicesService} from 'src/app/plugins/modules/time-planning-pn/services';
import {TimePlanningRegistrationDeviceModel} from 'src/app/plugins/modules/time-planning-pn/models';
import {TranslatePipe} from '@ngx-translate/core';

@Component({
    selector: 'app-registration-devices-delete-modal',
    imports: [TranslatePipe, MatDialogTitle, MatDialogContent, MatDialogActions],
    templateUrl: './registration-devices-delete-modal.component.html'
    // styleUrls: ['./registration-devices-delete.component.scss']
    ,
})
export class RegistrationDevicesDeleteModalComponent implements OnInit {
  private registrationDevicesService = inject(TimePlanningPnRegistrationDevicesService);
  public dialogRef = inject(MatDialogRef<RegistrationDevicesDeleteModalComponent>);
  private model = inject<{
      selectedRegistrationDevice: TimePlanningRegistrationDeviceModel
    }>(MAT_DIALOG_DATA);

  selectedRegistrationDevice: TimePlanningRegistrationDeviceModel = new TimePlanningRegistrationDeviceModel();
  

  ngOnInit() {
    this.selectedRegistrationDevice = {...this.model.selectedRegistrationDevice};
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
