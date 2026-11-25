// pages.js - one small controller per page; every list is drawn from the local store so it works offline.
import * as db from './db.js';
import { h, toast, fmtDate } from './ui.js';
import * as off from './offline.js';
import { renderResources, loadModel } from './resources.js';
import { syncNow, setTaskProgress } from './sync.js';
import * as diag from './diagnostics.js';

const page = document.querySelector('main[data-page]')?.dataset.page;
const $ = (id) => document.getElementById(id);
const qs = new URLSearchParams(location.search);

const PAGES = {
  async dashboard() {
    const { catalog, items } = await loadModel();
    const tasks = catalog?.tasks || [];
    const prog = new Map((await db.all('progress')).map((p) => [p.taskId, p]));
    const done = tasks.filter((t) => prog.get(t.taskId)?.isCompleted).length;
    const statuses = items.map((i) => off.statusOf(i.res, i.copy).code);
    const set = (id, v) => { if ($(id)) $(id).textContent = v; };
    set('stat-courses', new Set((catalog?.resources || []).map((r) => r.courseId)).size);
    set('stat-offline', statuses.filter((s) => s === 'available' || s === 'update').length);
    set('stat-tasks', tasks.length - done);
    set('stat-updates', statuses.filter((s) => s === 'update').length);
    set('stat-expired', statuses.filter((s) => s === 'expired').length);
    if ($('progress-bar')) $('progress-bar').style.width = (tasks.length ? Math.round(done * 100 / tasks.length) : 0) + '%';
    const recent = (await db.all('recent')).sort((a, b) => b.openedAt.localeCompare(a.openedAt)).slice(0, 3);
    const list = $('recent'); list.replaceChildren();
    if (!recent.length) list.append(h('p', { class: 'muted' }, 'Nothing opened yet.'));
    for (const r of recent) { const it = items.find((i) => i.res.resourceId === r.resourceId); if (it) list.append(h('a', { class: 'recent-link', href: '/Resources' }, h('span', { class: 'rl-title' }, it.res.title), h('span', { class: 'rl-date' }, fmtDate(r.openedAt)))); }
  },
  async courses() {
    const { catalog } = await loadModel();
    const box = $('courses'); box.replaceChildren();
    if (!catalog) { box.append(h('p', { class: 'muted' }, 'Connect and press Sync now to load your courses.')); return; }
    const by = new Map();
    for (const r of catalog.resources) {
      if (!by.has(r.courseId)) by.set(r.courseId, { code: r.courseCode, title: r.courseName, mods: new Map() });
      const c = by.get(r.courseId); c.mods.set(r.moduleId, { name: r.moduleName, n: (c.mods.get(r.moduleId)?.n || 0) + 1 });
    }
    for (const [cid, c] of by) {
      const total = [...c.mods.values()].reduce((a, m) => a + m.n, 0);
      box.append(h('article', { class: 'course-card' },
        h('header', { class: 'course-head' }, h('span', { class: 'course-code' }, c.code), h('h3', {}, c.title), h('span', { class: 'course-count' }, `${total} resource${total === 1 ? '' : 's'}`)),
        h('div', { class: 'course-links' }, [...c.mods].map(([mid, m]) => h('a', { href: `/Resources?course=${cid}&module=${mid}` }, h('span', {}, m.name), h('span', { class: 'count' }, String(m.n)))))));
    }
  },
  async resources() {
    const box = $('list'); const search = $('search');
    const state = { courseId: Number(qs.get('course') || 0), moduleId: Number(qs.get('module') || 0), query: '' };
    const { catalog } = await loadModel();
    const cs = $('filter-course'), ms = $('filter-module');
    if (catalog) {
      const courses = new Map(catalog.resources.map((r) => [r.courseId, `${r.courseCode}`]));
      for (const [id, n] of courses) cs.append(h('option', { value: id, selected: id === state.courseId }, n));
      const fillMods = () => { ms.replaceChildren(h('option', { value: 0 }, 'All modules')); for (const r of catalog.resources) if (!state.courseId || r.courseId === state.courseId) if (![...ms.options].some((o) => o.value == r.moduleId)) ms.append(h('option', { value: r.moduleId, selected: r.moduleId === state.moduleId }, r.moduleName)); };
      fillMods();
      cs.onchange = () => { state.courseId = Number(cs.value); state.moduleId = 0; fillMods(); draw(); };
      ms.onchange = () => { state.moduleId = Number(ms.value); draw(); };
    }
    const draw = () => renderResources(box, state);
    search.oninput = () => { state.query = search.value; draw(); };
    await draw();
  },
  async updates() {
    await renderResources($('list'), { onlyUpdates: true });
    const notices = await db.getMeta('notices', []);
    const nb = $('notices'); nb.replaceChildren();
    if (!notices.length) nb.append(h('p', { class: 'muted' }, 'No notices.'));
    for (const n of notices) nb.append(h('p', {}, h('strong', {}, n.title), ' ', n.text, ' ', h('span', { class: 'muted' }, fmtDate(n.time))));
  },
  async progress() {
    const { catalog } = await loadModel();
    const box = $('tasks'); box.replaceChildren();
    const tasks = catalog?.tasks || [];
    if (!tasks.length) { box.append(h('p', { class: 'muted' }, 'No learning tasks yet.')); return; }
    const prog = new Map((await db.all('progress')).map((p) => [p.taskId, p]));
    const mods = new Map();
    for (const t of tasks) { if (!mods.has(t.moduleId)) mods.set(t.moduleId, { name: t.moduleName, tasks: [] }); mods.get(t.moduleId).tasks.push(t); }
    for (const m of mods.values()) {
      const doneN = m.tasks.filter((t) => prog.get(t.taskId)?.isCompleted).length;
      box.append(h('div', { class: 'section-head' }, h('h3', {}, m.name), h('span', { class: 'pill' }, `${doneN} of ${m.tasks.length} complete`)),
        h('div', { class: 'bar slim' }, h('div', { style: `width:${Math.round(doneN * 100 / m.tasks.length)}%` })));
      for (const t of m.tasks) {
        box.append(h('label', { class: 'card task' },
          h('input', { type: 'checkbox', checked: !!prog.get(t.taskId)?.isCompleted, onchange: async (e) => { await setTaskProgress(t.taskId, e.target.checked); toast(navigator.onLine ? 'Saved.' : 'Saved on this device. It will sync when you are online.'); if (navigator.onLine) syncNow(); PAGES.progress(); } }),
          h('span', { class: 'task-text' }, t.taskTitle, t.resourceTitle ? h('span', { class: 'muted' }, ' — ' + t.resourceTitle) : null)));
      }
    }
  },
  async settings() {
    const testMode = document.body.dataset.testmode === 'true';
    const info = $('device-info');
    const refresh = async () => { info.textContent = JSON.stringify(await diag.describeState(), null, 2); };
    $('btn-sync').onclick = async () => { const r = await syncNow(); toast(r.ok ? 'Synchronised.' : 'Could not sync (' + r.reason + ').', r.ok ? 'ok' : 'error'); refresh(); };
    if (!testMode) { refresh(); return; }
    const base = $('chk-baseline'); base.checked = await off.isBaseline();
    base.onchange = async () => { await off.setBaseline(base.checked); toast('Baseline mode ' + (base.checked ? 'ON: resources saved from now on are plain, unchecked downloads.' : 'OFF.')); refresh(); };
    const sel = $('diag-resource');
    const { items } = await loadModel();
    for (const i of items.filter((x) => x.copy)) sel.append(h('option', { value: i.res.resourceId }, i.res.title));
    const rid = () => Number(sel.value);
    $('clock-minutes').value = Math.round(diag.getOffsetMs() / 60000);
    $('btn-clock').onclick = async () => { diag.setOffsetMs(Number($('clock-minutes').value || 0) * 60000); toast('Simulated clock offset set.'); refresh(); };
    $('btn-clock-reset').onclick = async () => { diag.setOffsetMs(0); await db.setMeta('lastSeenLocal', Date.now()); $('clock-minutes').value = 0; toast('Clock reset.'); refresh(); };
    $('btn-flip').onclick = async () => { toast((await diag.flipStoredByte(rid())) ? 'Stored file altered (byte flipped).' : 'No stored file.'); };
    $('btn-subst').onclick = async () => { toast((await diag.substituteStoredContent(rid())) ? 'Stored content substituted.' : 'No stored file / key.'); };
    $('btn-tamper-remove').onclick = async () => { $('diag-out').textContent = JSON.stringify(await diag.uploadTamperedLog('remove'), null, 2); };
    $('btn-tamper-alter').onclick = async () => { $('diag-out').textContent = JSON.stringify(await diag.uploadTamperedLog('alter'), null, 2); };
    $('btn-refresh').onclick = refresh;
    await refresh();
  },
};

if (page && PAGES[page]) PAGES[page]().catch((e) => toast(e.message, 'error'));
window.addEventListener('olearn:synced', () => { if (page && PAGES[page] && page !== 'settings') PAGES[page](); });
