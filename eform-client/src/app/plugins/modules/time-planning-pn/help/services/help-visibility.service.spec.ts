import { TestBed } from '@angular/core/testing';
import { Store } from '@ngrx/store';
import { BehaviorSubject, firstValueFrom } from 'rxjs';
import { HelpVisibilityService } from './help-visibility.service';

describe('HelpVisibilityService', () => {
  let isAdmin: BehaviorSubject<boolean | undefined>;

  const build = (): HelpVisibilityService => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        HelpVisibilityService,
        { provide: Store, useValue: { select: () => isAdmin } },
      ],
    });
    return TestBed.inject(HelpVisibilityService);
  };

  beforeEach(() => {
    isAdmin = new BehaviorSubject<boolean | undefined>(undefined);
  });

  it('hides help until the store says the user is an admin', () => {
    const service = build();
    expect(service.isVisible).toBe(false);
  });

  it('shows help once the admin flag arrives after construction', async () => {
    // The reason this service subscribes rather than taking a single value. The
    // planning container is built before the auth state has necessarily landed;
    // a take(1) read here would latch the `undefined` above and hide every help
    // surface from a real admin for the rest of the session, with nothing on
    // screen to explain it and nothing in the code that looks wrong.
    const service = build();
    expect(service.isVisible).toBe(false);

    isAdmin.next(true);

    expect(service.isVisible).toBe(true);
    expect(await firstValueFrom(service.isVisible$)).toBe(true);
  });

  it('hides help again if the flag goes away', () => {
    const service = build();
    isAdmin.next(true);
    isAdmin.next(false);
    expect(service.isVisible).toBe(false);
  });

  it('replays the current value to a late subscriber', async () => {
    // The components read it synchronously in a getter, but anything binding
    // isVisible$ subscribes after the fact and must not wait for the next change.
    const service = build();
    isAdmin.next(true);
    expect(await firstValueFrom(service.isVisible$)).toBe(true);
  });

  it('stops listening to the store when it is torn down', () => {
    const service = build();
    expect(isAdmin.observed).toBe(true);
    service.ngOnDestroy();
    expect(isAdmin.observed).toBe(false);
  });
});
