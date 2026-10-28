/** The button on a side panel's header: fold it to its rail, or (shown over the view for a moment) keep it open. */

import { PanelLeftClose, PanelRightClose, Pin } from 'lucide-react';
import { togglePanel, usePanels } from '../app/panels';
import { bindingText, useBindings } from '../app/commands';

export function PanelFold({ side, overlay }: { side: 'browser' | 'props'; overlay?: boolean }) {
  useBindings();
  const name = side === 'browser' ? 'project browser' : 'properties';
  const keys = bindingText(side === 'browser' ? 'app.toggleBrowser' : 'app.toggleProps').split(', ')[0];
  const k = keys ? ` (${keys})` : '';
  if (overlay) return (
    <button type="button" className="icon mini panel-fold" title={`Keep the ${name} open${k}`}
      onClick={() => { usePanels.setState({ overlay: null }); togglePanel(side); }}><Pin size={13} /></button>
  );
  return (
    <button type="button" className="icon mini panel-fold" title={`Fold the ${name}${k}`} onClick={() => togglePanel(side)}>
      {side === 'browser' ? <PanelLeftClose size={13} /> : <PanelRightClose size={13} />}
    </button>
  );
}
