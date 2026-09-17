import { test, expect } from '@playwright/test';
import {
  assertReadOnlyDialog, closeDayWithoutChange, expectSuccess, lastWeekMonday, openDay,
  reconcileDay, tdOf, UNLOCK_WORD, UNRECONCILE_PATH, useLastWeekDashboard, waitForIndex,
  waitForPut, waitForSpinner,
} from './reconcile-helpers';

/**
 * Spec §8.4: only the boundary offers unlock, and unlocking it takes a typed word.
 * The worker is grid row 9 at the start and is found by name after that.
 */
test.describe('Reconciled day lock: unlock', () => {
  const session = useLastWeekDashboard();

  test('only the boundary offers unlock, earlier days name it, and unlocking takes the word', async ({ page }) => {
    const worker = await session.pickWorker(page, 9);
    const boundaryDate = await reconcileDay(page, worker, 3);

    // A day below the boundary names the day to free first and offers no unlock. The
    // line says what releases the day; it does not tell the reader to go and do it,
    // because it is shown to users who have no Unlock button.
    await openDay(page, worker, 1);
    await expect(page.locator('#lockedFreeFirstText'))
      .toHaveText(`Låst, fordi ${boundaryDate} er afstemt. Dagen frigives, når ${boundaryDate} låses op.`);
    await assertReadOnlyDialog(page);
    await closeDayWithoutChange(page);

    // The boundary: its provenance, no "free first" line, and an unlock gated by the word.
    await openDay(page, worker, 3);
    await expect(page.locator('#reconciledProvenanceText')).toBeVisible();
    await expect(page.locator('#lockedFreeFirst')).toHaveCount(0);

    await page.locator('#unlockButton').click();
    await expect(page.locator('#unlockPrompt')).toContainText(UNLOCK_WORD);
    await expect(page.locator('#unlockWordInput')).toBeFocused();
    const confirm = page.locator('#unlockConfirmButton');
    await expect(confirm).toBeDisabled();
    await page.locator('#unlockWordInput').fill('LÅS');
    await expect(confirm).toBeDisabled();
    await page.locator('#unlockWordInput').fill('  lås   op ');
    await expect(confirm).toBeEnabled();

    // Backing out keeps the day reconciled.
    await page.locator('#unlockCancelButton').click();
    await expect(page.locator('#unlockWordInput')).toHaveCount(0);
    await expect(page.locator('#reconciledProvenanceText')).toBeVisible();

    // Enter in the field confirms. It must not submit the surrounding form, whose first
    // button is the version-history button in the title.
    await page.locator('#unlockButton').click();
    await page.locator('#unlockWordInput').fill(UNLOCK_WORD);
    const put = waitForPut(page, UNRECONCILE_PATH);
    const reload = waitForIndex(page, lastWeekMonday());
    await page.locator('#unlockWordInput').press('Enter');
    await expectSuccess(await put);
    await reload;
    await waitForSpinner(page);
    await expect(page.locator('app-version-history-modal')).toHaveCount(0);

    // The line moved back: both days are open again.
    await expect(tdOf(page, worker, 3)).not.toHaveClass(/reconciled-background/);
    await expect(tdOf(page, worker, 1)).not.toHaveClass(/locked-background/);
  });
});
