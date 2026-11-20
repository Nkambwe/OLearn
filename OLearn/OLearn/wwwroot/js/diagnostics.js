// diagnostics.js - test-mode helpers used by the Settings page to run the control tests (TC-19..TC-29).
// They change only data on this device. They are shown only when the server runs with TestMode:Enabled.
import * as db from './db.js';
import * as lc from './leasecrypto.js';
import { setOffsetMs, getOffsetMs, now } from './clock.js';
import { getDeviceId, pendingEntries, GENESIS, entryHash } from './audit.js';

export { setOffsetMs, getOffsetMs };

/** TC-25: flips one byte of the stored ciphertext (authentication tag check fails). */
export async function flipStoredByte(resourceId) {
  const c = await db.get('copies', resourceId);
  const f = c && await db.get('files', c.versionId);
  if (!f) return false;
  const buf = new Uint8Array(f.plain || f.data);
  buf[Math.floor(buf.length / 2)] ^= 0xff;
  if (f.plain) f.plain = buf.buffer; else f.data = buf.buffer;
  await db.put('files', f);
  return true;
}

/** TC-25: replaces the content with different bytes that are correctly encrypted (only the hash check catches it). */
export async function substituteStoredContent(resourceId) {
  const c = await db.get('copies', resourceId);
  const f = c && await db.get('files', c.versionId);
  if (!f) return false;
  const fake = new TextEncoder().encode('%PDF-1.4 substituted content ' + now());
  if (f.plain) { f.plain = fake.buffer; await db.put('files', f); return true; }
  const k = await db.get('keys', c.versionId);
  if (!k) return false;
  const { iv, data } = await lc.encryptFile(k.key, fake, c.userId, c.versionId);
  f.iv = iv; f.data = data;
  await db.put('files', f);
  return true;
}

/** TC-27: uploads a tampered copy of the pending audit log; the real log on the device is not changed. */
export async function uploadTamperedLog(mode /* 'remove' | 'alter' */) {
  const entries = (await pendingEntries()).map((e) => ({ ...e }));
  if (entries.length < 3) return { error: 'Perform at least 3 actions offline first (open, remove, save).' };
  if (mode === 'remove') entries.splice(1, 1);
  else entries[1].type = 'OPEN_ALTERED';
  const r = await fetch('/api/sync', {
    method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ deviceId: await getDeviceId(), clientTime: new Date(now()).toISOString(), baseline: false, progress: [], leases: [], audit: entries }),
  });
  return r.json();
}

export async function describeState() {
  const copies = await db.all('copies');
  const keys = await db.all('keys');
  return {
    deviceId: await getDeviceId(),
    baseline: await db.getMeta('baseline', false),
    clockOffsetMinutes: Math.round(getOffsetMs() / 60000),
    lastTrustedServerTime: new Date(await db.getMeta('lastTrustedServerTime', 0)).toISOString(),
    auditHead: await db.getMeta('auditHead', GENESIS),
    pendingAuditEntries: (await pendingEntries()).length,
    auditRejected: await db.getMeta('auditRejected', null),
    keysHeld: keys.map((k) => k.versionId),
    copies: copies.map((c) => ({ resourceId: c.resourceId, title: c.title, leaseId: c.leaseId, version: c.versionLabel, mode: c.mode, status: c.status, hasFile: c.hasFile, expiresAt: c.expiresAt })),
  };
}
export { entryHash };
