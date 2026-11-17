// app.js - runs on every signed-in page: service worker, connection badge, automatic sync, expiry enforcement
import * as db from './db.js';
import { toast } from './ui.js';
import { enforceExpiry, touchClock } from './offline.js';
import { syncNow, refreshCatalog } from './sync.js';

const badge = document.getElementById('conn-badge');
let online = navigator.onLine;

async function ping() {
  try { const r = await fetch('/api/ping', { cache: 'no-store' }); return r.ok; } catch { return false; }
}
function paint() { if (badge) { badge.textContent = online ? 'Online' : 'Offline'; badge.className = 'pill ' + (online ? 'online' : 'offline'); } }

async function doSync(manual = false) {
  if (!online) { if (manual) toast('You are offline.', 'error'); return; }
  const r = await syncNow();
  if (manual) toast(r.ok ? 'Synchronised.' : r.reason === 'signin' ? 'Please sign in again.' : 'Could not sync.', r.ok ? 'ok' : 'error');
  window.dispatchEvent(new Event('olearn:synced'));
}
window.olearn = { syncNow: () => doSync(true) };

async function check() {
  const was = online; online = navigator.onLine && (await ping()); paint();
  if (online && !was) doSync();
}
window.addEventListener('online', check); window.addEventListener('offline', check);

(async () => {
  if ('serviceWorker' in navigator) navigator.serviceWorker.register('/service-worker.js').catch(() => {});
  await touchClock();
  await enforceExpiry();
  await check();
  if (online && !(await db.getMeta('catalog', null))) await doSync();
  setInterval(async () => { await enforceExpiry(); await check(); if (online) doSync(); }, 60000);
  document.getElementById('btn-sync-top')?.addEventListener('click', () => doSync(true));
})();
