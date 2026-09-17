import {Component, OnInit,
  inject
} from '@angular/core';
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
    standalone: false
})
export class WorkingHoursUploadModalComponent implements OnInit {
  private toastrService = inject(ToastrService);
  private authStore = inject(Store);
  private translateService = inject(TranslateService);
  private authStateService = inject(AuthStateService);
  public dialogRef = inject(MatDialogRef<WorkingHoursUploadModalComponent>);
  public selectedTemplate = inject<TemplateDto>(MAT_DIALOG_DATA);

  workingHoursFileUploader: FileUploader;
  private selectBearerToken$ = this.authStore.select(selectBearerToken);

  

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
    // HTTP 200 DOES NOT MEAN THE IMPORT SUCCEEDED. The controller returns an
    // OperationResult as a 200 body whatever its Success flag says, so
    // ng2-file-upload routes server-reported FAILURES -- a malformed workbook,
    // or an exception part way through the file -- to onSuccessItem, not to
    // onErrorItem. Branch on the body, never on the transport.
    //
    // On success the body also reports what the import actually did: how many
    // days it could not write because they are reconciled, and how many sheets
    // belong to a worker with no id. Those rows are silently NOT imported, so a
    // hardcoded "uploaded successfully" would tell someone their corrections
    // went in when they did not, and they would re-upload the same file forever.
    this.workingHoursFileUploader.onSuccessItem = (item, response) => {
      this.workingHoursFileUploader.clearQueue();
      const result = this.parseOperationResult(response);
      if (result && !result.success) {
        // Report the server's own diagnosis and stop: no success toast, and the
        // dialog is not dismissed as done -- same shape as onErrorItem below.
        this.toastrService.error(
          result.message ??
            this.translateService.instant('Error while uploading file')
        );
        return;
      }
      // Green even when the message carries skips ("Imported. Locked days left
      // unchanged: 3."). A warning toast would read better, but the only signal
      // here is a localized string, and sniffing it for skip text is fragile.
      // Doing it properly means the endpoint returning counts as data --
      // OperationDataResult with a typed model, the way ReconcileThrough does.
      this.toastrService.success(
        result?.message ??
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

  /**
   * The server's OperationResult, parsed ONCE so the caller can branch on
   * success and reuse the message, or null when the body is not a recognisable
   * OperationResult. Defensive on purpose: ng2-file-upload hands back the raw
   * response text, and a proxy or an error page can put anything in it. A null
   * result means "cannot tell", which the caller treats as the old
   * success-with-translated-fallback behaviour.
   */
  private parseOperationResult(
    response: string
  ): { success: boolean; message: string | null } | null {
    try {
      const body = JSON.parse(response);
      if (!body || typeof body.success !== 'boolean') {
        return null;
      }
      return {
        success: body.success,
        message:
          typeof body.message === 'string' && body.message ? body.message : null,
      };
    } catch {
      return null;
    }
  }

  uploadTemplateZIP() {
    this.workingHoursFileUploader.queue[0].upload();
    this.dialogRef.close(true);
    // DO NOT merge this with the toast in onSuccessItem. The two say different
    // things at different times and both are wanted: this one fires the moment
    // the upload STARTS and is honest that processing takes a while, while
    // onSuccessItem fires when the server has finished and reports what the
    // import actually did. Collapsing them would either lose the "this takes a
    // while" warning or make the dialog block until the import completes.
    this.toastrService.success(
      this.translateService.instant('File has been uploaded successfully, processing file can take a while, depending on the number of records')
    );
  }

  hideZipModal(result = false) {
    this.workingHoursFileUploader.clearQueue();
    this.dialogRef.close(result);
  }
}
