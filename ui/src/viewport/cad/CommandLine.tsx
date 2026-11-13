/**
 * The command line: the last lines, the prompt and what is being typed;
 * snap, ortho and polar switches; the current layer. Under each 2D view
 * (its own line), or as one line with the status (View → Interface:
 * Compact, Focus), where it also carries the mode and, when the tool's
 * prompt goes in the command line, the length and angle inputs.
 */

import { useShallow } from 'zustand/react/shallow';
import { useUi } from '../../app/store';
import { useInterface } from '../../app/interface';
import { bindingText, useBindings } from '../../app/commands';
import { toolHint } from '../../shell/ViewportHud';
import { LiveBar } from '../LiveBox';
import { findTool } from '../../platform/modules';
import { ROLES, findCommand, roleLabel, useCad } from './session';
import { useLayers } from './layers';
import { openSnapMenu, UcsChip, useSnapSummary } from '../../shell/CursorCoords';
import { setPrefs, usePrefs } from '../../app/prefs';
import { t } from '../../i18n';

/** The prompt, what is typed, the switches and the layer: the part both kinds of line have. */
export function CadPrompt({ inline }: { inline?: boolean }) {
  // Only what the prompt shows: not the cursor, which moves on every pointer move.
  const { buffer, takeAll, step, command, osnap, ortho, polar, layer, role } = useCad(useShallow((s) => ({ buffer: s.buffer, takeAll: s.takeAll, step: s.step, command: s.command, osnap: s.osnap, ortho: s.ortho, polar: s.polar, layer: s.layer, role: s.role })));
  const tool = useUi((s) => s.tool);
  const snaps = useSnapSummary();
  const polarStep = usePrefs((s) => s.polarStep);
  const constrain = usePrefs((s) => s.constrainWhileDrawing);
  const view = useUi((s) => s.view);
  const promptHere = useInterface((s) => s.parts.prompt === 'commandLine');
  useBindings();
  const layers = useLayers();
  const hint = promptHere ? toolHint(tool) : null;
  const twoD = view === 'plan' || view === 'drawing';
  const key = (id: string) => { const k = bindingText(id).split(', ')[0]; return k ? ` (${k})` : ''; };
  return (
    <div className={`cad-prompt${inline ? ' inline' : ''}${takeAll ? ' takeall' : ''}`} data-takeall={takeAll ? 'on' : undefined}>
      <span className="cad-label">{step ? `${command}: ${step.prompt}` : hint ?? 'Command'}</span>
      <span className="cad-buffer">{buffer}<span className="caret" /></span>
      {takeAll && <span className="cad-takeall" data-no-i18n>{t('All typing goes to the command line')} · {(bindingText('app.commandLine').split(', ')[0] || '`')} {t('or Esc to leave')}</span>}
      {promptHere && !takeAll && <LiveBar inline />}
      <span className="grow" />
      {(() => { const Options = findTool(tool)?.options ?? (command ? findCommand(command)?.options : undefined); return Options ? <Options /> : null; })()}
      <button type="button" className={`cad-toggle${osnap ? ' on' : ''}`} data-testid="snap-toggle" title={`${t('Object snap')}${key('app.osnap')}: ${snaps}. ${t('Right click: which snaps')}`} onClick={() => useCad.setState({ osnap: !osnap })}
        onContextMenu={(e) => { e.preventDefault(); openSnapMenu(e.clientX, e.clientY); }}>SNAP</button>
      <button type="button" className="cad-toggle cad-more" data-testid="snap-menu" title="Snap settings" onClick={(e) => { const r = e.currentTarget.getBoundingClientRect(); openSnapMenu(r.left, r.top); }}>▾</button>
      <button type="button" className={`cad-toggle${ortho ? ' on' : ''}`} title={bindingText('app.ortho') ? `Ortho (${bindingText('app.ortho').split(', ')[0]}; hold Shift)` : 'Ortho (hold Shift)'} onClick={() => useCad.setState({ ortho: !ortho })}>ORTHO</button>
      <UcsChip />
      <button type="button" className={`cad-toggle${polar ? ' on' : ''}`} title={`${t('Polar tracking every')} ${polarStep}°${key('app.polar')}`} onClick={() => useCad.setState({ polar: !polar })}>POLAR</button>
      {twoD && <button type="button" className={`cad-toggle${constrain ? ' on' : ''}`} data-testid="constrain-toggle" aria-pressed={constrain}
        title="Constrain while drawing: in a sketch, snaps and typed values become constraints (hold Ctrl on a click for a free point)" onClick={() => setPrefs({ constrainWhileDrawing: !constrain })}>CNSTR</button>}
      {(!inline || twoD) && <>
        <select className="cad-layer" value={layer} title="The layer new shapes go on" onChange={(e) => useCad.setState({ layer: e.target.value })}>
          {(layers.length ? layers : [{ name: '0' }]).map((l) => <option key={l.name} value={l.name}>{l.name}</option>)}
        </select>
        <select className={`cad-layer${role ? ' on' : ''}`} value={role} title="The role new shapes take: a profile, a path… (Studio finds them by it); construction lines are never printed" onChange={(e) => useCad.setState({ role: e.target.value })}>
          <option value="">no role</option>
          {ROLES.map((r) => <option key={r} value={r}>{roleLabel(r)}</option>)}
        </select>
      </>}
    </div>
  );
}

/** The command line under a 2D view (when it has a line of its own). */
export function CommandLine() {
  const history = useCad((s) => s.history);
  const own = useInterface((s) => s.parts.commandLine === 'own');
  if (!own) return null;
  const lines = history.slice(-2);
  return (
    <div className="cad-line" data-testid="command-line">
      <div className="cad-history">{lines.map((l, i) => <div key={i}>{l || ' '}</div>)}</div>
      <CadPrompt />
    </div>
  );
}
