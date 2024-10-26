import {Component, EventEmitter, Input, OnInit, Output} from '@angular/core';
import {MtxGridColumn} from '@ng-matero/extensions/grid';
import {TranslateService} from '@ngx-translate/core';
import {DeviceUserModel} from 'src/app/plugins/modules/backend-configuration-pn/models';

@Component({
  selector: 'app-registration-devices-table',
  templateUrl: './registration-devices-table.component.html',
})
export class RegistrationDevicesTableComponent implements OnInit {
  get tableHeaders(): MtxGridColumn[] {
    return this._tableHeaders;
  }

  @Output() filtersChanged = new EventEmitter<unknown>();
  @Input() registrationDevices!: any;
  @Output() updateRegistrationDevices = new EventEmitter<unknown>();
  @Input() tainted!: any;
  private _tableHeaders: MtxGridColumn[];
  constructor(
    private translateService: TranslateService) {
  }

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
      {header: this.translateService.stream('Actions'), field: 'actions'},
    ];
  }

  openOtpModal(siteDto: DeviceUserModel) {
    if (!siteDto.unitId) {
      return;
    }
    // this.propertyWorkerOtpModalComponentAfterClosedSub$ = this.dialog.open(PropertyWorkerOtpModalComponent,
    //   {...dialogConfigHelper(this.overlay, siteDto)})
    //   .afterClosed().subscribe(data => data ? this.updateTable.emit() : undefined);
  }

}
