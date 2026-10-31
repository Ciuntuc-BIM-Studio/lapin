import { useSession } from '../engine/session';
import { useUi } from '../app/store';

/**
 * A view picked by its mark in a plan or a section (a section's line, a reference plane's cut; docs/plan-studio-thick.md)
 * opened on a double click: true when the id was such a view, false for anything else (an element, a dimension).
 */
export function openPickedView(id: string): boolean {
  const node = useSession.getState().nodes.get(id);
  if (node?.type !== 'LapinView') return false;
  const kind = node.props.kind;
  if (kind !== 'section' && kind !== 'elevation' && kind !== 'plane') return false;
  useUi.setState({ view: 'drawing', drawingViewId: id, selection: id, selected: [id] });
  return true;
}
