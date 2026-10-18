import { NgModule } from '@angular/core';
import { RequestHistoryRouting } from './request-history.routing';
import {
  RequestHistoryPageComponent
} from './components';

@NgModule({
  imports: [
    RequestHistoryRouting,
    RequestHistoryPageComponent
  ],
  providers: [],
})
export class RequestHistoryModule {}
