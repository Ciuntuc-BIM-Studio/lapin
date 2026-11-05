#!/usr/bin/env node
/**
 * The check that the documents Lapin writes open in an office suite (docs/plan-documents.md, Exportul).
 *
 *   node scripts/documents-check.mjs [<out folder>] [--png]
 *
 * Runs DocumentWriterTests' sample test with LAPIN_DOCS_SAMPLES set to a folder (a temporary one by default), which
 * writes sample.docx, sample.odt and sample.pdf there; then, when LibreOffice is installed (the macOS app, else soffice
 * on PATH), converts the DOCX and the ODT to PDF headless and counts the pages of all three. Exits 1 when the samples
 * are not written or a conversion fails or gives no page; skips the conversion (exit 0) when LibreOffice is absent.
 * With --png, also converts each file's first page to PNG beside it, to look at.
 */

import { execFileSync, spawnSync } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync, statSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(fileURLToPath(import.meta.url), '../..');
const args = process.argv.slice(2);
const png = args.includes('--png');
const out = resolve(args.find((a) => !a.startsWith('--')) ?? mkdtempSync(join(tmpdir(), 'lapin-docs-')));

console.log(`samples → ${out}`);
const test = spawnSync('dotnet', ['test', join(root, 'engine/tests/Lapin.Engine.Tests'), '--filter', 'FullyQualifiedName~DocumentWriterTests.Samples_are_written_when_asked', '-v', 'q'], {
  env: { ...process.env, LAPIN_DOCS_SAMPLES: out },
  stdio: ['ignore', 'pipe', 'pipe'],
  encoding: 'utf8',
});
if (test.status !== 0) {
  console.error(test.stdout + test.stderr);
  console.error('the sample test failed');
  process.exit(1);
}
const samples = ['sample.docx', 'sample.odt', 'sample.pdf'].map((f) => join(out, f));
for (const f of samples) if (!existsSync(f)) { console.error(`missing ${f}`); process.exit(1); }

/** A PDF's pages: its /Type /Page objects (not /Pages). */
const pages = (file) => (readFileSync(file).toString('latin1').match(/\/Type\s*\/Page(?![a-zA-Z])/g) ?? []).length;

console.log(`Lapin's PDF: ${pages(samples[2])} pages, ${statSync(samples[2]).size} bytes`);

const mac = '/Applications/LibreOffice.app/Contents/MacOS/soffice';
const soffice = existsSync(mac) ? mac : spawnSync('soffice', ['--version'], { stdio: 'ignore' }).status === 0 ? 'soffice' : null;
if (!soffice) {
  console.log('LibreOffice not found: the conversion is skipped');
  process.exit(0);
}

// Its own profile, so a LibreOffice already open does not take the job (or hold it).
const profile = mkdtempSync(join(tmpdir(), 'lapin-lo-'));
const convert = (file, format, dir) => {
  execFileSync(soffice, [`-env:UserInstallation=file://${profile}`, '--headless', '--norestore', '--convert-to', format, '--outdir', dir, file], { stdio: 'pipe', timeout: 180_000 });
};

let failed = false;
for (const [file, kind] of [[samples[0], 'docx'], [samples[1], 'odt']]) {
  const dir = mkdtempSync(join(tmpdir(), `lapin-${kind}-`));
  try {
    convert(file, 'pdf', dir);
    const pdf = join(dir, 'sample.pdf');
    const n = existsSync(pdf) ? pages(pdf) : 0;
    console.log(`${kind} → PDF by LibreOffice: ${n} pages${n ? ` (${pdf})` : ''}`);
    if (n === 0) failed = true;
  } catch (e) {
    console.error(`${kind}: LibreOffice could not convert it: ${e.message}`);
    failed = true;
  }
}
if (png) {
  for (const file of samples) {
    const dir = join(out, `png-${file.split('.').pop()}`);
    try { convert(file, 'png', dir); console.log(`first page as PNG: ${join(dir, 'sample.png')}`); } catch (e) { console.error(`png of ${file}: ${e.message}`); }
  }
}
process.exit(failed ? 1 : 0);
