/**
 * Focus mode's card over the view: with the panels hidden, what is selected
 * — its kind, its name and its first few properties, editable — and All
 * properties…, which opens the properties panel over the view.
 */

import { useUi } from '../app/store';
import { showOverlay, usePanels } from '../app/panels';
import { useInterface } from '../app/interface';
import { bindingText, useBindings } from '../app/commands';
import { useSession } from '../engine/session';
import { FieldRow } from './PropertiesPanel';

export function SelectionCard() {
  const hidden = useInterface((s) => s.parts.panels === 'hidden');
  const propsOpen = usePanels((p) => p.propsOpen);
  const overlay = usePanels((p) => p.overlay);
  const selection = useUi((s) => s.selection);
  const count = useUi((s) => s.selected.length);
  const node = useSession((s) => (selection ? s.nodes.get(selection) : undefined));
  const info = useSession((s) => s.info);
  useBindings();
  if (!hidden || propsOpen || overlay?.side === 'props' || !node) return null;
  const cls = info?.nodeTypes.find((t) => t.type === node.type);
  const fields = (cls?.fields ?? []).filter((f) => !f.readOnly).slice(0, 3);
  const keys = bindingText('app.propsOverlay').split(', ')[0];
  return (
    <div className="sel-card props" role="region" aria-label="Selection">
      <span className="eyebrow">{cls?.label ?? node.type}{count > 1 ? ` · ${count} selected` : ''}</span>
      <span className="panel-title">{node.name}</span>
      {node.editable && fields.length > 0 && <dl className="fields">{fields.map((f) => <FieldRow key={f.key} node={node} field={f} />)}</dl>}
      <button type="button" className="link" onClick={() => showOverlay('props')}>All properties…{keys && <kbd data-no-i18n>{keys}</kbd>}</button>
    </div>
  );
}
