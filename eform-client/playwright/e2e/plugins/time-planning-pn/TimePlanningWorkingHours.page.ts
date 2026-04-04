import { Page } from '@playwright/test';

export class TimePlanningWorkingHoursPage {
  constructor(private page: Page) {}

  async goToWorkingHours() {
    const workingHoursBtn = this.page.locator('#time-planning-pn-working-hours');
    if (!await workingHoursBtn.isVisible()) {
      await this.page.locator('#time-planning-pn').click();
    }
    await workingHoursBtn.click();
  }

  workingHoursExcel() {
    return this.page.locator('#workingHoursExcel');
  }

  workingHoursReload() {
    return this.page.locator('#workingHoursReload');
  }

  workingHoursSave() {
    return this.page.locator('#workingHoursSave');
  }

  workingHoursSite() {
    return this.page.locator('#workingHoursSite');
  }

  workingHoursRange() {
    return this.page.locator('#workingHoursRange');
  }

  dateFormInput() {
    return this.page.locator('mat-date-range-input');
  }
}
