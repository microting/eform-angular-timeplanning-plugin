import {Component, OnInit, inject, OnDestroy} from '@angular/core';
import {MAT_DIALOG_DATA, MatDialogRef} from '@angular/material/dialog';
import {DomSanitizer, SafeResourceUrl} from '@angular/platform-browser';
import {TimePlanningPnPlanningsService} from '../../../../services';
import {PlanRegistrationVersionHistoryModel, FieldChangeModel} from '../../../../models';
import {TemplateFilesService} from 'src/app/common/services';
import {Subscription} from 'rxjs';
import {TranslateService} from '@ngx-translate/core';

const GOOGLE_MAPS_EMBED_URL = 'https://www.google.com/maps?q={lat},{lng}&output=embed';
const PICTURE_SNAPSHOT_API_URL = '/api/template-files/get-image/';
const DATE_TIME_VALUE_REGEX = /^(\d{4}-\d{2}-\d{2})[ T](\d{2})[:.](\d{2})[:.]\d{2}/;
const NUMERIC_VALUE_REGEX = /^-?\d+(?:[.,](\d+))?$/;
// Fields stored as integer minutes and shown as HH:mm (0 is a real value, not unset).
const MINUTES_FIELD_REGEX = /^(Planned(Start|End|Break)OfShift[1-5]|Pause[1-5]OverrideMinutes)$/;
const MINUTES_VALUE_REGEX = /^\d+$/;
// Legacy 5-minute-tick fields: id 0 = unset, otherwise minutes = (id - 1) * 5.
const TICK_ID_FIELD_REGEX = /^(Start|Stop|Pause)[1-5]Id$/;

interface TimelineEvent {
  time: string;
  updatedByUserName: string;
  change: FieldChangeModel;
  fromDisplay?: string;
  toDisplay?: string;
}

interface TimelineDay {
  dateLabel: string;
  events: TimelineEvent[];
}

@Component({
  selector: 'app-version-history-modal',
  templateUrl: './version-history-modal.component.html',
  styleUrls: ['./version-history-modal.component.scss'],
  standalone: false
})
export class VersionHistoryModalComponent implements OnInit, OnDestroy {
  versionHistory: PlanRegistrationVersionHistoryModel;
  timelineDays: TimelineDay[] = [];
  workerName = '';
  headerDate = '';
  selectedGpsCoordinate: { latitude: number; longitude: number } | null = null;
  selectedSnapshot: string | null = null;
  mapUrl: SafeResourceUrl | null = null;
  snapshotUrl: string | null = null;
  loading = false;
  imageSub$: Subscription;

  private imageService = inject(TemplateFilesService);
  public dialogRef = inject(MatDialogRef<VersionHistoryModalComponent>);
  public data = inject(MAT_DIALOG_DATA) as { planRegistrationId: number; workerName?: string; date?: string };
  private planningsService = inject(TimePlanningPnPlanningsService);
  private sanitizer = inject(DomSanitizer);
  private translateService = inject(TranslateService);

  ngOnInit(): void {
    this.workerName = this.data.workerName || '';
    if (this.data.date) {
      this.headerDate = new Date(this.data.date).toLocaleDateString(this.currentLocale(), {
        weekday: 'long',
        year: 'numeric',
        month: 'long',
        day: 'numeric'
      });
    }
    this.loadVersionHistory();
  }

  loadVersionHistory(): void {
    this.loading = true;
    this.planningsService.getVersionHistory(this.data.planRegistrationId).subscribe({
      next: (result) => {
        if (result.success && result.model) {
          this.versionHistory = result.model;
          this.timelineDays = this.buildTimeline(result.model);
        }
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      }
    });
  }

  private buildTimeline(model: PlanRegistrationVersionHistoryModel): TimelineDay[] {
    const days: TimelineDay[] = [];
    let currentDay: TimelineDay | null = null;
    let currentDayKey = '';
    // Versions arrive newest-first from the backend; group them by local calendar day.
    for (const version of model.versions) {
      const updatedAt = new Date(version.updatedAt);
      const dayKey = [
        updatedAt.getFullYear(),
        this.pad2(updatedAt.getMonth() + 1),
        this.pad2(updatedAt.getDate())
      ].join('-');
      if (dayKey !== currentDayKey) {
        currentDayKey = dayKey;
        currentDay = {
          dateLabel: updatedAt.toLocaleDateString(this.currentLocale(), {
            year: 'numeric',
            month: 'long',
            day: 'numeric'
          }),
          events: []
        };
        days.push(currentDay);
      }
      const time = `${this.pad2(updatedAt.getHours())}:${this.pad2(updatedAt.getMinutes())}`;
      for (const change of version.changes) {
        const isStandard = change.fieldType !== 'gps' && change.fieldType !== 'snapshot';
        const fromDisplay = isStandard ? this.formatChangeValue(change.fromValue, dayKey, change.fieldName) : undefined;
        const toDisplay = isStandard ? this.formatChangeValue(change.toValue, dayKey, change.fieldName) : undefined;
        if (isStandard && fromDisplay === '—' && toDisplay === '—') {
          // Unset-to-unset transitions (e.g. null -> 0 tick ids) carry no information.
          continue;
        }
        currentDay.events.push({
          time,
          updatedByUserName: version.updatedByUserName,
          change,
          fromDisplay,
          toDisplay
        });
      }
    }
    return days.filter((day) => day.events.length > 0);
  }

  private formatChangeValue(value: string, eventDayKey: string, fieldName: string): string {
    if (!value) {
      return '—';
    }
    if (MINUTES_FIELD_REGEX.test(fieldName) && MINUTES_VALUE_REGEX.test(value)) {
      return this.minutesToHhMm(Number(value));
    }
    if (TICK_ID_FIELD_REGEX.test(fieldName) && MINUTES_VALUE_REGEX.test(value)) {
      const tickId = Number(value);
      if (tickId === 0) {
        return '—';
      }
      return this.minutesToHhMm((tickId - 1) * 5);
    }
    const dateTimeMatch = value.match(DATE_TIME_VALUE_REGEX);
    if (dateTimeMatch) {
      const [, datePart, hours, minutes] = dateTimeMatch;
      const time = `${hours}:${minutes}`;
      if (datePart === eventDayKey) {
        return time;
      }
      const [, month, day] = datePart.split('-');
      return `${day}.${month} ${time}`;
    }
    const numericMatch = value.match(NUMERIC_VALUE_REGEX);
    if (numericMatch && (numericMatch[1] || '').length > 2) {
      const parsed = Number(value.replace(',', '.'));
      if (Number.isFinite(parsed)) {
        return parsed.toLocaleString(this.currentLocale(), {maximumFractionDigits: 2});
      }
    }
    return value;
  }

  private pad2(value: number): string {
    return value.toString().padStart(2, '0');
  }

  private minutesToHhMm(totalMinutes: number): string {
    return `${this.pad2(Math.floor(totalMinutes / 60))}:${this.pad2(totalMinutes % 60)}`;
  }

  private currentLocale(): string {
    return this.translateService.currentLang || this.translateService.defaultLang || 'en-US';
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

  getFieldDisplayName(fieldName: string): string {
    const words = fieldName.replace(/([A-Z])/g, ' $1').trim().split(/\s+/);
    if (!words.length) {
      return '';
    }
    const [first, ...rest] = words;
    const normalizedFirst = first.charAt(0).toUpperCase() + first.slice(1).toLowerCase();
    const normalizedRest = rest.map((w) => w.toLowerCase());
    return this.translateService.instant([normalizedFirst, ...normalizedRest].join(' ').trim());
  }

  ngOnDestroy(): void {
    this.imageSub$?.unsubscribe();
    this.revokeSnapshotUrl();
  }
}
