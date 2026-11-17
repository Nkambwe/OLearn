// leasecrypto.js - Web Crypto helpers for the lease control layer (FR-14, FR-15, FR-18, NFR-08).
// The browser holds only the University's PUBLIC key. It can verify a lease but never create one.
const enc = new TextEncoder();

export const b64ToBytes = (b64) => Uint8Array.from(atob(b64), (c) => c.charCodeAt(0));
export const bytesToHex = (buf) => [...new Uint8Array(buf)].map((b) => b.toString(16).padStart(2, '0')).join('');

export async function sha256Hex(data) {
  const bytes = typeof data === 'string' ? enc.encode(data) : data;
  return bytesToHex(await crypto.subtle.digest('SHA-256', bytes));
}

let publicKey = null;
export function resetPublicKeyCache() { publicKey = null; }

async function getPublicKey() {
  if (!publicKey) {
    const spki = b64ToBytes(globalThis.OLEARN_PUBLIC_KEY || '');
    publicKey = await crypto.subtle.importKey('spki', spki, { name: 'ECDSA', namedCurve: 'P-256' }, false, ['verify']);
  }
  return publicKey;
}

/** Verifies the ECDSA P-256 / SHA-256 signature (IEEE P1363 r||s) over the exact manifest string. */
export async function verifyManifest(manifest, signatureB64) {
  try {
    return await crypto.subtle.verify({ name: 'ECDSA', hash: 'SHA-256' }, await getPublicKey(), b64ToBytes(signatureB64), enc.encode(manifest));
  } catch {
    return false;
  }
}

export const importFileKey = (keyB64) =>
  crypto.subtle.importKey('raw', b64ToBytes(keyB64), { name: 'AES-GCM' }, false, ['encrypt', 'decrypt']);

const aad = (userId, versionId) => enc.encode(`olearn:${userId}:${versionId}`);

export async function encryptFile(key, plain, userId, versionId) {
  const iv = crypto.getRandomValues(new Uint8Array(12));
  const data = await crypto.subtle.encrypt({ name: 'AES-GCM', iv, additionalData: aad(userId, versionId) }, key, plain);
  return { iv, data };
}

export const decryptFile = (key, iv, data, userId, versionId) =>
  crypto.subtle.decrypt({ name: 'AES-GCM', iv, additionalData: aad(userId, versionId) }, key, data);
