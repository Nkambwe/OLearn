// db.js - thin IndexedDB wrapper (local store D2 in the design)
//const DB_NAME = 'olearn';
const DB_VERSION = 1;
let dbPromise = null;

function dbName() {
    const uid = globalThis.document?.body?.dataset?.user;
    return uid ? 'olearn-u' + uid : 'olearn';
}

export function openDb() {
  if (dbPromise) return dbPromise;
  dbPromise = new Promise((resolve, reject) => {
      const req = indexedDB.open(dbName(), DB_VERSION);
    req.onupgradeneeded = () => {
      const d = req.result;
      d.createObjectStore('meta', { keyPath: 'key' });
      d.createObjectStore('copies', { keyPath: 'resourceId' });   // one copy record per resource
      d.createObjectStore('files', { keyPath: 'versionId' });     // encrypted (or baseline plain) file bytes
      d.createObjectStore('keys', { keyPath: 'versionId' });      // non-extractable AES-GCM keys, held only while the lease is valid
      d.createObjectStore('audit', { keyPath: 'seq' });           // hash-chained log entries not yet uploaded
      d.createObjectStore('progress', { keyPath: 'taskId' });
      d.createObjectStore('recent', { keyPath: 'resourceId' });
    };
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
  return dbPromise;
}

export function resetForTests() { dbPromise = null; }

function run(store, mode, fn) {
  return openDb().then((d) => new Promise((resolve, reject) => {
    const tx = d.transaction(store, mode);
    const s = tx.objectStore(store);
    let result;
    const req = fn(s);
    if (req) req.onsuccess = () => { result = req.result; };
    tx.oncomplete = () => resolve(result);
    tx.onerror = () => reject(tx.error);
    tx.onabort = () => reject(tx.error);
  }));
}

export const get = (store, key) => run(store, 'readonly', (s) => s.get(key));
export const put = (store, value) => run(store, 'readwrite', (s) => s.put(value));
export const del = (store, key) => run(store, 'readwrite', (s) => s.delete(key));
export const all = (store) => run(store, 'readonly', (s) => s.getAll());
export const clearStore = (store) => run(store, 'readwrite', (s) => s.clear());

export async function getMeta(key, fallback) {
  const r = await get('meta', key);
  return r === undefined ? fallback : r.value;
}
export const setMeta = (key, value) => put('meta', { key, value });
