import { existsSync, readFileSync } from 'fs';
import { join } from 'path';
import { TOUR_STORAGE_KEY } from './services/help-tour.service';

/**
 * The planning page starts its onboarding tours automatically the first time a
 * planner lands on it, which is exactly what every Playwright context looks like:
 * a fresh profile with empty localStorage. An unseeded run therefore paints a tour
 * card over the top grid rows and, inside the day-cell dialog, over the shift-1
 * fields — and Playwright's actionability check fails on an intercepting overlay,
 * turning the whole e2e matrix red.
 *
 * The seed below is what prevents that. It is easy to delete by accident and its
 * link to the app is a bare string, so this test asserts both halves: the file
 * exists and names the key HelpTourService actually reads, and the config points
 * at the file.
 */
const CLIENT_ROOT = join(__dirname, '..', '..', '..', '..', '..', '..');
const SEED_RELATIVE = 'playwright/helpers/tour-seen.storage.json';
const SEED_PATH = join(CLIENT_ROOT, SEED_RELATIVE);
const CONFIG_PATH = join(CLIENT_ROOT, 'playwright.config.ts');

interface StorageStateFile {
  origins?: { origin: string; localStorage?: { name: string; value: string }[] }[];
}

describe('playwright tour seed', () => {
  it('ships a storage-state file where the config and CI expect it', () => {
    expect(existsSync(SEED_PATH)).toBe(true);
  });

  it('seeds the key HelpTourService reads, for the origin the config runs against', () => {
    const config = readFileSync(CONFIG_PATH, 'utf8');
    const baseUrl = /baseURL:\s*'([^']+)'/.exec(config);
    expect(baseUrl).not.toBeNull();

    const seed = JSON.parse(readFileSync(SEED_PATH, 'utf8')) as StorageStateFile;
    const origin = (seed.origins ?? [])
      .find(candidate => candidate.origin === (baseUrl as RegExpExecArray)[1]);
    expect(origin).toBeDefined();

    const entry = (origin?.localStorage ?? []).find(item => item.name === TOUR_STORAGE_KEY);
    expect(entry).toBeDefined();
    // The value has to list both tours, or the surface it omits still auto-starts.
    expect(JSON.parse((entry as { value: string }).value).sort()).toEqual(['dialog', 'page']);
  });

  it('wires the seed into the config, or it is a file nothing reads', () => {
    const config = readFileSync(CONFIG_PATH, 'utf8');
    expect(config).toMatch(new RegExp(`storageState:\\s*'${SEED_RELATIVE}'`));
  });
});
