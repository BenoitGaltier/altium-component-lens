import test from 'node:test';
import assert from 'node:assert/strict';
import { categoryKey, categoriesFor, filterGroups, csvCell, toCsv, exportCsv } from '../src/ComponentLens/Web/core.mjs';
import { dictionaries, languages, translate } from '../src/ComponentLens/Web/i18n.mjs';

const resistor = { id: 'res', name: 'Resistors', path: 'Passive/Resistors' };
const capacitor = { id: 'cap', name: 'Capacitors', path: 'Passive/Capacitors' };
const occurrence = (workspace, category, name = 'CMP-1') => ({ workspace, workspaceName: workspace, category, name, id: name, revision: '01', manufacturer: 'Yageo', mpn: 'RC-1', evidence: ['parameters'] });
const result = {
  demo: true, completeness: 'complete', createdAt: '2026-10-29T10:00:00Z', ignoreCase: true, issues: [],
  groups: [
    { id: 'D1', manufacturer: 'Yageo', mpn: 'RC-1', scope: 'cross', a: [occurrence('a', resistor)], b: [occurrence('b', capacitor)] },
    { id: 'D2', manufacturer: 'Murata', mpn: 'C-1', scope: 'a', a: [occurrence('a', capacitor, 'CAP-1'), occurrence('a', capacitor, 'CAP-2')], b: [] },
  ],
};
test('Category filter retains cross-workspace matches classified differently', () => {
  assert.equal(filterGroups(result, { categories: new Set([categoryKey(resistor)]) }).length, 1);
  assert.equal(filterGroups(result, { categories: new Set([categoryKey(capacitor)]) }).length, 1);
  assert.equal(filterGroups(result, { categories: new Set(['passive']) }).length, 1);
});
test('Category identities use full paths rather than workspace-local IDs', () => {
  assert.equal(categoryKey(resistor), categoryKey({ ...resistor, id: 'different', path: ' Passive / Resistors ' }));
  assert.notEqual(categoryKey(resistor), categoryKey({ ...resistor, path: 'Other/Resistors' }));
  const categories = categoriesFor([{ slot: 'a', categories: [resistor] }, { slot: 'b', categories: [{ ...resistor, id: 'different' }] }], null);
  assert.equal(categories.length, 1); assert.equal(categories[0].slots.size, 2);
});
test('Scope, search and category filters compose', () => {
  assert.equal(filterGroups(result, { scope: 'all' }).length, 2);
  assert.equal(filterGroups(result, { scope: 'a', query: 'cap-2' })[0].id, 'D2');
  assert.equal(filterGroups(result, { scope: 'b' }).length, 0);
  assert.equal(filterGroups(result, { query: '  yageo ' }).length, 1);
  assert.equal(filterGroups(result, { query: 'not present' }).length, 0);
});
test('CSV quotes commas/newlines/quotes and neutralizes spreadsheet formulas', () => {
  assert.equal(csvCell('a,"b"\nc'), '"a,""b""\nc"');
  for (const unsafe of ['=1+1', '+CMD', '-2+4', '@SUM(A1)', '\t=HYPERLINK()', '  =1']) assert.ok(csvCell(unsafe).startsWith('"\''));
  assert.ok(toCsv(['a'], [['b']]).startsWith('\uFEFF'));
});
test('Filtered CSV exports every occurrence and carries demo and completeness metadata', () => {
  const csv = exportCsv(result, [result.groups[0]]);
  assert.equal(csv.split('\r\n').length, 4);
  assert.ok(csv.includes('"demo"')); assert.ok(csv.includes('"true","complete"'));
  assert.ok(!csv.includes('"D2"'));
});
test('All UI and error strings are present in FR/EN/ES with identical placeholders', () => {
  const keys = Object.keys(dictionaries.en).sort();
  for (const lang of languages) {
    assert.deepEqual(Object.keys(dictionaries[lang]).sort(), keys);
    for (const key of keys) {
      assert.equal(typeof dictionaries[lang][key], 'string'); assert.ok(dictionaries[lang][key].length > 0);
      const placeholders = text => [...text.matchAll(/\{(\w+)\}/g)].map(x => x[1]).sort();
      assert.deepEqual(placeholders(dictionaries[lang][key]), placeholders(dictionaries.en[key]));
    }
  }
  assert.equal(translate('es', 'pageInfo', { from: 1, to: 20, count: 42 }), '1–20 de 42');
});
