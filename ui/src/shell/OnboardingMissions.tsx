/**
 * Interactive Role-Differentiated Onboarding Card (docs/plan-ux-game-like.md, P2):
 * floats discreetly over the workspace, tracks real engine/UI progress, lets
 * users launch a procedural ~70% pre-built Sandbox mini-project, and ticks off
 * steps automatically.
 */

import { useEffect, useState } from 'react';
import { CheckCircle2, Circle, Compass, Play, RotateCcw, Sparkles, X } from 'lucide-react';
import { useInterface, WORKSPACE_LABELS } from '../app/interface';
import { allStepsFor, closeOnboarding, evaluateMissions, launchRoleSandbox, markStepsDone, resetOnboarding, useOnboarding } from '../app/onboarding';
import { useSession } from '../engine/session';
import { useUi } from '../app/store';
import { activate, findTool } from '../platform/modules';
import { useWalk } from '../viewport/walk';

export function OnboardingMissions() {
  const open = useOnboarding((s) => s.open);
  const track = useOnboarding((s) => s.track);
  const done = useOnboarding((s) => s.done);
  const events = useOnboarding((s) => s.events);
  const workspace = useInterface((s) => s.workspace);
  const version = useSession((s) => s.version);
  const view = useUi((s) => s.view);
  const panes = useUi((s) => s.panes.length);
  const tool = useUi((s) => s.tool);
  const dialog = useUi((s) => s.dialog);
  const walking = useWalk((s) => s.start !== null);
  const [minimized, setMinimized] = useState(false);

  useEffect(() => {
    if (!open) return;
    const newly = evaluateMissions(workspace);
    if (newly.length) markStepsDone(newly);
  }, [open, workspace, version, view, panes, tool, dialog, walking, events]);

  if (!open) return null;

  const all = allStepsFor(workspace);
  const shown = all.filter((s) => s.track === track);
  const doneSet = new Set(done);
  const totalDone = all.filter((s) => doneSet.has(s.id)).length;
  const pct = all.length ? Math.round((totalDone / all.length) * 100) : 0;

  const tryStep = (step: (typeof shown)[number]) => {
    if (step.dialog) { useUi.setState({ dialog: step.dialog }); return; }
    if (step.toolId) {
      const def = findTool(step.toolId);
      if (def) activate(def);
    }
  };

  if (minimized) {
    return (
      <button type="button" className="mission-pill" data-testid="mission-pill" onClick={() => setMinimized(false)}>
        <Sparkles size={13} />
        <span>Missions</span>
        <span className="mission-count">{totalDone}/{all.length}</span>
      </button>
    );
  }

  return (
    <aside className="mission-card" role="region" aria-label="Interactive missions" data-testid="mission-card">
      <header className="mission-head">
        <span className="mission-title"><Compass size={14} /><span>Missions · {WORKSPACE_LABELS[workspace]}</span></span>
        <span className="mission-count">{totalDone}/{all.length}</span>
        <button type="button" className="icon mini" title="Reset progress" onClick={resetOnboarding}><RotateCcw size={12} /></button>
        <button type="button" className="icon mini" title="Minimize" onClick={() => setMinimized(true)}>–</button>
        <button type="button" className="icon mini" title="Close" onClick={closeOnboarding}><X size={13} /></button>
      </header>
      <div className="mission-bar" aria-hidden="true"><span style={{ width: `${pct}%` }} /></div>
      <div className="mission-tabs" role="tablist">
        <button type="button" role="tab" aria-selected={track === 'role'} className={`mission-tab${track === 'role' ? ' on' : ''}`}
          onClick={() => useOnboarding.setState({ track: 'role' })}>Role mission</button>
        <button type="button" role="tab" aria-selected={track === 'basics'} className={`mission-tab${track === 'basics' ? ' on' : ''}`}
          onClick={() => useOnboarding.setState({ track: 'basics' })}>Basics</button>
      </div>
      <ul className="mission-list">
        {shown.map((s) => {
          const isDone = doneSet.has(s.id);
          return (
            <li key={s.id} className={`mission-step${isDone ? ' done' : ''}`} data-step={s.id}>
              <span className="mission-check" aria-hidden>{isDone ? <CheckCircle2 size={15} /> : <Circle size={15} />}</span>
              <span className="mission-body">
                <span className="mission-step-title">{s.title}{s.shortcut && <kbd data-no-i18n>{s.shortcut}</kbd>}</span>
                <span className="muted small">{s.hint}</span>
              </span>
              {!isDone && (s.toolId || s.dialog) && (
                <button type="button" className="mission-try" onClick={() => tryStep(s)} title="Highlight & start">Try</button>
              )}
            </li>
          );
        })}
      </ul>
      <footer className="mission-foot">
        <button type="button" className="btn small" onClick={() => void launchRoleSandbox(workspace)}>
          <Play size={12} /><span>Reset sandbox model</span>
        </button>
      </footer>
    </aside>
  );
}
