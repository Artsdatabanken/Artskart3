import { describe, expect, it } from 'vitest';
import * as fs from 'node:fs';
import * as path from 'node:path';
import * as process from 'node:process';

/**
 * Guards against referencing --adb-* design tokens that don't exist.
 * CSS custom properties fail silently (var() falls back or resolves to
 * nothing), so undefined token references are invisible to builds and
 * regular unit tests. This spec cross-checks every --adb-* variable used
 * in src/ against the definitions shipped by @artsdatabanken/tokens and
 * the styling hooks exposed by @artsdatabanken/components.
 */

const TOKEN_PATTERN = /--adb-[a-z0-9-]+/g;
const DECLARATION_PATTERN = /(--adb-[a-z0-9-]+)\s*:/g;

// Tokens that are intentionally neither defined by the design system
// packages nor by this app (e.g. documented external hooks).
const ALLOWLIST: string[] = [];

const ROOT = process.cwd();

function collectFiles(dir: string, extensions: string[]): string[] {
  if (!fs.existsSync(dir)) return [];
  const results: string[] = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      results.push(...collectFiles(full, extensions));
    } else if (extensions.some((ext) => entry.name.endsWith(ext))) {
      results.push(full);
    }
  }
  return results;
}

function tokensIn(file: string, pattern: RegExp): Set<string> {
  const content = fs.readFileSync(file, 'utf8');
  return new Set([...content.matchAll(pattern)].map((m) => m[1] ?? m[0]));
}

function definedTokens(): Set<string> {
  const defined = new Set<string>();
  // Tokens package: any occurrence is a definition or a reference to one.
  for (const file of collectFiles(path.join(ROOT, 'node_modules/@artsdatabanken/tokens/src'), ['.css'])) {
    tokensIn(file, TOKEN_PATTERN).forEach((t) => defined.add(t));
  }
  // Components package: exposes styling hooks (custom properties it reads)
  // in its bundled JS/CSS.
  for (const file of collectFiles(path.join(ROOT, 'node_modules/@artsdatabanken/components/dist'), [
    '.css',
    '.js',
  ])) {
    tokensIn(file, TOKEN_PATTERN).forEach((t) => defined.add(t));
  }
  // App-local definitions (--adb-foo: ...).
  for (const file of collectFiles(path.join(ROOT, 'src'), ['.css'])) {
    tokensIn(file, DECLARATION_PATTERN).forEach((t) => defined.add(t));
  }
  return defined;
}

describe('design tokens', () => {
  it('only references --adb-* variables that are defined', () => {
    const defined = definedTokens();
    const offenders: string[] = [];

    for (const file of collectFiles(path.join(ROOT, 'src'), ['.css'])) {
      const lines = fs.readFileSync(file, 'utf8').split('\n');
      lines.forEach((line: string, index: number) => {
        for (const match of line.matchAll(TOKEN_PATTERN)) {
          const token = match[0];
          if (!defined.has(token) && !ALLOWLIST.includes(token)) {
            offenders.push(`${path.relative(ROOT, file)}:${index + 1}  ${token}`);
          }
        }
      });
    }

    expect(
      offenders,
      `Undefined --adb-* token references found:\n${offenders.join('\n')}\n\n` +
        'Fix the reference or, if it is an intentional external hook, add it to ALLOWLIST in src/design-tokens.spec.ts',
    ).toEqual([]);
  });

  it('does not use fallbacks on --adb-* variables', () => {
    // A fallback such as var(--adb-x, #333) silently masks a
    // renamed or removed token, causing subtle styling bugs. Design tokens
    // are bundled with the app, so fallbacks are unnecessary.
    const fallbackPattern = /var\(\s*(--adb-[a-z0-9-]+)\s*,/;
    const offenders: string[] = [];

    for (const file of collectFiles(path.join(ROOT, 'src'), ['.css'])) {
      const lines = fs.readFileSync(file, 'utf8').split('\n');
      lines.forEach((line: string, index: number) => {
        const match = line.match(fallbackPattern);
        if (match) {
          offenders.push(`${path.relative(ROOT, file)}:${index + 1}  ${match[1]}`);
        }
      });
    }

    expect(
      offenders,
      `--adb-* variables used with a fallback:\n${offenders.join('\n')}\n\n` +
        'Remove the fallback so a missing token fails visibly instead of silently using a stale value.',
    ).toEqual([]);
  });
});
