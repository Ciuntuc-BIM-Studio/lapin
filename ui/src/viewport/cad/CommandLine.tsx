/**
 * The command line: the last lines, the prompt and what is being typed;
 * snap, ortho and polar switches; the current layer. Under each 2D view
 * (its own line), or as one line with the status (View → Interface:
 * Compact, Focus), where it also carries the mode and, when the tool's
 * prompt goes in the command line, the length and angle inputs.
 */

import { useEffect, useState } from 'react';
import { query, useSession } from '../../engine/session';
import { useUi } from '../../app/store';
import { useInterface } from '../../app/interface';
import { bindingText, useBindings } from '../../app/commands';
import { toolHint } from '../../shell/ViewportHud';
import { LiveBar } from '../LiveBox';
import { ROLES, roleLabel, useCad } from './session';

interface LayerDto { name: string; color: string; visible: boolean; locked: boolean }

/** The prompt, what is typed, the switches and the layer: the part both kinds of line have. */
export function CadPrompt({ inline }: { inline?: boolean }) {
  const { buffer, step, command, osnap, ortho, polar, layer, role } = useCad();
  const tool = useUi((s) => s.tool);
  const view = useUi((s) => s.view);
  const promptHere = useInterface((s) => s.parts.prompt === 'commandLine');
  const nodes = useSession((s) => s.nodes);
  useBindings();
  const [layers, setLayers] = useState<LayerDto[]>([]);
  useEffect(() => { void query<LayerDto[]>('drafting.layers').then((l) => l && setLayers(l)); }, [nodes]);
  const hint = promptHere ? toolHint(tool) : null;
  const twoD = view === 'plan' || view === 'drawing';
  const key = (id: string) => { const k = bindingText(id).split(', ')[0]; return k ? ` (${k})` : ''; };
  return (
    <div className={`cad-prompt${inline ? ' inline' : ''}`}>
      <span className="cad-label">{step ? `${command}: ${step.prompt}` : hint ?? 'Command'}</span>
      <span className="cad-buffer">{buffer}<span className="caret" /></span>
      {promptHere && <LiveBar inline />}
      <span className="grow" />
      <button type="button" className={`cad-toggle${osnap ? ' on' : ''}`} title={`Object snap${key('app.osnap')}`} onClick={() => useCad.setState({ osnap: !osnap })}>SNAP</button>
      <button type="button" className={`cad-toggle${ortho ? ' on' : ''}`} title={bindingText('app.ortho') ? `Ortho (${bindingText('app.ortho').split(', ')[0]}; hold Shift)` : 'Ortho (hold Shift)'} onClick={() => useCad.setState({ ortho: !ortho })}>ORTHO</button>
      <button type="button" className={`cad-toggle${polar ? ' on' : ''}`} title={`Polar tracking every 45°${key('app.polar')}`} onClick={() => useCad.setState({ polar: !polar })}>POLAR</button>
      {(!inline || twoD) && <>
        <select className="cad-layer" value={layer} title="The layer new shapes go on" onChange={(e) => useCad.setState({ layer: e.target.value })}>
          {(layers.length ? layers : [{ name: '0', color: '#18181b', visible: true, locked: false }]).map((l) => <option key={l.name} value={l.name}>{l.name}</option>)}
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
