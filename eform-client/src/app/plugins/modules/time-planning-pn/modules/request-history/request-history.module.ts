import { NgModule } from '@angular/core';
import {MatDialogModule} from '@angular/material/dialog';
import { RequestHistoryRouting } from './request-history.routing';
import {
  RequestHistoryPageComponent
} from './components';

@NgModule({
  imports: [
    MatDialogModule,
    RequestHistoryRouting,
    RequestHistoryPageComponent
  ],
  providers: [],
})
export class RequestHistoryModule {}
