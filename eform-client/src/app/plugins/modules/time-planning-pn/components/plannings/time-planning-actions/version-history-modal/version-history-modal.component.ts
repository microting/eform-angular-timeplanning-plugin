import {Component, Inject, OnInit, inject, OnDestroy, ViewChild, TemplateRef, AfterViewInit} from '@angular/core';
import {MAT_DIALOG_DATA, MatDialogRef} from '@angular/material/dialog';
import {DomSanitizer, SafeResourceUrl} from '@angular/platform-browser';
import {TimePlanningPnPlanningsService} from '../../../../services';
import {PlanRegistrationVersionHistoryModel, FieldChangeModel} from '../../../../models';
import {TemplateFilesService} from 'src/app/common/services';
import {Subscription} from 'rxjs';
import {MtxGridColumn} from '@ng-matero/extensions/grid';
import {TranslateService} from '@ngx-translate/core';

const GOOGLE_MAPS_EMBED_URL = 'https://www.google.com/maps?q={lat},{lng}&output=embed';
const PICTURE_SNAPSHOT_API_URL = '/api/template-files/get-image/';

@Component({
  selector: 'app-version-history-modal',
  templateUrl: './version-history-modal.component.html',
  styleUrls: ['./version-history-modal.component.scss'],
  standalone: false
})
export class VersionHistoryModalComponent implements OnInit, AfterViewInit, OnDestroy {
  versionHistory: PlanRegistrationVersionHistoryModel;
  selectedGpsCoordinate: { latitude: number; longitude: number } | null = null;
  selectedSnapshot: string | null = null;
  mapUrl: SafeResourceUrl | null = null;
  snapshotUrl: string | null = null;
  loading = false;
  imageSub$: Subscription;
  tableHeaders: MtxGridColumn[] = [];

  @ViewChild('fieldNameTemplate', {static: false}) fieldNameTemplate!: TemplateRef<FieldChangeModel>;
  @ViewChild('toValueTemplate', {static: false}) toValueTemplate!: TemplateRef<FieldChangeModel>;

  private imageService = inject(TemplateFilesService);
  public dialogRef = inject(MatDialogRef<VersionHistoryModalComponent>);
  public data = inject(MAT_DIALOG_DATA) as { planRegistrationId: number };
  private planningsService = inject(TimePlanningPnPlanningsService);
  private sanitizer = inject(DomSanitizer);
  private translateService = inject(TranslateService);

  ngOnInit(): void {
    this.loadVersionHistory();
  }

  ngAfterViewInit(): void {
    this.initTableHeaders();
  }

  initTableHeaders(): void {
    this.tableHeaders = [
      {
        header: this.translateService.stream('Variable'),
        field: 'fieldName',
        cellTemplate: this.fieldNameTemplate,
      },
      {
        header: this.translateService.stream('From value'),
        field: 'fromValue',
      },
      {
        header: this.translateService.stream('To value'),
        field: 'toValue',
        cellTemplate: this.toValueTemplate,
      },
    ];
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
