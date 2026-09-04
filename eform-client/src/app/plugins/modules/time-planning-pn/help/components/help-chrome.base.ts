import { Directive, inject, Input } from '@angular/core';
import { HelpEntryId, HelpProse, HelpUiStrings } from '../help.model';
import { HelpContentService } from '../services/help-content.service';

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

  get ui(): HelpUiStrings {
    return this.helpContent.ui();
  }
}

/** A help component that renders the prose of one registry entry. */
@Directive()
export abstract class HelpEntryChromeBase extends HelpChromeBase {
  @Input() helpId!: HelpEntryId;

  /** Undefined for an id the registry does not know, so the template renders nothing. */
  get prose(): HelpProse | undefined {
    return this.helpContent.entry(this.helpId) ? this.helpContent.prose(this.helpId) : undefined;
  }
}
