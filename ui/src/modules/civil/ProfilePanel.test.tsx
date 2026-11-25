import { afterEach, describe, expect, it, vi } from 'vitest';

// Rendered on the server, a zustand store gives its initial state: here every store gives what the test set.
vi.mock('zustand', async (importOriginal) => {
  const z = await importOriginal<typeof import('zustand')>();
  type Init = (set: unknown, get: unknown, api: { getState: () => unknown; getServerState?: () => unknown }) => unknown;
  const live = (f: Init): Init => (set, get, api) => { api.getServerState = api.getState; return f(set, get, api); };
  const create = ((f?: Init) => (f ? z.create(live(f) as never) : (g: Init) => z.create(live(g) as never))) as typeof z.create;
  return { ...z, create, default: create };
});
import { renderToStaticMarkup } from 'react-dom/server';
import { useUi } from '../../app/store';
import { PartBoundary, failureReport } from '../../shell/PartBoundary';
import { ProfilePanel } from './ProfilePanel';
import { readProfile, type ProfileData } from './profile';
import { closeProfile, useProfile } from './profileStore';

/**
 * Forum topic 258: a retaining wall drawn on a bare site (no terrain), its properties' Profile… pressed in the 3D view, and
 * the whole window went white. The engine's answer for such a wall (civil.profile, as ProfileEditorTests' engine side gives
 * it): a corridor following the ground, no points, and no `existing` / `design` series since there is no terrain under it.
 */
const L = 6.0667;
const stations = Array.from({ length: 26 }, (_, i) => Math.round(((L * i) / 25) * 1e6) / 1e6);
const bareWall = {
  kind: 'corridor', id: 'wall-258', name: 'Retaining wall 1', start: 0, length: L, closed: false,
  path: stations.map((s) => [s, 0, s]), line: stations.map((s) => [s, 0]), points: [], mode: 'ground',
};
const bareCross = [{ station: 1, label: '0+001.00', width: 10, at: [1, 0], from: [1, 10], to: [1, -10], z: 0 }];


function open(data: unknown, view: '3d' | 'plan' = '3d', tab: 'long' | 'cross' = 'long', cross: unknown[] = []) {
  useUi.setState({ view });
  useProfile.setState({ lineId: 'wall-258', data: data as ProfileData, tab, cross: cross as never, stations: cross.length ? [1] : [] });
}

afterEach(() => closeProfile());

describe('the profile panel on a retaining wall with no terrain under it (forum 258)', () => {
  it('threw on the engine answer taken as it came (the white window of 0.1.1958)', () => {
    open(bareWall);
    expect(() => renderToStaticMarkup(<ProfilePanel />)).toThrow(/is not iterable/);
  });

  it('draws the long profile once the answer is read, its grounds empty, offering to design the profile', () => {
    const d = readProfile(bareWall);
    expect(d).not.toBeNull();
    expect(d!.existing).toEqual([]);
    expect(d!.design).toEqual([]);
    for (const view of ['3d', 'plan'] as const) {
      open(d, view);
      const html = renderToStaticMarkup(<ProfilePanel />);
      expect(html).toContain('data-testid="profile-panel"');
      expect(html).toContain('data-testid="profile-graph"');
      expect(html).toContain('data-testid="profile-design"');
      expect(html).toContain('0+000.00 → 0+006.07');
    }
  });

  it('draws its cross sections too, the ground across them empty', () => {
    const d = readProfile(bareWall);
    open(d, '3d', 'cross', bareCross.map((c) => ({ ...c, existing: [], design: [] })));
    const html = renderToStaticMarkup(<ProfilePanel />);
    expect(html).toContain('data-testid="profile-cross"');
  });

  it('reads what is no profile as none', () => {
    expect(readProfile(null)).toBeNull();
    expect(readProfile({ kind: 'corridor' })).toBeNull();
    expect(readProfile({ ...bareWall, points: undefined })).toBeNull();
  });
});

describe('a part of the window that throws stops alone', () => {
  it('says so in place, with Try again, Report… and Close, and the error for the report', () => {
    const b = new PartBoundary({ part: 'Profile panel', overlay: true, onClose: () => {}, children: null });
    b.state = PartBoundary.getDerivedStateFromError(new TypeError("s is not iterable"));
    const html = renderToStaticMarkup(<>{b.render()}</>);
    expect(html).toContain('data-testid="part-failed"');
    expect(html).toContain('This part of Lapin stopped working; the rest of the window still works.');
    expect(html).toContain('s is not iterable');
    expect(html).toContain('Try again');
    expect(html).toContain('Report…');
    expect(html).toContain('Close');
    expect(html).toContain('part-failed overlay');
  });

  it('passes its children through while nothing threw', () => {
    const html = renderToStaticMarkup(<PartBoundary part="x"><span>fine</span></PartBoundary>);
    expect(html).toBe('<span>fine</span>');
  });

  it('starts a bug report naming the part and the error', () => {
    const r = failureReport('Profile panel', 's is not iterable');
    expect(r.kind).toBe('bug');
    expect(r.title).toBe('Profile panel: stopped working');
    expect(r.description).toContain('Profile panel stopped working: s is not iterable');
  });
});
