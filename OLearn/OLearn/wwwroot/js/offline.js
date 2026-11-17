// offline.js - save / open / renew / remove a resource under a lease, and enforce the lease rules.
// Requirements: FR-02, FR-03, FR-04, FR-09, FR-14, FR-15, FR-16, FR-17, FR-18 (see docs/TRACEABILITY.md).
import * as db from './db.js';
import { now } from './clock.js';
import * as lc from './leasecrypto.js';
import { appendEvent } from './audit.js';

export const CLOCK_TOLERANCE_MS = 10 * 60 * 1000;
export const LABELS = {
  not_stored: 'Not stored on this device',
  available: 'Available offline',
  update: 'Update available',
  expired: 'Lease expired: connect to renew',
};
const MIME = {
  PDF: 'application/pdf',
  PPTX: 'application/vnd.openxmlformats-officedocument.presentationml.presentation',
};

export class OfflineError extends Error {
  constructor(code, message) { super(message); this.code = code; }
}

export const isBaseline = async () => !!(await db.getMeta('baseline', false));
export const setBaseline = (v) => db.setMeta('baseline', !!v);

/** Records the latest trusted server time (used by the clock-rollback check, FR-15). */
export async function noteServerTime(iso) {
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return;
  const last = await db.getMeta('lastTrustedServerTime', 0);
  if (t > last) await db.setMeta('lastTrustedServerTime', t);
  await db.setMeta('lastSeenLocal', now());
}

export async function touchClock() {
  const seen = await db.getMeta('lastSeenLocal', 0);
  if (now() > seen) await db.setMeta('lastSeenLocal', now());
}

async function clockLooksWrong() {
  const n = now();
  const trusted = await db.getMeta('lastTrustedServerTime', 0);
  const seen = await db.getMeta('lastSeenLocal', 0);
  return n < trusted - CLOCK_TOLERANCE_MS || n < seen - CLOCK_TOLERANCE_MS;
}

async function postJson(url, body) {
  return fetch(url, {
    method: 'POST', credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
  });
}

/** Asks the server for a lease, and (unless baseline) verifies its signature before trusting it. */
async function requestLease(resourceId, baseline) {
  let r;
  try { r = await postJson('/api/leases', { resourceId }); }
  catch { throw new OfflineError('offline', 'You need a connection to save or renew a resource.'); }
  if (r.status === 401) throw new OfflineError('signin', 'Please sign in again while connected.');
  const lj = await r.json().catch(() => ({}));
  if (!r.ok) throw new OfflineError(lj.code || 'denied', lj.message || 'The server did not issue a lease.');
  await noteServerTime(lj.serverTime);
  if (!baseline && !(await lc.verifyManifest(lj.manifest, lj.signature))) {
    await appendEvent('REFUSE_SIGNATURE', { detail: 'save' });
    throw new OfflineError('signature', 'The lease signature is not valid, so the resource was not saved.');
  }
  return { lj, m: JSON.parse(lj.manifest) };
}

export async function saveResource(res) {
  const baseline = await isBaseline();
  const { lj, m } = await requestLease(res.resourceId, baseline);
  let fr;
  try { fr = await fetch('/api/files/' + m.resourceVersionId, { credentials: 'same-origin' }); }
  catch { throw new OfflineError('offline', 'The connection was lost while saving. Try again.'); }
  if (!fr.ok) throw new OfflineError('download', 'The file could not be downloaded.');
  const plain = await fr.arrayBuffer();
  if ((await lc.sha256Hex(plain)) !== m.sha256) {
    throw new OfflineError('integrity', 'The downloaded file does not match the approved file.');
  }
  const existing = await db.get('copies', res.resourceId);
  if (existing && existing.versionId !== m.resourceVersionId) {
    await db.del('files', existing.versionId);
    await db.del('keys', existing.versionId);
  }
  try {
    if (baseline) {
      await db.put('files', { versionId: m.resourceVersionId, resourceId: res.resourceId, plain });
    } else {
      const key = await lc.importFileKey(lj.key);
      const { iv, data } = await lc.encryptFile(key, plain, m.userId, m.resourceVersionId);
      await db.put('files', { versionId: m.resourceVersionId, resourceId: res.resourceId, iv, data });
      await db.put('keys', { versionId: m.resourceVersionId, key });
    }
  } catch (e) {
    throw new OfflineError('storage', 'The file could not be stored on this device (storage full or blocked).');
  }
  const copy = {
    resourceId: res.resourceId, versionId: m.resourceVersionId, versionLabel: m.versionLabel,
    title: res.title, fileType: res.fileType, userId: m.userId, leaseId: m.leaseId,
    manifest: lj.manifest, signature: lj.signature, expiresAt: m.expiresAt, expiresAtMs: Date.parse(m.expiresAt),
    sha256: m.sha256, sizeBytes: m.sizeBytes, status: 'active', hasFile: true,
    mode: baseline ? 'baseline' : 'controlled', savedAt: new Date(now()).toISOString(),
  };
  await db.put('copies', copy);
  await appendEvent('SAVE', { leaseId: m.leaseId, versionId: m.resourceVersionId });
  try { await navigator.storage?.persist?.(); } catch { /* optional */ }
  return copy;
}

/** FR-17: renew an expired lease. The same key is released again, so the file is not downloaded again. */
export async function renewLease(resourceId) {
  const copy = await db.get('copies', resourceId);
  if (!copy || !copy.hasFile) throw new OfflineError('not_stored', 'This resource is not stored on this device.');
  const { lj, m } = await requestLease(resourceId, copy.mode === 'baseline');
  if (m.resourceVersionId !== copy.versionId) {
    throw new OfflineError('newer', 'A newer version is available. Use Update instead.');
  }
  if (copy.mode === 'controlled') {
    await db.put('keys', { versionId: copy.versionId, key: await lc.importFileKey(lj.key) });
  }
  Object.assign(copy, {
    leaseId: m.leaseId, manifest: lj.manifest, signature: lj.signature,
    expiresAt: m.expiresAt, expiresAtMs: Date.parse(m.expiresAt), status: 'active',
  });
  await db.put('copies', copy);
  await appendEvent('RENEW', { leaseId: m.leaseId, versionId: m.resourceVersionId });
  return copy;
}

export async function removeResource(resourceId) {
  const copy = await db.get('copies', resourceId);
  if (!copy) return;
  await db.del('files', copy.versionId);
  await db.del('keys', copy.versionId);
  await db.del('copies', resourceId);
  await appendEvent('REMOVE', { leaseId: copy.leaseId, versionId: copy.versionId });
}

/** Deletes the key of every expired lease (FR-15). Works offline. */
export async function enforceExpiry() {
  let changed = 0;
  const n = now();
  for (const c of await db.all('copies')) {
    if (c.mode !== 'controlled' || !c.hasFile || c.status === 'expired') continue;
    if (n >= c.expiresAtMs) {
      await db.del('keys', c.versionId);
      c.status = 'expired';
      await db.put('copies', c);
      await appendEvent('EXPIRE', { leaseId: c.leaseId, versionId: c.versionId });
      changed++;
    }
  }
  return changed;
}

async function refuse(copy, event, code, message) {
  await appendEvent(event, { leaseId: copy.leaseId, versionId: copy.versionId });
  throw new OfflineError(code, message);
}

/** FR-03, FR-14, FR-15, FR-18: every check runs on the device and needs no connection. */
export async function openResource(resourceId) {
  const copy = await db.get('copies', resourceId);
  if (!copy || !copy.hasFile) throw new OfflineError('not_stored', 'This resource is not stored on this device.');
  const type = MIME[copy.fileType] || 'application/octet-stream';
  const fileName = `${copy.title}.${String(copy.fileType || 'pdf').toLowerCase()}`;
  const stored = await db.get('files', copy.versionId);
  if (!stored) throw new OfflineError('not_stored', 'The stored file is missing. Save it again.');

  if (copy.mode === 'baseline') { // baseline comparison mode: no checks
    return { blob: new Blob([stored.plain], { type }), fileName, copy };
  }
  if (!(await lc.verifyManifest(copy.manifest, copy.signature))) {
    await refuse(copy, 'REFUSE_SIGNATURE', 'signature', 'The lease on this copy is not valid, so it cannot be opened.');
  }
  if (await clockLooksWrong()) {
    await refuse(copy, 'REFUSE_CLOCK', 'clock', 'This device clock is earlier than expected. Connect to the internet and correct the date and time.');
  }
  if (copy.status === 'expired' || now() >= copy.expiresAtMs) {
    await enforceExpiry();
    await refuse(copy, 'REFUSE_EXPIRED', 'expired', 'The lease has expired. Connect to the internet to renew it.');
  }
  const k = await db.get('keys', copy.versionId);
  if (!k) await refuse(copy, 'REFUSE_EXPIRED', 'expired', 'The key for this copy is no longer available. Connect to renew it.');
  let plain;
  try {
    plain = await lc.decryptFile(k.key, stored.iv, stored.data, copy.userId, copy.versionId);
  } catch {
    await refuse(copy, 'REFUSE_INTEGRITY', 'integrity', 'The stored file has been changed and cannot be opened. Remove it and save it again.');
  }
  if ((await lc.sha256Hex(plain)) !== copy.sha256) {
    await refuse(copy, 'REFUSE_INTEGRITY', 'integrity', 'The stored file does not match the approved file and cannot be opened.');
  }
  await touchClock();
  await db.put('recent', { resourceId, openedAt: new Date(now()).toISOString() });
  await appendEvent('OPEN', { leaseId: copy.leaseId, versionId: copy.versionId });
  return { blob: new Blob([plain], { type }), fileName, copy };
}

/** FR-04: the four statuses. `res` is the catalogue entry (or null if it was withdrawn), `copy` the local record. */
export function statusOf(res, copy, nowMs = now()) {
  if (!copy) return { code: 'not_stored', label: LABELS.not_stored };
  if (copy.status === 'update' || (res && copy.versionId !== res.versionId)) {
    return { code: 'update', label: LABELS.update, canOpen: !!copy.hasFile && !isExpired(copy, nowMs) };
  }
  if (isExpired(copy, nowMs)) return { code: 'expired', label: LABELS.expired };
  return { code: 'available', label: LABELS.available, expiresAt: copy.mode === 'controlled' ? copy.expiresAt : null, canOpen: true };
}
function isExpired(copy, nowMs) {
  return copy.mode === 'controlled' && (copy.status === 'expired' || nowMs >= copy.expiresAtMs);
}

/** FR-16: applies the server's view of each lease at synchronisation. */
export async function applyLeaseStatuses(statuses) {
  const notices = await db.getMeta('notices', []);
  for (const s of statuses || []) {
    const copy = (await db.all('copies')).find((c) => c.leaseId === s.leaseId && c.mode === 'controlled');
    if (!copy) continue;
    if (s.status === 'Revoked') {
      await db.del('files', copy.versionId); await db.del('keys', copy.versionId); await db.del('copies', copy.resourceId);
      await appendEvent('REVOKE_APPLIED', { leaseId: copy.leaseId, versionId: copy.versionId });
      notices.unshift({ time: new Date(now()).toISOString(), title: copy.title, text: 'The University withdrew this resource. The copy on this device was removed.' });
    } else if (s.status === 'Superseded') {
      await db.del('files', copy.versionId); await db.del('keys', copy.versionId);
      copy.status = 'update'; copy.hasFile = false;
      await db.put('copies', copy);
      await appendEvent('SUPERSEDE_APPLIED', { leaseId: copy.leaseId, versionId: copy.versionId });
      notices.unshift({ time: new Date(now()).toISOString(), title: copy.title, text: 'A newer version replaced this resource. Use Update to save the new version.' });
    }
  }
  await db.setMeta('notices', notices.slice(0, 20));
}

export const getCatalog = () => db.getMeta('catalog', null);
export const listCopies = () => db.all('copies');
