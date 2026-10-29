export function categoryKey(category) {
  return category ? (category.path || category.name || category.id).normalize('NFKC').trim().replace(/\\/g, '/').replace(/\s*\/\s*/g, '/').toLocaleLowerCase('en') : '__uncategorized';
}
export function categoriesFor(workspaces, result) {
  const map = new Map();
  const add = c => { const key = categoryKey(c); if (!map.has(key)) map.set(key, { key, name: c?.name || '', path: c?.path || c?.name || '', slots: new Set() }); return map.get(key); };
  for (const w of workspaces.filter(Boolean)) for (const c of w.categories || []) add(c).slots.add(w.slot);
  if (result) {
    for (const g of result.groups) for (const o of [...g.a, ...g.b]) add(o.category).slots.add(o.workspace);
    for (const i of result.issues) add(i.category).slots.add(i.workspace);
  }
  return [...map.values()].sort((a, b) => a.path.localeCompare(b.path));
}
export function categoryMatches(category, selected) {
  if (!selected.size) return true;
  const key = categoryKey(category);
  return [...selected].some(s => key === s || key.startsWith(s + '/'));
}
export function filterGroups(result, { scope = 'cross', query = '', categories = new Set() } = {}) {
  if (!result) return [];
  const needle = query.trim().toLocaleLowerCase();
  return result.groups.filter(g => {
    if (scope === 'cross' && g.scope !== 'cross') return false;
    if (scope === 'a' && g.a.length < 2) return false;
    if (scope === 'b' && g.b.length < 2) return false;
    const all = [...g.a, ...g.b];
    if (!all.some(o => categoryMatches(o.category, categories))) return false;
    return !needle || [g.manufacturer, g.mpn, g.id, ...all.flatMap(o => [o.name, o.revision, o.description, o.category?.path, o.category?.name, o.folder, o.manufacturer, o.mpn])].some(v => String(v || '').toLocaleLowerCase().includes(needle));
  });
}
export function csvCell(value) {
  let text = String(value ?? '');
  if (/^[\s]*[=+\-@\t\r\n]/.test(text) || /^[\t\r\n]/.test(text)) text = "'" + text;
  return '"' + text.replaceAll('"', '""') + '"';
}
export function toCsv(headers, rows) {
  return '\uFEFF' + [headers, ...rows].map(row => row.map(csvCell).join(',')).join('\r\n') + '\r\n';
}
export function exportCsv(result, groups) {
  const headers = ['group_id', 'scope', 'manufacturer', 'manufacturer_part_number', 'workspace', 'workspace_name', 'component_id', 'component_name', 'revision', 'category', 'folder', 'evidence', 'detail_error', 'demo', 'completeness', 'scan_utc', 'ignore_case'];
  const rows = groups.flatMap(g => [...g.a, ...g.b].map(o => [g.id, g.scope, o.manufacturer, o.mpn, o.workspace.toUpperCase(), o.workspaceName, o.id, o.name, o.revision, o.category?.path || o.category?.name || '', o.folder, o.evidence.join(';'), o.detailError || '', result.demo, result.completeness, result.createdAt, result.ignoreCase]));
  return toCsv(headers, rows);
}
