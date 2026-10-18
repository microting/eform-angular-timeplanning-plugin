import {Component, OnInit,
  inject
} from '@angular/core';
import {MatDialogRef, MatDialogTitle, MatDialogContent, MatDialogActions} from '@angular/material/dialog';
import {TimePlanningPnRegistrationDevicesService} from '../../../../../services/time-planning-pn-registration-devices.service';
import {TimePlanningRegistrationDeviceModel} from '../../../../../../../modules/time-planning-pn/models';
import {FormsModule} from '@angular/forms';
import {TranslatePipe} from '@ngx-translate/core';
import {MatFormField, MatLabel} from '@angular/material/form-field';
import {MatInput} from '@angular/material/input';

@Component({
    selector: 'app-registration-devices-create-modal',
    imports: [FormsModule, TranslatePipe, MatFormField, MatLabel, MatInput, MatDialogTitle, MatDialogContent, MatDialogActions],
    templateUrl: './registration-devices-create-modal.component.html'
    // styleUrls: ['./registration-devices-create.component.scss']
    ,
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
