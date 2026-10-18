import {Component, OnInit,
  inject
} from '@angular/core';
import {MAT_DIALOG_DATA, MatDialogRef, MatDialogTitle, MatDialogContent, MatDialogActions} from '@angular/material/dialog';
import {TimePlanningPnRegistrationDevicesService} from 'src/app/plugins/modules/time-planning-pn/services';
import {TimePlanningRegistrationDeviceModel} from 'src/app/plugins/modules/time-planning-pn/models';
import {FormsModule} from '@angular/forms';
import {TranslatePipe} from '@ngx-translate/core';
import {MatFormField, MatLabel} from '@angular/material/form-field';
import {MatInput} from '@angular/material/input';

@Component({
    selector: 'app-registration-devices-edit-modal',
    imports: [FormsModule, TranslatePipe, MatFormField, MatLabel, MatInput, MatDialogTitle, MatDialogContent, MatDialogActions],
    templateUrl: './registration-devices-edit-modal.component.html'
    // styleUrls: ['./registration-devices-create.component.scss']
    ,
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
