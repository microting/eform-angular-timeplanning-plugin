import { da, daUi } from './da';
import { enUS, enUSUi } from './enUS';
import { HELP_IDS, HelpUiKey } from '../help.model';
import { PLANNING_HELP_ENTRIES } from '../planning-help.registry';

describe('Danish help content', () => {
  it('covers every registry id', () => {
    for (const id of HELP_IDS) {
      expect(da[id]).toBeDefined();
    }
  });

  it('is actually translated, not copied from English', () => {
    const identical = HELP_IDS.filter(id => da[id].short === enUS[id].short);
    expect(identical).toEqual([]);
  });

  it('gives every task Danish steps', () => {
    for (const entry of PLANNING_HELP_ENTRIES.filter(e => e.kind === 'task')) {
      expect(da[entry.id].steps?.length ?? 0).toBeGreaterThan(0);
    }
  });

  it('gives every control a title and short text, and no steps', () => {
    for (const entry of PLANNING_HELP_ENTRIES) {
      const prose = da[entry.id];
      expect(prose.title.length).toBeGreaterThan(0);
      expect(prose.short.length).toBeGreaterThan(0);
      expect(prose.keywords.length).toBeGreaterThan(0);
      if (entry.kind === 'control') {
        expect(prose.steps).toBeUndefined();
      }
    }
  });

  it('carries Danish search keywords the English file does not have', () => {
    const danish = new Set(HELP_IDS.flatMap(id => da[id].keywords));
    for (const word of ['ferie', 'sygdom', 'fri', 'afspadsering', 'barsel']) {
      expect(danish.has(word)).toBe(true);
    }
  });

  it('carries the everyday words a Danish planner actually types', () => {
    const danish = new Set(HELP_IDS.flatMap(id => da[id].keywords));
    for (const word of [
      'løn', 'timer', 'vagt', 'fravær', 'kursus', 'orlov',
      'saldo', 'glemt', 'rettelse', 'feriefridag', 'fridag', 'flex',
    ]) {
      expect(danish.has(word)).toBe(true);
    }
  });

  it('keeps keywords lower case and free of duplicates within an entry', () => {
    for (const id of HELP_IDS) {
      const keywords = da[id].keywords;
      expect(keywords).toEqual(keywords.map(k => k.toLowerCase()));
      expect(new Set(keywords).size).toBe(keywords.length);
    }
  });

  // The Danish labels are not a word-for-word map of the English ones: DayOff is
  // "Fridag", VacationDayOff is "Afspadsering", and TimeOff is "Ferie fridag" — which
  // looks like a day off but keeps the planned hours. The day-type entry has to name
  // all of them the way the checkboxes do.
  it('names the day types that zero the day, and the look-alikes that do not', () => {
    const text = [da['dayCell.flags'].short, da['dayCell.flags'].detail ?? ''].join(' ');
    for (const label of ['Fridag', 'Afspadsering', 'Ferie', 'Ferie fridag', 'nul timer']) {
      expect(text).toContain(label);
    }
  });

  it('never mentions administrators', () => {
    // \w* catches the Danish definite and possessive forms — administratoren,
    // administratorens — which a content author is most likely to reach for.
    const banned = /\badministrator\w*\b|\badmin\b/i;
    for (const id of HELP_IDS) {
      const prose = da[id];
      const text = [prose.title, prose.short, prose.detail ?? '', ...(prose.steps ?? [])].join(' ');
      expect(text).not.toMatch(banned);
    }
  });

  it('translates every chrome label', () => {
    for (const key of Object.keys(enUSUi) as HelpUiKey[]) {
      expect(daUi[key]?.length ?? 0).toBeGreaterThan(0);
    }
  });
});
