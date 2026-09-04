import { HELP_LOCALES } from './index';
import { HelpProse } from '../help.model';

/**
 * The day-type checkboxes are the highest-value thing the help explains, and the two
 * ways to get the copy wrong are the same in every language:
 *
 *  1. Claiming the two opposite types sit next to each other. They do not — the render
 *     order is Day off, Vacation, Sick, Course, Leave of absence, Children 1st sick day,
 *     Children 2st sick day, Time off, Maternity leave, Vacation day off, Holiday,
 *     Pregnancy-related absence, so Vacation is 2nd and Vacation day off is 10th.
 *  2. Leaving out the confusion that actually bites: one type is named like the two that
 *     zero the day, but behaves like the ones that keep the planned hours.
 *
 * These rules hold for every locale, so this spec walks the registered locales rather
 * than one content file.
 */
describe('day type copy in every locale', () => {
  const ADJACENCY = /next to each other|side by side|adjacent|ved siden af hinanden/i;

  interface LocaleExpectation {
    /** The type that keeps the planned hours despite being named like a day off. */
    lookAlike: string;
    /** The two types that rewrite the day to zero hours. */
    zeroing: [string, string];
    /** How this language says "the hours planned for the day". */
    keepsPlanned: RegExp;
    /** How this language says "zero hours". */
    zeroHours: RegExp;
  }

  const EXPECTED: Record<string, LocaleExpectation> = {
    'en-US': {
      lookAlike: 'Time off',
      zeroing: ['Day off', 'Vacation day off'],
      keepsPlanned: /planned/i,
      zeroHours: /zero hours/i,
    },
    da: {
      lookAlike: 'Ferie fridag',
      zeroing: ['Fridag', 'Afspadsering'],
      keepsPlanned: /planlagt/i,
      zeroHours: /nul timer/i,
    },
  };

  const flagsText = (locale: string): string => {
    const prose = HELP_LOCALES[locale]?.['dayCell.flags'] as HelpProse | undefined;
    expect(prose).toBeDefined();
    return [(prose as HelpProse).short, (prose as HelpProse).detail ?? ''].join(' ');
  };

  for (const [locale, expected] of Object.entries(EXPECTED)) {
    it(`${locale} never claims the opposite day types sit next to each other`, () => {
      expect(flagsText(locale)).not.toMatch(ADJACENCY);
    });

    it(`${locale} warns that ${expected.lookAlike} keeps the planned hours despite its name`, () => {
      const text = flagsText(locale);
      expect(text).toContain(expected.lookAlike);
      for (const zeroing of expected.zeroing) {
        expect(text).toContain(zeroing);
      }
      expect(text).toMatch(expected.keepsPlanned);
      expect(text).toMatch(expected.zeroHours);
    });
  }

  it('registers a locale for every expectation above', () => {
    for (const locale of Object.keys(EXPECTED)) {
      expect(HELP_LOCALES[locale]).toBeDefined();
    }
  });
});
