import { Directive, inject, Input } from '@angular/core';
import { HelpEntryId, HelpProse, HelpUiStrings } from '../help.model';
import { HelpContentService } from '../services/help-content.service';
import { HelpVisibilityService } from '../services/help-visibility.service';

/**
 * Chrome labels for the help components. They come from HelpUiStrings via
 * HelpContentService and never from the plugin's 25 shared ngx-translate locale
 * files, which this feature must not add keys to — so every help component needs
 * the same one-line accessor, and it lives here rather than four times over.
 *
 * Abstract and unselected: it is never declared in a module, only extended.
 */
@Directive()
export abstract class HelpChromeBase {
  protected readonly helpContent = inject(HelpContentService);
  protected readonly helpVisibility = inject(HelpVisibilityService);

  get ui(): HelpUiStrings {
    return this.helpContent.ui();
  }

  /** Whether help exists for this user at all. See HelpVisibilityService. */
  get isVisible(): boolean {
    return this.helpVisibility.isVisible;
  }
}

/** A help component that renders the prose of one registry entry. */
@Directive()
export abstract class HelpEntryChromeBase extends HelpChromeBase {
  @Input() helpId!: HelpEntryId;

  /**
   * Undefined for an id the registry does not know, so the template renders
   * nothing — and undefined for a non-admin, for the same reason. Both the icon
   * and the hint template are wrapped in `*ngIf="prose as ..."`, so this one
   * getter is what hides every ⓘ and every inline hint at once, rather than an
   * *ngIf repeated at each of the eighteen call sites.
   */
  get prose(): HelpProse | undefined {
    if (!this.isVisible) {
      return undefined;
    }
    return this.helpContent.entry(this.helpId) ? this.helpContent.prose(this.helpId) : undefined;
  }
}
