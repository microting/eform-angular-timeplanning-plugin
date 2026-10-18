import { afterAll, beforeAll } from 'vitest';

/**
 * CDK overlays watch their pane with a MutationObserver when they detach. The
 * `ng test` DOM hands back an observer without `observe`, so closing a
 * connected overlay throws. Call inside a `describe` whose specs open and close
 * overlays; it installs a no-op observer only when the environment's is unusable.
 */
export function useWorkingMutationObserver(): void {
  const original = globalThis.MutationObserver;
  beforeAll(() => {
    if (typeof original?.prototype?.observe === 'function') {
      return;
    }
    globalThis.MutationObserver = class {
      observe(): void { /* no-op */ }
      disconnect(): void { /* no-op */ }
      takeRecords(): MutationRecord[] { return []; }
    } as unknown as typeof MutationObserver;
  });
  afterAll(() => {
    globalThis.MutationObserver = original;
  });
}
