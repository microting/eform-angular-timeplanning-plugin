import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';

/**
 * Plannings-table overview "Samlet pause" contract: when an AssignedSite has
 * UseOneMinuteIntervals=true and a worker has a sub-slot pause (e.g.
 * Pause10StartedAt/Pause10StoppedAt), the day cell must show the correct
 * minute count instead of 00:00. Regression test for the bug fix introduced
 * in commit 6ebc5fe6 + 43da9f2e (PR #1575) where the backend only iterated
 * the 5 main pause slots and missed sub-slot stamps.
 *
 * Awaits DB fixture seeding for sub-slot pause stamps (the b1m CI shard's
 * default seed flips UseOneMinuteIntervals on but does NOT write to Pause10*
 * columns). Once that fixture lands, un-skip and the assertion below should
 * pass.
 *
 * Until the fixture lands, the helper-level unit test
 *   PlanRegistrationHelperTests.AggregatePauseMinutes_OneMinuteInterval_3MinPauseInPause10SubSlot
 * (eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/PlanRegistrationHelperTests.cs)
 * plus the service-level call-site test
 *   PlanningServiceMultiShiftTests.Index_OneMinuteInterval_WithSubSlotPauseStamps_AggregatesCorrectly
 * (same dir) cover the format-helper + aggregation contract on the
 * Index → UpdatePlanRegistrationsInPeriod → AggregatePauseMinutes chain.
 */

async function waitForSpinner(page: Page) {
  if (await page.locator('.overlay-spinner').count() > 0) {
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
  }
}

test.describe('Dashboard — Samlet pause aggregation for sub-slot stamps (b1m, flag-on)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
  });

  test.skip('plannings-table cell renders correct Samlet pause for sub-slot pause when flag is on', async ({ page }) => {
    // TODO(fixture): seed AssignedSite.UseOneMinuteIntervals = true on the
    // worker referenced by #cell3_0 AND a PlanRegistration row with
    //   Pause10StartedAt = '2026-05-14T12:00:00Z'
    //   Pause10StoppedAt = '2026-05-14T12:03:00Z'
    // on a date inside the dashboard's default visible range.
    //
    // Then the assertion shape is:
    //
    //   await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    //   await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    //   await waitForSpinner(page);
    //
    //   const totalBreak = page.locator('[id^="totalBreakTime"]').first();
    //   await expect(totalBreak).toContainText('00:03');
    //   // Negative guard — must not silently show 00:00 even when a sub-slot pause exists.
    //   await expect(totalBreak).not.toContainText('00:00');
    //
    // The unit-test layer covers the format-helper contract:
    //   PlanRegistrationHelperTests.AggregatePauseMinutes_OneMinuteInterval_3MinPauseInPause10SubSlot
    // (eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/PlanRegistrationHelperTests.cs).
  });
});
