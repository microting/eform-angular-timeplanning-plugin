import {Component, OnInit,
  inject
} from '@angular/core';
import {
  TimePlanningPnRegistrationDevicesService
} from '../../../../services/time-planning-pn-registration-devices.service';
import {Subscription} from 'rxjs';
import {MatDialog} from '@angular/material/dialog';
import {Overlay} from '@angular/cdk/overlay';
import {dialogConfigHelper} from 'src/app/common/helpers';
import {
  RegistrationDevicesCreateModalComponent
} from '../index';
import {TranslatePipe} from '@ngx-translate/core';
import {EformNewSubheaderComponent} from 'src/app/common/modules/eform-shared/components';
import {RegistrationDevicesTableComponent} from '../registration-devices-table/registration-devices-table.component';

@Component({
    selector: 'app-registration-devices-container',
    imports: [TranslatePipe, EformNewSubheaderComponent, RegistrationDevicesTableComponent],
    templateUrl: './registration-devices-container.component.html',
})
export class RegistrationDevicesContainerComponent implements OnInit {
  public dialog = inject(MatDialog);
  private overlay = inject(Overlay);
  private registrationDevicesService = inject(TimePlanningPnRegistrationDevicesService);

  tainted: any;
  registrationDevices: any;
  getRegistrationDevices$: Subscription;
  createRegistrationDevicesComponentAfterClosedSub$: Subscription;
  

  ngOnInit(): void {
    this.getRegistrationDevices();
  }

  onUpdateRegistrationDevices() {
    this.getRegistrationDevices();
  }

  onRegistrationDevicesFiltersChanged($event: unknown) {
  }

  getRegistrationDevices() {
    this.getRegistrationDevices$ = this.registrationDevicesService.getRegistrationDevices({}).subscribe((data) => {
      if (data && data.success) {
        this.registrationDevices = data.model;
      }
    }
    );
  }

  openCreateModal() {
    this.createRegistrationDevicesComponentAfterClosedSub$ = this.dialog.open(RegistrationDevicesCreateModalComponent,
      dialogConfigHelper(this.overlay)).afterClosed().subscribe((data) => {
      if (data) {
        this.getRegistrationDevices();
      }
    });
  }
}
