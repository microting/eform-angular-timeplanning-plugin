export function secondsToHM(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  if (m === 0) {
    return `${h}h`;
  }
  return `${h}h${m}m`;
}

export function secondsToHHMM(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  return `${h.toString().padStart(2, '0')}:${m.toString().padStart(2, '0')}`;
}

/**
 * Renders a duration for the hh:mm editor input, e.g. 26640 -> '7:24'.
 * A null/undefined duration means "unlimited" and renders as an empty
 * string, so the field shows its (translated) "Unlimited" placeholder.
 */
export function secondsToHhMmInput(seconds: number | null | undefined): string {
  if (seconds === null || seconds === undefined) {
    return '';
  }
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  return `${h}:${m.toString().padStart(2, '0')}`;
}

/**
 * Parses an 'h:mm' / 'hh:mm' duration into seconds.
 * Returns null when the text is not a well-formed duration; an empty string
 * is NOT handled here (empty means "unlimited" and is handled by the caller).
 */
export function parseHhMmToSeconds(text: string): number | null {
  const match = /^(\d{1,3}):(\d{1,2})$/.exec((text || '').trim());
  if (!match) {
    return null;
  }
  const hours = Number(match[1]);
  const minutes = Number(match[2]);
  if (minutes > 59) {
    return null;
  }
  return hours * 3600 + minutes * 60;
}

export function formatTierChain(
  tiers: Array<{ order: number; upToSeconds: number | null; payCode: string }>,
  unlimitedLabel?: string
): string {
  return [...tiers]
    .sort((a, b) => a.order - b.order)
    .map(t => {
      if (t.upToSeconds != null) {
        return `${t.payCode} (${secondsToHM(t.upToSeconds)})`;
      }
      return unlimitedLabel ? `${t.payCode} (${unlimitedLabel})` : t.payCode;
    })
    .join(' → ');
}

export function formatTimeBands(
  bands: Array<{ startSecondOfDay: number; endSecondOfDay: number; payCode: string }>
): string {
  return bands
    .map(b => `${secondsToHHMM(b.startSecondOfDay)}-${secondsToHHMM(b.endSecondOfDay)} ${b.payCode}`)
    .join(' | ');
}
