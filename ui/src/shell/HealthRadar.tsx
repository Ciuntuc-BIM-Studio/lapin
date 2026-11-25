/**
 * Model Health Radar HUD pill (docs/plan-ux-game-like.md, P10):
 * sits discreetly in the status bar; clicking opens a compact list of
 * detected BIM hygiene issues with 1-click Select & Zoom. A Flow extra, off by
 * default (Preferences → General).
 */

import { useMemo, useState } from 'react';
import { AlertTriangle, CheckCircle2, Crosshair, X } from 'lucide-react';
import { useSession } from '../engine/session';
import { scanModelHealth } from '../app/modelHealth';
import { select } from '../app/store';
import { runCommand } from '../app/commands';
import { playSound } from '../app/sounds';

export function HealthRadar() {
  const nodes = useSession((s) => s.nodes);
  const [open, setOpen] = useState(false);
  const issues = useMemo(() => scanModelHealth(nodes), [nodes]);
  const count = issues.length;

  const focusIssue = (nodeId: string) => {
    playSound('click');
    select(nodeId);
    setTimeout(() => runCommand('app.zoomSelection'), 0);
  };

  return (
    <span className="health-radar-host">
      <button type="button" className={`status-link health-pill${count > 0 ? ' warn' : ' ok'}`} data-testid="health-radar"
        title={count === 1 ? '1 model health issue: click to inspect' : count > 0 ? `${count} model health issues: click to inspect` : 'Model health: clean'}
        onClick={() => setOpen((v) => !v)}>
        {count > 0 ? <AlertTriangle size={12} /> : <CheckCircle2 size={12} />}
        <span>{count === 1 ? '1 issue' : count > 0 ? `${count} issues` : 'Model OK'}</span>
      </button>
      {open && (
        <div className="health-popover" role="dialog" aria-label="Model health radar" data-testid="health-popover">
          <header className="health-head">
            <span>Model health radar</span>
            <button type="button" className="icon mini" title="Close" onClick={() => setOpen(false)}><X size={12} /></button>
          </header>
          {count === 0 ? (
            <p className="muted small">No geometric or storey issues detected.</p>
          ) : (
            <ul className="health-list">
              {issues.slice(0, 25).map((it) => (
                <li key={it.id} className="health-row">
                  <span className="health-text">
                    {/* The element's own name, as the user gave it. */}
                    <b data-no-i18n>{it.label}</b>
                    <span className="muted small">{it.detail}</span>
                  </span>
                  <button type="button" className="icon mini" title="Select and zoom" onClick={() => focusIssue(it.nodeId)}><Crosshair size={13} /></button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </span>
  );
}
