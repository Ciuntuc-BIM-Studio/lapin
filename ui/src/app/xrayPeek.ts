/**
 * Hold-to-Peek X-Ray (docs/plan-ux-game-like.md, P7): holding X (>200 ms) in
 * the 3D view while idle temporarily switches the view to X-ray so you can see
 * rebar, structure or MEP behind finishes, then restores the previous visual
 * style the instant the key is released. A quick tap of X still goes to normal
 * two-letter shortcuts or the command line.
 */

import { useEffect } from 'react';
import { create } from 'zustand';
import { useUi } from './store';
import type { VisualStyle } from './visualStyle';
import { useCad } from '../viewport/cad/session';
import { useLive } from '../viewport/liveInput';
import { valueOwner } from './valueOwner';
import { useWalk } from '../viewport/walk';

export const XRAY_PEEK_HOLD_MS = 200;

export const useXrayPeek = create<{ active: boolean; prev: VisualStyle | null }>(() => ({ active: false, prev: null }));

/** Whether the current UI state allows holding X to peek in X-ray. */
export function canStartXrayPeek(state: {
  screen: string;
  view: string;
  dialog: string | null;
  visualStyle: VisualStyle;
  buffer: string;
  hasStep: boolean;
  liveActive: boolean;
  hasValueOwner: boolean;
  walking: boolean;
}): boolean {
  if (state.screen !== 'workspace' || state.view !== '3d' || state.dialog) return false;
  if (state.visualStyle === 'xray' || state.hasStep || state.liveActive || state.hasValueOwner || state.walking) return false;
  const b = state.buffer.trim().toLowerCase();
  return b === '' || b === 'x';
}

export function beginXrayPeek(): boolean {
  const ui = useUi.getState();
  const cad = useCad.getState();
  const ok = canStartXrayPeek({
    screen: ui.screen,
    view: ui.view,
    dialog: ui.dialog,
    visualStyle: ui.visualStyle,
    buffer: cad.buffer,
    hasStep: Boolean(cad.step),
    liveActive: Boolean(useLive.getState().owner),
    hasValueOwner: Boolean(valueOwner()),
    walking: Boolean(useWalk.getState().start),
  });
  if (!ok || useXrayPeek.getState().active) return false;
  if (cad.buffer.trim().toLowerCase() === 'x') useCad.setState({ buffer: '' });
  useXrayPeek.setState({ active: true, prev: ui.visualStyle });
  useUi.setState({ visualStyle: 'xray' });
  return true;
}

export function endXrayPeek(): void {
  const { active, prev } = useXrayPeek.getState();
  if (!active) return;
  useXrayPeek.setState({ active: false, prev: null });
  if (prev) useUi.setState({ visualStyle: prev });
}

/** Installs the global hold-X listener for the workspace. */
export function useXrayPeekKeys(): void {
  useEffect(() => {
    let timer: ReturnType<typeof setTimeout> | null = null;
    const clear = () => { if (timer) { clearTimeout(timer); timer = null; } };
    const onDown = (e: KeyboardEvent) => {
      if (e.repeat || e.ctrlKey || e.metaKey || e.shiftKey) return;
      if (e.key.toLowerCase() !== 'x') return;
      const target = e.target as HTMLElement | null;
      if (target?.closest?.('input, textarea, select, [contenteditable]')) return;
      clear();
      timer = setTimeout(() => { timer = null; beginXrayPeek(); }, XRAY_PEEK_HOLD_MS);
    };
    const onUp = (e: KeyboardEvent) => {
      if (e.key.toLowerCase() !== 'x') return;
      clear();
      endXrayPeek();
    };
    const onBlur = () => { clear(); endXrayPeek(); };
    window.addEventListener('keydown', onDown);
    window.addEventListener('keyup', onUp);
    window.addEventListener('blur', onBlur);
    return () => {
      clear();
      endXrayPeek();
      window.removeEventListener('keydown', onDown);
      window.removeEventListener('keyup', onUp);
      window.removeEventListener('blur', onBlur);
    };
  }, []);
}
