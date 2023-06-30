import {CommonModule} from '@angular/common';
import {NgModule} from '@angular/core';
import {FormsModule, ReactiveFormsModule} from '@angular/forms';
import {RouterModule} from '@angular/router';
import {TranslateModule} from '@ngx-translate/core';
import {EformSharedModule} from 'src/app/common/modules/eform-shared/eform-shared.module';
import {FlexRouting} from './flex.routing';
import {
  TimeFlexesCommentOfficeAllUpdateModalComponent,
  TimeFlexesCommentOfficeUpdateModalComponent,
  TimeFlexesContainerComponent,
  TimeFlexesTableComponent,
} from './components';
import {MtxGridModule} from '@ng-matero/extensions/grid';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import {MatButtonModule} from '@angular/material/button';
import {MatIconModule} from '@angular/material/icon';
import {MatDialogModule} from '@angular/material/dialog';
import {MatTooltipModule} from '@angular/material/tooltip';

@NgModule({
  imports: [
    CommonModule,
    TranslateModule,
    FormsModule,
    EformSharedModule,
    RouterModule,
    ReactiveFormsModule,
    FlexRouting,
    MtxGridModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    MatTooltipModule,
  ],
  declarations: [
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
