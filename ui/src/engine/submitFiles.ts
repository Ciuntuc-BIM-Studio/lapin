/**
 * Publishing → Submit files… (docs/plan-cde-references.md §8, stage R7): another tool's deliverables (PDFs, DXFs, IFCs) picked from
 * disk and put into the publish queue with a revision, a status (suitability) and a container, as Lapin's own issues are. The pure
 * parts: which files are taken, the statuses each container offers, what is wrong with the form, and the body the server takes
 * (`POST {p}/submissions` with no commit: the server takes main's, Atrium docs/atrium-server.md "Files from disk").
 */

/** The files a submission from disk takes: drawings and models of other programs. */
export const SUBMIT_EXTENSIONS = ['pdf', 'dxf', 'ifc', 'ifczip'] as const;
export const SUBMIT_ACCEPT = SUBMIT_EXTENSIONS.map((e) => `.${e}`).join(',');

export const isSubmittable = (name: string) => (SUBMIT_EXTENSIONS as readonly string[]).includes(name.toLowerCase().match(/\.([a-z0-9]+)$/)?.[1] ?? '');

/** The ISO 19650 status codes a container takes: S1–S7 for Shared; A1–A9, B1–B9 and CR for Published (the server's rule). */
export function suitabilities(purpose: 'shared' | 'published'): string[] {
  if (purpose === 'shared') return ['S1', 'S2', 'S3', 'S4', 'S5', 'S6', 'S7'];
  const n = [1, 2, 3, 4, 5, 6, 7, 8, 9];
  return [...n.map((i) => `A${i}`), ...n.map((i) => `B${i}`), 'CR'];
}

/** The status a container starts with: S2 and A1, as the server's defaults. */
export const defaultSuitability = (purpose: 'shared' | 'published') => (purpose === 'shared' ? 'S2' : 'A1');

export interface PickedFile { name: string; size: number }
export interface SubmitForm { files: PickedFile[]; revision: string; purpose: 'shared' | 'published'; suitability: string; issue: string; note: string }

/** What stops the form being sent, in words (the file it is about apart, so the words translate); an empty list when it may go. */
export interface Problem { file: string | null; text: string }
export function formProblems(f: SubmitForm): Problem[] {
  const out: Problem[] = [];
  if (f.files.length === 0) out.push({ file: null, text: 'Choose at least one file.' });
  const names = new Set<string>();
  for (const file of f.files) {
    if (!isSubmittable(file.name)) out.push({ file: file.name, text: 'Only PDF, DXF and IFC files are submitted here.' });
    if (/[/\\\0]/.test(file.name) || file.name.length > 255) out.push({ file: file.name, text: 'A file name has at most 255 characters and no slash.' });
    const key = file.name.toLowerCase();
    if (names.has(key)) out.push({ file: file.name, text: 'Two files have this name.' });
    names.add(key);
  }
  if (!/^[PC][0-9]{2,3}$/.test(f.revision.trim().toUpperCase())) out.push({ file: null, text: 'The revision is P01, P02… (in progress) or C01, C02… (contractual).' });
  if (!suitabilities(f.purpose).includes(f.suitability)) out.push({ file: null, text: 'Choose a status the container takes.' });
  if (f.issue.trim() && !(/^\d{1,9}$/.test(f.issue.trim()) && Number(f.issue) >= 1)) out.push({ file: null, text: 'The issue number is a whole number from 1, or empty for the next one.' });
  return out;
}

/** The submission's body without its commit (the server takes main's): the files named as they are on disk, with their hashes. */
export function submissionBody(f: SubmitForm, hashes: { name: string; sha256: string; size: number }[]): Record<string, unknown> {
  const note = f.note.trim();
  return {
    issue: f.issue.trim() ? Number(f.issue.trim()) : null,
    revision: f.revision.trim().toUpperCase(),
    purpose: f.purpose,
    suitability: f.suitability,
    ...(note ? { note } : {}),
    files: hashes.map((h) => ({ name: h.name, sha256: h.sha256, size: h.size })),
  };
}

/** A file's SHA-256 as lower-case hex (its object's name on the server). */
export async function sha256Hex(bytes: Uint8Array): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', bytes as BufferSource);
  return [...new Uint8Array(digest)].map((b) => b.toString(16).padStart(2, '0')).join('');
}
