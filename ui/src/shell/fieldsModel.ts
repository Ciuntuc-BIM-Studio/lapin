/**
 * The Parameters inspector's model (forum topic 84): the fields the engine lists for an element (`fields.available`), filtered
 * by what is typed, and the piece a click writes — `{token}` into a text with fields (a tag, a stamp, a template), the bare name
 * into a formula's expression (its spaces left out, as formulas read names).
 */

export interface FieldInfo { token: string; label: string; value: string | null; description: string; kind?: string | null; formulaOnly?: boolean }
export interface FieldGroup { id: string; label: string; scope: string | null; entityId: string | null; entityName: string | null; entityClass: string | null; fields: FieldInfo[] }
export interface AvailableFields { id: string; name: string; class: string; groups: FieldGroup[]; functions: string[] }

/** Where a pick goes: a text with fields in braces, or a formula's expression. */
export type InsertMode = 'text' | 'formula';

/** The groups with only the fields matching every word typed (in the token, label, value or description); empty groups left out. */
export function filterGroups(groups: FieldGroup[], typed: string): FieldGroup[] {
  const words = typed.toLowerCase().split(/\s+/).filter(Boolean);
  if (!words.length) return groups;
  return groups
    .map((g) => ({ ...g, fields: g.fields.filter((f) => {
      const hay = `${f.token} ${f.label} ${f.value ?? ''} ${f.description} ${g.label}`.toLowerCase();
      return words.every((w) => hay.includes(w));
    }) }))
    .filter((g) => g.fields.length > 0);
}

/** A name as a formula reads it: without its spaces, each word after the first capitalised („Fire rating” is FireRating). */
export const formulaName = (token: string) => token.trim().split(/\s+/).map((w, i) => (i === 0 ? w : w.charAt(0).toUpperCase() + w.slice(1))).join('');

/** What a pick of `f` writes: `{token}` in a text (a quantity, read only by formulas, as `{= Name}`), the name in a formula. */
export function pieceFor(f: Pick<FieldInfo, 'token' | 'formulaOnly'>, mode: InsertMode): string {
  if (mode === 'formula') return formulaName(f.token);
  return f.formulaOnly ? `{= ${formulaName(f.token)}}` : `{${f.token}}`;
}

/** Whether a formula's text is a template (fields in braces) rather than an expression: a pick then writes `{token}`. */
export const modeOf = (formula: string, kind?: string): InsertMode => formula.includes('{') || (kind === 'text' && formula.trim() === '') ? 'text' : 'formula';

/** `piece` put in `text` over the selection from `start` to `end`, and where the caret goes after it. */
export function insertAt(text: string, start: number, end: number, piece: string): { text: string; caret: number } {
  const a = Math.max(0, Math.min(start, text.length));
  const b = Math.max(a, Math.min(end, text.length));
  return { text: text.slice(0, a) + piece + text.slice(b), caret: a + piece.length };
}
