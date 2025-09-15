import loginPage from '../../../Login.page';

describe('Dashboard edit values', () => {
  beforeEach(() => {
    cy.visit('http://localhost:4200');
    loginPage.login();
    cy.get('mat-nested-tree-node').contains('Timeregistrering').click();
    cy.intercept('POST', '**/api/time-planning-pn/plannings/index').as('index-update');
    cy.intercept('PUT', '**/api/time-planning-pn/plannings/*').as('saveWorkdayEntity');

    cy.get('mat-tree-node').contains('Dashboard').click();
    // cy.get('#backwards').click();
    cy.wait('@index-update', {timeout: 60000});
    cy.get('#workingHoursSite').click();
    cy.get('.ng-option').contains('ac ad').click();
  });


  it('should edit time planned in last week', () => {

    // Planned time
    cy.get('#cell0_0').click();

    cy.get('#plannedStartOfShift1').click();
    cy.get('ngx-material-timepicker-face')
      .contains('1')
      .click({force: true});
    cy.get('ngx-material-timepicker-face')
      .contains('00')
      .click({force: true});
    cy.wait(1000);
    cy.contains('button', /^Ok$/).click({force: true});
    cy.get('#plannedStartOfShift1').should('have.value', '01:00');
    // cy.get('#plannedEndOfShift1').should('have.value', '00:00');
    cy.get('#planHours').should('have.value', '23');
    cy.get('#saveButton').click();
    cy.wait('@saveWorkdayEntity', {timeout: 60000});
    cy.wait(1000);
  });

  it('should edit time registration in last week', () => {
    // Registrar time
    cy.get('#cell0_0').click();
    cy.get('#start1StartedAt').click();
    cy.get('ngx-material-timepicker-face')
      .contains('1')
      .click({ force: true });
    cy.get('ngx-material-timepicker-face')
      .contains('00')
      .click({ force: true });
    cy.contains('button', /^Ok$/).click({ force: true });
    cy.get('#start1StartedAt').should('have.value', '01:00');

    cy.get('#stop1StoppedAt').click();
    cy.get('ngx-material-timepicker-face')
      .contains('00')
      .click({ force: true });
    cy.get('ngx-material-timepicker-face')
      .contains('00')
      .click({ force: true });

    cy.contains('button', /^Ok$/).click({ force: true });
    cy.wait(1000);
    // cy.get('#stop1StoppedAt').should('have.value', '00:00');
    cy.get('#saveButton').click();
    cy.wait('@saveWorkdayEntity', { timeout: 60000 });
  });

  afterEach(() => {
    cy.get('#cell0_0').click();

    cy.get('#plannedStartOfShift1')
      .closest('.flex-row')
      .find('button mat-icon')
      .contains('delete')
      .click({ force: true });
    cy.wait(500);

    cy.get('#plannedEndOfShift1')
      .closest('.flex-row')
      .find('button mat-icon')
      .contains('delete')
      .click({ force: true });
    cy.wait(500);

    cy.get('#start1StartedAt')
      .closest('.flex-row')
      .find('button mat-icon')
      .contains('delete')
      .click({ force: true });
    cy.wait(500);

    cy.get('#stop1StoppedAt')
      .closest('.flex-row')
      .find('button mat-icon')
      .contains('delete')
      .click({ force: true });
    cy.wait(500);

    cy.get('#saveButton').click();
    cy.wait('@saveWorkdayEntity', { timeout: 60000 });
    cy.wait(1000);

  })

});
