/**
 * Optional Tactile Sound Pack (docs/plan-ux-game-like.md, P9):
 * ultra-subtle synthetic Web Audio API clicks for snap lock, placement,
 * undo/redo and mission completion. Zero binary audio assets (respecting the
 * 24 MB web build budget). Controlled by `prefs.tactileSounds` so users can
 * disable or re-enable sounds anytime from Preferences, the status bar or
 * Commands….
 */

import { setPrefs, usePrefs } from './prefs';

export type TactileSound = 'snap' | 'place' | 'undo' | 'complete' | 'click';

let audioCtx: AudioContext | null = null;

function ctx(): AudioContext | null {
  if (typeof window === 'undefined') return null;
  const Ctor = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
  if (!Ctor) return null;
  if (!audioCtx) audioCtx = new Ctor();
  if (audioCtx.state === 'suspended') void audioCtx.resume().catch(() => undefined);
  return audioCtx;
}

/** Synthesizes a subtle, crisp mechanical tone if tactile sounds are enabled. */
export function playSound(kind: TactileSound): boolean {
  if (!usePrefs.getState().tactileSounds) return false;
  const ac = ctx();
  if (!ac) return false;
  try {
    const now = ac.currentTime;
    const osc = ac.createOscillator();
    const gain = ac.createGain();
    osc.connect(gain);
    gain.connect(ac.destination);

    if (kind === 'snap') {
      osc.type = 'triangle';
      osc.frequency.setValueAtTime(1180, now);
      osc.frequency.exponentialRampToValueAtTime(780, now + 0.025);
      gain.gain.setValueAtTime(0.035, now);
      gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.028);
      osc.start(now);
      osc.stop(now + 0.03);
    } else if (kind === 'place') {
      osc.type = 'sine';
      osc.frequency.setValueAtTime(340, now);
      osc.frequency.exponentialRampToValueAtTime(190, now + 0.045);
      gain.gain.setValueAtTime(0.055, now);
      gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.05);
      osc.start(now);
      osc.stop(now + 0.055);
    } else if (kind === 'undo') {
      osc.type = 'sine';
      osc.frequency.setValueAtTime(520, now);
      osc.frequency.exponentialRampToValueAtTime(360, now + 0.04);
      gain.gain.setValueAtTime(0.035, now);
      gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.045);
      osc.start(now);
      osc.stop(now + 0.05);
    } else if (kind === 'complete') {
      osc.type = 'sine';
      osc.frequency.setValueAtTime(587.3, now);
      osc.frequency.setValueAtTime(880, now + 0.06);
      gain.gain.setValueAtTime(0.045, now);
      gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.16);
      osc.start(now);
      osc.stop(now + 0.17);
    } else {
      osc.type = 'triangle';
      osc.frequency.setValueAtTime(880, now);
      gain.gain.setValueAtTime(0.025, now);
      gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.02);
      osc.start(now);
      osc.stop(now + 0.022);
    }
    return true;
  } catch {
    return false;
  }
}

/** Toggle tactile sounds on/off and persist in user preferences. */
export function toggleTactileSounds(): boolean {
  const next = !usePrefs.getState().tactileSounds;
  setPrefs({ tactileSounds: next });
  if (next) playSound('click');
  return next;
}
