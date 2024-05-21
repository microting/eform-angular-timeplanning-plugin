import {Component, OnInit} from '@angular/core';
import {
  TimePlanningPnRegistrationDevicesService
} from '../../../../services/time-planning-pn-registration-devices.service';
import {Subscription} from 'rxjs';

@Component({
  selector: 'app-registration-devices-container',
  templateUrl: './registration-devices-container.component.html',
})
export class RegistrationDevicesContainerComponent implements OnInit {
  tainted: any;
  registrationDevices: any;
  getRegistrationDevices$: Subscription;
  constructor(
    private registrationDevicesService: TimePlanningPnRegistrationDevicesService,
  ) {
  }

  ngOnInit(): void {
    this.getRegistrationDevices();
  }

  onUpdateRegistrationDevices() {
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
}
