import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { EformSharedModule } from 'src/app/common/modules/eform-shared/eform-shared.module';
import { MatButton } from '@angular/material/button';
import { TranslateModule } from '@ngx-translate/core';
import { RequestHistoryRouting } from './request-history.routing';
import { MtxGrid } from '@ng-matero/extensions/grid';
import { FormsModule } from '@angular/forms';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import {
  RequestHistoryPageComponent
} from './components';

@NgModule({
  imports: [
    CommonModule,
    EformSharedModule,
    MatButton,
    TranslateModule,
    RequestHistoryRouting,
    MtxGrid,
    FormsModule,
    MatFormField,
    MatInput,
    MatLabel
  ],
  declarations: [
    RequestHistoryPageComponent
  ],
  providers: [],
})
export class RequestHistoryModule {}
