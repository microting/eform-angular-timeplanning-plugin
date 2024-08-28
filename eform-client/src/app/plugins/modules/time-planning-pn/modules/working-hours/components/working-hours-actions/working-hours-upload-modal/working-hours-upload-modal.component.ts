import {Component, Inject, OnInit} from '@angular/core';
import {TranslateService} from '@ngx-translate/core';
import {FileUploader} from 'ng2-file-upload';
import {ToastrService} from 'ngx-toastr';
import {TemplateDto} from 'src/app/common/models/dto';
import {AuthStateService} from 'src/app/common/store';
import {MAT_DIALOG_DATA, MatDialogRef} from '@angular/material/dialog';
import {selectBearerToken} from 'src/app/state/auth/auth.selector';
import {Store} from '@ngrx/store';

@Component({
  selector: 'app-working-hours-upload-modal',
  templateUrl: './working-hours-upload-modal.component.html',
  styleUrls: ['./working-hours-upload-modal.component.scss'],
})
export class WorkingHoursUploadModalComponent implements OnInit {
  workingHoursFileUploader: FileUploader;
  private selectBearerToken$ = this.authStore.select(selectBearerToken);

  constructor(
    private toastrService: ToastrService,
    private authStore: Store,
    private translateService: TranslateService,
    private authStateService: AuthStateService,
    public dialogRef: MatDialogRef<WorkingHoursUploadModalComponent>,
    @Inject(MAT_DIALOG_DATA) public selectedTemplate: TemplateDto,
  ) {
  }

  ngOnInit() {
    let token = '';
    this.selectBearerToken$.subscribe((bearerToken) => {
      token = bearerToken;
    });
    this.workingHoursFileUploader  = new FileUploader({
      url: '/api/time-planning-pn/working-hours/reports/import',
      authToken: 'Bearer '+token,
    });
    // this.workingHoursFileUploader.onBuildItemForm = (item, form) => {
    //   //form.append('templateId', this.selectedTemplate.id);
    // };
    this.workingHoursFileUploader.onSuccessItem = () => {
      this.workingHoursFileUploader.clearQueue();
      this.toastrService.success(
        this.translateService.instant('File has been uploaded successfully')
      );
      this.hideZipModal(true);
    };
    this.workingHoursFileUploader.onErrorItem = () => {
      this.workingHoursFileUploader.clearQueue();
      this.toastrService.error(
        this.translateService.instant('Error while uploading file')
      );
    };
    this.workingHoursFileUploader.onAfterAddingFile = (f) => {
      if (this.workingHoursFileUploader.queue.length > 1) {
        this.workingHoursFileUploader.removeFromQueue(this.workingHoursFileUploader.queue[0]);
      }
    };
  }

  uploadTemplateZIP() {
    this.workingHoursFileUploader.queue[0].upload();
    this.dialogRef.close(true);
    this.toastrService.success(
      this.translateService.instant('File has been uploaded successfully, processing file can take a while, depending on the number of records')
    );
  }

  hideZipModal(result = false) {
    this.workingHoursFileUploader.clearQueue();
    this.dialogRef.close(result);
  }
}
