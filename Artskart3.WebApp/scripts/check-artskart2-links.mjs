// Checks that old Artskart 2 links still translate correctly against an Artskart 3 database, typically after it has
// been rebuilt. Read-only: it queries the Artskart 3 API given and Artskart 2 production.
//
// Usage (Node 23.6 or later, which loads the TypeScript translator directly):
//   node --no-warnings scripts/check-artskart2-links.mjs [--api https://localhost:49219] [--year-to 2025] [--skip-counts]
//
// --year-to should be the last whole year before the database was copied, so both apps count the same records.
// The count comparison sends about 50 count queries to each app and takes a couple of minutes.
// Run Scripts/CheckArtskart2Links.sql against the same database too; see Scripts/README.md for the whole list.

import { readFileSync } from 'node:fs';
import https from 'node:https';
import { translateArtskart2Filter } from '../src/app/shared/guards/artskart2-filter.ts';

const args = process.argv.slice(2);
const option = (name, fallback) => (args.includes(name) ? args[args.indexOf(name) + 1] : fallback);
const API = option('--api', 'https://localhost:49219').replace(/\/$/, '');
const YEAR_TO = Number(option('--year-to', new Date().getFullYear() - 1));
const SKIP_COUNTS = args.includes('--skip-counts');
const AK2 = 'https://artskart2.artsdatabanken.no/appapi/api/data';
const ASSETS = new URL('../src/assets/', import.meta.url);
const areaTable = JSON.parse(readFileSync(new URL('artskart2-areas.json', ASSETS), 'utf8'));
const nodeTable = JSON.parse(readFileSync(new URL('artskart2-nodes.json', ASSETS), 'utf8'));

let problems = 0;
const report = (ok, message) => {
  if (!ok) problems++;
  console.log(`${ok ? 'OK  ' : 'FEIL'} ${message}`);
};

// A local API has a self-signed certificate, so certificates are only checked for other hosts.
const local = /^https:\/\/(localhost|127\.0\.0\.1)[:/]/.test(API + '/');
function ak3(method, path, body, headers = {}) {
  return new Promise((resolve, reject) => {
    const request = https.request(
      API + path,
      { method, rejectUnauthorized: !local, headers: { 'X-CSRF': '1', 'Content-Type': 'application/json', ...headers } },
      (response) => {
        let data = '';
        response.on('data', (chunk) => (data += chunk));
        response.on('end', () => resolve({ status: response.statusCode, body: data ? JSON.parse(data) : null }));
      },
    );
    request.on('error', reject);
    request.setTimeout(900_000);
    if (body !== undefined) request.write(JSON.stringify(body));
    request.end();
  });
}
const ak3Get = async (path) => {
  const { status, body } = await ak3('GET', path);
  return status === 200 ? body : null;
};
async function ak2(path) {
  const response = await fetch(`${AK2}/${path}`);
  if (!response.ok) throw new Error(`Artskart 2 answered ${response.status} for ${path}`);
  return response.json();
}

/** Runs `task` over `items`, a few at a time. */
async function inBatches(items, task, size = 8) {
  const results = [];
  for (let i = 0; i < items.length; i += size) results.push(...(await Promise.all(items.slice(i, i + size).map(task))));
  return results;
}

async function checkAreaTable() {
  console.log('\n== Area table (src/assets/artskart2-areas.json) against the Artskart 3 area list');
  const areas = await ak3Get('/api/Lookup/Areas');
  const fids = (group) => new Set((areas?.[group]?.areas ?? []).map((area) => area.fid));
  const known = {
    countyIds: new Set([...fids('counties'), ...fids('svalbardBjørnøyaAndJanMayen')]),
    municipalityIds: fids('municipalities'),
    restrictedAreaIds: fids('restrictedAreas'),
    oceanAreaIds: fids('oceanAreas'),
  };

  const missing = [];
  const historical = [];
  for (const [id, entry] of Object.entries(areaTable)) {
    if (entry[0] === 'historical') historical.push([id, entry]);
    else entry.slice(1).forEach((code) => known[entry[0]].has(code) || missing.push(`${id} -> ${entry[0]} ${code}`));
  }
  const missingReserves = missing.filter((line) => line.includes('restrictedAreaIds'));
  const missingOther = missing.filter((line) => !line.includes('restrictedAreaIds'));
  report(missingOther.length === 0, `Counties, municipalities, ocean and Svalbard areas: ${missingOther.length} codes missing ${missingOther.slice(0, 10).join('; ')}`);
  // Reserves Artskart 2 knew and the copy doesn't have yet only cost the reserve filter; they aren't an error in the table.
  console.log(`INFO Reserves missing from Artskart 3: ${missingReserves.length}`);

  const found = await inBatches(historical, async ([id, [, type, code, name]]) => {
    const matches = (await ak3Get(`/api/Lookup/Areas/Search?search=${encodeURIComponent(name)}`)) ?? [];
    return matches.some((area) => !area.isCurrent && area.areaTypeId === type && area.fid === code) ? null : `${id} ${name} (${code})`;
  });
  const notFound = found.filter(Boolean);
  // A historical area without locations isn't searchable, and filtering on it would find nothing anyway.
  console.log(`INFO Historical areas not found by name: ${notFound.length} of ${historical.length} ${notFound.slice(0, 10).join('; ')}`);
  report(notFound.length < historical.length / 2, 'Historical areas are still found by name, type and code');
}

async function checkSharedIds() {
  console.log('\n== IDs shared with Artskart 2 production');
  for (const id of [1000000, 10000000, 30000000, 50000000]) {
    const feature = JSON.parse(await ak2(`GetFeature?observationId=${id}`)).features?.[0];
    const observation = await ak3Get(`/api/observations/${id}`);
    if (!feature || !observation) {
      console.log(`INFO Observation ${id}: ${feature ? '' : 'not in Artskart 2 '}${observation ? '' : 'not in Artskart 3'}`);
      continue;
    }
    const [east, north] = feature.geometry.coordinates;
    const same = String(observation.locationId) === String(feature.id) && Math.abs(observation.point.east - east) < 2 && Math.abs(observation.point.north - north) < 2;
    report(same, `Observation ${id} has the same location in both (${feature.id} / ${observation.locationId})`);
  }

  for (const id of [1, 441, 655, 1749, 2046, 2592, 17216]) {
    const description = (await ak2(`GetObservationCount?collectionIds=${id}&yearTo=${YEAR_TO}`)).SearchFilterDescription;
    const ak2Name = description?.Collections?.[0];
    const ak3Name = (await ak3Get(`/api/Lookup/Organizations/${id}`))?.name;
    report(ak2Name === ak3Name, `Organisation ${id}: "${ak2Name}" / "${ak3Name}"`);
  }

  for (const guid of ['45652bc7-5911-4918-ae64-0007e7969f73', '353d7bff-6e80-4de3-915e-001d25e4125b']) {
    const bookmark = await ak3Get(`/api/Lookup/Artskart2Bookmarks/${guid}`);
    report(bookmark?.Id === guid, `Bookmark ${guid} is in the Artskart 3 database`);
  }
}

// The same steps as Artskart2LinkService, so the counts measure the link translation the app does.
async function translate(source) {
  const { filter, dropped, areaIds, catalogNumbers, collectionIds, nodeIds } = translateArtskart2Filter(source, new Date().getFullYear());
  for (const id of areaIds) {
    const area = areaTable[id];
    if (!area) {
      dropped.push('AreaIds');
    } else if (area[0] === 'historical') {
      const [, type, code, name] = area;
      const matches = (await ak3Get(`/api/Lookup/Areas/Search?search=${encodeURIComponent(name)}`)) ?? [];
      const match = matches.find((a) => !a.isCurrent && a.areaTypeId === type && a.fid === code);
      if (match) filter.historicalAreaIds = [...(filter.historicalAreaIds ?? []), match.id];
      else dropped.push('AreaIds');
    } else {
      filter[area[0]] = [...new Set([...(filter[area[0]] ?? []), ...area.slice(1)])];
    }
  }
  for (const number of catalogNumbers) {
    const matches = ((await ak3Get(`/api/Lookup/CatalogNumbers?search=${encodeURIComponent(number)}`)) ?? []).filter(
      (match) => match.catalogNumber?.toLowerCase() === number.toLowerCase(),
    );
    if (matches.length === 0) dropped.push('CatalogNumbers');
    const ids = matches.flatMap((match) => match.observationIds ?? []);
    if (ids.length) filter.observationIds = [...new Set([...(filter.observationIds ?? []), ...ids])];
  }
  if (collectionIds.length) {
    const fields = { 1: 'organizationIds', 2: 'datasetOrgIds', 3: 'projectOrgIds' };
    const found = [];
    for (const id of collectionIds) {
      const field = fields[(await ak3Get(`/api/Lookup/Organizations/${id}`))?.organizationTypeId];
      if (field) found.push({ id, field });
    }
    const distinct = new Set(found.map((f) => f.field));
    const [field] = distinct;
    if (distinct.size !== 1 || filter[field] || found.length !== collectionIds.length) dropped.push('CollectionIds');
    if (distinct.size === 1 && !filter[field]) filter[field] = found.map((f) => f.id);
  }
  if (nodeIds.length) {
    const datasets = nodeIds.map((id) => nodeTable[id]);
    if (filter.datasetOrgIds || datasets.some((d) => !d)) dropped.push('NodeIds');
    else filter.datasetOrgIds = [...new Set(datasets.flat())];
  }
  return { filter, dropped: [...new Set(dropped)] };
}

async function compareCounts() {
  console.log(`\n== Counts in Artskart 2 production and Artskart 3, records up to ${YEAR_TO}`);
  const token = (await ak3('POST', '/api/export/csv/anonymous-session', {})).body.token;
  const ak3Count = async (filter) =>
    (await ak3('POST', '/api/export/csv/summary', { name: 'check', selectedColumns: [], filter }, { 'X-Anonymous-User-Id': token })).body.totalRows;
  const ak2Count = async (source) => {
    const params = new URLSearchParams({ epsgCode: '32633', includeSubTaxonIds: String(source.IncludeSubTaxonIds ?? true) });
    for (const [key, value] of Object.entries(source)) {
      if (key !== 'IncludeSubTaxonIds') params.set(key, Array.isArray(value) ? value.join(',') : String(value));
    }
    return (await ak2(`GetObservationCount?${params}`)).Count;
  };

  const oslo = 'POLYGON((255000 6640000,270000 6640000,270000 6655000,255000 6655000,255000 6640000))';
  // Gaps found when the translation was built, which come from how Artskart 3 filters, not from the links.
  const known = {
    precisionTo: 'Artskart 3 leaves out records without a coordinate precision',
    institution: 'Artskart 3 matches one institution per record',
    dropped: 'no Artskart 3 filter, dropped with a warning',
    svalbard: 'the Svalbard areas have slightly different boundaries',
  };
  const cases = [
    ['All records', {}],
    ['AK2 defaults: found, recovered, not hidden', { Found: [2], NotRecovered: [2], Blocked: [2] }],
    ['Absent only', { Found: [1] }],
    ['"Show found" alone, as found only', { Found: [2] }],
    ['Not recovered only', { NotRecovered: [1] }],
    ['Hidden only', { Blocked: [1] }],
    ['Not hidden', { Blocked: [2] }],
    ['Changed by annotation', { Blocked: [2], Annotated: [1] }],
    ['With images', { Img: [1] }],
    ['Without images', { Img: [2] }],
    ['Uncertain identification', { UnsureId: [1] }],
    ['Introduced', { Spontan: [1] }],
    ['Sensitive', { Sensitive: [1] }],
    ['No quality issues', { Errors: [2] }, known.dropped],
    ['Validated', { Valid: [1] }, known.dropped],
    ['Brugde (taxon 26016)', { TaxonIds: [26016] }],
    ['Håbranner (order 281)', { TaxonIds: [281] }],
    ['Taxon group 51', { TaxonGroupIds: [51] }],
    ['Categories 2 and 12', { Categories: [2, 12] }],
    ['Basis of record 1', { BasisOfRecords: [1] }],
    ['Behaviour 2', { Behaviors: [2] }],
    ['Institution 655', { InstitutionIds: [655] }, known.institution],
    ['June', { Months: [6] }],
    ['Years from 2000', { YearFrom: 2000 }],
    ['Precision up to 100 m', { CoordinatePrecisionTo: 100 }, known.precisionTo],
    ['Precision 100-1000 m', { CoordinatePrecisionFrom: 100, CoordinatePrecisionTo: 1000 }],
    ['Oslo county', { AreaIds: [2611] }],
    ['Stavanger municipality', { AreaIds: [2181] }],
    ['Ullasundet reserve', { AreaIds: [660] }],
    ['Akershus before 2020 (historical)', { AreaIds: [2606] }],
    ['Nordsjøen og Skagerrak (ocean)', { AreaIds: [39200] }],
    ['Svalbard county', { AreaIds: [39227] }, known.svalbard],
    ['Drawn area around Oslo', { Geometry: oslo }],
    ['Catalogue number 6099/85', { CatalogNumbers: ['6099/85'] }],
    ['Collection 441 (AK3 dataset)', { CollectionIds: [441] }],
    ['Dataset 17216 (AK3 project)', { CollectionIds: [17216] }],
    ['Import source 34', { NodeIds: [34] }],
    ['Brugde taxon-page link', { TaxonIds: [26016], IncludeSubTaxonIds: true, Found: [2], NotRecovered: [2], Blocked: [2], Style: 1 }],
  ];

  const rows = [];
  for (const [label, extra, explanation] of cases) {
    const source = { ...extra, YearTo: YEAR_TO };
    const [ak2Total, translated] = await Promise.all([ak2Count(source), translate(source)]);
    rows.push({ label, ak2Total, ak3Total: await ak3Count(translated.filter), dropped: translated.dropped, explanation });
  }

  // Both apps hold the same data up to the copy, so each case should differ about as much as all records do.
  const baseline = rows[0].ak3Total / rows[0].ak2Total;
  for (const { label, ak2Total, ak3Total, dropped, explanation } of rows) {
    const off = ak2Total ? ak3Total / ak2Total - baseline : 0;
    const flagged = Math.abs(off) > 0.05 && Math.abs(ak3Total - ak2Total) > 20 && !explanation;
    const line = `${label.padEnd(44)} ${String(ak2Total).padStart(11)} ${String(ak3Total).padStart(11)} ${(off * 100).toFixed(1).padStart(7)} pp`;
    report(!flagged, `${line}${dropped.length ? `  dropped: ${dropped.join(', ')}` : ''}${explanation ? `  (${explanation})` : ''}`);
  }
}

await checkAreaTable();
await checkSharedIds();
if (!SKIP_COUNTS) await compareCounts();
console.log(problems === 0 ? '\nNo problems found.' : `\n${problems} problem(s) found.`);
process.exitCode = problems === 0 ? 0 : 1;
