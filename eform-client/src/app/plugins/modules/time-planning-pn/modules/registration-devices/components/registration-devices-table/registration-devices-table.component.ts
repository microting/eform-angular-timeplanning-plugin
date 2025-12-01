import {Component, EventEmitter, Input, OnInit, Output,
  inject
} from '@angular/core';
import {MtxGridColumn} from '@ng-matero/extensions/grid';
import {TranslateService} from '@ngx-translate/core';
import {TimePlanningRegistrationDeviceModel} from '../../../../../../modules/time-planning-pn/models';
import {Subscription} from 'rxjs';
import {
  RegistrationDevicesDeleteModalComponent,
  RegistrationDevicesEditModalComponent,
  RegistrationDevicesOtpCodeComponent
} from '../../../../modules/registration-devices/components';
import {MatDialog} from '@angular/material/dialog';
import {dialogConfigHelper} from 'src/app/common/helpers';
import {Overlay} from '@angular/cdk/overlay';

@Component({
    selector: 'app-registration-devices-table',
    templateUrl: './registration-devices-table.component.html',
    standalone: false
})
export class RegistrationDevicesTableComponent implements OnInit {
  private dialog = inject(MatDialog);
  private overlay = inject(Overlay);
  private translateService = inject(TranslateService);

  get tableHeaders(): MtxGridColumn[] {
    return this._tableHeaders;
  }

  @Output() filtersChanged = new EventEmitter<void>();
  @Input() registrationDevices!: any;
  @Output() updateRegistrationDevices = new EventEmitter<void>();
  @Input() tainted!: any;
  registrationDeviceOtpCodeComponentAfterClosedSub$: Subscription;
  registrationDeviceEditModalComponentAfterClosedSub$: Subscription;
  registrationDeviceDeleteModalComponentAfterClosedSub$: Subscription
  private _tableHeaders: MtxGridColumn[];
  

  ngOnInit(): void {
    this._tableHeaders = [
      {header: this.translateService.stream('Id'), field: 'id'},
      {header: this.translateService.stream('Created at'), field: 'createdAt', type: 'date', typeParameter: {format: 'dd.MM.y HH:mm:ss'}},
      {header: this.translateService.stream('Updated at'), field: 'updatedAt', type: 'date', typeParameter: {format: 'dd.MM.y HH:mm:ss'}},
      {header: this.translateService.stream('Name'), field: 'name'},
      {header: this.translateService.stream('Description'), field: 'description'},
      {
        header: this.translateService.stream('Model & OS version'),
        field: 'manufacturer',
      },
      {
        header: this.translateService.stream('Software version'),
        field: 'softwareVersion',
      },
      // {header: this.translateService.stream('OS version'), field: 'osVersion'},
      // {header: this.translateService.stream('Software version'), field: 'softwareVersion'},
      {header: this.translateService.stream('Last IP'), field: 'lastIp'},
      {header: this.translateService.stream('Customer no & OTP'), field: 'otpCode'},
      {
        width: '160px',
        pinned: 'right',
        header: this.translateService.stream('Actions'), field: 'actions'},
    ];
  }

  openOtpModal(registrationDeviceModel: TimePlanningRegistrationDeviceModel) {
    if (!registrationDeviceModel.id) {
      return;
    }
    this.registrationDeviceOtpCodeComponentAfterClosedSub$ = this.dialog.open(RegistrationDevicesOtpCodeComponent,
      {...dialogConfigHelper(this.overlay, registrationDeviceModel)})
      .afterClosed().subscribe(data => data ? this.updateRegistrationDevices.emit() : undefined);
    // this.propertyWorkerOtpModalComponentAfterClosedSub$ = this.dialog.open(PropertyWorkerOtpModalComponent,
    //   {...dialogConfigHelper(this.overlay, siteDto)})
    //   .afterClosed().subscribe(data => data ? this.updateTable.emit() : undefined);
  }

  openEditModal(row: TimePlanningRegistrationDeviceModel) {
    const selectedRegistrationDevice = {...row};
    this.registrationDeviceEditModalComponentAfterClosedSub$ = this.dialog.open(RegistrationDevicesEditModalComponent,
      {
        ...dialogConfigHelper(this.overlay, {
          selectedRegistrationDevice: selectedRegistrationDevice
        })
      })
      .afterClosed().subscribe(data => data ? this.updateRegistrationDevices.emit() : undefined);

  }

  openDeleteRegistrationDeviceModal(row: TimePlanningRegistrationDeviceModel) {
    const selectedRegistrationDevice = {...row};
    this.registrationDeviceDeleteModalComponentAfterClosedSub$ = this.dialog.open(RegistrationDevicesDeleteModalComponent,
      {
        ...dialogConfigHelper(this.overlay, {
          selectedRegistrationDevice: selectedRegistrationDevice
        })
      })
      .afterClosed().subscribe(data => data ? this.updateRegistrationDevices.emit() : undefined);
  }
}
