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


    const dayBreakMinutesDividerValues = [
      '03:00',
      '03:00',
      '03:00',
      '03:00',
      '03:00',
      '02:00',
      '02:00'
    ];
    const newDayBreakMinutesDividerValues = [
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
    const newDayBreakMinutesPrDividerValues = [
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
    const newDayBreakMinutesUpperLimitValues = [
      '01:00',
      '01:00',
      '01:00',
      '01:00',
      '01:00',
      '01:00',
      '01:00'
    ];

    // const daysOfWeek = ['monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday', 'sunday'];
    // daysOfWeek.forEach((day, index) => {
    //   const breakMinutesDividerFieldId = `${day}BreakMinutesDivider`;
    //   let breakMinutesDividerInputField = cy.get(`#${breakMinutesDividerFieldId}`);
    //   breakMinutesDividerInputField
    //     .should('have.length', 1)
    //     .should('be.visible');
    //   breakMinutesDividerInputField
    //     .should('have.attr', 'disabled');
    //
    //   breakMinutesDividerInputField = cy.get(`#${breakMinutesDividerFieldId}`);
    //   breakMinutesDividerInputField
    //     .should('include.value', dayBreakMinutesDividerValues[index]);
    //
    //   const breakMinutesPrDividerFieldId = `${day}BreakMinutesPrDivider`;
    //   let breakMinutesPrDividerInputField = cy.get(`#${breakMinutesPrDividerFieldId}`);
    //   breakMinutesPrDividerInputField
    //     .should('have.length', 1)
    //     .should('be.visible');
    //   breakMinutesPrDividerInputField
    //     .should('have.attr', 'disabled');
    //
    //   breakMinutesPrDividerInputField = cy.get(`#${breakMinutesPrDividerFieldId}`);
    //   breakMinutesPrDividerInputField
    //     .should('include.value', dayBreakMinutesPrDividerValues[index]);
    //
    //   const breakMinutesUpperLimitFieldId = `${day}BreakMinutesUpperLimit`;
    //   let breakMinutesUpperLimitInputField = cy.get(`#${breakMinutesUpperLimitFieldId}`);
    //   breakMinutesUpperLimitInputField
    //     .should('have.length', 1)
    //     .should('be.visible');
    //   breakMinutesUpperLimitInputField
    //     .should('have.attr', 'disabled');
    //
    //   breakMinutesUpperLimitInputField = cy.get(`#${breakMinutesUpperLimitFieldId}`);
    //   breakMinutesUpperLimitInputField
    //     .should('include.value', dayBreakMinutesUpperLimitValues[index]);
    // });

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
      breakMinutesDividerInputField
        .clear()
        .type(newDayBreakMinutesDividerValues[index]);
      breakMinutesDividerInputField
        .should('include.text', newDayBreakMinutesDividerValues[index]);
    });
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
