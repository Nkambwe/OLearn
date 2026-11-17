// ui.js - small DOM helpers
export function h(tag, attrs = {}, ...kids) {
  const e = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs || {})) {
    if (v === false || v == null) continue;
    if (k === 'class') e.className = v;
    else if (k.startsWith('on')) e.addEventListener(k.slice(2), v);
    else e.setAttribute(k, v === true ? '' : v);
  }
  for (const kid of kids.flat()) if (kid != null && kid !== false) e.append(kid.nodeType ? kid : document.createTextNode(String(kid)));
  return e;
}
export function toast(msg, kind = 'info') {
  let box = document.getElementById('toasts');
  if (!box) { box = h('div', { id: 'toasts', 'aria-live': 'polite' }); document.body.append(box); }
  const t = h('div', { class: 'toast ' + kind }, msg);
  box.append(t); setTimeout(() => t.remove(), kind === 'error' ? 8000 : 4000);
}
export const fmtDate = (iso) => iso ? new Date(iso).toLocaleString(undefined, { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '';
export const fmtSize = (n) => n > 1048576 ? (n / 1048576).toFixed(1) + ' MB' : Math.max(1, Math.round((n || 0) / 1024)) + ' KB';

/** Shows a PDF in an overlay on large screens; otherwise hands the file to the device. */
export function showFile({ blob, fileName }) {
  const url = URL.createObjectURL(blob);
  const isPdf = blob.type === 'application/pdf';
  const wide = window.matchMedia('(min-width: 900px)').matches;
  if (isPdf && wide) {
    const close = () => { overlay.remove(); URL.revokeObjectURL(url); };
    const overlay = h('div', { class: 'viewer' },
      h('div', { class: 'viewer-bar' }, h('strong', {}, fileName), h('button', { class: 'btn', onclick: close }, 'Close')),
      h('iframe', { src: url, title: fileName }));
    document.body.append(overlay);
  } else {
    const a = h('a', { href: url, download: fileName }); document.body.append(a); a.click(); a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 60000);
  }
}
