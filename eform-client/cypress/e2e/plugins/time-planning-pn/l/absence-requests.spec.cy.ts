import loginPage from '../../../Login.page';
import pluginPage from '../../../Plugin.page';

describe('Time Planning - Absence Requests', () => {
  beforeEach(() => {
    cy.visit('http://localhost:4200');
    loginPage.login();

    pluginPage.Navbar.goToPluginsPage();
    cy.get('#actionMenu')
      .should('be.visible')
      .click({ force: true });

    cy.get('#plugin-settings-link0').click({ force: true });

    cy.get('mat-nested-tree-node').contains('Timeregistrering').click();
    cy.get('mat-tree-node').contains('Absence Requests').click();
  });

  it('should display absence requests inbox view', () => {
    cy.get('#absenceRequestsInboxBtn').should('be.visible');
    cy.get('#absenceRequestsMineBtn').should('be.visible');
    cy.get('#time-planning-pn-absence-requests-grid').should('be.visible');
  });

  it('should switch between inbox and my requests views', () => {
    cy.get('#absenceRequestsInboxBtn').should('have.class', 'active');
    
    cy.get('#absenceRequestsMineBtn').click();
    cy.get('#absenceRequestsMineBtn').should('have.class', 'active');
    cy.get('#absenceRequestsInboxBtn').should('not.have.class', 'active');
    
    cy.get('#absenceRequestsInboxBtn').click();
    cy.get('#absenceRequestsInboxBtn').should('have.class', 'active');
    cy.get('#absenceRequestsMineBtn').should('not.have.class', 'active');
  });

  it('should display approve and reject buttons for pending requests in inbox', () => {
    cy.get('#absenceRequestsInboxBtn').click();
    
    cy.get('mtx-grid').within(() => {
      cy.get('tr').each(($row) => {
        cy.wrap($row).within(() => {
          cy.get('td').then(($cells) => {
            const statusCell = $cells.filter((i, cell) => 
              Cypress.$(cell).text().includes('Pending')
            );
            
            if (statusCell.length > 0) {
              cy.get('[id^="approveAbsenceRequestBtn-"]').should('exist');
              cy.get('[id^="rejectAbsenceRequestBtn-"]').should('exist');
            }
          });
        });
      });
    });
  });

  it('should open approve modal when approve button is clicked', () => {
    cy.get('#absenceRequestsInboxBtn').click();
    
    cy.get('[id^="approveAbsenceRequestBtn-"]').first().click();
    
    cy.get('h3[mat-dialog-title]').should('contain', 'Approve Absence Request');
    cy.get('#saveApproveBtn').should('be.visible');
    cy.get('#cancelApproveBtn').should('be.visible');
    
    cy.get('#cancelApproveBtn').click();
  });

  it('should open reject modal when reject button is clicked', () => {
    cy.get('#absenceRequestsInboxBtn').click();
    
    cy.get('[id^="rejectAbsenceRequestBtn-"]').first().click();
    
    cy.get('h3[mat-dialog-title]').should('contain', 'Reject Absence Request');
    cy.get('#saveRejectBtn').should('be.visible');
    cy.get('#cancelRejectBtn').should('be.visible');
    
    cy.get('#cancelRejectBtn').click();
  });
});
