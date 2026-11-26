// Writes src/assets/artskart2-areas.json: Artskart 2 production area ID -> the Artskart 3 filter it means.
//
// Old Artskart 2 links carry Artskart 2's own Area.Id, which doesn't exist in Artskart 3 (and differs
// between Artskart 2 test and production). Current areas map by code, which Artskart 3 filters on, so the
// table survives a rebuild of the Artskart 3 database. Historical areas filter on Artskart 3's own ID,
// so they are stored by type, code and name and looked up when a link opens.
//
// Usage: node scripts/generate-artskart2-areas.mjs

import { writeFileSync } from 'node:fs';

const SOURCE = 'https://artskart2.artsdatabanken.no/appapi/api/data/SearchAreas?name=%25&areaType=0';
const OUTPUT = new URL('../src/assets/artskart2-areas.json', import.meta.url);

const MUNICIPALITY = 1;
const COUNTY = 2;
const RESTRICTED_AREA = 3;
const OCEAN_AREA = 4;

// Artskart 3 got its own Svalbard and Jan Mayen areas with other codes; these are matched by name and
// checked against observation counts. Artskart 3 filters them as counties.
const SVALBARD_MUNICIPALITIES = { 2101: '2101', 2102: '2104', 2103: '2105', 2104: '2102', 2105: '2103', 2201: '2201' };

// Artskart 2 counties that are sea or Svalbard areas. 13 ("Norskekysten hav S") has no Artskart 3 match.
const SPECIAL_COUNTIES = {
  13: null,
  21: ['countyIds', '2101', '2102', '2103', '2104', '2105'],
  22: ['oceanAreaIds', '22'],
  23: ['oceanAreaIds', '23'],
  99: ['oceanAreaIds', '21'],
};

// Only "Nordsjøen og Skagerrak" has an Artskart 3 ocean area with the same extent.
const OCEAN_AREAS = { 91: ['oceanAreaIds', '13'] };

const HISTORICAL = /\s*\(historisk[^)]*\)\s*$/i;

function toFilter({ Type: type, Code: code, Name: name }) {
  if (HISTORICAL.test(name)) {
    return type === MUNICIPALITY || type === COUNTY ? ['historical', type, code, name.replace(HISTORICAL, '')] : null;
  }
  switch (type) {
    case MUNICIPALITY:
      return code in SVALBARD_MUNICIPALITIES ? ['countyIds', SVALBARD_MUNICIPALITIES[code]] : ['municipalityIds', code];
    case COUNTY:
      return code in SPECIAL_COUNTIES ? SPECIAL_COUNTIES[code] : ['countyIds', code];
    case RESTRICTED_AREA:
      return ['restrictedAreaIds', code];
    case OCEAN_AREA:
      return OCEAN_AREAS[code] ?? null;
    default:
      return null;
  }
}

const response = await fetch(SOURCE);
if (!response.ok) throw new Error(`${SOURCE} answered ${response.status}`);
const areas = await response.json();

const table = {};
const unmapped = [];
for (const area of [...new Map(areas.map((a) => [a.Id, a])).values()].sort((a, b) => a.Id - b.Id)) {
  const filter = toFilter(area);
  if (filter) table[area.Id] = filter;
  else unmapped.push(`${area.Id} ${area.Code} ${area.Name}`);
}

writeFileSync(OUTPUT, JSON.stringify(table) + '\n');
console.log(`Wrote ${Object.keys(table).length} areas. Without an Artskart 3 match:\n  ${unmapped.join('\n  ')}`);
