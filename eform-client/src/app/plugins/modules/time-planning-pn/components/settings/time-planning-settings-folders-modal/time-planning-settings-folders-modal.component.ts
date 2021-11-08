import {
  Component,
  EventEmitter,
  Input,
  OnDestroy,
  OnInit,
  Output,
  ViewChild,
} from '@angular/core';
import { SiteNameDto, FolderDto } from 'src/app/common/models';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';

@AutoUnsubscribe()
@Component({
  selector: 'app-time-planning-settings-folders-modal',
  templateUrl: './time-planning-settings-folders-modal.component.html',
  styleUrls: ['./time-planning-settings-folders-modal.component.scss'],
})
export class TimePlanningSettingsFoldersModalComponent implements OnInit, OnDestroy {
  @ViewChild('frame', { static: true }) frame;
  @Output()
  folderSelected: EventEmitter<FolderDto> = new EventEmitter<FolderDto>();
  sitesDto: Array<SiteNameDto> = [];
  @Input() folders: FolderDto[] = [];
  selectedFolderId: number;

  constructor() {}

  ngOnInit() {}

  show(selectedFolderId?: number) {
    this.selectedFolderId = selectedFolderId ?? null;
    this.frame.show();
  }

  select(folder: FolderDto) {
    this.folderSelected.emit(folder);
    this.frame.hide();
    this.selectedFolderId = 0;
  }

  ngOnDestroy(): void {}
}
