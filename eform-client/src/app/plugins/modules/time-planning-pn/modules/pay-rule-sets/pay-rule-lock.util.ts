import { PAY_RULE_SET_PRESETS } from '../../models';

/**
 * Trailing agreement validity period, e.g. ` 2024-2026` or ` 2026–2029`.
 * Matched with hyphen, en-dash and em-dash so a stored name written with a
 * typographic dash still normalizes to the same value.
 */
const VALIDITY_PERIOD_SUFFIX = /\s+\d{4}\s*[-–—]\s*\d{4}$/;

/**
 * Strips the trailing validity period from a pay rule set name so that names
 * differing only by agreement period compare equal.
 *
 * `'GLS-A / 3F - Jordbrug Dyrehold 2024-2026'` and
 * `'GLS-A / 3F - Jordbrug Dyrehold 2026-2029'` both normalize to
 * `'GLS-A / 3F - Jordbrug Dyrehold'`.
 */
export function normalizePayRuleSetName(name: string): string {
  if (!name) {
    return '';
  }
  return name.trim().replace(VALIDITY_PERIOD_SUFFIX, '').trim();
}

/**
 * True when the given name matches — ignoring the validity period — a preset
 * entry flagged as locked (e.g. GLS-A / 3F overenskomster). Rule sets created
 * before a catalogue rename stay locked because only the period differs.
 */
export function isLockedPresetName(name: string): boolean {
  const normalized = normalizePayRuleSetName(name);
  if (!normalized) {
    return false;
  }
  return PAY_RULE_SET_PRESETS.some(
    p => p.locked && normalizePayRuleSetName(p.name) === normalized
  );
}
