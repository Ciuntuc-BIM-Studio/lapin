import { useSession } from '../engine/session';
import { notify, useUi } from '../app/store';

/**
 * A view picked by its mark in a plan or a section (a section's line, a reference plane's cut; docs/plan-studio-thick.md;
 * a detail callout, forum topic 107) opened on a double click: true when the id was such a view, false for anything else
 * (an element, a dimension).
 */
export function openPickedView(id: string): boolean {
  const node = useSession.getState().nodes.get(id);
  if (node?.type === 'LapinCallout') return openCallout(id);
  if (node?.type !== 'LapinView') return false;
  const kind = node.props.kind;
  if (kind !== 'section' && kind !== 'elevation' && kind !== 'plane') return false;
  useUi.setState({ view: 'drawing', drawingViewId: id, selection: id, selected: [id] });
  return true;
}

/** The view a callout calls, opened: a plan in the plan pane, a section or an elevation in the drawing pane. True when it was a callout. */
export function openCallout(id: string): boolean {
  const { nodes } = useSession.getState();
  const callout = nodes.get(id);
  if (callout?.type !== 'LapinCallout') return false;
  const target = nodes.get(String(callout.props.target ?? ''));
  if (!target) { notify('The view this callout calls is gone.'); return true; }
  if ((target.props.kind ?? 'plan') === 'plan') useUi.setState({ view: 'plan', storeyId: String(target.props.storeyId ?? ''), planViewId: target.id, selection: target.id, selected: [target.id] });
  else useUi.setState({ view: 'drawing', drawingViewId: target.id, selection: target.id, selected: [target.id] });
  return true;
}
