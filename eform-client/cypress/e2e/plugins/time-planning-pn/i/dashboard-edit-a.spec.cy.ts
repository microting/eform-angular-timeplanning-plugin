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

  const setTimepickerValue = (selector: string, hour: string, minute: string) => {
    cy.get(selector).click();
    cy.get('ngx-material-timepicker-face')
      .contains(hour)
      .click({force: true});
    cy.get('ngx-material-timepicker-face')
      .contains(minute)
      .click({force: true});
    cy.wait(1000);
    cy.contains('button', /^Ok$/).click({force: true});
  };


  it('should edit time planned in last week', () => {

    // Planned time
    cy.get('#cell0_0').click();

    setTimepickerValue('#plannedStartOfShift1', '1', '00');
    setTimepickerValue('#plannedEndOfShift1', '23', '35');

    cy.contains('button', /^Ok$/).click({force: true});
    cy.wait(1000);
    cy.get('#todaysFlex').should('have.value', '-22.58');
    cy.get('#saveButton').click();
    cy.wait('@saveWorkdayEntity', {timeout: 60000});
    cy.wait(1000);

    cy.get('#cell0_0').click();

    cy.get('#paidOutFlex').clear().type('-22.58');
    cy.wait(1000);
    cy.get('#saveButton').click();
    cy.wait('@saveWorkdayEntity', {timeout: 60000});
    cy.get('#cell0_0').click();
    cy.wait(1000);
    cy.get('#flexIncludingToday').should('have.value', '0.00');

    cy.get('#saveButton').click();
  });

  afterEach(() => {
    cy.get('#cell0_0').click();

    ['#plannedStartOfShift1', '#plannedEndOfShift1', '#start1StartedAt', '#stop1StoppedAt'].forEach(
      (selector) => {
        cy.get(selector)
          .closest('.flex-row')
          .find('button mat-icon')
          .contains('delete')
          .click({force: true});
        cy.wait(500);
      }
    );

    cy.get('#saveButton').click();
    cy.wait('@saveWorkdayEntity', {timeout: 60000});
    cy.wait(1000);

  })

});
