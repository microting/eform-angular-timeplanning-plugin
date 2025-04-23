import loginPage from '../../../Login.page';
import pluginPage from '../../../Plugin.page';

describe('Enable Backend Config plugin', () => {
  beforeEach(() => {
    cy.visit('http://localhost:4200');
    loginPage.login();
    pluginPage.Navbar.goToPluginsPage();
  });

  it('should validate default Time registration plugin settings', () => {
    const pluginName = 'Microting Time Planning Plugin';
    pluginPage.enablePluginByName(pluginName);
    let row = cy.contains('.mat-mdc-row', pluginName).first();
    row.find('.mat-column-actions button')
      .should('contain.text', 'toggle_on'); // plugin is enabled
    row = cy.contains('.mat-mdc-row', pluginName).first();
    row.find('.mat-column-actions a')
      .should('contain.text', 'settings'); // plugin is enabled
    row = cy.contains('.mat-mdc-row', pluginName).first();
    let settingsElement = row
      .find('.mat-column-actions a')
      // .should('be.enabled')
      .should('be.visible');
    settingsElement.click();

    const googleSheetIdInputField = cy.get('.flex-cards.mt-3 mat-form-field');
    googleSheetIdInputField
      .should('have.length', 1)
      .should('be.visible');
    googleSheetIdInputField
      .should('have.attr', 'class')
      .and('not.include', 'mat-form-field-disabled');

    const disabledInputFields = cy.get('.flex-cards.mt-4 mat-form-field');
    disabledInputFields
      .should('have.length', 21)
      .should('be.visible');
    disabledInputFields
      .should('have.attr', 'class')
      .and('include', 'mat-form-field-disabled');


    const dayBreakMinutesDividerValues = [
      '03:00',
      '03:00',
      '03:00',
      '03:00',
      '03:00',
      '02:00',
      '02:00'
    ];
    const dayBreakMinutesPrDividerValues = [
      '00:30',
      '00:30',
      '00:30',
      '00:30',
      '00:30',
      '00:30',
      '00:30'
    ];
    const dayBreakMinutesUpperLimitValues = [
      '01:00',
      '01:00',
      '01:00',
      '01:00',
      '01:00',
      '01:00',
      '01:00'
    ];

    const daysOfWeek = ['monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday', 'sunday'];
    daysOfWeek.forEach((day, index) => {
      const breakMinutesDividerFieldId = `${day}BreakMinutesDivider`;
      let breakMinutesDividerInputField = cy.get(`#${breakMinutesDividerFieldId}`);
      breakMinutesDividerInputField
        .should('have.length', 1)
        .should('be.visible');
      breakMinutesDividerInputField
        .should('have.attr', 'disabled');

      breakMinutesDividerInputField = cy.get(`#${breakMinutesDividerFieldId}`);
      breakMinutesDividerInputField
        .should('include.value', dayBreakMinutesDividerValues[index]);

      const breakMinutesPrDividerFieldId = `${day}BreakMinutesPrDivider`;
      let breakMinutesPrDividerInputField = cy.get(`#${breakMinutesPrDividerFieldId}`);
      breakMinutesPrDividerInputField
        .should('have.length', 1)
        .should('be.visible');
      breakMinutesPrDividerInputField
        .should('have.attr', 'disabled');

      breakMinutesPrDividerInputField = cy.get(`#${breakMinutesPrDividerFieldId}`);
      breakMinutesPrDividerInputField
        .should('include.value', dayBreakMinutesPrDividerValues[index]);

      const breakMinutesUpperLimitFieldId = `${day}BreakMinutesUpperLimit`;
      let breakMinutesUpperLimitInputField = cy.get(`#${breakMinutesUpperLimitFieldId}`);
      breakMinutesUpperLimitInputField
        .should('have.length', 1)
        .should('be.visible');
      breakMinutesUpperLimitInputField
        .should('have.attr', 'disabled');

      breakMinutesUpperLimitInputField = cy.get(`#${breakMinutesUpperLimitFieldId}`);
      breakMinutesUpperLimitInputField
        .should('include.value', dayBreakMinutesUpperLimitValues[index]);
    });

    let autoBreakCalculationToggle = cy.get('mat-slide-toggle');
    autoBreakCalculationToggle
      .should('be.visible')
      .should('not.be.checked');
    autoBreakCalculationToggle.click();
    autoBreakCalculationToggle = cy.get('mat-slide-toggle div button');
    autoBreakCalculationToggle
      .should('have.attr', 'aria-checked', 'true');

    const enabledInputFields = cy.get('.flex-cards.mt-4 mat-form-field');
    enabledInputFields
      .should('have.length', 21)
      .should('be.visible');
    enabledInputFields
      .should('have.attr', 'class')
      .and('not.include', 'mat-form-field-disabled');
  });

  it('should activate auto calculation break times', () => {
    const pluginName = 'Microting Time Planning Plugin';
    // pluginPage.enablePluginByName(pluginName);
    let row = cy.contains('.mat-mdc-row', pluginName).first();
    row.find('.mat-column-actions button')
      .should('contain.text', 'toggle_on'); // plugin is enabled
    row = cy.contains('.mat-mdc-row', pluginName).first();
    row.find('.mat-column-actions a')
      .should('contain.text', 'settings'); // plugin is enabled
    row = cy.contains('.mat-mdc-row', pluginName).first();
    let settingsElement = row
      .find('.mat-column-actions a')
      // .should('be.enabled')
      .should('be.visible');
    settingsElement.click();

    const googleSheetIdInputField = cy.get('.flex-cards.mt-3 mat-form-field');
    googleSheetIdInputField
      .should('have.length', 1)
      .should('be.visible');
    googleSheetIdInputField
      .should('have.attr', 'class')
      .and('not.include', 'mat-form-field-disabled');

    const disabledInputFields = cy.get('.flex-cards.mt-4 mat-form-field');
    disabledInputFields
      .should('have.length', 21)
      .should('be.visible');
    disabledInputFields
      .should('have.attr', 'class')
      .and('include', 'mat-form-field-disabled');


    const newDayBreakMinutesDividerValues = [
      '04:00',
      '05:00',
      '06:00',
      '07:00',
      '08:00',
      '09:00',
      '10:00'
    ];
    const newDayBreakMinutesPrDividerValues = [
      '01:30',
      '02:35',
      '03:40',
      '04:45',
      '05:50',
      '06:55',
      '07:30'
    ];
    const newDayBreakMinutesUpperLimitValues = [
      '02:05',
      '03:10',
      '04:15',
      '05:20',
      '06:25',
      '07:35',
      '08:40'
    ];

    let autoBreakCalculationToggle = cy.get('mat-slide-toggle');
    autoBreakCalculationToggle
      .should('be.visible')
      .should('not.be.checked');
    autoBreakCalculationToggle.click();
    autoBreakCalculationToggle = cy.get('mat-slide-toggle div button');
    autoBreakCalculationToggle
      .should('have.attr', 'aria-checked', 'true');

    const enabledInputFields = cy.get('.flex-cards.mt-4 mat-form-field');
    enabledInputFields
      .should('have.length', 21)
      .should('be.visible');
    enabledInputFields
      .should('have.attr', 'class')
      .and('not.include', 'mat-form-field-disabled');
    const daysOfWeek = ['monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday', 'sunday'];
    daysOfWeek.forEach((day, index) => {
      // set new values for break minutes divider
      const breakMinutesDividerFieldId = `${day}BreakMinutesDivider`;
      let breakMinutesDividerInputField = cy.get(`#${breakMinutesDividerFieldId}`);
      breakMinutesDividerInputField
        .should('have.length', 1)
        .should('be.visible');
      breakMinutesDividerInputField
        .should('have.attr', 'class')
        .and('not.include', 'mat-form-field-disabled');

      /* ==== Generated with Cypress Studio ==== */
      cy.get(`#${breakMinutesDividerFieldId}`).click();
      // eslint-disable-next-line max-len
      let degrees = 360 / 12 * parseInt(newDayBreakMinutesDividerValues[index].split(':')[0]);
      let minuteDegrees = 360 / 60 * parseInt(newDayBreakMinutesDividerValues[index].split(':')[1]);
      cy.get('[style="transform: rotateZ(' + degrees + 'deg) translateX(-50%);"] > span').click();
      if (minuteDegrees > 0) {
        cy.wait(1000);
        cy.get('[style="transform: rotateZ(' + minuteDegrees + 'deg) translateX(-50%);"] > span').click();
      }
      cy.get('.timepicker-button span').contains('Ok').click();

      const breakMinutesPrDividerFieldId = `${day}BreakMinutesPrDivider`;
      let breakMinutesPrDividerInputField = cy.get(`#${breakMinutesPrDividerFieldId}`);
      breakMinutesPrDividerInputField
        .should('have.length', 1)
        .should('be.visible');
      breakMinutesPrDividerInputField
        .should('have.attr', 'class')
        .and('not.include', 'mat-form-field-disabled');

      /* ==== Generated with Cypress Studio ==== */
      cy.get(`#${breakMinutesPrDividerFieldId}`).click();
      // eslint-disable-next-line max-len
      degrees = 360 / 12 * parseInt(newDayBreakMinutesPrDividerValues[index].split(':')[0]);
      minuteDegrees = 360 / 60 * parseInt(newDayBreakMinutesPrDividerValues[index].split(':')[1]);
      cy.get('[style="transform: rotateZ(' + degrees + 'deg) translateX(-50%);"] > span').click();
      if (minuteDegrees > 0) {
        cy.get('[style="transform: rotateZ(' + minuteDegrees + 'deg) translateX(-50%);"] > span').click({force: true});
      }
      cy.get('.timepicker-button span').contains('Ok').click();

      const breakMinutesUpperLimitFieldId = `${day}BreakMinutesUpperLimit`;
          let breakMinutesUpperLimitInputField = cy.get(`#${breakMinutesUpperLimitFieldId}`);
      breakMinutesUpperLimitInputField
        .should('have.length', 1)
        .should('be.visible');
      breakMinutesUpperLimitInputField
        .should('have.attr', 'class')
        .and('not.include', 'mat-form-field-disabled');


      cy.get(`#${breakMinutesUpperLimitFieldId}`).click();
      // eslint-disable-next-line max-len
      degrees = 360 / 12 * parseInt(newDayBreakMinutesUpperLimitValues[index].split(':')[0]);
      minuteDegrees = 360 / 60 * parseInt(newDayBreakMinutesPrDividerValues[index].split(':')[1]);
      cy.get('[style="transform: rotateZ(' + degrees + 'deg) translateX(-50%);"] > span').click();
      if (minuteDegrees > 0) {
        cy.get('[style="transform: rotateZ(' + minuteDegrees + 'deg) translateX(-50%);"] > span').click({force: true});
      }
      cy.get('.timepicker-button span').contains('Ok').click();
      //     breakMinutesUpperLimitInputField
      //       .should('have.length', 1)
      //       .should('be.visible');
      //     breakMinutesUpperLimitInputField
      //       .should('have.attr', 'disabled');

    });
    cy.intercept('PUT', '/api/time-planning-pn/settings').as('updateSettings');
    cy.get('#saveSettings').click();
    cy.wait('@updateSettings').then((interception) => {
      expect(interception.response.statusCode).to.equal(200);
    });
    cy.visit('http://localhost:4200');
    pluginPage.Navbar.goToPluginsPage();
    row = cy.contains('.mat-mdc-row', pluginName).first();
    // row = cy.contains('.mat-mdc-row', pluginName).first();
    settingsElement = row
      .find('.mat-column-actions a');
    // .should('be.enabled')
    // .should('be.visible');
    settingsElement.click();

    const daysOfWeek = ['monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday', 'sunday'];
    daysOfWeek.forEach((day, index) => {
      const breakMinutesDividerFieldId = `${day}BreakMinutesDivider`;
      let breakMinutesDividerInputField = cy.get(`#${breakMinutesDividerFieldId}`);
      breakMinutesDividerInputField
        .should('have.length', 1)
        .should('be.visible');
      breakMinutesDividerInputField
        .should('have.attr', 'disabled');

      breakMinutesDividerInputField = cy.get(`#${breakMinutesDividerFieldId}`);
      breakMinutesDividerInputField
        .should('include.value', newDayBreakMinutesDividerValues[index]);

      const breakMinutesPrDividerFieldId = `${day}BreakMinutesPrDivider`;
      let breakMinutesPrDividerInputField = cy.get(`#${breakMinutesPrDividerFieldId}`);
      breakMinutesPrDividerInputField
        .should('have.length', 1)
        .should('be.visible');
      breakMinutesPrDividerInputField
        .should('have.attr', 'disabled');

      breakMinutesPrDividerInputField = cy.get(`#${breakMinutesPrDividerFieldId}`);
      breakMinutesPrDividerInputField
        .should('include.value', newDayBreakMinutesPrDividerValues[index]);

      const breakMinutesUpperLimitFieldId = `${day}BreakMinutesUpperLimit`;
      let breakMinutesUpperLimitInputField = cy.get(`#${breakMinutesUpperLimitFieldId}`);
      breakMinutesUpperLimitInputField
        .should('have.length', 1)
        .should('be.visible');
      breakMinutesUpperLimitInputField
        .should('have.attr', 'disabled');

      breakMinutesUpperLimitInputField = cy.get(`#${breakMinutesUpperLimitFieldId}`);
      breakMinutesUpperLimitInputField
        .should('include.value', newDayBreakMinutesUpperLimitValues[index]);
    });

    /* ==== End Cypress Studio ==== */
  });
  afterEach(() => {
    cy.visit('http://localhost:4200');
    pluginPage.Navbar.goToPluginsPage();
    const pluginName = 'Microting Time Planning Plugin';
    // pluginPage.enablePluginByName(pluginName);
    let row = cy.contains('.mat-mdc-row', pluginName).first();
    // row = cy.contains('.mat-mdc-row', pluginName).first();
    let settingsElement = row
      .find('.mat-column-actions a');
      // .should('be.enabled')
      // .should('be.visible');
    settingsElement.click();
    const resetGlobalAutoBreakCalculationSettingsButton = cy.get('#resetGlobalAutoBreakCalculationSettings');
    resetGlobalAutoBreakCalculationSettingsButton
      .should('be.visible')
      .should('be.enabled');
    resetGlobalAutoBreakCalculationSettingsButton.click();
  });
});
