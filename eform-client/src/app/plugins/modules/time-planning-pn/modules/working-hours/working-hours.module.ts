import {NgModule} from '@angular/core';
import {WorkingHoursRouting} from './working-hours.routing';
import {
  WorkingHoursContainerComponent,
  WorkingHoursHeaderComponent,
  WorkingHoursTableComponent,
  WorkingHoursCommentOfficeUpdateModalComponent,
  WorkingHoursCommentOfficeAllUpdateModalComponent,
  WorkingHoursUploadModalComponent,
  MobileWorkingHoursComponent,
} from './components';
import {MatDialogModule} from '@angular/material/dialog';

@NgModule({
  imports: [
    WorkingHoursRouting,
    MatDialogModule,
    MobileWorkingHoursComponent,
    WorkingHoursContainerComponent,
    WorkingHoursHeaderComponent,
    WorkingHoursTableComponent,
    WorkingHoursCommentOfficeUpdateModalComponent,
    WorkingHoursCommentOfficeAllUpdateModalComponent,
    WorkingHoursUploadModalComponent,
  ],
  providers: [
  ],
})
export class WorkingHoursModule {
}
