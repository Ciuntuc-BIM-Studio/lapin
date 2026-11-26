/**
 * For the tests: an ES module's text evaluated as a module, written to a file of its own under the checkout's build/
 * (ignored by git; the test runner's loader takes neither blob: nor data: URLs, nor files outside the checkout), then imported.
 */

import { mkdirSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';

const dir = resolve(__dirname, '../../../../build/vitest-plugins');
let n = 0;

export async function importFromText(js: string): Promise<unknown> {
  mkdirSync(dir, { recursive: true });
  const file = join(dir, `plugin-${process.pid}-${Date.now()}-${++n}.mjs`);
  writeFileSync(file, js);
  return import(/* @vite-ignore */ file);
}
