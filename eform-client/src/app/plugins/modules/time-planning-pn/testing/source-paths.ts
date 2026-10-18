import { join } from 'path';

/**
 * Specs that read source files from disk. Angular's unit-test builder bundles
 * each spec, so `__dirname` no longer points next to the spec. `ng test` runs
 * from the host's eform-client directory (locally and in CI), so paths are
 * resolved from the working directory instead.
 */
export const CLIENT_ROOT = process.cwd();
export const MODULE_ROOT = join(CLIENT_ROOT, 'src', 'app', 'plugins', 'modules', 'time-planning-pn');
