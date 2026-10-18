import {NgModule} from '@angular/core';
import {FlexRouting} from './flex.routing';
import {
  TimeFlexesCommentOfficeAllUpdateModalComponent,
  TimeFlexesCommentOfficeUpdateModalComponent,
  TimeFlexesContainerComponent,
  TimeFlexesTableComponent,
} from './components';
import {MatDialogModule} from '@angular/material/dialog';

@NgModule({
  imports: [
    FlexRouting,
    MatDialogModule,
    TimeFlexesCommentOfficeAllUpdateModalComponent,
    TimeFlexesCommentOfficeUpdateModalComponent,
    TimeFlexesContainerComponent,
    TimeFlexesTableComponent,
  ],
  providers: [
  ],
})
export class FlexModule {
}
