import { translate, languages } from './i18n.mjs';
import { categoriesFor, categoryKey, filterGroups, exportCsv, toCsv, categoryMatches } from './core.mjs';

const $ = id => document.getElementById(id);
const storage = { get(key) { try { return localStorage.getItem(key); } catch { return null; } }, set(key, value) { try { localStorage.setItem(key, value); } catch {} } };
let language = storage.get('lens.language') || navigator.language.slice(0, 2);
if (!languages.includes(language)) language = 'en';
const hash = new URLSearchParams(location.hash.slice(1));
let session = hash.get('session');
if (session) { try { sessionStorage.setItem('lens.session', session); } catch {} history.replaceState(null, '', location.pathname); }
else { try { session = sessionStorage.getItem('lens.session'); } catch {} }
const state = { a: null, b: null, result: null, realResult: null, categories: new Set(), scope: 'cross', query: '', page: 0, expanded: new Set(), running: false, connecting: new Set(), job: null, notice: null, stopped: false };
const pageSize = 20;
const t = (key, values) => translate(language, key, values);
const number = value => new Intl.NumberFormat(language).format(value);
const el = (tag, className, text) => { const node = document.createElement(tag); if (className) node.className = className; if (text !== undefined) node.textContent = text; return node; };

async function api(path, options = {}) {
  let response;
  try { response = await fetch(path, { ...options, headers: { 'Content-Type': 'application/json', 'X-Lens-Session': session || '', ...options.headers } }); }
  catch { throw new Error('serverOffline'); }
  if (response.status === 403) throw new Error('sessionLost');
  let data;
  try { data = await response.json(); } catch { if (!response.ok) throw new Error('unexpectedError'); return null; }
  if (!response.ok) throw new Error(data.error || 'unexpectedError');
  return data;
}
function notify(key, kind = 'success', values = {}) { state.notice = { key, kind, values }; renderNotice(); }
function renderNotice() { const node = $('notification'); node.hidden = !state.notice; if (state.notice) { node.textContent = t(state.notice.key, state.notice.values); node.className = state.notice.kind; } }
function applyLanguage() {
  document.documentElement.lang = language; $('language').value = language;
  for (const node of document.querySelectorAll('[data-i18n]')) node.textContent = t(node.dataset.i18n);
  for (const node of document.querySelectorAll('[data-i18n-placeholder]')) node.placeholder = t(node.dataset.i18nPlaceholder);
  for (const node of document.querySelectorAll('[data-i18n-aria]')) node.setAttribute('aria-label', t(node.dataset.i18nAria));
  for (const slot of ['a', 'b']) renderConnection(slot);
  for (const button of document.querySelectorAll('.reveal')) button.textContent = t($('token-' + button.dataset.slot).type === 'password' ? 'showToken' : 'hideToken');
  renderNotice(); renderCategories(); renderResults(); renderProgress(state.lastProgress || []);
}
function controls() {
  $('scan').disabled = state.running || state.connecting.size > 0 || !state.a || !state.b || state.stopped;
  $('scan').hidden = state.running; $('cancel').hidden = !state.running;
  $('demo').disabled = state.running || state.stopped;
  $('ignore-case').disabled = state.running;
  for (const slot of ['a', 'b']) {
    for (const input of $('form-' + slot).querySelectorAll('input')) input.disabled = state.running || state.connecting.has(slot) || state.stopped;
    const button = $('form-' + slot).querySelector('[type=submit]'); button.disabled = state.running || state.connecting.has(slot) || state.stopped;
    button.querySelector('span').textContent = t(state.connecting.has(slot) ? 'connecting' : 'connect');
  }
}
function renderConnection(slot) {
  const info = state[slot];
  $('status-' + slot).textContent = t(state.connecting.has(slot) ? 'connecting' : info ? 'connected' : 'notConnected');
  $('status-' + slot).className = 'connection-status' + (info ? ' connected' : '');
  $('note-' + slot).textContent = info ? t('connectedNote', { components: number(info.componentCount), categories: number(info.categories.length) }) : t('memoryOnly');
  $('note-' + slot).className = 'connection-note';
  controls();
}
async function connect(slot, event) {
  event.preventDefault();
  if (state.running) return;
  const name = $('name-' + slot).value.trim();
  const endpoint = $('endpoint-' + slot).value.trim();
  const token = $('token-' + slot).value;
  state.connecting.add(slot); renderConnection(slot);
  try {
    const info = await api('/api/workspaces/' + slot + '/connect', { method: 'POST', body: JSON.stringify({ name, endpoint, token }) });
    state[slot] = info;
    $('token-' + slot).value = ''; $('token-' + slot).type = 'password';
    $('name-' + slot).value = info.name; $('endpoint-' + slot).value = info.endpoint;
    storage.set('lens.workspace.' + slot, JSON.stringify({ name: info.name, endpoint: info.endpoint }));
    state.result = null; state.realResult = null; state.categories.clear(); state.notice = null;
    renderCategories(); renderResults(); renderNotice();
  } catch (error) { notify(error.message, 'error'); }
  finally { state.connecting.delete(slot); renderConnection(slot); document.querySelector(`.reveal[data-slot="${slot}"]`).textContent = t($('token-' + slot).type === 'password' ? 'showToken' : 'hideToken'); }
}
function renderCategories() {
  const workspaces = state.result ? [state.result.a, state.result.b] : [state.a, state.b];
  const categories = categoriesFor(workspaces, state.result);
  const list = $('category-list'); list.replaceChildren();
  if (!categories.length) { list.append(el('p', 'category-empty', t('noCategories'))); return; }
  const all = el('label', 'category-option' + (!state.categories.size ? ' selected' : ''));
  const allCheck = el('input'); allCheck.type = 'checkbox'; allCheck.checked = !state.categories.size;
  allCheck.addEventListener('change', () => { state.categories.clear(); state.page = 0; renderCategories(); renderResults(); });
  all.append(allCheck, el('span', '', t('allCategories'))); list.append(all);
  const needle = $('category-search').value.toLocaleLowerCase();
  const visible = categories.filter(c => (c.path + c.name).toLocaleLowerCase().includes(needle));
  for (const category of visible) {
    const row = el('label', 'category-option' + (state.categories.has(category.key) ? ' selected' : ''));
    const check = el('input'); check.type = 'checkbox'; check.checked = state.categories.has(category.key);
    check.addEventListener('change', () => { check.checked ? state.categories.add(category.key) : state.categories.delete(category.key); state.page = 0; renderCategories(); renderResults(); });
    const label = el('span', '', category.name || t('uncategorized'));
    if (category.path && category.path !== category.name) label.append(el('small', '', category.path));
    row.title = category.path || t('uncategorized'); row.append(check, label, el('em', '', [...category.slots].sort().map(x => x.toUpperCase()).join(' · '))); list.append(row);
  }
  if (!visible.length) list.append(el('p', 'category-empty', t('noCategorySearch')));
}
function currentGroups() { return filterGroups(state.result, { scope: state.scope, query: state.query, categories: state.categories }); }
function emptyState(initial) {
  const box = el('div', 'empty-state');
  const diagram = el('div', 'empty-diagram'); diagram.setAttribute('aria-hidden', 'true');
  diagram.append(el('span', 'workspace-letter a', 'A'), el('span', '', '⇄'), el('span', 'workspace-letter b', 'B'));
  box.append(diagram, el('h3', '', t(initial ? 'initialTitle' : 'noMatches')), el('p', '', t(initial ? 'initialText' : 'noMatchesHint')));
  if (initial) box.append(el('small', '', t('initialFootnote')));
  return box;
}
function detailWorkspace(slot, group) {
  const box = el('section', 'detail-workspace'); const heading = el('h3');
  heading.append(el('span', 'workspace-letter ' + slot, slot.toUpperCase()), document.createTextNode(state.result[slot].name)); box.append(heading);
  const occurrences = group[slot];
  if (!occurrences.length) box.append(el('p', 'no-occurrence', t('noOccurrence')));
  for (const o of occurrences) {
    const card = el('article', 'occurrence-card');
    card.append(el('strong', '', o.name), el('p', 'reference', `${t('revision')} ${o.revision} · ${o.manufacturer} · ${o.mpn}`));
    if (o.description) card.append(el('p', '', o.description));
    card.append(el('p', '', o.category?.path || o.category?.name || t('uncategorized')));
    if (o.folder) card.append(el('p', '', o.folder));
    for (const source of o.evidence) card.append(el('span', 'evidence-tag', t(source)));
    if (o.detailError) card.append(el('p', 'detail-warning', t('detailWarning')));
    box.append(card);
  }
  return box;
}
function renderResults() {
  const result = state.result;
  $('demo-banner').hidden = !result?.demo;
  $('partial-banner').hidden = result?.completeness !== 'partial';
  $('partial-banner').textContent = result?.completeness === 'partial' ? t('partialNotice', { count: number(result.summary.detailFailures) }) : '';
  $('metric-cross').textContent = result ? number(result.summary.crossGroups) : '—';
  $('metric-a').textContent = result ? number(result.summary.componentsA) : state.a ? number(state.a.componentCount) : '—';
  $('metric-b').textContent = result ? number(result.summary.componentsB) : state.b ? number(state.b.componentCount) : '—';
  $('metric-quality').textContent = result ? number(result.summary.missingIdentity) : '—';
  $('metric-a-note').textContent = result?.a.name || t('latestRevisions'); $('metric-b-note').textContent = result?.b.name || t('latestRevisions');
  $('quality').disabled = !result;
  const groups = currentGroups();
  $('export-csv').disabled = !result; $('export-json').disabled = !result;
  const count = groups.length;
  state.page = Math.max(0, Math.min(state.page, Math.ceil(count / pageSize) - 1));
  const content = $('result-content'); content.replaceChildren();
  if (!result || !count) content.append(emptyState(!result));
  else {
    const wrapper = el('div', 'table-scroll'); const table = el('table');
    const head = el('thead'), header = el('tr');
    for (const key of ['colPart', 'colCategory', 'colA', 'colB', 'colDetails']) { const th = el('th', '', t(key)); th.scope = 'col'; header.append(th); }
    head.append(header); table.append(head); const body = el('tbody');
    for (const group of groups.slice(state.page * pageSize, (state.page + 1) * pageSize)) {
      const row = el('tr');
      const part = el('td'); const button = el('button', 'mpn-button', group.mpn); const opened = state.expanded.has(group.id);
      button.setAttribute('aria-expanded', String(opened)); button.setAttribute('aria-controls', 'details-' + group.id);
      const toggle = () => { opened ? state.expanded.delete(group.id) : state.expanded.add(group.id); renderResults(); const focus = document.querySelector(`[data-toggle="${group.id}"]`); focus?.focus({ preventScroll: true }); };
      button.dataset.toggle = group.id; button.addEventListener('click', toggle);
      part.append(button, el('span', 'manufacturer', group.manufacturer)); row.append(part);
      const categoryCell = el('td'); const categories = new Map([...group.a, ...group.b].map(o => [categoryKey(o.category), o.category]));
      for (const c of categories.values()) { const tag = el('span', 'category-tag', c?.name || t('uncategorized')); tag.title = c?.path || ''; categoryCell.append(tag); }
      row.append(categoryCell);
      for (const slot of ['a', 'b']) { const td = el('td'); td.append(el('span', 'occurrence-count' + (slot === 'b' ? ' blue' : '') + (!group[slot].length ? ' zero' : ''), number(group[slot].length))); row.append(td); }
      const actions = el('td'); const expand = el('button', 'expand-button', opened ? '−' : '+'); expand.setAttribute('aria-label', `${t('colDetails')} ${group.mpn}`); expand.setAttribute('aria-expanded', String(opened)); expand.setAttribute('aria-controls', 'details-' + group.id); expand.addEventListener('click', toggle); actions.append(expand); row.append(actions); body.append(row);
      const detailRow = el('tr', 'detail-row'); detailRow.id = 'details-' + group.id; detailRow.hidden = !opened;
      if (opened) { const cell = el('td'); cell.colSpan = 5; const grid = el('div', 'details-grid'); grid.append(detailWorkspace('a', group), detailWorkspace('b', group)); cell.append(grid); detailRow.append(cell); }
      body.append(detailRow);
    }
    table.append(body); wrapper.append(table); content.append(wrapper);
  }
  $('result-caption').textContent = result ? t(count === 1 ? 'resultsCountOne' : 'resultsCount', { count: number(count), date: new Intl.DateTimeFormat(language, { dateStyle: 'short', timeStyle: 'short' }).format(new Date(result.createdAt)) }) : t('resultsCaption');
  $('pagination').hidden = count <= pageSize;
  $('page-info').textContent = t('pageInfo', { from: number(state.page * pageSize + 1), to: number(Math.min((state.page + 1) * pageSize, count)), count: number(count) });
  $('previous').disabled = state.page === 0; $('next').disabled = (state.page + 1) * pageSize >= count;
  renderQuality();
}
function renderQuality() {
  const box = $('quality-content'); box.replaceChildren(); if (!state.result) { $('quality-panel').hidden = true; return; }
  const issues = state.result.issues.filter(i => categoryMatches(i.category, state.categories));
  if (!issues.length) box.append(el('p', 'category-empty', t('noIssues')));
  else {
    const list = el('div', 'quality-list');
    for (const issue of issues.slice(0, 200)) {
      const item = el('div', 'quality-item'); const detail = el('div', '', `${issue.name} · ${issue.workspaceName}`); detail.append(el('small', '', t(issue.reason + 'Reason')));
      item.append(el('span', '', issue.workspace.toUpperCase()), detail); list.append(item);
    }
    box.append(list);
    if (issues.length > 200) box.append(el('p', 'quality-more', t('pageInfo', { from: 1, to: 200, count: number(issues.length) })));
  }
  $('export-issues').disabled = !issues.length;
}
function renderProgress(progress) {
  state.lastProgress = progress;
  const box = $('progress-lines'); box.replaceChildren();
  for (const p of progress) {
    const row = el('div', 'progress-line'); const text = el('div', '', state[p.workspace]?.name || p.workspace.toUpperCase()); text.append(el('p', '', t(p.phase)));
    const bar = el('progress'); bar.max = Math.max(1, p.total); bar.value = p.loaded; bar.setAttribute('aria-label', `${p.workspace.toUpperCase()} ${t(p.phase)}`);
    row.append(text, bar, el('span', '', `${number(p.loaded)} / ${number(p.total)}`)); box.append(row);
  }
}
async function scan(existingJob = null) {
  state.running = true; controls(); state.notice = null; renderNotice();
  $('progress-panel').hidden = false; renderProgress([]);
  // A previous scan must never look like the report of an in-progress or failed scan.
  state.result = null; state.realResult = null; state.expanded.clear(); renderResults();
  try {
    let job = existingJob || await api('/api/scans', { method: 'POST', body: JSON.stringify({ ignoreCase: $('ignore-case').checked }) });
    state.job = job.id;
    while (job.status === 'running') {
      renderProgress(job.progress);
      await new Promise(resolve => setTimeout(resolve, 700));
      job = await api('/api/scans/' + state.job);
    }
    renderProgress(job.progress);
    if (job.status === 'completed') {
      state.result = await api(`/api/scans/${state.job}/results`); state.realResult = state.result; state.page = 0;
      $('ignore-case').checked = state.result.ignoreCase;
      notify(state.result.completeness === 'partial' ? 'partialNotice' : 'completeNotice', state.result.completeness === 'partial' ? 'warning' : 'success', { count: number(state.result.completeness === 'partial' ? state.result.summary.detailFailures : state.result.summary.crossGroups) });
      renderCategories(); renderResults();
    } else if (job.status === 'cancelled') notify('cancelledNotice', 'warning');
    else throw new Error(job.errorCode || 'unexpectedError');
  } catch (error) { notify(error.message, 'error'); }
  finally { state.running = false; state.job = null; controls(); $('progress-panel').hidden = true; $('cancel').disabled = false; }
}
function download(contents, extension, mime) {
  const url = URL.createObjectURL(new Blob([contents], { type: mime }));
  const anchor = el('a'); anchor.href = url; anchor.download = `component-lens-${state.result?.demo ? 'DEMO-' : ''}${new Date().toISOString().slice(0, 10)}.${extension}`;
  document.body.append(anchor); anchor.click(); anchor.remove(); setTimeout(() => URL.revokeObjectURL(url), 1000);
}
for (const slot of ['a', 'b']) {
  try { const saved = JSON.parse(storage.get('lens.workspace.' + slot)); if (saved) { $('name-' + slot).value = saved.name; $('endpoint-' + slot).value = saved.endpoint; } } catch {}
  $('form-' + slot).addEventListener('submit', event => connect(slot, event));
  // Editing connection settings invalidates the connected UI until explicitly reconnected.
  for (const input of $('form-' + slot).querySelectorAll('input')) input.addEventListener('input', () => { if (state[slot]) { state[slot] = null; renderConnection(slot); } });
}
for (const button of document.querySelectorAll('.reveal')) button.addEventListener('click', () => { const input = $('token-' + button.dataset.slot); input.type = input.type === 'password' ? 'text' : 'password'; button.textContent = t(input.type === 'password' ? 'showToken' : 'hideToken'); });
$('language').addEventListener('change', () => { language = $('language').value; storage.set('lens.language', language); applyLanguage(); });
$('scan').addEventListener('click', () => scan());
$('cancel').addEventListener('click', async () => { if (!state.job) return; $('cancel').disabled = true; try { await api('/api/scans/' + state.job + '/cancel', { method: 'POST' }); } catch (error) { notify(error.message, 'error'); $('cancel').disabled = false; } });
$('demo').addEventListener('click', async () => { try { const result = await api('/api/demo'); state.result = result; state.expanded.clear(); state.categories.clear(); state.page = 0; state.scope = 'cross'; state.query = ''; $('result-search').value = ''; syncTabs(); state.notice = null; renderNotice(); renderCategories(); renderResults(); } catch (error) { notify(error.message, 'error'); } });
$('leave-demo').addEventListener('click', () => { state.result = state.realResult; state.expanded.clear(); state.categories.clear(); state.page = 0; renderCategories(); renderResults(); });
$('category-search').addEventListener('input', renderCategories);
$('clear-categories').addEventListener('click', () => { state.categories.clear(); $('category-search').value = ''; state.page = 0; renderCategories(); renderResults(); });
$('result-search').addEventListener('input', () => { state.query = $('result-search').value; state.page = 0; renderResults(); });
function syncTabs() { for (const button of document.querySelectorAll('[data-scope]')) { const active = button.dataset.scope === state.scope; button.classList.toggle('active', active); button.setAttribute('aria-selected', String(active)); button.tabIndex = active ? 0 : -1; } }
for (const button of document.querySelectorAll('[data-scope]')) {
  button.addEventListener('click', () => { state.scope = button.dataset.scope; state.page = 0; syncTabs(); renderResults(); });
  button.addEventListener('keydown', event => { if (!['ArrowRight', 'ArrowLeft', 'Home', 'End'].includes(event.key)) return; event.preventDefault(); const tabs = [...document.querySelectorAll('[data-scope]')]; const current = tabs.indexOf(button); const index = event.key === 'Home' ? 0 : event.key === 'End' ? tabs.length - 1 : (current + (event.key === 'ArrowRight' ? 1 : -1) + tabs.length) % tabs.length; tabs[index].click(); tabs[index].focus(); });
}
$('previous').addEventListener('click', () => { state.page--; renderResults(); }); $('next').addEventListener('click', () => { state.page++; renderResults(); });
$('quality').addEventListener('click', () => { $('quality-panel').hidden = !$('quality-panel').hidden; if (!$('quality-panel').hidden) $('quality-panel').scrollIntoView({ behavior: 'smooth', block: 'nearest' }); });
$('close-quality').addEventListener('click', () => { $('quality-panel').hidden = true; });
$('export-csv').addEventListener('click', () => download(exportCsv(state.result, currentGroups()), 'csv', 'text/csv;charset=utf-8'));
$('export-json').addEventListener('click', () => { const result = state.result; const groups = currentGroups(); download(JSON.stringify({ ...result, export: { scope: state.scope, query: state.query, categories: [...state.categories], filteredGroupCount: groups.length, summaryScope: 'fullScan', issuesScope: 'fullScan' }, groups }, null, 2), 'json', 'application/json'); });
$('export-issues').addEventListener('click', () => { const issues = state.result.issues.filter(i => categoryMatches(i.category, state.categories)); download(toCsv(['workspace', 'workspace_name', 'component_id', 'component_name', 'category', 'reason', 'demo', 'completeness'], issues.map(i => [i.workspace.toUpperCase(), i.workspaceName, i.id, i.name, i.category?.path || '', i.reason, state.result.demo, state.result.completeness])), 'issues.csv', 'text/csv;charset=utf-8'); });
$('quit').addEventListener('click', async () => { try { await api('/api/quit', { method: 'POST' }); state.stopped = true; controls(); notify('stopped'); } catch (error) { notify(error.message, 'error'); } });
applyLanguage(); syncTabs();
if (!session) notify('sessionLost', 'error');
else {
  try {
    const current = await api('/api/session');
    for (const slot of ['a', 'b']) if (current[slot]) {
      state[slot] = current[slot]; $('name-' + slot).value = current[slot].name; $('endpoint-' + slot).value = current[slot].endpoint; renderConnection(slot);
    }
    renderCategories(); renderResults();
    if (current.job?.status === 'running') await scan(current.job);
    else if (current.job?.status === 'completed') {
      state.result = await api(`/api/scans/${current.job.id}/results`); state.realResult = state.result; $('ignore-case').checked = state.result.ignoreCase; renderCategories(); renderResults();
    } else if (current.job?.status === 'failed') notify(current.job.errorCode, 'error');
  } catch (error) { notify(error.message, 'error'); }
}
