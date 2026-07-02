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

export function formatTierChain(
  tiers: Array<{ order: number; upToSeconds: number | null; payCode: string }>
): string {
  return [...tiers]
    .sort((a, b) => a.order - b.order)
    .map(t => {
      if (t.upToSeconds != null) {
        return `${t.payCode} (${secondsToHM(t.upToSeconds)})`;
      }
      return t.payCode;
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
