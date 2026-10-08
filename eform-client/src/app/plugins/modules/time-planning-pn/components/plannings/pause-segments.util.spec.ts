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
      { start: '10:02:40', stop: '10:17:10' },
      { start: '12:30:00', stop: '12:45:00' },
    ]);
  });

  it('shows the seconds of a stamp, so pauses within the same minute are told apart', () => {
    expect(buildPauseSegments([
      ['2026-05-15T14:50:07Z', '2026-05-15T14:50:59Z'],
    ])).toEqual([{ start: '14:50:07', stop: '14:50:59' }]);
  });

  it('reads Date stamps, which is how they reach the dialog, by their UTC fields', () => {
    // The core DateInterceptor parses the API's zone-less wall-clock strings with
    // date-fns parseJSON, i.e. as UTC.
    expect(buildPauseSegments([
      [new Date(Date.UTC(2026, 4, 15, 10, 2, 5)), new Date(Date.UTC(2026, 4, 15, 10, 17, 30))],
    ])).toEqual([{ start: '10:02:05', stop: '10:17:30' }]);
  });

  it('merges overlapping and touching pauses, as the backend does before cutting them out', () => {
    expect(buildPauseSegments([
      ['2026-05-15T10:00:00Z', '2026-05-15T10:20:00Z'],
      ['2026-05-15T10:10:00Z', '2026-05-15T10:15:00Z'],
      ['2026-05-15T10:20:00Z', '2026-05-15T10:30:00Z'],
    ])).toEqual([{ start: '10:00:00', stop: '10:30:00' }]);
  });

  it('lists a pause that is still running open-ended, and does not merge it', () => {
    expect(buildPauseSegments([
      ['2026-05-15T14:05:00Z', null],
      ['2026-05-15T10:00:00Z', '2026-05-15T10:15:00Z'],
    ])).toEqual([
      { start: '10:00:00', stop: '10:15:00' },
      { start: '14:05:00', stop: null },
    ]);
  });

  it('clamps a pause running past midnight to 24:00:00, as the backend does', () => {
    expect(buildPauseSegments([
      ['2026-05-15T23:50:00Z', '2026-05-16T00:10:00Z'],
    ])).toEqual([{ start: '23:50:00', stop: '24:00:00' }]);
  });

  it('skips a stop without a start and a pause that does not end after it starts', () => {
    expect(buildPauseSegments([
      [null, '2026-05-15T10:15:00Z'],
      ['2026-05-15T11:00:00Z', '2026-05-15T11:00:00Z'],
      ['2026-05-15T12:00:00Z', '2026-05-15T11:50:00Z'],
    ])).toEqual([]);
  });
});
