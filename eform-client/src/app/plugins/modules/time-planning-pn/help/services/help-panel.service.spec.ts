import { firstValueFrom } from 'rxjs';
import { HelpEntryId } from '../help.model';
import { HelpPanelService } from './help-panel.service';

describe('HelpPanelService', () => {
  let service: HelpPanelService;

  beforeEach(() => {
    service = new HelpPanelService();
  });

  it('starts closed with no target', async () => {
    expect(await firstValueFrom(service.isOpen$)).toBe(false);
    expect(await firstValueFrom(service.target$)).toBeNull();
  });

  it('opens with no target', async () => {
    service.open();

    expect(await firstValueFrom(service.isOpen$)).toBe(true);
    expect(await firstValueFrom(service.target$)).toBeNull();
  });

  it('opens on a target and clears it on close', async () => {
    service.open('flex.sumFlex');

    expect(await firstValueFrom(service.isOpen$)).toBe(true);
    expect(await firstValueFrom(service.target$)).toBe('flex.sumFlex');

    service.close();

    expect(await firstValueFrom(service.isOpen$)).toBe(false);
    expect(await firstValueFrom(service.target$)).toBeNull();
  });

  it('replaces the target when opened again on a different entry', async () => {
    service.open('flex.sumFlex');
    service.open('toolbar.dateRange');

    expect(await firstValueFrom(service.target$)).toBe('toolbar.dateRange');
  });

  it('drops the previous target when reopened without one', async () => {
    service.open('flex.sumFlex');
    service.open();

    expect(await firstValueFrom(service.target$)).toBeNull();
  });

  it('pushes every state change to subscribers, current value first', () => {
    const open: boolean[] = [];
    const targets: (HelpEntryId | null)[] = [];
    const openSub = service.isOpen$.subscribe(value => open.push(value));
    const targetSub = service.target$.subscribe(value => targets.push(value));

    service.open('flex.sumFlex');
    service.close();

    expect(open).toEqual([false, true, false]);
    expect(targets).toEqual([null, 'flex.sumFlex', null]);

    openSub.unsubscribe();
    targetSub.unsubscribe();
  });
});
