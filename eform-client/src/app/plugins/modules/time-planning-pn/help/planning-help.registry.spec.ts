import { PLANNING_HELP_ENTRIES } from './planning-help.registry';
import { enUS } from './i18n/enUS';
import { HelpEntry, HelpEntryId } from './help.model';

describe('planning help registry', () => {
  const byId = new Map<HelpEntryId, HelpEntry>(
    PLANNING_HELP_ENTRIES.map(e => [e.id, e]),
  );

  it('has no duplicate ids', () => {
    expect(byId.size).toBe(PLANNING_HELP_ENTRIES.length);
  });

  it('gives every entry English prose with a title, short text and a keyword', () => {
    for (const entry of PLANNING_HELP_ENTRIES) {
      const prose = enUS[entry.id];
      expect(prose).toBeDefined();
      expect(prose.title.length).toBeGreaterThan(0);
      expect(prose.short.length).toBeGreaterThan(0);
      expect(prose.keywords.length).toBeGreaterThan(0);
    }
  });

  it('gives every task steps, and no control steps', () => {
    for (const entry of PLANNING_HELP_ENTRIES) {
      const prose = enUS[entry.id];
      if (entry.kind === 'task') {
        expect(prose.steps?.length ?? 0).toBeGreaterThan(0);
      } else {
        expect(prose.steps).toBeUndefined();
      }
    }
  });

  it('keeps tasks out of tours and off the page', () => {
    for (const entry of PLANNING_HELP_ENTRIES.filter(e => e.kind === 'task')) {
      expect(entry.anchor).toBeUndefined();
      expect(entry.tourStep).toBeUndefined();
      expect(entry.tour).toBeUndefined();
    }
  });

  it('gives every tour step a tour and an anchor', () => {
    for (const entry of PLANNING_HELP_ENTRIES.filter(e => e.tourStep !== undefined)) {
      expect(entry.tour).toBeDefined();
      expect(entry.anchor).toBeDefined();
    }
  });

  it('numbers tour steps uniquely within each tour', () => {
    for (const tour of ['page', 'dialog'] as const) {
      const steps = PLANNING_HELP_ENTRIES
        .filter(e => e.tour === tour && e.tourStep !== undefined)
        .map(e => e.tourStep as number);
      expect(new Set(steps).size).toBe(steps.length);
      expect(steps.length).toBeGreaterThan(0);
    }
  });

  it('ends the page tour by inviting the user to open a day', () => {
    // The tour's closing move is meant to hand the planner the thing they came to
    // do, not an export they may never touch.
    const pageSteps = PLANNING_HELP_ENTRIES
      .filter(e => e.tour === 'page' && e.tourStep !== undefined)
      .sort((a, b) => (a.tourStep as number) - (b.tourStep as number));
    expect(pageSteps.length).toBeGreaterThan(1);
    expect(pageSteps[pageSteps.length - 1].id).toBe('grid.openDay');
  });

  it('resolves every related id', () => {
    for (const entry of PLANNING_HELP_ENTRIES) {
      for (const related of entry.related ?? []) {
        expect(byId.has(related)).toBe(true);
      }
    }
  });

  it('marks exactly one entry admin-only', () => {
    const adminOnly = PLANNING_HELP_ENTRIES.filter(e => e.adminOnly);
    expect(adminOnly.map(e => e.id)).toEqual(['toolbar.payrollExport']);
  });

  it('never mentions administrators in user-facing copy', () => {
    const banned = /\badmin(istrator)?s?\b/i;
    for (const entry of PLANNING_HELP_ENTRIES) {
      const prose = enUS[entry.id];
      const text = [prose.title, prose.short, prose.detail ?? '', ...(prose.steps ?? [])].join(' ');
      expect(text).not.toMatch(banned);
    }
  });
});
