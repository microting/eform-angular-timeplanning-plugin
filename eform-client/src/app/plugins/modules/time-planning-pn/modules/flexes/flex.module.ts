import {CommonModule} from '@angular/common';
import {NgModule} from '@angular/core';
import {FormsModule, ReactiveFormsModule} from '@angular/forms';
import {RouterModule} from '@angular/router';
import {
  OwlDateTimeModule,
  OwlMomentDateTimeModule,
  OWL_DATE_TIME_FORMATS,
} from '@danielmoncada/angular-datetime-picker';
import {FontAwesomeModule} from '@fortawesome/angular-fontawesome';
import {TranslateModule} from '@ngx-translate/core';
import {NgxMaskModule} from 'ngx-mask';
import {EformSharedModule} from 'src/app/common/modules/eform-shared/eform-shared.module';
import {FlexRouting} from './flex.routing';
import {
  TimeFlexesCommentOfficeAllUpdateModalComponent,
  TimeFlexesCommentOfficeUpdateModalComponent,
  TimeFlexesContainerComponent,
  TimeFlexesTableComponent,
} from './components';
import {MY_MOMENT_FORMATS_FOR_WORKING_HOURS} from '../../consts/custom-date-time-adapter-for-working-hours';
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
    FontAwesomeModule,
    RouterModule,
    ReactiveFormsModule,
    NgxMaskModule,
    FlexRouting,
    OwlDateTimeModule,
    OwlMomentDateTimeModule,
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
    {
      provide: OWL_DATE_TIME_FORMATS,
      useValue: MY_MOMENT_FORMATS_FOR_WORKING_HOURS,
    },
  ],
})
export class FlexModule {
}
