/**
 * Selection overlay over the view:
 * - In Focus mode (panels hidden): editable card with the selection's first
 *   properties and All properties….
 * - In all other modes: a compact 1-row Contextual Mini-Toolbar (Move, Copy,
 *   Rotate, Create similar, Isolate, Hide, Delete) so common actions stay one
 *   click away without travelling to the ribbon (docs/plan-ux-game-like.md, P5).
 */

import { Copy, CopyCheck, Eye, EyeOff, Move, RotateCw, Trash2 } from 'lucide-react';
import { useUi } from '../app/store';
import { showOverlay, usePanels } from '../app/panels';
import { useInterface } from '../app/interface';
import { bindingText, runCommand, useBindings } from '../app/commands';
import { useSession } from '../engine/session';
import { fieldShown } from '../engine/protocol';
import { FieldRow } from './PropertiesPanel';
import { startCommand } from '../viewport/cad/session';
import { hideSelection, isolateSelection } from '../viewport/temporary';
import { deleteElements } from '../engine/dependents';

export function SelectionCard() {
  const hidden = useInterface((s) => s.parts.panels === 'hidden');
  const propsOpen = usePanels((p) => p.propsOpen);
  const overlay = usePanels((p) => p.overlay);
  const selection = useUi((s) => s.selection);
  const selected = useUi((s) => s.selected);
  const tool = useUi((s) => s.tool);
  const count = selected.length;
  const node = useSession((s) => (selection ? s.nodes.get(selection) : undefined));
  const info = useSession((s) => s.info);
  useBindings();

  if (!node) return null;
  const cls = info?.nodeTypes.find((t) => t.type === node.type);

  // Focus mode: full mini-inspector card when properties panel is hidden.
  if (hidden && !propsOpen && overlay?.side !== 'props') {
    const fields = (cls?.fields ?? []).filter((f) => !f.readOnly && fieldShown(f, node.props)).slice(0, 3);
    const keys = bindingText('app.propsOverlay').split(', ')[0];
    return (
      <div className="sel-card props" role="region" aria-label="Selection">
        <span className="eyebrow"><span data-i18n-ctx="class">{cls?.label ?? node.type}</span>{count > 1 ? ` · ${count} selected` : ''}</span>
        <span className="panel-title">{node.name}</span>
        {node.editable && fields.length > 0 && <dl className="fields">{fields.map((f) => <FieldRow key={f.key} node={node} field={f} />)}</dl>}
        <button type="button" className="link" onClick={() => showOverlay('props')}>All properties…{keys && <kbd data-no-i18n>{keys}</kbd>}</button>
      </div>
    );
  }

  // Non-Focus modes: compact 1-row floating Contextual Mini-Toolbar (suppressed while a placing tool is active).
  if (tool || !node.editable) return null;
  const remove = () => {
    const nodes = useSession.getState().nodes;
    const ids = useUi.getState().selected.filter((id) => nodes.get(id)?.type !== 'LapinView');
    if (ids.length) { void deleteElements(ids); useUi.setState({ selection: null, selected: [] }); }
  };
  return (
    <div className="sel-mini-bar" role="toolbar" aria-label="Selection actions" data-testid="selection-mini-bar">
      <span className="sel-mini-tag">
        <span data-i18n-ctx="class">{cls?.label ?? node.type}</span>
        {count > 1 && <span className="muted"> · {count}</span>}
      </span>
      <button type="button" className="icon mini" title="Move (Ctrl+D)" aria-label="Move" onClick={() => void startCommand('MOVE')}><Move size={13} /></button>
      <button type="button" className="icon mini" title="Copy (Ctrl+Shift+D)" aria-label="Copy" onClick={() => void startCommand('COPY')}><Copy size={13} /></button>
      <button type="button" className="icon mini" title="Rotate (Ctrl+E)" aria-label="Rotate" onClick={() => void startCommand('ROTATE')}><RotateCw size={13} /></button>
      <button type="button" className="icon mini" title="Pick & create similar (Alt+I)" aria-label="Pick & create similar" onClick={() => runCommand('app.eyedropper')}><CopyCheck size={13} /></button>
      <button type="button" className="icon mini" title="Isolate (HI)" aria-label="Isolate" onClick={isolateSelection}><Eye size={13} /></button>
      <button type="button" className="icon mini" title="Hide (HH)" aria-label="Hide" onClick={hideSelection}><EyeOff size={13} /></button>
      <button type="button" className="icon mini danger" title="Delete" aria-label="Delete" onClick={remove}><Trash2 size={13} /></button>
    </div>
  );
}
