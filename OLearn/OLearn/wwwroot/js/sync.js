// sync.js - controlled synchronisation (FR-06, FR-08, FR-16, FR-19, NFR-06).
import * as db from './db.js';
import { nowIso } from './clock.js';
import { getDeviceId, pendingEntries, acknowledge } from './audit.js';
import { applyLeaseStatuses, noteServerTime, isBaseline, enforceExpiry } from './offline.js';

let running = null;

export async function refreshCatalog() {
  const r = await fetch('/api/catalog', { credentials: 'same-origin' });
  if (r.status === 401) return { ok: false, reason: 'signin' };
  if (!r.ok) return { ok: false, reason: 'server' };
  const j = await r.json();
  await noteServerTime(j.serverTime);
  await db.setMeta('catalog', j);
  return { ok: true, catalog: j };
}

export async function setTaskProgress(taskId, isCompleted) {
  await db.put('progress', { taskId, isCompleted, updatedAt: nowIso(), dirty: true });
}

export function syncNow() {
  if (!running) running = doSync().finally(() => { running = null; });
  return running;
}

async function doSync() {
  const deviceId = await getDeviceId();
  const baseline = await isBaseline();
  await enforceExpiry();
  const dirty = (await db.all('progress')).filter((p) => p.dirty);
  const leases = (await db.all('copies')).filter((c) => c.mode === 'controlled' && c.leaseId).map((c) => ({ leaseId: c.leaseId }));
  const audit = baseline ? [] : await pendingEntries();
  const body = {
    deviceId, clientTime: nowIso(), baseline,
    progress: dirty.map(({ taskId, isCompleted, updatedAt }) => ({ taskId, isCompleted, updatedAt })),
    leases, audit,
  };
  let r;
  try {
    r = await fetch('/api/sync', {
      method: 'POST', credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
    });
  } catch { return { ok: false, reason: 'offline' }; }
  if (r.status === 401) return { ok: false, reason: 'signin' };
  if (!r.ok) return { ok: false, reason: 'server' };
  const j = await r.json();
  await noteServerTime(j.serverTime);

  // progress: keep the most recent change (conflict rule)
  for (const sp of j.progress || []) {
    const local = await db.get('progress', sp.taskId);
    if (!local || local.updatedAt <= sp.updatedAt) await db.put('progress', { ...sp, dirty: false });
  }
  await applyLeaseStatuses(j.leaseStatuses);

  if (j.audit && j.audit.accepted) {
    if (audit.length) await acknowledge(audit[audit.length - 1].seq);
    await db.setMeta('auditRejected', null);
  } else if (j.audit && audit.length) {
    await db.setMeta('auditRejected', { time: nowIso(), reason: j.audit.reason });
  }
  const cat = await refreshCatalog();
  await db.setMeta('lastSync', nowIso());
  return { ok: true, audit: j.audit, catalogOk: cat.ok };
}
