// clock.js - the one place the app reads the time.
// A test-mode offset lets a tester simulate a device clock that is moved forward or back.
const KEY = 'olearn.clockOffsetMs';

export function getOffsetMs() {
  try { return Number(globalThis.localStorage?.getItem(KEY) || 0) || 0; } catch { return 0; }
}
export function setOffsetMs(ms) {
  try { globalThis.localStorage?.setItem(KEY, String(ms)); } catch { /* ignore */ }
}
export const now = () => Date.now() + getOffsetMs();
export const nowIso = () => new Date(now()).toISOString();
