import {CommonModule} from '@angular/common';
import {NgModule} from '@angular/core';
import {FormsModule, ReactiveFormsModule} from '@angular/forms';
import {RouterModule} from '@angular/router';
import {TranslateModule} from '@ngx-translate/core';
import {EformSharedModule} from 'src/app/common/modules/eform-shared/eform-shared.module';
import {WorkingHoursRouting} from './working-hours.routing';
import {
  WorkingHoursContainerComponent,
  WorkingHoursHeaderComponent,
  WorkingHoursTableComponent,
  WorkingHoursCommentOfficeUpdateModalComponent,
  WorkingHoursCommentOfficeAllUpdateModalComponent,
} from './components';
import {MtxGridModule} from '@ng-matero/extensions/grid';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import {MtxSelectModule} from '@ng-matero/extensions/select';
import {MatButtonModule} from '@angular/material/button';
import {MatIconModule} from '@angular/material/icon';
import {MatTableModule} from '@angular/material/table';
import {MatDialogModule} from '@angular/material/dialog';
import {MatDatepickerModule} from '@angular/material/datepicker';

@NgModule({
  imports: [
    CommonModule,
    TranslateModule,
    FormsModule,
    EformSharedModule,
    RouterModule,
    ReactiveFormsModule,
    WorkingHoursRouting,
    MtxGridModule,
    MatFormFieldModule,
    MatInputModule,
    MtxSelectModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatDialogModule,
    MatDatepickerModule,
  ],
  declarations: [
    WorkingHoursContainerComponent,
    WorkingHoursHeaderComponent,
    WorkingHoursTableComponent,
    WorkingHoursCommentOfficeUpdateModalComponent,
    WorkingHoursCommentOfficeAllUpdateModalComponent,
  ],
  providers: [
  ],
})
export class WorkingHoursModule {
}
