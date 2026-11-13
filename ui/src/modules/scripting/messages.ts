/**
 * What the Script panel's messages console says (decision 538): one row per step of a run (started, done with its duration,
 * stopped, failed with the error's last line and the script's file and line when the traceback gives them). Pure: the store
 * feeds the rows to the log (platform/messages.ts).
 */

import type { MessageInput } from '../../platform/messages';
import { doneText, type ScriptEvent } from './logic';

/** The last non-empty line of an error or traceback: the error itself (`ValueError: …`). */
export function lastLine(text: string | null | undefined): string {
  const lines = (text ?? '').split(/\r?\n/).map((l) => l.trim()).filter(Boolean);
  return lines[lines.length - 1] ?? '';
}

/**
 * Where a Python traceback ends in the script `file` (the name the run was given): the last frame `File "<file>", line N`.
 * A frame of the kernel's or a library's is not the script's: left out. null when the traceback has none.
 */
export function tracebackSite(traceback: string | null | undefined, file: string): { file: string; line: number } | null {
  let site: { file: string; line: number } | null = null;
  for (const m of (traceback ?? '').matchAll(/File "([^"\n]+)", line (\d+)/g)) if (m[1] === file) site = { file: m[1]!, line: Number(m[2]) };
  return site;
}

/** The rows one event of the kernel makes, for the run `name` (null: a run of the console, which has its own lines). */
export function runMessages(e: ScriptEvent, name: string | null): MessageInput[] {
  if (e.type === 'exit') return [{ kind: 'warning', source: 'Python', text: 'Python ended; the next Run starts it again.' }];
  if (name === null) return [];
  const run = { sourcePlain: true, source: name };
  if (e.type === 'start') return [{ ...run, kind: 'info', text: 'Started' }];
  if (e.type !== 'done') return [];
  if (e.ok) return [{ ...run, kind: 'info', text: doneText(e) }];
  if (e.interrupted) return [{ ...run, kind: 'warning', text: e.error || 'Stopped.', textPlain: !!e.traceback }];
  const site = tracebackSite(e.traceback, name);
  const line = site?.line ?? e.line ?? null;
  const error = lastLine(e.error) || lastLine(e.traceback) || 'The script failed.';
  return [{
    kind: 'error', sourcePlain: true, source: line ? `${name}:${line}` : name, text: error, textPlain: !!(e.traceback || e.line),
    ...(line ? { target: { script: name, line } } : { target: { script: name } }),
  }];
}
