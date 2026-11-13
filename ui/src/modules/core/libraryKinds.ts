/**
 * Core's library kinds (docs/plan-user-extensibility.md §4, docs/plan-extensions.md §3): for each file kind of a library of the
 * shell's own (symbols, types, line types, standards, rules, parameters, hatches, mappings) how its names are read and how the
 * file comes into the open project through the library's own importer (the firm folders and the Catalog use it), and the
 * extension installers of the kinds that arrived with them: each package checked without the engine, kept on this computer
 * and, on request, brought into the project.
 */

import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { readTitleBlock, readLineTypes, readMapping, readParameters, readPatterns, readRulePack, readStandards, readSymbolPack, readTypePack } from '../../platform/extensions/packs';
import { fileKindInstaller } from '../../platform/extensions/fileKind';
import { addContentInstaller } from '../../platform/extensions/runtime';
import { computerFileId, computerFileRows, forgetComputerFile, keepComputerFile, registerLibraryKind } from '../../platform/libraries';
import { forgetSymbol, keepSymbol, librarySymbols, parsePack } from './symbols/symbolModel';
import { importSymbols } from './symbols/symbols';
import { importLin, readLin } from './lineTypes/lineTypes';
import { defaultLineChoices } from './lineTypes/lineTypeModel';
import { importDefs } from './classifications/classifications';
import { ask, runOk } from './LibraryChoices';
import { defaultStandardsChoices, type StandardsPack } from './standardsLibraryModel';
import { forgetMappingReceipt, parseMappingFile, rememberMappingFile, type MappingReceipt } from './tables/tableModel';
import { keepPatterns } from './myPatterns';
import { forgetHatchPreviews } from './hatchCache';
import { importTitleBlock } from './titleBlocks/titleBlocks';
import type { TbFile } from './titleBlocks/titleBlockModel';

const bytes = (text: string) => new TextEncoder().encode(text);
const connected = () => useSession.getState().connected;
const needProject = (): boolean => { if (!connected()) { notify('Open a project first.'); return false; } return true; };

// ------------------------------------------------------------ bringing a file into the project

export async function bringSymbols(text: string): Promise<string | null> {
  if (!needProject()) return null;
  const r = await importSymbols(text, {});
  return r ? `Symbols imported: ${r.added} added, ${r.updated} updated.` : null;
}

export async function bringLineTypes(text: string): Promise<string | null> {
  if (!needProject()) return null;
  const read = await readLin(text, 'mm');
  if (!read || read.lineTypes.length === 0) return null;
  const r = await importLin({ text, units: 'mm', names: read.lineTypes.map((l) => l.name), choices: defaultLineChoices(read.lineTypes) });
  return r ? `${r.added} line types added, ${r.updated} updated, ${r.skipped} skipped.` : null;
}

export async function bringStandards(text: string): Promise<string | null> {
  if (!needProject()) return null;
  const pack = await ask<StandardsPack>('standards.readPack', { text });
  if (!pack) return null;
  const r = await runOk<{ added?: number; updated?: number; kept?: number; skipped?: number }>('standards.importFrom', { text, choices: defaultStandardsChoices(pack) });
  return r ? `${r.added ?? 0} added, ${r.updated ?? 0} updated, ${r.kept ?? 0} the same, ${r.skipped ?? 0} skipped.` : null;
}

export async function bringRules(text: string): Promise<string | null> {
  if (!needProject()) return null;
  const r = await runOk<{ id: string; name: string }>('rules.import', { text });
  return r ? `Rule pack added to the project: ${r.name}.` : null;
}

export async function bringParameters(text: string): Promise<string | null> {
  if (!needProject()) return null;
  const r = await importDefs(text);
  return r ? `${r.added} parameters added, ${r.updated} updated.` : null;
}

export async function bringTypes(text: string): Promise<string | null> {
  if (!needProject()) return null;
  const r = await execute('types.importFrom', { text }) as { created: number; reused: number } | null;
  return r ? `Types added to the project: ${r.created} new, ${r.reused} already in it.` : null;
}

export async function bringPatterns(text: string, file: string): Promise<string | null> {
  if (!needProject()) return null;
  const r = await execute('hatch.import', { text }) as { count: number } | null;
  if (!r) return null;
  forgetHatchPreviews();
  keepPatterns(file, text);
  return `${r.count} hatch pattern${r.count === 1 ? '' : 's'} added to the project.`;
}

/** A title block file (format lapin.titleBlock) added to the project's title blocks. */
export async function bringTitleBlock(text: string): Promise<string | null> {
  if (!needProject()) return null;
  const file = readTitleBlock(bytes(text));
  const id = await importTitleBlock(file as unknown as TbFile);
  return id ? `Title block added to the project: ${file.name}.` : null;
}

const said = (f: (text: string, file: string) => Promise<string | null>) => async (text: string, file: string): Promise<string | null> => {
  const r = await f(text, file);
  if (r) notify(r);
  return r;
};

const names = (read: (b: Uint8Array) => { names?: string[]; codes?: string[]; types?: { name: string }[] }) => (text: string): string[] => {
  const r = read(bytes(text));
  return r.names ?? r.codes ?? r.types?.map((t) => t.name) ?? [];
};

registerLibraryKind({ ext: 'lapinsymbols', label: 'Symbols', names: names(readSymbolPack), bring: said(bringSymbols) });
registerLibraryKind({ ext: 'lapintypes', label: 'Types', names: names(readTypePack), bring: said(bringTypes) });
registerLibraryKind({ ext: 'lin', label: 'Line types', names: names(readLineTypes), bring: said(bringLineTypes) });
registerLibraryKind({ ext: 'lapinstandards', label: 'Drafting standards', names: (t) => [readStandards(bytes(t)).name ?? 'Standards'], bring: said(bringStandards) });
registerLibraryKind({ ext: 'lapinrules', label: 'Rule packs', names: (t) => [readRulePack(bytes(t)).name], bring: said(bringRules) });
registerLibraryKind({ ext: 'lapinparams', label: 'Shared parameters', names: names(readParameters), bring: said(bringParameters) });
registerLibraryKind({ ext: 'pat', label: 'Hatch patterns', names: names(readPatterns), bring: said(bringPatterns) });
registerLibraryKind({ ext: 'lapintitleblock', label: 'Title blocks', names: (t) => [readTitleBlock(bytes(t)).name], bring: said(bringTitleBlock) });
registerLibraryKind({ ext: 'lapinmapping', label: 'Mappings', names: names(readMapping) });

// ------------------------------------------------------------ extension installers

const files = {
  keep: keepComputerFile,
  forget: (ext: string, name: string) => forgetComputerFile(computerFileId(ext, name)),
  get: async (ext: string, name: string) => (await computerFileRows.all()).find((f) => f.id === computerFileId(ext, name))?.text ?? null,
};

for (const [kind, ext, read, bring] of [
  ['lineTypes', 'lin', readLineTypes, bringLineTypes],
  ['standards', 'lapinstandards', readStandards, bringStandards],
  ['rules', 'lapinrules', readRulePack, bringRules],
  ['parameters', 'lapinparams', readParameters, bringParameters],
] as const) {
  addContentInstaller(fileKindInstaller({ kind, ext, read: (b) => { const r = read(b) as { text: string; names?: string[]; name?: string | null }; return { text: r.text, names: r.names ?? (r.name ? [r.name] : []) }; }, files, bring: said(bring) }));
}

/** Symbols: each kept in this computer's symbols (any project takes them: Manage → Symbols…); Add to this project brings them into the open one. */
addContentInstaller({
  kind: 'symbols',
  install: async (file, m, ctx) => {
    const r = readSymbolPack(file.bytes);
    const entries = parsePack(r.text);
    if (!entries) throw new Error('The file is not a Lapin symbol library.');
    const have = new Set(librarySymbols().map((s) => s.name));
    if (entries.some((e) => have.has(e.name)) && !(await ctx.confirm(`This computer's symbols already have one named like those of „${m.name}”. Keep both?`))) throw new Error('Not installed: yours were kept.');
    const ids: string[] = [];
    for (const e of entries) { const k = keepSymbol(e.name, e.def); if (!k) { ids.forEach(forgetSymbol); throw new Error("This computer's library of symbols is full."); } ids.push(k.id); }
    await keepComputerFile('lapinsymbols', m.name, r.text, m.id);
    return { ids, file: m.name, names: r.names };
  },
  uninstall: async (r) => {
    for (const id of (Array.isArray(r.ids) ? r.ids : []) as string[]) forgetSymbol(id);
    if (typeof r.file === 'string') await forgetComputerFile(computerFileId('lapinsymbols', r.file));
  },
  open: { label: 'Symbols…', run: () => useUi.setState({ dialog: 'symbols' }) },
});

/** Mappings: remembered as the user's own (a table's by its headings, a layer map by layer), put back on uninstalling. */
addContentInstaller({
  kind: 'mapping',
  install: async (file) => {
    const r = readMapping(file.bytes);
    return { mapping: rememberMappingFile(parseMappingFile(r.text)) as unknown as Record<string, unknown>, names: r.names };
  },
  uninstall: async (r) => { if (r.mapping) forgetMappingReceipt(r.mapping as unknown as MappingReceipt); },
});

