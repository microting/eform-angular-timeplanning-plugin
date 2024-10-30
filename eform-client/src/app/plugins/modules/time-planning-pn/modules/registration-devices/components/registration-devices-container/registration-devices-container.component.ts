import {Component, OnInit} from '@angular/core';
import {
  TimePlanningPnRegistrationDevicesService
} from '../../../../services/time-planning-pn-registration-devices.service';
import {Subscription} from 'rxjs';
import {MatDialog} from '@angular/material/dialog';
import {Overlay} from '@angular/cdk/overlay';
import {dialogConfigHelper} from 'src/app/common/helpers';
import {
  RegistrationDevicesCreateComponent
} from '../index';

@Component({
  selector: 'app-registration-devices-container',
  templateUrl: './registration-devices-container.component.html',
})
export class RegistrationDevicesContainerComponent implements OnInit {
  tainted: any;
  registrationDevices: any;
  getRegistrationDevices$: Subscription;
  createRegistrationDevicesComponentAfterClosedSub$: Subscription;
  constructor(
    public dialog: MatDialog,
    private overlay: Overlay,
    private registrationDevicesService: TimePlanningPnRegistrationDevicesService,
  ) {
  }

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
    this.createRegistrationDevicesComponentAfterClosedSub$ = this.dialog.open(RegistrationDevicesCreateComponent,
      dialogConfigHelper(this.overlay)).afterClosed().subscribe((data) => {
      if (data) {
        this.getRegistrationDevices();
      }
    });
  }
}
