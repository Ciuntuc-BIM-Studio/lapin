import { useEffect, useRef, useState } from 'react';
import * as THREE from 'three';
import { useSession, execute } from '../engine/session';
import { useUi, notify } from '../app/store';
import { useLive } from './liveInput';
import { setLooks } from '../platform/looks';
import { fromPairs } from './contour';

const ACCENT_COLOR = 0xb5532d;
const ACCENT_GRAPHITE = 0xe07a4f;
const OPENING_COLOR = 0x4a7c3b;
const SLAB_EXTEND = 0.15; // 150mm apron

let externalRecomputeActive = false;
export function markLiteStepRecomputing(active: boolean) {
  externalRecomputeActive = active;
}

function createBoxMesh(size: [number, number, number], pos: [number, number, number], color: number, opacity = 0.18): THREE.Group {
  const group = new THREE.Group();
  const geom = new THREE.BoxGeometry(size[0], size[1], size[2]);

  // Translucent face mesh
  const mat = new THREE.MeshBasicMaterial({
    color,
    transparent: true,
    opacity,
    depthWrite: false,
    side: THREE.DoubleSide,
  });
  const mesh = new THREE.Mesh(geom, mat);
  mesh.position.set(pos[0], pos[1], pos[2]);
  group.add(mesh);

  // Wireframe outline
  const edges = new THREE.EdgesGeometry(geom);
  const lineMat = new THREE.LineBasicMaterial({ color, linewidth: 2, transparent: true, opacity: 0.85 });
  const wire = new THREE.LineSegments(edges, lineMat);
  wire.position.set(pos[0], pos[1], pos[2]);
  group.add(wire);

  return group;
}

export function LiteStepAABBOverlay({
  scene,
  invalidate,
}: {
  scene: THREE.Scene | null;
  invalidate: () => void;
}) {
  const nodes = useSession((s) => s.nodes);
  const selected = useUi((s) => s.selected);
  const theme = useUi((s) => s.theme);
  const cursor = useLive((s) => s.cursor);
  const base = useLive((s) => s.base);
  const liveOwner = useLive((s) => s.owner);

  const [compiling, setCompiling] = useState(false);
  const [progress, setProgress] = useState(0);
  const [hudInfo, setHudInfo] = useState<{ length: number; width: number; status: string } | null>(null);

  const overlayGroupRef = useRef<THREE.Group | null>(null);
  const lastDimensionsRef = useRef<{ len: number; wid: number; cx: number; cy: number } | null>(null);

  // Find the foundation slab node (must be IfcSlab or have a valid contour)
  const foundationNode =
    [...nodes.values()].find((n) => selected.includes(n.id) && n.type === 'IfcSlab') ||
    [...nodes.values()].find((n) => n.type === 'IfcSlab') ||
    [...nodes.values()].find((n) => Array.isArray(n.props?.contour) && n.name.toLowerCase().includes('slab'));

  const isSlabSelected = foundationNode ? selected.includes(foundationNode.id) : false;

  // Initialize Three.js group
  useEffect(() => {
    if (!scene) return;
    const grp = new THREE.Group();
    grp.name = 'litestep-aabb-overlay';
    scene.add(grp);
    overlayGroupRef.current = grp;

    return () => {
      scene.remove(grp);
      grp.clear();
      overlayGroupRef.current = null;
    };
  }, [scene]);

  // Progress simulation while compiling
  useEffect(() => {
    if (!compiling) {
      setProgress(0);
      return;
    }
    setProgress(15);
    const interval = setInterval(() => {
      setProgress((p) => {
        if (p < 30) return p + 8;
        if (p < 70) return p + 5;
        if (p < 92) return p + 2;
        return p;
      });
    }, 150);
    return () => clearInterval(interval);
  }, [compiling]);

  // Handle Dragging and Live AABB calculation at 60 FPS
  useEffect(() => {
    if (!overlayGroupRef.current || !scene || !foundationNode) return;

    const grp = overlayGroupRef.current;
    const rawContour = fromPairs(foundationNode.props.contour);
    if (rawContour.length < 3) return;

    // Detect dragging: active whenever cursor exists and slab is selected
    const isDragging = cursor !== null && isSlabSelected;

    // Build the active contour (live preview if dragging, else current committed)
    let activeContour = rawContour;
    if (isDragging && base && cursor) {
      const n = rawContour.length;
      let bestVertexIdx = -1;
      let minVertexD = Infinity;

      rawContour.forEach((pt, i) => {
        const d = Math.hypot(pt.x - base.x, pt.y - base.y);
        if (d < minVertexD) {
          minVertexD = d;
          bestVertexIdx = i;
        }
      });

      let bestEdgeIdx = -1;
      let minEdgeD = Infinity;
      for (let i = 0; i < n; i++) {
        const p1 = rawContour[i];
        const p2 = rawContour[(i + 1) % n];
        const midX = (p1.x + p2.x) / 2;
        const midY = (p1.y + p2.y) / 2;
        const d = Math.hypot(midX - base.x, midY - base.y);
        if (d < minEdgeD) {
          minEdgeD = d;
          bestEdgeIdx = i;
        }
      }

      // If closer to an edge midpoint than a corner vertex, move the entire edge
      if (minEdgeD < minVertexD) {
        const dx = cursor.x - base.x;
        const dy = cursor.y - base.y;
        const idx1 = bestEdgeIdx;
        const idx2 = (bestEdgeIdx + 1) % n;
        activeContour = rawContour.map((pt, i) =>
          i === idx1 || i === idx2 ? { x: pt.x + dx, y: pt.y + dy } : pt
        );
      } else if (bestVertexIdx !== -1) {
        // Move single corner vertex
        activeContour = rawContour.map((pt, i) => (i === bestVertexIdx ? { x: cursor.x, y: cursor.y } : pt));
      }
    }

    // Only display AABBs when dragging or compiling
    if (!isDragging && !compiling) {
      if (grp.children.length > 0) {
        grp.clear();
        setLooks('litestep.ghost', {});
        setHudInfo(null);
        invalidate();
      }
      return;
    }

    // Compute extents
    let minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity;
    activeContour.forEach((p) => {
      minX = Math.min(minX, p.x);
      maxX = Math.max(maxX, p.x);
      minY = Math.min(minY, p.y);
      maxY = Math.max(maxY, p.y);
    });

    const wallMinX = minX + SLAB_EXTEND;
    const wallMaxX = maxX - SLAB_EXTEND;
    const wallMinY = minY + SLAB_EXTEND;
    const wallMaxY = maxY - SLAB_EXTEND;

    const wallLen = Math.max(1.0, wallMaxX - wallMinX);
    const wallWid = Math.max(1.0, wallMaxY - wallMinY);
    const wallCenterX = (wallMinX + wallMaxX) / 2;
    const wallCenterY = (wallMinY + wallMaxY) / 2;

    const wallHeight = 2.1;
    const foundationTopZ = 0.3; // Slab sits flush 0 to 0.3
    const wallCenterZ = foundationTopZ + wallHeight / 2;

    // Ghost non-Lapin procedural elements
    const ghostMap: Record<string, { opacity: number }> = {};
    nodes.forEach((n) => {
      if (
        n.id !== foundationNode.id &&
        (n.type === 'IfcBuildingElementProxy' ||
          n.name.startsWith('proxy:') ||
          n.name.startsWith('moss_') ||
          n.name.startsWith('log_') ||
          n.type === 'IfcWall')
      ) {
        ghostMap[n.id] = { opacity: 0.15 };
      }
    });
    setLooks('litestep.ghost', ghostMap);

    // Rebuild AABB meshes in real time
    grp.clear();
    const accent = theme === 'graphite' ? ACCENT_GRAPHITE : ACCENT_COLOR;

    // 1. Log Walls Assembly AABB (stretches dynamically across the full envelope)
    grp.add(createBoxMesh([wallLen, wallWid, wallHeight], [wallCenterX, wallCenterY, wallCenterZ], accent, 0.22));

    // 2. Terrain Clearing Pad AABB
    grp.add(createBoxMesh([wallLen + 3.0, wallWid + 3.0, 0.05], [wallCenterX, wallCenterY, 0.0], 0x5b6b45, 0.25));

    // 3. Openings AABBs (Door on South, Windows on South, North, East, West)
    // South Door (matches 05_log_cabin.py: dx0 = wallMinX - 0.25 + 1.20, width = 1.0 -> center = wallMinX + 1.45)
    grp.add(createBoxMesh([1.0, 0.35, 2.1], [wallMinX + 1.45, wallMinY, foundationTopZ + 1.05], OPENING_COLOR, 0.4));
    // South Window (matches 05_log_cabin.py: wx0 = wallMaxX + 0.25 - 2.20, width = 1.0 -> center = wallMaxX - 1.45)
    grp.add(createBoxMesh([1.0, 0.35, 1.2], [wallMaxX - 1.45, wallMinY, foundationTopZ + 0.8 + 0.6], OPENING_COLOR, 0.4));
    // North Window
    grp.add(createBoxMesh([1.0, 0.35, 1.2], [wallCenterX, wallMaxY, foundationTopZ + 0.8 + 0.6], OPENING_COLOR, 0.4));
    // East Window
    grp.add(createBoxMesh([0.35, 1.0, 1.2], [wallMaxX, wallCenterY, foundationTopZ + 0.8 + 0.6], OPENING_COLOR, 0.4));
    // West Window
    grp.add(createBoxMesh([0.35, 1.0, 1.2], [wallMinX, wallCenterY, foundationTopZ + 0.8 + 0.6], OPENING_COLOR, 0.4));

    setHudInfo({
      length: Math.round(wallLen * 1000),
      width: Math.round(wallWid * 1000),
      status: isDragging ? 'Dragging envelope (60 FPS)' : 'Compiling procedural logs into AABB...',
    });

    invalidate();
  }, [liveOwner, cursor, base, isSlabSelected, foundationNode, theme, compiling, scene, invalidate, nodes]);

  // Watch for committed contour change after drag to trigger compile
  useEffect(() => {
        if (!foundationNode || !foundationNode.props?.contour) return;
    const rawContour = fromPairs(foundationNode.props.contour);
    if (rawContour.length < 3) return;

    let minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity;
    rawContour.forEach((p) => {
      minX = Math.min(minX, p.x);
      maxX = Math.max(maxX, p.x);
      minY = Math.min(minY, p.y);
      maxY = Math.max(maxY, p.y);
    });
    const len = Math.round((maxX - minX - 2 * SLAB_EXTEND) * 1000);
    const wid = Math.round((maxY - minY - 2 * SLAB_EXTEND) * 1000);
    const centerX = Math.round(((minX + maxX) / 2) * 1000);
    const centerY = Math.round(((minY + maxY) / 2) * 1000);

        const isWindowRecomputing = typeof window !== 'undefined' && Boolean((window as any).__litestepRecomputing);
    if (!lastDimensionsRef.current || externalRecomputeActive || isWindowRecomputing || compiling) {
      lastDimensionsRef.current = { len, wid, cx: centerX, cy: centerY };
      return;
    }

    const prev = lastDimensionsRef.current;
    const dLen = Math.abs(len - prev.len);
    const dWid = Math.abs(wid - prev.wid);
    const dCx = Math.abs(centerX - prev.cx);
    const dCy = Math.abs(centerY - prev.cy);

    if (dLen < 25 && dWid < 25 && dCx < 25 && dCy < 25) {
      return;
    }

    if (len >= 3000 && wid >= 2000) {
      lastDimensionsRef.current = { len, wid, cx: centerX, cy: centerY };
      setCompiling(true);
      setHudInfo({
        length: len,
        width: wid,
        status: `Recompiling watertight logs & moss to fit new ${len}×${wid}mm slab...`,
      });

      const doRecompute = async () => {
        const legacyIds: string[] = [];
        useSession.getState().nodes.forEach((n) => {
          if (n.type === 'IfcBuildingElementProxy' && n.name.includes('_id_')) {
            legacyIds.push(n.id);
          }
        });
        if (legacyIds.length > 0) {
          await execute('element.delete', { ids: legacyIds }).catch(() => {});
        }
        await execute('litestep.recompute', {
          params: {
            CABIN_LENGTH: len,
            CABIN_WIDTH: wid,
            CENTER_X: centerX,
            CENTER_Y: centerY,
          },
        }, { timeoutMs: 120_000 });
        const postLegacyIds: string[] = [];
        useSession.getState().nodes.forEach((n) => {
          if (n.type === 'IfcBuildingElementProxy' && n.name.includes('_id_')) {
            postLegacyIds.push(n.id);
          }
        });
        if (postLegacyIds.length > 0) {
          await execute('element.delete', { ids: postLegacyIds }).catch(() => {});
        }
      };

      doRecompute()
        .then(() => {
          setProgress(100);
          window.dispatchEvent(new CustomEvent('litestep:params-updated'));
          notify(`Lite-STEP logs compiled into new ${len}×${wid}mm envelope (Single Room)!`);
        })
        .catch((err: unknown) => {
          console.error('Auto recompute failed:', err);
          notify(`Recompute failed: ${err}`);
        })
        .finally(() => {
          setCompiling(false);
          if (overlayGroupRef.current) {
            overlayGroupRef.current.clear();
          }
          setLooks('litestep.ghost', {});
          setHudInfo(null);
          invalidate();
        });
    }
  }, [foundationNode]);

  if (!hudInfo) return null;

  return (
    <div
      style={{
        position: 'absolute',
        bottom: 24,
        left: '50%',
        transform: 'translateX(-50%)',
        background: 'rgba(28, 27, 25, 0.94)',
        color: '#f3efe7',
        border: '1px solid #e07a4f',
        borderRadius: 8,
        padding: '10px 18px',
        fontSize: 12,
        fontFamily: 'system-ui, -apple-system, monospace',
        pointerEvents: 'none',
        display: 'flex',
        flexDirection: 'column',
        gap: 8,
        boxShadow: '0 8px 24px rgba(0,0,0,0.5)',
        zIndex: 100,
        minWidth: 320,
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          {compiling ? (
            <svg
              style={{
                width: 14,
                height: 14,
                animation: 'spin 0.8s linear infinite',
              }}
              viewBox="0 0 24 24"
              fill="none"
              stroke="#e07a4f"
              strokeWidth="3"
            >
              <circle cx="12" cy="12" r="10" stroke="#443c36" />
              <path d="M12 2a10 10 0 0 1 10 10" strokeLinecap="round" />
            </svg>
          ) : (
            <span style={{ width: 8, height: 8, borderRadius: '50%', background: '#e07a4f' }} />
          )}
          <span style={{ color: '#e07a4f', fontWeight: 600, letterSpacing: '0.05em' }}>
            {compiling ? 'UPDATING MODEL' : 'AABB ENVELOPE'}
          </span>
        </div>
        <span style={{ fontFamily: 'monospace', opacity: 0.85 }}>
          {hudInfo.length} mm × {hudInfo.width} mm
        </span>
      </div>

      {compiling && (
        <div
          style={{
            width: '100%',
            background: 'rgba(255, 255, 255, 0.1)',
            borderRadius: 4,
            height: 6,
            overflow: 'hidden',
          }}
        >
          <div
            style={{
              width: `${progress}%`,
              background: 'linear-gradient(90deg, #e07a4f, #f29a6b)',
              height: '100%',
              borderRadius: 4,
              transition: 'width 0.2s ease',
            }}
          />
        </div>
      )}

      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          fontSize: 11,
          color: compiling ? '#9cc8e8' : '#e07a4f',
        }}
      >
        <span>{hudInfo.status}</span>
        {compiling && <span style={{ fontFamily: 'monospace' }}>{progress}%</span>}
      </div>

      <style>{`
        @keyframes spin {
          from { transform: rotate(0deg); }
          to { transform: rotate(360deg); }
        }
      `}</style>
    </div>
  );
}
