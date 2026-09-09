import { Injectable } from '@angular/core';
import { HelpEntry, HelpEntryId, HelpProse } from '../help.model';
import { HELP_FALLBACK } from '../i18n';
import { HelpContentService } from './help-content.service';

export interface HelpSearchResult {
  entry: HelpEntry;
  prose: HelpProse;
  /**
   * True when this result is part of the task list handed back because the query
   * matched nothing (or was empty), rather than a match on the query itself. A
   * help search must never dead-end, so the caller shows these — but it has to be
   * able to say so instead of passing them off as hits.
   */
  fallback?: boolean;
}

/** Match location, lower is better. */
const RANK_TITLE = 0;
const RANK_KEYWORD = 1;
const RANK_BODY = 2;
const RANK_NONE = 99;

const LIGATURES: Record<string, string> = { æ: 'ae', ø: 'o', Æ: 'ae', Ø: 'o' };

export function fold(value: string): string {
  return value
    .replace(/[æøÆØ]/g, char => LIGATURES[char])
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .trim();
}

@Injectable({ providedIn: 'root' })
export class HelpSearchService {
  constructor(private helpContent: HelpContentService) {}

  search(query: string, opts: { isAdmin: boolean }): HelpSearchResult[] {
    const needle = fold(query);
    const entries = this.helpContent.entries(opts);

    if (!needle) {
      return this.tasksOnly(entries);
    }

    const ranked = entries
      .map(entry => ({ entry, prose: this.helpContent.prose(entry.id), rank: this.rank(entry.id, needle) }))
      .filter(result => result.rank !== RANK_NONE);

    if (!ranked.length) {
      return this.tasksOnly(entries);
    }

    return ranked
      .sort((a, b) =>
        (a.entry.kind === 'task' ? 0 : 1) - (b.entry.kind === 'task' ? 0 : 1) ||
        a.rank - b.rank)
      .map(({ entry, prose }) => ({ entry, prose }));
  }

  /** Best match location across the active locale and the English fallback. */
  private rank(id: HelpEntryId, needle: string): number {
    const candidates = [this.helpContent.prose(id), HELP_FALLBACK[id]];
    let best = RANK_NONE;

    for (const prose of candidates) {
      if (fold(prose.title).includes(needle)) {
        return RANK_TITLE;
      }
      if (prose.keywords.some(keyword => fold(keyword).includes(needle))) {
        // A keyword match already beats any body match, so skip the body scan.
        best = Math.min(best, RANK_KEYWORD);
        continue;
      }
      const body = [prose.short, prose.detail ?? '', ...(prose.steps ?? [])].join(' ');
      if (fold(body).includes(needle)) {
        best = Math.min(best, RANK_BODY);
      }
    }

    return best;
  }

  private tasksOnly(entries: HelpEntry[]): HelpSearchResult[] {
    return entries
      .filter(entry => entry.kind === 'task')
      .map(entry => ({ entry, prose: this.helpContent.prose(entry.id), fallback: true }));
  }
}
