/**
 * "Edit style…" beside a style picked in Properties (forum topic 173, decision 732): Manage → Drafting standards opens on the
 * style's own tab (Text, Dimensions, Tags) with the style picked out, the standard one when none is chosen.
 */

import { openStandards, type StandardsTab } from './standardsTab';

/** The style an annotation's properties name ("" or none: the standard one). */
export const styleToEdit = (styleId: unknown): string => (typeof styleId === 'string' && styleId ? styleId : 'standard');

export function EditStyleButton({ tab, styleId, field }: { tab: Exclude<StandardsTab, 'project'>; styleId: unknown; field?: string }) {
  return (
    <button type="button" className="btn mini" data-field={field ?? `edit-style-${tab}`} title="Opens Manage → Drafting standards on this style"
      onClick={() => openStandards(tab, styleToEdit(styleId))}>Edit style…</button>
  );
}
