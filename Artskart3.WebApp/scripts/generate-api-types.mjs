#!/usr/bin/env node
/**
 * Fetches the OpenAPI spec from the running API server, filters to specified paths,
 * and generates TypeScript types using openapi-typescript.
 *
 * Usage: node scripts/generate-api-types.mjs
 *
 * Requires the API to be running (dotnet run on the Artskart3.Api project).
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import openapiTS, { astToString } from 'openapi-typescript';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const OUTPUT_FILE = path.resolve(__dirname, '../src/app/shared/types/api.generated.ts');
const SWAGGER_URL = 'https://localhost:5088/swagger/v1/swagger.json';

// Only generate types for these path prefixes (case-insensitive match)
const INCLUDED_PATHS = ['/api/search/observation', '/api/lookup/categories'];

async function fetchSpec() {
  const response = await fetch(SWAGGER_URL);
  if (!response.ok) {
    throw new Error(`Failed to fetch spec: ${response.status} ${response.statusText}`);
  }
  return response.json();
}

function filterSpec(spec) {
  const filtered = { ...spec, paths: {}, components: { ...spec.components, schemas: {} } };

  // Keep only matching paths
  for (const [pathKey, pathValue] of Object.entries(spec.paths || {})) {
    if (INCLUDED_PATHS.some((p) => pathKey.toLowerCase().startsWith(p))) {
      filtered.paths[pathKey] = pathValue;
    }
  }

  // Collect referenced schemas
  const referencedSchemas = new Set();
  collectRefs(filtered.paths, referencedSchemas);

  // Resolve transitive refs
  let size = 0;
  while (referencedSchemas.size > size) {
    size = referencedSchemas.size;
    for (const name of [...referencedSchemas]) {
      if (spec.components?.schemas?.[name]) {
        collectRefs(spec.components.schemas[name], referencedSchemas);
      }
    }
  }

  // Keep only referenced schemas
  for (const name of referencedSchemas) {
    if (spec.components?.schemas?.[name]) {
      filtered.components.schemas[name] = spec.components.schemas[name];
    }
  }

  return filtered;
}

function collectRefs(obj, refs) {
  if (obj === null || typeof obj !== 'object') return;
  if (Array.isArray(obj)) {
    obj.forEach((item) => collectRefs(item, refs));
    return;
  }
  if (obj.$ref && typeof obj.$ref === 'string') {
    const match = obj.$ref.match(/^#\/components\/schemas\/(.+)$/);
    if (match) refs.add(match[1]);
  }
  for (const value of Object.values(obj)) {
    collectRefs(value, refs);
  }
}

async function main() {
  console.log(`Fetching OpenAPI spec from ${SWAGGER_URL}...`);
  let spec;
  try {
    spec = await fetchSpec();
  } catch (e) {
    console.error(
      `\nError: Could not fetch the OpenAPI spec.\nMake sure the API is running (dotnet run in Artskart3.Api).\n\n${e.message}`
    );
    process.exit(1);
  }

  console.log(`Filtering spec to paths: ${INCLUDED_PATHS.join(', ')}`);
  const filtered = filterSpec(spec);

  const pathCount = Object.keys(filtered.paths).length;
  const schemaCount = Object.keys(filtered.components.schemas).length;
  console.log(`Generating types for ${pathCount} path(s) and ${schemaCount} schema(s)...`);

  const ast = await openapiTS(filtered);
  const output = astToString(ast);

  fs.mkdirSync(path.dirname(OUTPUT_FILE), { recursive: true });
  fs.writeFileSync(OUTPUT_FILE, output, 'utf-8');
  console.log(`✓ Types written to ${path.relative(process.cwd(), OUTPUT_FILE)}`);
}

main();
