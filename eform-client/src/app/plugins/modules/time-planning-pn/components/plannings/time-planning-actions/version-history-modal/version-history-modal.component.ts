import { Component, Inject, OnInit } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { TimePlanningPnPlanningsService } from '../../../../services';
import { PlanRegistrationVersionHistoryModel, PlanRegistrationVersionModel, FieldChangeModel } from '../../../../models';

const GOOGLE_MAPS_EMBED_URL = 'https://www.google.com/maps?q={lat},{lng}&output=embed';
const PICTURE_SNAPSHOT_API_URL = '/api/time-planning-pn/picture-snapshots/';

@Component({
  selector: 'app-version-history-modal',
  templateUrl: './version-history-modal.component.html',
  styleUrls: ['./version-history-modal.component.scss'],
  standalone: false
})
export class VersionHistoryModalComponent implements OnInit {
  versionHistory: PlanRegistrationVersionHistoryModel;
  selectedGpsCoordinate: { latitude: number; longitude: number } | null = null;
  selectedSnapshot: string | null = null;
  mapUrl: SafeResourceUrl | null = null;
  snapshotUrl: string | null = null;
  loading = false;

  constructor(
    public dialogRef: MatDialogRef<VersionHistoryModalComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { planRegistrationId: number },
    private planningsService: TimePlanningPnPlanningsService,
    private sanitizer: DomSanitizer
  ) {}

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
    if (change.pictureHash) {
      this.selectedSnapshot = change.pictureHash;
      this.selectedGpsCoordinate = null;
      this.mapUrl = null;
      // Assuming the picture hash is a URL or we need to construct a URL to fetch the image
      // This might need adjustment based on how snapshots are stored
      this.snapshotUrl = `${PICTURE_SNAPSHOT_API_URL}${change.pictureHash}`;
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
    if (!date) return '';
    const d = new Date(date);
    return d.toLocaleString('en-US', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false
    });
  }

  getFieldDisplayName(fieldName: string): string {
    // Convert camelCase to space-separated words
    return fieldName.replace(/([A-Z])/g, ' $1').trim();
  }
}
