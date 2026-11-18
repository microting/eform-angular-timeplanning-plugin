import { expect, test } from '@playwright/test';
import {
  Api, createPlainUser, grantPlannerPermission, mondayKey, signInAs, waitForSpinner,
} from './planner-helpers';

/**
 * "Registrér fravær" for a user who may plan but is nobody's leader. Found on a
 * real tenant: every worker was a disabled option, the only explanation could
 * never appear, and nothing said why the button stayed disabled.
 *
 * The user is a plain user with the planner permission and no worker of their
 * own. The server does not show such a user any workers (no own site), so the
 * grid is answered with the admin's grid with every canApproveAbsence false –
 * exactly what a non-admin, non-leader gets for workers in their scope.
 */
const GROUP_NAME = 'Planner t no leader';
const USER = { firstName: 'Planner', lastName: 'Noleader', email: 'planner.noleader@microting.test', password: 'secretpassword' };

test.describe('Planner - absence for a user who is nobody\'s leader', () => {
  let grid: any;

  test.beforeAll(async () => {
    const api = await Api.signIn();
    const groupId = await createPlainUser(api, GROUP_NAME, USER);
    await grantPlannerPermission(api, groupId);
    grid = await api.grid(mondayKey(), 14);
    await api.dispose();
    expect(grid.workers.length, 'the seed has workers').toBeGreaterThan(1);
    for (const worker of grid.workers) {
      worker.canApproveAbsence = false;
    }
  });

  test('explains why nothing can be registered, and keeps the workers selectable', async ({ page }) => {
    await page.route(url => url.pathname === '/api/time-planning-pn/planner/grid', route =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, model: grid }) }));
    // No preview is asked for a worker the user may not decide for.
    const previews: string[] = [];
    page.on('request', r => {
      if (r.url().includes('/planner/absence/preview')) {
        previews.push(r.url());
      }
    });

    await signInAs(page, USER.email, USER.password);
    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    await page.locator('#time-planning-pn-planner').click();
    await expect(page.locator('#planner-grid tbody tr').first()).toBeVisible();
    await waitForSpinner(page);

    await page.locator('#planner-absence-open').click();
    await expect(page.locator('#planner-absence-dialog')).toBeVisible();

    // The reason, and where a leader is set up.
    const message = page.locator('[data-testid="planner-absence-no-approvable"]');
    await expect(message).toBeVisible();
    await expect(message).toContainText('ikke er sat op som leder');
    await expect(message).toContainText('Plantimer');
    await expect(message).toContainText('Er leder');

    // What is missing, while the button is disabled. "Ferie" is preselected.
    const save = page.locator('#planner-absence-save');
    await expect(save).toBeDisabled();
    await expect(page.locator('#planner-absence-type-2')).toHaveAttribute('aria-checked', 'true');
    await expect(page.locator('[data-testid="planner-absence-missing"]'))
      .toHaveText('Mangler: vælg en medarbejder, vælg fra- og til-dato');

    // Every worker is still an option (not disabled), so choosing one explains it.
    await page.locator('#planner-absence-worker').click();
    const options = page.locator('.cdk-overlay-pane mat-option');
    await expect(options).toHaveCount(grid.workers.length);
    await expect(options.first()).not.toHaveAttribute('aria-disabled', 'true');
    await expect(options.first()).toContainText('ikke leder');
    await options.first().click();

    await expect(page.locator('[data-testid="planner-absence-not-manager"]')).toHaveText('Du er ikke leder for denne medarbejder.');
    await expect(page.locator('[data-testid="planner-absence-missing"]'))
      .toHaveText('Mangler: du er ikke leder for medarbejderen, vælg fra- og til-dato');
    await expect(save).toBeDisabled();
    expect(previews).toEqual([]);
  });
});
