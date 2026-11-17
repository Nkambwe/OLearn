// audit.js - tamper-evident (hash-chained) log created on the device (FR-19).
// Each entry's hash covers the previous entry's hash, so removing or altering an entry breaks the chain.
// The same hash formula is implemented in Services/AuditService.cs (AuditChain.Hash).
import { getMeta, setMeta, put, all, del } from './db.js';
import { sha256Hex } from './leasecrypto.js';
import { nowIso } from './clock.js';

export const GENESIS = '0'.repeat(64);
let chain = Promise.resolve();

export async function getDeviceId() {
  let id = await getMeta('deviceId', null);
  if (!id) { id = crypto.randomUUID(); await setMeta('deviceId', id); }
  return id;
}

export function entryHash(deviceId, e) {
  return sha256Hex([deviceId, e.seq, e.type, e.leaseId, e.versionId, e.time, e.detail, e.prevHash].join('|'));
}

/** Appends an event. Serialised through a promise chain so sequence numbers never collide. */
export function appendEvent(type, { leaseId = '', versionId = 0, detail = '' } = {}) {
  const run = chain.then(async () => {
    if (await getMeta('baseline', false)) return null; // baseline mode: controls (and the log) are off
    const deviceId = await getDeviceId();
    const seq = (await getMeta('auditSeq', 0)) + 1;
    const prevHash = await getMeta('auditHead', GENESIS);
    const e = { seq, type, leaseId: leaseId || '', versionId: versionId || 0, time: nowIso(), detail: String(detail).replace(/\|/g, '/'), prevHash };
    e.hash = await entryHash(deviceId, e);
    await put('audit', e);
    await setMeta('auditSeq', seq);
    await setMeta('auditHead', e.hash);
    return e;
  });
  chain = run.catch(() => {});
  return run;
}

export async function pendingEntries() {
  return (await all('audit')).sort((a, b) => a.seq - b.seq);
}

/** Called after the server accepted the uploaded chain; the head hash stays so the chain continues. */
export async function acknowledge(upToSeq) {
  for (const e of await all('audit')) if (e.seq <= upToSeq) await del('audit', e.seq);
}
