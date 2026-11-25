/**
 * Interactive 3D ViewCube (docs/plan-ux-game-like.md, P7):
 * a compact CSS 3D orientation cube synced live to `useCamera()`, with
 * one-click animated transitions to Top, Front, Back, Left, Right and ISO,
 * plus a quick Perspective / Orthographic toggle.
 */

import { STANDARD, useCamera } from './camera';
import { playSound } from '../app/sounds';

/** Converts camera azimuth & pitch (radians) into CSS rotateX / rotateZ degrees for the cube. */
export function cubeTransform(azimuth: number, pitch: number): string {
  const rx = Math.round(90 + (pitch * 180) / Math.PI);
  const rz = Math.round(((azimuth - Math.PI / 2) * 180) / Math.PI);
  return `rotateX(${rx}deg) rotateZ(${rz}deg)`;
}

export function ViewCube3D() {
  const azimuth = useCamera((c) => c.azimuth);
  const pitch = useCamera((c) => c.pitch);
  const ortho = useCamera((c) => c.ortho);
  const api = useCamera((c) => c.api);

  const go = (pose: { azimuth: number; pitch: number }) => {
    playSound('click');
    api?.orbitTo(pose, true);
  };

  return (
    <div className="viewcube-host" aria-label="3D view cube" data-testid="viewcube-3d">
      <div className="viewcube-scene">
        <div className="viewcube" style={{ transform: cubeTransform(azimuth, pitch) }}>
          <button type="button" className="vc-face vc-top" title="Top view" onClick={() => go(STANDARD.plan)}>TOP</button>
          <button type="button" className="vc-face vc-bottom" title="Bottom view" onClick={() => go({ azimuth: Math.PI / 2, pitch: Math.PI / 2 - 0.01 })}>BTM</button>
          <button type="button" className="vc-face vc-front" title="Front (South)" onClick={() => go(STANDARD.S)}>S</button>
          <button type="button" className="vc-face vc-back" title="Back (North)" onClick={() => go(STANDARD.N)}>N</button>
          <button type="button" className="vc-face vc-right" title="Right (East)" onClick={() => go(STANDARD.E)}>E</button>
          <button type="button" className="vc-face vc-left" title="Left (West)" onClick={() => go(STANDARD.W)}>W</button>
        </div>
      </div>
      <div className="viewcube-bar">
        <button type="button" className="vc-chip" title="Isometric view" onClick={() => go(STANDARD.SW)}>ISO</button>
        <button type="button" className={`vc-chip${ortho ? ' on' : ''}`} title="Toggle perspective / orthographic"
          onClick={() => { playSound('click'); api?.setOrtho(!ortho); }}>{ortho ? 'ORTHO' : 'PERSP'}</button>
      </div>
    </div>
  );
}
