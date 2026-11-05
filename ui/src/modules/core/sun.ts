/**
 * The project's sun (docs/plan-visualisation.md, stage 1): a day and an hour
 * for its place (Manage → Location), asked of the engine (sun.at), and the 3D
 * view lit by it. View → Sun and shadows turns it on and sets the day and hour; a view
 * rendered with a sun (medium, realistic: render3d.ts) takes the same day and
 * hour when the panel has not turned it on. The urban module's shadow plans
 * use the same state.
 */

import { create } from 'zustand';
import type { NodeDto } from '../../engine/protocol';
import { query, useSession } from '../../engine/session';
import { setSunlight } from '../../platform/sunlight';
import { SPECS, useRenderProfile } from '../../viewport/render3d';

export interface SunState { on: boolean; date: string; hour: number; playing: boolean }

export const today = (): string => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};

export const useSun = create<SunState>(() => ({ on: false, date: today(), hour: 14, playing: false }));

export const setSun = (patch: Partial<SunState>): void => {
  useSun.setState(patch);
  void applySun();
};

export interface SunInfo { azimuth: number; altitude: number; dir: [number, number, number]; lon: number; lat: number; placeKnown: boolean; utcOffset: number; summerTime: boolean }

export const sunQuery = (date: string, time: number) => query<SunInfo>('sun.at', { date, time });

export const hourText = (h: number) => `${String(Math.floor(h)).padStart(2, '0')}:${String(Math.round((h % 1) * 60) % 60).padStart(2, '0')}`;

/** Whether the 3D view has a sun now: the panel's, or the profile's. */
export const sunWanted = (): boolean => useSun.getState().on || SPECS[useRenderProfile.getState().profile].sun;

let asked = 0;

/** The 3D view's sun for the state shown: aimed, or given back (the latest answer wins). */
export async function applySun(): Promise<SunInfo | null> {
  const n = ++asked;
  if (!sunWanted()) { setSunlight({ dir: null, label: null }); return null; }
  const { on, date, hour } = useSun.getState();
  const s = await sunQuery(date, hour).catch(() => null);
  if (n !== asked || !s) return s;
  setSunlight({ dir: s.dir, label: on ? `${date} ${hourText(hour)} · ${s.altitude.toFixed(1)}° high, ${s.azimuth.toFixed(0)}° from north` : null });
  return s;
}

// A profile with a sun chosen or left; another project opened, or its place changed: the sun again.
useRenderProfile.subscribe(() => void applySun());
let projectId: string | null = null;
let project: NodeDto | undefined;
useSession.subscribe((s, before) => {
  if (s.nodes === before.nodes) return;
  if (!projectId || !s.nodes.has(projectId)) {
    projectId = null;
    for (const n of s.nodes.values()) if (n.type === 'IfcProject') { projectId = n.id; break; }
  }
  const now = projectId ? s.nodes.get(projectId) : undefined;
  if (now !== project) { project = now; if (now && sunWanted()) void applySun(); }
});
