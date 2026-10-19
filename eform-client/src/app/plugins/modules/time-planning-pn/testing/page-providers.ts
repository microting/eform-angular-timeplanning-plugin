import { DatePipe } from '@angular/common';
import { EnvironmentProviders, Provider } from '@angular/core';
import { provideNativeDateAdapter } from '@angular/material/core';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AppMenuStateService, AuthStateService } from 'src/app/common/store';
import { SitesService, TitleService } from 'src/app/common/services';
import { vi } from 'vitest';

/**
 * What a page component's frontend children need in a test bed: the page
 * subheader (eform-new-subheader) injects the menu/title/auth/sites services and
 * the router, the toolbar's datepickers need a DateAdapter, and the grid injects
 * the DatePipe the app provides at its root.
 */
export function pageProviders(): Array<Provider | EnvironmentProviders> {
  return [
    provideRouter([]),
    provideNoopAnimations(),
    provideNativeDateAdapter(),
    DatePipe,
    { provide: AppMenuStateService, useValue: {} },
    { provide: TitleService, useValue: { setTitle: vi.fn() } },
    { provide: AuthStateService, useValue: {} },
    { provide: SitesService, useValue: {} },
  ];
}
