import { buildPauseSegments } from './pause-segments.util';

describe('buildPauseSegments', () => {
  it('lists nothing for a shift with no pause stamps', () => {
    expect(buildPauseSegments([])).toEqual([]);
    expect(buildPauseSegments([[null, null], [undefined, undefined]])).toEqual([]);
  });

  it('lists the pauses in clock order whatever slot they were recorded in', () => {
    expect(buildPauseSegments([
      ['2026-05-15T12:30:00Z', '2026-05-15T12:45:00Z'],
      [null, null],
      ['2026-05-15T10:02:40Z', '2026-05-15T10:17:10Z'],
    ])).toEqual([
      { start: '10:02', stop: '10:17' },
      { start: '12:30', stop: '12:45' },
    ]);
  });

  it('merges overlapping and touching pauses, as the backend does before cutting them out', () => {
    expect(buildPauseSegments([
      ['2026-05-15T10:00:00Z', '2026-05-15T10:20:00Z'],
      ['2026-05-15T10:10:00Z', '2026-05-15T10:15:00Z'],
      ['2026-05-15T10:20:00Z', '2026-05-15T10:30:00Z'],
    ])).toEqual([{ start: '10:00', stop: '10:30' }]);
  });

  it('lists a pause that is still running open-ended, and does not merge it', () => {
    expect(buildPauseSegments([
      ['2026-05-15T14:05:00Z', null],
      ['2026-05-15T10:00:00Z', '2026-05-15T10:15:00Z'],
    ])).toEqual([
      { start: '10:00', stop: '10:15' },
      { start: '14:05', stop: null },
    ]);
  });

  it('clamps a pause running past midnight to 24:00, as the backend does', () => {
    expect(buildPauseSegments([
      ['2026-05-15T23:50:00Z', '2026-05-16T00:10:00Z'],
    ])).toEqual([{ start: '23:50', stop: '24:00' }]);
  });

  it('skips a stop without a start and a pause that does not end after it starts', () => {
    expect(buildPauseSegments([
      [null, '2026-05-15T10:15:00Z'],
      ['2026-05-15T11:00:00Z', '2026-05-15T11:00:00Z'],
      ['2026-05-15T12:00:00Z', '2026-05-15T11:50:00Z'],
    ])).toEqual([]);
  });
});
