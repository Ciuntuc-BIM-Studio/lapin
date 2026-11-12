/**
 * Trust in a project's scripts (docs/plan-python-scripting.md §7, stage 3; decision 489). A script that came with a
 * project never runs as the project opens, and never without asking: its first run, or its first after its code
 * changed, asks "Run once / Trust / Cancel". Trust is kept on this computer by the SHA-256 of the code
 * (<user-dir>/scripting-trust.json, the engine host's script.trust / script.trusted); code this user saves into the
 * project is theirs, so it is trusted as it is saved. The user's own scripts (Mine) and what they type never ask.
 */

/** Where the editor's script comes from. */
export type ScriptSource =
  | { kind: 'new' }
  | { kind: 'mine'; name: string }
  | { kind: 'project'; id: string; name: string };

/** The SHA-256 of a script's code, as the engine keeps it (UTF-8, lowercase hex: ScriptingModule.HashOf). */
export async function sha256(code: string): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(code));
  return [...new Uint8Array(digest)].map((b) => b.toString(16).padStart(2, '0')).join('');
}

/** What this window knows of the trust kept on this computer: the hashes of the code known trusted. */
export interface TrustState { trusted: ReadonlySet<string> }

export const EMPTY_TRUST: TrustState = { trusted: new Set() };

/** Whether running this code asks first: only a project's script, whose code (as it is now) is not trusted here. */
export function asksTrust(source: ScriptSource, codeSha: string, trust: TrustState): boolean {
  return source.kind === 'project' && !trust.trusted.has(codeSha);
}

/** The answer kept: Trust remembers the code (the host keeps it on this computer); Run once runs it this time only, remembering nothing. */
export function remember(trust: TrustState, codeSha: string, answer: 'trust' | 'once'): TrustState {
  return answer === 'trust' ? { trusted: new Set([...trust.trusted, codeSha]) } : trust;
}

/** Where the script came from, as the question says it (the project's file name, without its folder). */
export function trustQuestion(project: string | null, script: string): string {
  return project ? `„${script}” came with the project „${project}”.` : `„${script}” came with this project.`;
}

/** What the question warns. */
export const TRUST_WARNING = 'A script can do anything you can do on this computer: run it only if you know where it comes from. Trust remembers this code on this computer; a change to it asks again.';
