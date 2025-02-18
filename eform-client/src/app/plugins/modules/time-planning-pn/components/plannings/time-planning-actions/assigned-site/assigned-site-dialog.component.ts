import { Component, Inject } from '@angular/core';
import {
  MAT_DIALOG_DATA,
  MatDialogActions,
  MatDialogClose,
  MatDialogContent,
  MatDialogTitle
} from '@angular/material/dialog';
import { AssignedSiteModel } from '../../../../models';
import {MatButton} from '@angular/material/button';
import {FormsModule} from '@angular/forms';
import {MatFormField, MatLabel} from '@angular/material/form-field';
import {MatInput} from '@angular/material/input';
import {TranslatePipe} from '@ngx-translate/core';
import {selectCurrentUserIsAdmin} from 'src/app/state';
import {Store} from '@ngrx/store';
import {AsyncPipe, NgIf} from '@angular/common';

@Component({
  selector: 'app-assigned-site-dialog',
  templateUrl: './assigned-site-dialog.component.html',
  imports: [
    MatDialogTitle,
    MatDialogContent,
    MatDialogActions,
    MatButton,
    MatDialogClose,
    FormsModule,
    MatFormField,
    MatInput,
    MatLabel,
    TranslatePipe,
    AsyncPipe,
    NgIf
  ],
  styleUrls: ['./assigned-site-dialog.component.scss']
})
export class AssignedSiteDialogComponent {
  public selectCurrentUserIsAdmin$ = this.authStore.select(selectCurrentUserIsAdmin);
  constructor(@Inject(MAT_DIALOG_DATA) public data: AssignedSiteModel,
  private authStore: Store) {}
}
