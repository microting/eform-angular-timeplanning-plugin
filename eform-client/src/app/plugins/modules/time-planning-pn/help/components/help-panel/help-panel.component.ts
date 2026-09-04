import {
  AfterViewChecked, Component, ElementRef, EventEmitter, HostListener, Input, OnChanges, OnDestroy,
  OnInit, Output, SimpleChanges,
} from '@angular/core';
import { OverlayContainer } from '@angular/cdk/overlay';
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
export class HelpPanelComponent implements OnInit, OnChanges, AfterViewChecked, OnDestroy {
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

  /**
   * A deep link expands its target, but the panel still opens scrolled to the
   * top, so a target low in the list — any of the flex entries — lands off
   * screen. Set when a target arrives, cleared once it has been scrolled to.
   */
  private pendingTargetScroll = false;

  /** Where the panel host sits when it is not parked in the overlay container. */
  private originalParent: Node | null = null;
  private pendingFocus = false;

  constructor(
    private helpContent: HelpContentService,
    private helpSearch: HelpSearchService,
    private helpPanel: HelpPanelService,
    private host: ElementRef<HTMLElement>,
    private overlayContainer: OverlayContainer,
  ) {}

  /** Chrome labels. Never the shared ngx-translate catalogue. */
  get ui(): HelpUiStrings {
    return this.helpContent.ui();
  }

  get isSearching(): boolean {
    return this.query.trim().length > 0;
  }

  /**
   * True when the query matched nothing and the search handed back the task list
   * instead. The panel says so rather than passing twelve tasks off as hits.
   */
  get isFallback(): boolean {
    return this.results.length > 0 && this.results.every(result => result.fallback === true);
  }

  kindLabel(entry: HelpEntry): string {
    return entry.kind === 'task' ? this.ui.kindTask : this.ui.kindControl;
  }

  ngOnInit(): void {
    this.subscriptions.add(this.helpPanel.isOpen$.subscribe(isOpen => {
      this.isOpen = isOpen;
      if (isOpen) {
        this.buildSections();
        this.moveIntoOverlayContainer();
        this.pendingFocus = true;
      } else {
        this.onQueryChange('');
        this.restoreFromOverlayContainer();
        this.pendingFocus = false;
      }
    }));
    this.subscriptions.add(this.helpPanel.target$.subscribe(target => {
      this.targetId = target;
      this.expanded = target;
      this.pendingTargetScroll = target !== null;
    }));
  }

  /** isAdmin can arrive after the panel is already open; rebuild what it filters. */
  ngOnChanges(changes: SimpleChanges): void {
    if (changes['isAdmin'] && !changes['isAdmin'].firstChange && this.isOpen) {
      this.buildSections();
      this.onQueryChange(this.query);
    }
  }

  ngAfterViewChecked(): void {
    if (this.pendingFocus) {
      // The panel can be opened from inside the modal day-cell dialog, whose
      // focus trap wraps Tab within itself. Handing focus to the search input is
      // what makes the panel reachable at all from there.
      const search = this.host.nativeElement
        .querySelector<HTMLElement>('.tp-help-panel__search input');
      if (search) {
        this.pendingFocus = false;
        search.focus();
      }
    }
    if (!this.pendingTargetScroll) {
      return;
    }
    const target = this.host.nativeElement.querySelector('.tp-help-entry--target');
    if (target) {
      this.pendingTargetScroll = false;
      // Optional call: jsdom and other non-layout hosts do not implement it.
      target.scrollIntoView?.({ block: 'nearest' });
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.isOpen) {
      this.close();
    }
  }

  ngOnDestroy(): void {
    this.restoreFromOverlayContainer();
    this.subscriptions.unsubscribe();
  }

  /**
   * CDK's Dialog marks every body sibling of .cdk-overlay-container
   * aria-hidden="true" while a modal is open, and the day-cell dialog's help
   * icons link into this panel. Left where it is declared, the panel would open
   * hidden from screen readers and behind the dialog. Inside the container it is
   * neither, and it stacks above the dialog pane on DOM order alone — no
   * z-index hack.
   */
  private moveIntoOverlayContainer(): void {
    const host = this.host.nativeElement;
    const container = this.overlayContainer.getContainerElement();
    if (host.parentNode === container) {
      return;
    }
    this.originalParent = this.originalParent ?? host.parentNode;
    container.appendChild(host);
  }

  /** Put the host back before Angular tears the view down around it. */
  private restoreFromOverlayContainer(): void {
    const host = this.host.nativeElement;
    if (this.originalParent && host.parentNode !== this.originalParent) {
      this.originalParent.appendChild(host);
    }
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
