/** The analysis' notes folded under one line, Notes (n), those that ask to be looked at first (docs/plan-fem.md §11.10). */

import { orderNotes } from './results';

export function NotesList({ notes, test }: { notes: readonly string[]; test: string }) {
  const { notes: ordered, warnings } = orderNotes(notes);
  if (ordered.length === 0) return null;
  return (
    <details className="small analysis-notes" data-testid={test} data-warnings={warnings}>
      <summary>
        <span>Notes</span> <span data-no-i18n>({ordered.length})</span>
        {warnings > 0 && <> <span className="warn-text"><span>to check:</span> <span data-no-i18n>{warnings}</span></span></>}
      </summary>
      {ordered.map((n, i) => <p key={i} className={n.warning ? 'warn-text small' : 'muted small'}>{n.text}</p>)}
    </details>
  );
}
