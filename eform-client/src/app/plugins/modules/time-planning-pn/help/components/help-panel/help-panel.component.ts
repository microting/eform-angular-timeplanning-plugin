import { Component, EventEmitter, Input, OnDestroy, OnInit, Output } from '@angular/core';
import { Subscription } from 'rxjs';
import { HelpEntry, HelpEntryId, HelpProse, HelpSection, HelpUiStrings } from '../../help.model';
import { HelpContentService } from '../../services/help-content.service';
import { HelpPanelService } from '../../services/help-panel.service';
import { HelpSearchResult, HelpSearchService } from '../../services/help-search.service';

export interface PanelSection {
  section: HelpSection;
  entries: HelpEntry[];
}

/** Tasks first — that is what a planner came looking for — then the page top to bottom. */
const SECTION_ORDER: HelpSection[] = ['task', 'toolbar', 'grid', 'dayCell', 'flex'];

@Component({
  selector: 'tp-help-panel',
  templateUrl: './help-panel.component.html',
  styleUrls: ['./help-panel.component.scss'],
  standalone: false,
})
export class HelpPanelComponent implements OnInit, OnDestroy {
  @Input() isAdmin = false;

  /**
   * Asks the host to replay the tour. The panel deliberately does not depend on
   * HelpTourService — keeping the two independent means neither has to know about
   * the other, and the host already owns where the tour is anchored.
   */
  @Output() replayTourRequested = new EventEmitter<void>();

  isOpen = false;
  targetId: HelpEntryId | null = null;
  query = '';
  results: HelpSearchResult[] = [];
  sections: PanelSection[] = [];
  expanded: HelpEntryId | null = null;

  private readonly subscriptions = new Subscription();

  constructor(
    private helpContent: HelpContentService,
    private helpSearch: HelpSearchService,
    private helpPanel: HelpPanelService,
  ) {}

  /** Chrome labels. Never the shared ngx-translate catalogue. */
  get ui(): HelpUiStrings {
    return this.helpContent.ui();
  }

  get isSearching(): boolean {
    return this.query.trim().length > 0;
  }

  ngOnInit(): void {
    this.subscriptions.add(this.helpPanel.isOpen$.subscribe(isOpen => {
      this.isOpen = isOpen;
      if (isOpen) {
        this.buildSections();
      } else {
        this.onQueryChange('');
      }
    }));
    this.subscriptions.add(this.helpPanel.target$.subscribe(target => {
      this.targetId = target;
      this.expanded = target;
    }));
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  onQueryChange(query: string): void {
    this.query = query;
    // A blank query browses instead of searching, so there is nothing to rank.
    this.results = this.isSearching ? this.helpSearch.search(query, { isAdmin: this.isAdmin }) : [];
  }

  clearQuery(): void {
    this.onQueryChange('');
  }

  toggleEntry(id: HelpEntryId): void {
    this.expanded = this.expanded === id ? null : id;
  }

  prose(id: HelpEntryId): HelpProse {
    return this.helpContent.prose(id);
  }

  sectionLabel(section: HelpSection): string {
    const labels: Record<HelpSection, string> = {
      task: this.ui.sectionTask,
      toolbar: this.ui.sectionToolbar,
      grid: this.ui.sectionGrid,
      dayCell: this.ui.sectionDayCell,
      flex: this.ui.sectionFlex,
    };
    return labels[section];
  }

  close(): void {
    this.helpPanel.close();
  }

  replayTour(): void {
    this.helpPanel.close();
    this.replayTourRequested.emit();
  }

  private buildSections(): void {
    const entries = this.helpContent.entries({ isAdmin: this.isAdmin });
    this.sections = SECTION_ORDER
      .map(section => ({ section, entries: entries.filter(entry => entry.section === section) }))
      .filter(group => group.entries.length > 0);
  }
}
