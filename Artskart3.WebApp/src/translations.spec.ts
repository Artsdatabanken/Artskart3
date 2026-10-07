import { describe, expect, it } from 'vitest';
import * as fs from 'node:fs';
import * as path from 'node:path';
import * as process from 'node:process';

/**
 * Keeps every translation file in sync with the reference language, including
 * languages not yet enabled in the UI. Missing keys and {{placeholders}} fail
 * silently at runtime, so builds and regular unit tests don't catch them.
 */

const LANGUAGES_DIR = path.join(process.cwd(), 'src/assets/languages');
const REFERENCE_LANGUAGE = 'no';
const PLACEHOLDER_PATTERN = /\{\{\s*([\w.]+)\s*\}\}/g;

interface Translations {
  [key: string]: string | Translations;
}

function loadTranslations(lang: string): Map<string, string> {
  const content = fs.readFileSync(path.join(LANGUAGES_DIR, `${lang}.json`), 'utf8');
  return flatten(JSON.parse(content) as Translations);
}

function flatten(node: Translations, prefix = '', result = new Map<string, string>()): Map<string, string> {
  for (const [key, value] of Object.entries(node)) {
    if (typeof value === 'object') {
      flatten(value, `${prefix}${key}.`, result);
    } else {
      result.set(`${prefix}${key}`, value);
    }
  }
  return result;
}

function placeholdersIn(text: string): string {
  return [...new Set([...text.matchAll(PLACEHOLDER_PATTERN)].map((m) => m[1]))].sort().join(', ');
}

const otherLanguages = fs
  .readdirSync(LANGUAGES_DIR)
  .filter((file) => file.endsWith('.json'))
  .map((file) => path.basename(file, '.json'))
  .filter((lang) => lang !== REFERENCE_LANGUAGE);

describe.each(otherLanguages)('translations: %s.json', (lang) => {
  const reference = loadTranslations(REFERENCE_LANGUAGE);
  const translations = loadTranslations(lang);

  it(`has every key from ${REFERENCE_LANGUAGE}.json`, () => {
    const missing = [...reference.keys()].filter((key) => !translations.has(key));

    expect(missing, `Keys in ${REFERENCE_LANGUAGE}.json missing from ${lang}.json:\n${missing.join('\n')}`).toEqual([]);
  });

  it(`has no keys that are missing from ${REFERENCE_LANGUAGE}.json`, () => {
    const extra = [...translations.keys()].filter((key) => !reference.has(key));

    expect(extra, `Keys in ${lang}.json missing from ${REFERENCE_LANGUAGE}.json:\n${extra.join('\n')}`).toEqual([]);
  });

  it(`uses the same {{placeholders}} as ${REFERENCE_LANGUAGE}.json`, () => {
    const mismatches: string[] = [];
    for (const [key, text] of translations) {
      const referenceText = reference.get(key);
      if (referenceText !== undefined && placeholdersIn(text) !== placeholdersIn(referenceText)) {
        mismatches.push(
          `${key}  ${REFERENCE_LANGUAGE}: [${placeholdersIn(referenceText)}]  ${lang}: [${placeholdersIn(text)}]`,
        );
      }
    }

    expect(mismatches, `Placeholder mismatches in ${lang}.json:\n${mismatches.join('\n')}`).toEqual([]);
  });
});
