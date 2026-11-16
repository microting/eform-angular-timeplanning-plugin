import { expect, Page, test } from '@playwright/test';
import { Api, BASE_URL, waitForGrid } from './planner-helpers';

/**
 * The "Planlægning (beta)" menu entry is the only one in the plugin's menu
 * behind a permission of its own (time_planning_planner, "Use the planner
 * (beta)"). An admin holds every permission, so this spec signs in as a plain
 * user in a security group of its own and grants the permission to that group
 * through the plugin's group permissions, the same call the host's
 * "Plugin permissions" dialog makes.
 */
const GROUP_NAME = 'Planner t users';
const USER = { firstName: 'Planner', lastName: 'Tester', email: 'planner.tester@microting.test', password: 'secretpassword' };
const PLUGIN_ID = 'eform-angular-time-planning-plugin';
const PLANNER_CLAIM = 'time_planning_planner';
const ACCESS_CLAIM = 'time_planning_plugin_access';

/** Turns the planner permission on or off for the spec's group; the plugin access stays on. */
async function setPlannerPermission(api: Api, groupId: number, enabled: boolean): Promise<void> {
  const installed = await api.call('GET', '/api/plugins-management/installed');
  const plugin = installed.pluginsList.find((p: any) => p.pluginId === PLUGIN_ID);
  expect(plugin, 'the time planning plugin must be installed').toBeTruthy();
  const permissions = await api.call('GET', `/api/plugins-permissions/${plugin.id}`);
  const groups = (await api.call('GET', `/api/plugins-permissions/group-permissions/${plugin.id}`))
    .filter((g: any) => g.groupId !== groupId);
  groups.push({
    groupId,
    permissions: permissions.map((p: any) => ({
      ...p,
      isEnabled: p.claimName === ACCESS_CLAIM || (enabled && p.claimName === PLANNER_CLAIM),
    })),
  });
  await api.call('PUT', `/api/plugins-permissions/group-permissions/${plugin.id}`, groups);
}

async function loginAsUser(page: Page): Promise<void> {
  await page.goto(BASE_URL);
  await page.locator('#loginBtn').waitFor({ state: 'visible', timeout: 60000 });
  await page.locator('#username').fill(USER.email);
  await page.locator('#password').fill(USER.password);
  await page.locator('#loginBtn').click();
  const menu = page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' });
  await menu.waitFor({ state: 'visible', timeout: 120000 });
  await menu.click();
  // Another entry of the same menu: the menu is open and loaded, so a missing planner entry means something.
  await expect(page.locator('#time-planning-pn-working-hours')).toBeVisible();
}

test.describe('Planner - menu entry follows the planner permission', () => {
  test.describe.configure({ mode: 'serial' });
  let groupId: number;

  test.beforeAll(async () => {
    const api = await Api.signIn();
    await api.call('POST', '/api/security/groups', { name: GROUP_NAME, userIds: [] });
    const groups = await api.call('GET', '/api/security/groups/dictionary');
    groupId = groups.find((g: any) => g.name === GROUP_NAME).id;
    await api.call('POST', '/api/admin/create-user', {
      ...USER, userName: USER.email, passwordConfimation: USER.password, role: 'user', groupId,
    });
    await setPlannerPermission(api, groupId, false);
    await api.dispose();
  });

  test('is hidden without the permission', async ({ page }) => {
    await loginAsUser(page);
    await expect(page.locator('#time-planning-pn-planner')).toHaveCount(0);
  });

  test('is shown with the permission and opens the planner', async ({ page }) => {
    const api = await Api.signIn();
    await setPlannerPermission(api, groupId, true);
    await api.dispose();

    await loginAsUser(page);
    const entry = page.locator('#time-planning-pn-planner');
    await expect(entry).toBeVisible();
    await expect(entry).toContainText('Planlægning (beta)');
    const grid = waitForGrid(page);
    await entry.click();
    // The planner's API lets the user in (no 401/403); what the user may plan is the scope's business.
    expect((await grid).status()).toBe(200);
    await expect(page).toHaveURL(/\/plugins\/time-planning-pn\/planner$/);
    await expect(page.locator('#planner-grid')).toBeVisible();
  });
});
