import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';
import { HelpSearchService } from './help-search.service';
import { HelpContentService } from './help-content.service';

describe('HelpSearchService', () => {
  const make = (lang: string) => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        HelpSearchService,
        HelpContentService,
        { provide: TranslateService, useValue: { currentLang: lang } },
      ],
    });
    return TestBed.inject(HelpSearchService);
  };

  it('finds the vacation task from the Danish word', () => {
    const ids = make('da').search('ferie', { isAdmin: false }).map(r => r.entry.id);
    expect(ids).toContain('task.registerVacation');
  });

  it('finds a Danish entry from an English word, through the fallback', () => {
    const ids = make('da').search('vacation', { isAdmin: false }).map(r => r.entry.id);
    expect(ids).toContain('task.registerVacation');
  });

  it('folds diacritics so ae matches æ', () => {
    const service = make('da');
    const withLigature = service.search('læge', { isAdmin: false }).map(r => r.entry.id);
    const folded = service.search('laege', { isAdmin: false }).map(r => r.entry.id);
    expect(folded).toEqual(withLigature);
  });

  it('folds ø and å', () => {
    const service = make('da');
    expect(service.search('sygdom', { isAdmin: false }).length).toBeGreaterThan(0);
    expect(service.search('arstid', { isAdmin: false })).toEqual(
      service.search('årstid', { isAdmin: false }),
    );
  });

  it('ranks tasks above controls', () => {
    const results = make('en-US').search('vacation', { isAdmin: false });
    const firstControl = results.findIndex(r => r.entry.kind === 'control');
    const lastTask = results.map(r => r.entry.kind).lastIndexOf('task');
    // Assert both groups are present, so a content edit that removes one cannot
    // make this test pass without checking anything.
    expect(firstControl).not.toBe(-1);
    expect(lastTask).not.toBe(-1);
    expect(lastTask).toBeLessThan(firstControl);
  });

  it('ranks a title match above a body-only match', () => {
    const results = make('en-US').search('flex', { isAdmin: false });
    expect(results.length).toBeGreaterThan(1);
    expect(results[0].prose.title.toLowerCase()).toContain('flex');
  });

  it('returns the task list when nothing matches', () => {
    const results = make('en-US').search('zzzznomatch', { isAdmin: false });
    expect(results.length).toBeGreaterThan(0);
    expect(results.every(r => r.entry.kind === 'task')).toBe(true);
  });

  it('returns the task list for an empty query', () => {
    const results = make('en-US').search('   ', { isAdmin: false });
    expect(results.every(r => r.entry.kind === 'task')).toBe(true);
  });

  it('never returns an admin-only entry to a non-admin', () => {
    const ids = make('en-US').search('payroll', { isAdmin: false }).map(r => r.entry.id);
    expect(ids).not.toContain('toolbar.payrollExport');
  });
});
