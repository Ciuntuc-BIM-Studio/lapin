/**
 * The SDNF import's protocol (docs/sdnf.md, report #148): what the engine's steel.importSdnf answers, laid out as the
 * report dialog shows it and as the text the user copies or saves: each packet's count and what of it came in, what
 * was left out and why, what was changed, the notes, and the time it took.
 */

/** A reason, how many items it covers, and the first of them (each a number and its line in the file). */
export interface SdnfGroup { reason: string; count: number; items: string[] }

/** A packet as the file has it: its header's line, the items its count announced, those read, those imported. */
export interface SdnfPacket { packet: string; line: number; announced: number; read: number; imported: number }

export interface SdnfImportReport {
  name: string;
  version?: string;
  source?: string;
  made: number;
  updated: number;
  remade: number;
  plates: number;
  holes: number;
  grids: number;
  skipped: number;
  byClass?: Record<string, number>;
  unknownProfiles: string[];
  notes: string[];
  packets?: SdnfPacket[];
  skips?: SdnfGroup[];
  warnings?: SdnfGroup[];
  lines?: number;
  /** The engine's reading and model changes, ms (the bodies are built after). */
  ms?: number;
}

/** What each packet holds, as the protocol names it. */
export const PACKET_KINDS: Record<string, string> = {
  '10': 'linear members',
  '20': 'plates',
  '22': 'holes',
  '30': 'member loads (not imported)',
  '40': 'connection details (not imported)',
  '50': 'grid sets',
  '60': 'curved members',
};

/** The time an import took, as the protocol says it. */
export function durationText(ms: number): string {
  if (ms < 1000) return `${Math.max(0, Math.round(ms))} ms`;
  if (ms < 60_000) return `${(ms / 1000).toFixed(1)} s`;
  const s = Math.round(ms / 1000);
  return `${Math.floor(s / 60)} min ${s % 60} s`;
}

/** The headline: what came in (one phrase, translated by its pattern). */
export function sdnfSummary(r: SdnfImportReport): string {
  return `Imported ${r.name}: ${r.made} new, ${r.updated + r.remade} updated, ${r.plates} plates, ${r.holes} holes, ${r.grids} grids, ${r.skipped} left out.`;
}

/** What a group lists: its first items, and how many more it has. */
export function groupItems(g: SdnfGroup, shown = 10): string {
  const first = g.items.slice(0, shown).join(', ');
  const more = g.count - Math.min(shown, g.items.length);
  return more > 0 ? `${first} and ${more} more` : first;
}

/** The notes the engine writes besides its groups (the groups' own summary lines are left to the groups). */
export function plainNotes(r: SdnfImportReport): string[] {
  return r.notes.filter((n) => !/^(Left out|Changed), /.test(n));
}

/**
 * The protocol as plain text, to copy or save: the file, the packets, the elements by class, what was left out and
 * changed (every item the engine kept, not only those the dialog shows), the notes, the time. `totalMs`: the whole
 * import as the window saw it (reading, the model, its bodies built and drawn).
 */
export function sdnfReportText(r: SdnfImportReport, totalMs?: number, when = new Date()): string {
  const out: string[] = [];
  out.push(`SDNF import protocol — ${r.name}`);
  out.push(`Date: ${when.toISOString().replace('T', ' ').slice(0, 19)} UTC`);
  if (r.version || r.source) out.push(`File: ${[r.version, r.source ? `written by ${r.source}` : ''].filter(Boolean).join(', ')}${r.lines ? `, ${r.lines} lines` : ''}`);
  if (totalMs != null) out.push(`Time: ${durationText(totalMs)}${r.ms != null ? ` (reading and the model ${durationText(r.ms)}, the rest building the bodies)` : ''}`);
  else if (r.ms != null) out.push(`Time: ${durationText(r.ms)}`);
  out.push('');
  out.push(sdnfSummary(r));
  out.push(`Elements: ${r.made} new, ${r.updated} updated, ${r.remade} made again, ${r.skipped} left out.`);
  const classes = Object.entries(r.byClass ?? {});
  if (classes.length) out.push(`By class: ${classes.map(([k, v]) => `${k} ${v}`).join(', ')}.`);
  if (r.packets?.length) {
    out.push('');
    out.push('Packets:');
    for (const p of r.packets) {
      const kind = PACKET_KINDS[p.packet] ?? (p.packet === '00' ? 'title' : 'not defined by SDNF (not imported)');
      const counted = p.announced || p.read ? `: ${p.read} read${p.announced !== p.read ? ` of ${p.announced} announced` : ''}, ${p.imported} imported` : '';
      out.push(`  Packet ${p.packet} (line ${p.line}), ${kind}${counted}`);
    }
  }
  const groups = (head: string, gs: SdnfGroup[] | undefined) => {
    if (!gs?.length) return;
    out.push('');
    out.push(`${head}:`);
    for (const g of gs) {
      out.push(`  ${g.reason}: ${g.count}`);
      for (const i of g.items) out.push(`    ${i}`);
      if (g.count > g.items.length) out.push(`    … and ${g.count - g.items.length} more`);
    }
  };
  groups('Left out', r.skips);
  groups('Changed', r.warnings);
  if (r.unknownProfiles.length) {
    out.push('');
    out.push(`Profiles in no library (drawn as placeholders): ${r.unknownProfiles.join(', ')}`);
  }
  const notes = plainNotes(r);
  if (notes.length) {
    out.push('');
    out.push('Notes:');
    for (const n of notes) out.push(`  ${n}`);
  }
  return out.join('\n') + '\n';
}

/** The protocol's file name beside the SDNF's: `frame.sdnf` → `frame-import.txt`. */
export function reportFileName(name: string): string {
  return `${name.replace(/\.[^.]+$/, '') || 'sdnf'}-import.txt`;
}
