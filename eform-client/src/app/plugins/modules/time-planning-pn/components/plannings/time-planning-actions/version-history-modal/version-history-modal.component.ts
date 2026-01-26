import {Component, Inject, OnInit, inject, OnDestroy} from '@angular/core';
import {MAT_DIALOG_DATA, MatDialogRef} from '@angular/material/dialog';
import {DomSanitizer, SafeResourceUrl} from '@angular/platform-browser';
import {TimePlanningPnPlanningsService} from '../../../../services';
import {PlanRegistrationVersionHistoryModel, FieldChangeModel} from '../../../../models';
import {TemplateFilesService} from 'src/app/common/services';
import {Subscription} from 'rxjs';

const GOOGLE_MAPS_EMBED_URL = 'https://www.google.com/maps?q={lat},{lng}&output=embed';
const PICTURE_SNAPSHOT_API_URL = '/api/template-files/get-image/';

@Component({
  selector: 'app-version-history-modal',
  templateUrl: './version-history-modal.component.html',
  styleUrls: ['./version-history-modal.component.scss'],
  standalone: false
})
export class VersionHistoryModalComponent implements OnInit, OnDestroy {
  versionHistory: PlanRegistrationVersionHistoryModel;
  selectedGpsCoordinate: { latitude: number; longitude: number } | null = null;
  selectedSnapshot: string | null = null;
  mapUrl: SafeResourceUrl | null = null;
  snapshotUrl: string | null = null;
  loading = false;
  imageSub$: Subscription;

  private imageService = inject(TemplateFilesService);
  public dialogRef = inject(MatDialogRef<VersionHistoryModalComponent>);
  public data = inject(MAT_DIALOG_DATA) as { planRegistrationId: number };
  private planningsService = inject(TimePlanningPnPlanningsService);
  private sanitizer = inject(DomSanitizer);

  ngOnInit(): void {
    this.loadVersionHistory();
  }

  loadVersionHistory(): void {
    this.loading = true;
    this.planningsService.getVersionHistory(this.data.planRegistrationId).subscribe({
      next: (result) => {
        if (result.success && result.model) {
          this.versionHistory = result.model;
        }
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      }
    });
  }

  onGpsClick(change: FieldChangeModel): void {
    if (change.latitude && change.longitude) {
      this.selectedGpsCoordinate = {
        latitude: change.latitude,
        longitude: change.longitude
      };
      this.selectedSnapshot = null;
      const url = GOOGLE_MAPS_EMBED_URL
        .replace('{lat}', change.latitude.toString())
        .replace('{lng}', change.longitude.toString());
      this.mapUrl = this.sanitizer.bypassSecurityTrustResourceUrl(url);
    }
  }

  onSnapshotClick(change: FieldChangeModel): void {
    if (!change.pictureHash) {
      return;
    }
    this.selectedSnapshot = change.pictureHash;
    this.selectedGpsCoordinate = null;
    this.mapUrl = null;
    this.snapshotUrl = null;
    this.imageSub$?.unsubscribe();
    this.imageSub$ = this.imageService.getImage(change.toValue).subscribe((blob) => {
      this.revokeSnapshotUrl();
      this.snapshotUrl = URL.createObjectURL(blob);
    });
  }

  private revokeSnapshotUrl(): void {
    if (this.snapshotUrl?.startsWith('blob:')) {
      URL.revokeObjectURL(this.snapshotUrl);
    }
  }

  closePanel(): void {
    this.selectedGpsCoordinate = null;
    this.selectedSnapshot = null;
    this.mapUrl = null;
    this.snapshotUrl = null;
  }

  close(): void {
    this.dialogRef.close();
  }

  formatDateTime(date: Date): string {
    if (!date) {
      return '';
    }
    const d = new Date(date);
    return d.toLocaleString('en-US', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false
    });
  }

  getFieldDisplayName(fieldName: string): string {
    return fieldName.replace(/([A-Z])/g, ' $1').trim();
  }

  ngOnDestroy(): void {
    this.imageSub$?.unsubscribe();
    this.revokeSnapshotUrl();
  }
}
