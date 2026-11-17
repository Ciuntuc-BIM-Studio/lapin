/**
 * A sketch board over the 3D view (audit S11, docs/plan-sketch-canvas.md): the work plane's board (a part's own sketch, Studio's
 * sketch on a face) shows its constraints and dimensions on its plane, drawn through the camera by the same overlay as a plan's
 * (ConstraintOverlay), and the same bar as every board. The drawing, the commands, the snaps and the grips are the 3D canvas's own
 * (Draft3D.tsx, SketchGrips3D.tsx).
 */

import { useMemo } from 'react';
import * as THREE from 'three';
import type { Overlay3DProps, Vec2 } from '../../platform/module';
import { useUi } from '../../app/store';
import { useSession } from '../../engine/session';
import { poseKey, useCamera } from '../../viewport/camera';
import { useAxisDrag, middleOf } from '../../viewport/axisDrag';
import { boardOf } from '../../viewport/sketch/board';
import { lookAtPlane, useWorkPlane } from '../../viewport/sketch/sketch3d';
import { frameOfView, rayOnPlane, toWorld } from '../../viewport/workPlane';
import { ConstraintOverlay } from './ConstraintOverlay';
import { SketchBar } from './SketchBar';

export function Board3D({ camera, canvas }: Overlay3DProps) {
  useCamera();
  const view = useUi((s) => s.view);
  const plane = useWorkPlane((s) => s.plane);
  const nodes = useSession((s) => s.nodes);
  const dragging = useAxisDrag((s) => !!s.spec);
  const board = plane ? boardOf(plane.planeId, nodes) : null;
  const frame = useMemo(() => (plane ? frameOfView(plane.planeId, nodes) : null), [plane, nodes]);
  const cam = camera.current, rect = canvas.current?.getBoundingClientRect();
  if (view !== '3d' || !board || !frame || !cam || !rect || dragging) return null;
  void poseKey();
  cam.updateMatrixWorld();
  const toScreen = (p: Vec2) => {
    const w = toWorld(frame, p);
    const v = new THREE.Vector3(w.x, w.y, w.z).project(cam);
    return { x: ((v.x + 1) / 2) * rect.width, y: ((1 - v.y) / 2) * rect.height };
  };
  const toPlane = (x: number, y: number): Vec2 => {
    const nx = (x / rect.width) * 2 - 1, ny = 1 - (y / rect.height) * 2;
    const a = new THREE.Vector3(nx, ny, -1).unproject(cam), b = new THREE.Vector3(nx, ny, 1).unproject(cam);
    const d = b.sub(a).normalize();
    return rayOnPlane(frame, { x: a.x, y: a.y, z: a.z }, { x: d.x, y: d.y, z: d.z }) ?? { x: 0, y: 0 };
  };
  const a = toScreen({ x: 0, y: 0 }), b = toScreen({ x: 1, y: 0 });
  const scale = Math.max(Math.hypot(b.x - a.x, b.y - a.y), 1e-6);
  const look = () => lookAtPlane(frame.normal, plane?.outline?.length ? middleOf(plane.outline) : frame.origin);
  return (
    <>
      {board.constraints && (
        <svg className="grip-overlay face-sketch board-3d" data-board3d={board.kind} style={{ pointerEvents: 'none' }}>
          <ConstraintOverlay toScreen={toScreen} toWorld={toPlane} scale={scale} view={{ id: board.viewId }} />
        </svg>
      )}
      <SketchBar board={board} in3d onLook={look} />
    </>
  );
}
