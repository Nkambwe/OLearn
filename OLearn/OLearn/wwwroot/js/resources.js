// resources.js - resource cards with status, expiry date and the Save / Open / Update / Renew / Remove actions
import { h, toast, fmtDate, fmtSize, showFile } from './ui.js';
import * as off from './offline.js';
import { syncNow } from './sync.js';

export async function loadModel() {
  const catalog = await off.getCatalog();
  const copies = await off.listCopies();
  const byRes = new Map(copies.map((c) => [c.resourceId, c]));
  const items = (catalog?.resources || []).map((r) => ({ res: r, copy: byRes.get(r.resourceId) }));
  for (const c of copies) if (!items.some((i) => i.res.resourceId === c.resourceId)) {
    items.push({ res: { resourceId: c.resourceId, title: c.title, fileType: c.fileType, versionId: c.versionId, versionLabel: c.versionLabel, moduleName: '', courseName: '' }, copy: c });
  }
  return { catalog, items };
}

async function act(fn, okMsg, rerender) {
  try { await fn(); if (okMsg) toast(okMsg, 'ok'); }
  catch (e) { toast(e.message || 'Something went wrong.', 'error'); }
  await rerender();
}

export function card(item, rerender) {
  const { res, copy } = item;
  const st = off.statusOf(res, copy);
  const btn = (label, fn, cls = 'btn') => h('button', { class: cls, onclick: fn }, label);
  const actions = [];
  if (st.code === 'not_stored') actions.push(btn('Save', () => act(async () => { toast('Saving…'); await off.saveResource(res); }, 'Saved for offline use.', rerender), 'btn primary'));
  if (st.code === 'available') actions.push(btn('Open', () => openIt(res.resourceId, rerender), 'btn primary'));
  if (st.code === 'update') {
    if (st.canOpen) actions.push(btn('Open', () => openIt(res.resourceId, rerender)));
    actions.push(btn('Update', () => act(async () => { toast('Updating…'); await off.saveResource(res); }, 'Updated.', rerender), 'btn primary'));
  }
  if (st.code === 'expired') actions.push(btn('Renew', () => act(() => off.renewLease(res.resourceId), 'Lease renewed.', rerender), 'btn primary'));
  if (copy) actions.push(btn('Remove', () => act(() => off.removeResource(res.resourceId), 'Removed from this device.', rerender)));
  const meta = [res.moduleName, res.fileType, res.versionLabel, res.sizeBytes ? fmtSize(res.sizeBytes) : ''].filter(Boolean).join(' • ');
  return h('article', { class: 'card resource', 'data-resource': res.resourceId },
    h('h3', {}, res.title),
    h('p', { class: 'muted' }, meta),
    h('p', {}, h('span', { class: 'pill ' + st.code }, st.label),
      st.expiresAt ? h('span', { class: 'muted expiry' }, ' Lease expires ' + fmtDate(st.expiresAt)) : null),
    h('div', { class: 'actions' }, actions));
}

async function openIt(resourceId, rerender) {
  try { showFile(await off.openResource(resourceId)); }
  catch (e) { toast(e.message, 'error'); }
  await rerender();
}

export async function renderResources(container, { courseId = 0, moduleId = 0, query = '', onlyUpdates = false } = {}) {
  const { catalog, items } = await loadModel();
  const rerender = () => renderResources(container, { courseId, moduleId, query, onlyUpdates });
  container.replaceChildren();
  if (!catalog) { container.append(h('p', { class: 'muted' }, 'No resources yet. Connect to the internet and press Sync now.')); return; }
  const q = query.trim().toLowerCase();
  let shown = items.filter((i) => (!courseId || i.res.courseId === courseId) && (!moduleId || i.res.moduleId === moduleId)
    && (!q || (i.res.title + ' ' + (i.res.moduleName || '')).toLowerCase().includes(q)));
  if (onlyUpdates) shown = shown.filter((i) => off.statusOf(i.res, i.copy).code === 'update');
  if (!shown.length) container.append(h('p', { class: 'muted' }, onlyUpdates ? 'No resource has an update.' : 'No resources match.'));
  for (const i of shown) container.append(card(i, rerender));
}
export { syncNow };
