/**
 * Time-Lapse / Build Replay (docs/plan-ux-game-like.md, P10):
 * scrubs through the model's physical elements in construction order
 * (storey elevation → Footings → Columns/Walls → Beams/Slabs → Roofs →
 * Openings → MEP/Interior) using `setLooks('buildReplay', …)` with zero
 * engine mutation. Closing restores full visibility immediately.
 */

import { useEffect, useMemo } from 'react';
import { create } from 'zustand';
import { Pause, Play, RotateCcw, X } from 'lucide-react';
import { useSession } from '../engine/session';
import { num } from '../engine/model';
import type { NodeDto } from '../engine/protocol';
import { setLooks, type Look } from '../platform/looks';
import { useUi } from '../app/store';

const STAGE_ORDER: Record<string, number> = {
  IfcFooting: 1,
  IfcColumn: 2,
  IfcWall: 3,
  IfcBeam: 4,
  IfcSlab: 5,
  IfcStair: 6,
  IfcRamp: 6,
  IfcRoof: 7,
  IfcDoor: 8,
  IfcWindow: 8,
  IfcCurtainWall: 8,
};

/** Orders physical model elements in construction sequence (lowest storey first, structural before finishes). */
export function buildSequence(nodes: Map<string, NodeDto>): string[] {
  const elev = (n: NodeDto) => {
    const st = n.storeyId ? nodes.get(n.storeyId) : undefined;
    return st ? num(st, 'elevation') : -999;
  };
  const stage = (n: NodeDto) => STAGE_ORDER[n.type] ?? 9;
  const list: NodeDto[] = [];
  for (const n of nodes.values()) {
    if (n.type === 'IfcBuildingStorey' || n.type === 'IfcProject' || n.type === 'IfcSite' || n.type === 'IfcBuilding' || n.type.startsWith('Lapin') || n.type.endsWith('Type') || n.type === 'IfcMaterial') continue;
    list.push(n);
  }
  list.sort((a, b) => elev(a) - elev(b) || stage(a) - stage(b) || a.name.localeCompare(b.name));
  return list.map((n) => n.id);
}

interface ReplayState { open: boolean; playing: boolean; step: number; speed: number }

export const useBuildReplay = create<ReplayState>(() => ({ open: false, playing: false, step: 0, speed: 1 }));

export function openBuildReplay(): void {
  if (useUi.getState().view !== '3d') useUi.setState({ view: '3d' });
  useBuildReplay.setState({ open: true, playing: true, step: 0 });
}

export function closeBuildReplay(): void {
  useBuildReplay.setState({ open: false, playing: false, step: 0 });
  setLooks('buildReplay', {});
}

export function BuildReplay() {
  const { open, playing, step, speed } = useBuildReplay();
  const nodes = useSession((s) => s.nodes);
  const seq = useMemo(() => (open ? buildSequence(nodes) : []), [open, nodes]);
  const total = seq.length;

  useEffect(() => {
    if (!open) { setLooks('buildReplay', {}); return; }
    const clamped = Math.min(step, total);
    const map: Record<string, Look> = {};
    for (let i = clamped; i < total; i++) map[seq[i]] = { hidden: true };
    setLooks('buildReplay', map);
  }, [open, step, seq, total]);

  useEffect(() => {
    if (!open || !playing || total === 0) return;
    const ms = Math.max(40, Math.round(220 / speed));
    const t = setInterval(() => {
      const cur = useBuildReplay.getState().step;
      if (cur >= total) useBuildReplay.setState({ playing: false });
      else useBuildReplay.setState({ step: cur + 1 });
    }, ms);
    return () => clearInterval(t);
  }, [open, playing, speed, total]);

  useEffect(() => () => setLooks('buildReplay', {}), []);

  if (!open) return null;
  const shown = Math.min(step, total);
  return (
    <div className="replay-bar" role="region" aria-label="Build replay" data-testid="build-replay">
      <button type="button" className="icon mini" title={playing ? 'Pause' : 'Play'}
        onClick={() => useBuildReplay.setState((s) => ({ playing: !s.playing, step: s.step >= total ? 0 : s.step }))}>
        {playing ? <Pause size={13} /> : <Play size={13} />}
      </button>
      <button type="button" className="icon mini" title="Restart" onClick={() => useBuildReplay.setState({ step: 0, playing: true })}><RotateCcw size={13} /></button>
      <input type="range" min={0} max={Math.max(1, total)} value={shown} aria-label="Build progress"
        onChange={(e) => useBuildReplay.setState({ playing: false, step: Number(e.target.value) })} />
      <span className="mono small">{shown}/{total}</span>
      <button type="button" className="vc-chip" title="Replay speed" onClick={() => useBuildReplay.setState((s) => ({ speed: s.speed === 1 ? 2 : s.speed === 2 ? 4 : 1 }))}>{speed}×</button>
      <button type="button" className="icon mini" title="Close" onClick={closeBuildReplay}><X size={13} /></button>
    </div>
  );
}
