import { useState, useEffect } from 'react';
import { execute, useSession } from '../engine/session';
import { useUi, notify } from '../app/store';
import { useCamera } from '../viewport/camera';
import { markLiteStepRecomputing } from '../viewport/LiteStepAABBOverlay';
import {
  Play,
  RefreshCw,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  Code2,
  GitBranch,
  Sliders,
  Copy,
  Sparkles
} from 'lucide-react';

interface ParamSpec {
  id: string;
  name: string;
  kind: string;
  default: number;
  min: number;
  max: number;
  step: number;
  unit: string;
  tier: string;
}

export function LiteStepBrowserPanel() {
  const [params, setParams] = useState<Record<string, number>>({
    CABIN_LENGTH: 6000.0,
    CABIN_WIDTH: 4000.0,
    NUM_LOGS_PER_WALL: 7,
    ROOF_PITCH: 35.0,
    RANDOM_SEED: 42,
  });
  const [specs, setSpecs] = useState<ParamSpec[]>([]);
  const [computing, setComputing] = useState(false);
  const [settledTime, setSettledTime] = useState<number | null>(null);

  // Collapsible section state
  const [paramsOpen, setParamsOpen] = useState(true);
  const [dagOpen, setDagOpen] = useState(true);
  const [codeOpen, setCodeOpen] = useState(false);
  const [codeFormat, setCodeFormat] = useState<'py' | 'lapin' | 'ifc'>('py');

  const nodes = useSession((s) => s.nodes);
  const entitiesCount = nodes.size;

  useEffect(() => {
    execute('litestep.schema', {})
      .then((res: unknown) => {
        const data = res as { parameters?: ParamSpec[] };
        if (data?.parameters) setSpecs(data.parameters);
      })
      .catch((e: unknown) => console.error('Failed to load litestep schema:', e));

    const refreshParams = () => {
      execute('litestep.getParams', {})
        .then((res: unknown) => {
          if (res && typeof res === 'object') setParams(res as Record<string, number>);
        })
        .catch((e: unknown) => console.error('Failed to get params:', e));
    };
    refreshParams();
    window.addEventListener('litestep:params-updated', refreshParams);
    return () => window.removeEventListener('litestep:params-updated', refreshParams);
  }, []);

  const handleSliderChange = (id: string, value: number) => {
    setParams((prev) => ({ ...prev, [id]: value }));
  };

  const handleRecompute = async (overrides?: Record<string, number>) => {
    setComputing(true);
    const start = performance.now();
    try {
      const active = overrides || params;
      markLiteStepRecomputing(true);
      const legacyIds: string[] = [];
      useSession.getState().nodes.forEach((n) => {
        if (n.type === 'IfcBuildingElementProxy' && n.name.includes('_id_')) {
          legacyIds.push(n.id);
        }
      });
      if (legacyIds.length > 0) {
        await execute('element.delete', { ids: legacyIds }).catch(() => {});
      }

      // Recompute with deterministic stable GUIDs in-place
      await execute('litestep.recompute', { params: active }, { timeoutMs: 120_000 });
      const postLegacyIds: string[] = [];
      useSession.getState().nodes.forEach((n) => {
        if (n.type === 'IfcBuildingElementProxy' && n.name.includes('_id_')) {
          postLegacyIds.push(n.id);
        }
      });
      if (postLegacyIds.length > 0) {
        await execute('element.delete', { ids: postLegacyIds }).catch(() => {});
      }
      const elapsed = Math.round(performance.now() - start);
      setSettledTime(elapsed);
      notify(`Lite-STEP model recomputed and imported in ${elapsed} ms!`);
      useUi.setState({ view: '3d' });
      setTimeout(() => {
        useCamera.getState().api?.fit();
      }, 150);
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : String(e);
      notify(`Recompute error: ${msg}`);
    } finally {
      setTimeout(() => markLiteStepRecomputing(false), 100);
      setComputing(false);
    }
  };

  const getPythonCode = () => [
    '# ── Lite-STEP Declarative Parameters ──',
    `CABIN_LENGTH = ${params.CABIN_LENGTH?.toFixed(1) || '6000.0'}`,
    `CABIN_WIDTH = ${params.CABIN_WIDTH?.toFixed(1) || '4000.0'}`,
    `CORNER_OVERHANG = 250.0`,
    `NUM_LOGS_PER_WALL = ${Math.round(params.NUM_LOGS_PER_WALL || 7)}`,
    `ROOF_PITCH_DEG = ${params.ROOF_PITCH?.toFixed(1) || '35.0'}`,
    `RANDOM_SEED = ${Math.round(params.RANDOM_SEED || 42)}`,
    '',
    '# ── Procedural Freeform Mesh Log Walls ──',
    'logs_s = [make_log(i, LOG_LEN_LONG, rng) for i in range(NUM_LOGS_PER_WALL)]',
    'opt_s = optimize_log_wall(logs_s, base_height=SLAB_HEIGHT)',
    'wall_south.add(build_log_mesh(...))',
    'wall_south.opening(make_door("door_south", 1000, 2100), along=1200, up=0)',
    'wall_south.opening(CABIN_WINDOW.occurrence("win_south"), along=4200, up=800)',
    '',
    '# ── BubbleBIM Parametric Foundation & Roof ──',
    'slab_elem = Slab(name="foundation_slab")',
    'slab_elem.add(Box(start=Point(-3150, -2150, 0), end=Point(3150, 2150, 300)))',
    'roof = HippedRoof(shape="gable", pitch=ROOF_PITCH_DEG, eave_offset=wall_top_z)'
  ].join('\n');

  const getLapinJson = () => JSON.stringify({
    format: "Lapin Native Document (BubbleBIM)",
    entities: {
      "slab:foundation_slab": {
        class: "IfcSlab",
        attrs: {
          contour: [
            [-params.CABIN_LENGTH / 2000 - 0.15, -params.CABIN_WIDTH / 2000 - 0.15],
            [params.CABIN_LENGTH / 2000 + 0.15, -params.CABIN_WIDTH / 2000 - 0.15],
            [params.CABIN_LENGTH / 2000 + 0.15, params.CABIN_WIDTH / 2000 + 0.15],
            [-params.CABIN_LENGTH / 2000 - 0.15, params.CABIN_WIDTH / 2000 + 0.15]
          ],
          thickness: 0.3,
          interactiveGrips: "architecture.updateSlab"
        }
      },
      "roof:gable": {
        class: "IfcRoof",
        attrs: { pitch: params.ROOF_PITCH, eaveZ: 2.85 }
      }
    }
  }, null, 2);

  const getIfcPreview = () => [
    'ISO-10303-21;',
    'HEADER;',
    "FILE_DESCRIPTION(('ViewDefinition [CoordinationView]'),'2;1');",
    'FILE_SCHEMA((\'IFC4X3_ADD2\'));',
    'DATA;',
    "#1=IFCPROJECT('0WE0uKUSv56OlhOAn9GEDE',$,'Laftehus — Log Cabin',$,$,$,$,(#10),#5);",
    "#28=IFCSLAB('2aqEWRt$D28fPAD5QayJXy',$,'slab:foundation_slab',$,$,#31,$,$,.FLOOR.);",
    `#32=IFCRECTANGLEPROFILEDEF(.AREA.,$,$,${params.CABIN_LENGTH + 300}.,${params.CABIN_WIDTH + 300}.);`,
    '#36=IFCEXTRUDEDAREASOLID(#32,#34,#7,300.);',
    "#49=IFCBUILDINGELEMENTPROXY('0lp3V_yOvFOAXO$mAx6IlU',$,'mesh:south_tier_1_id_5',$,$,#16,#56,$,$);",
    "#270=IFCOPENINGELEMENT('2SBBpK0N50SR$XNWLujPNp',$,'door:door_south:void',$,$,#286,#280,$,$);",
    "#274=IFCDOOR('1pP4g9u4X8kO9R$g15aZzQ',$,'door_south',$,$,#290,#291,2.1,1.0);",
    'ENDSEC;',
    'END-ISO-10303-21;'
  ].join('\n');

  const defaultSpecs: ParamSpec[] = [
    { id: 'CABIN_LENGTH', name: 'Cabin Length', kind: 'length', default: 6000, min: 4000, max: 10000, step: 500, unit: 'mm', tier: 'Tier 0 & 1' },
    { id: 'CABIN_WIDTH', name: 'Cabin Width', kind: 'length', default: 4000, min: 3000, max: 7000, step: 500, unit: 'mm', tier: 'Tier 0 & 1' },
    { id: 'NUM_LOGS_PER_WALL', name: 'Log Tiers', kind: 'number', default: 7, min: 5, max: 11, step: 1, unit: 'tiers', tier: 'Tier 1' },
    { id: 'ROOF_PITCH', name: 'Roof Pitch', kind: 'angle', default: 35, min: 20, max: 50, step: 5, unit: 'deg', tier: 'Tier 2' },
    { id: 'RANDOM_SEED', name: 'Wood Grain Seed', kind: 'number', default: 42, min: 1, max: 99999, step: 1, unit: 'seed', tier: 'Tier 1' },
  ];

  const activeSpecs = specs.length > 0 ? specs : defaultSpecs;

  return (
    <div className="litestep-browser-panel" style={{ display: 'flex', flexDirection: 'column', height: '100%', overflowY: 'auto' }}>
      {/* Banner / Info */}
      <div style={{ padding: '10px 14px', borderBottom: '1px solid var(--rule)', background: 'var(--wash)' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 11, fontWeight: 600, letterSpacing: '0.06em', textTransform: 'uppercase', color: 'var(--accent)' }}>
          <Sparkles size={13} />
          Lite-STEP ⇄ BubbleBIM
        </div>
        <div style={{ fontSize: 13, fontWeight: 700, color: 'var(--ink)', marginTop: 2 }}>
          Parametric Cabin Bridge
        </div>
        <div style={{ fontSize: 11.5, color: 'var(--ink-muted)', marginTop: 2 }}>
          3-format synchronization with DAG-guided optimistic rendering.
        </div>
      </div>

      {/* Sliders Group */}
      <div className="tree-group">
        <div
          className="tree-label"
          style={{ cursor: 'pointer', userSelect: 'none' }}
          onClick={() => setParamsOpen(!paramsOpen)}
        >
          {paramsOpen ? <ChevronDown size={12} /> : <ChevronRight size={12} />}
          <Sliders size={13} />
          Parameters (θ)
          <span className="count">{activeSpecs.length}</span>
        </div>

        {paramsOpen && (
          <div style={{ padding: '4px 14px 10px', display: 'flex', flexDirection: 'column', gap: 10 }}>
            {activeSpecs.map((s) => (
              <div key={s.id} style={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: 11.5 }}>
                  <span style={{ color: 'var(--ink)' }}>{s.name}</span>
                  <span style={{ fontFamily: 'var(--mono, monospace)', color: 'var(--accent)', fontWeight: 600, fontSize: 11 }}>
                    {params[s.id] ?? s.default} {s.unit}
                  </span>
                </div>
                <input
                  type="range"
                  min={s.min}
                  max={s.max}
                  step={s.step}
                  value={params[s.id] ?? s.default}
                  onChange={(e) => handleSliderChange(s.id, parseFloat(e.target.value))}
                  style={{ width: '100%', accentColor: 'var(--accent)', cursor: 'ew-resize' }}
                />
              </div>
            ))}

            {/* Action buttons */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6, marginTop: 6 }}>
              <button
                type="button"
                className="btn primary"
                disabled={computing}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: 6,
                  padding: '7px 12px',
                  fontWeight: 600,
                  fontSize: 12,
                  background: 'var(--accent)',
                  color: 'var(--bg)',
                  border: 0,
                  borderRadius: 3,
                  cursor: computing ? 'wait' : 'pointer'
                }}
                onClick={() => handleRecompute()}
              >
                {computing ? <RefreshCw size={13} style={{ animation: "spin 0.8s linear infinite" }} /> : <Play size={13} fill="currentColor" />}
                {computing ? 'Solving Log Stack…' : 'Compile & Render in Lapin'}
              </button>

              <button
                type="button"
                className="btn"
                disabled={computing}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: 6,
                  padding: '5px 10px',
                  fontSize: 11.5,
                  background: 'var(--raised)',
                  border: '1px solid var(--rule-strong)',
                  borderRadius: 3,
                  cursor: 'pointer'
                }}
                onClick={() => {
                  const rnd = Math.floor(Math.random() * 9999) + 1;
                  handleSliderChange('RANDOM_SEED', rnd);
                  handleRecompute({ ...params, RANDOM_SEED: rnd });
                }}
              >
                <RefreshCw size={11} />
                Reroll Organic Wood Grain
              </button>
            </div>

            {/* Performance notification */}
            {settledTime && (
              <div style={{ display: 'flex', alignItems: 'center', gap: 5, fontSize: 11, color: 'var(--ok, #10b981)', marginTop: 2 }}>
                <CheckCircle2 size={13} />
                Recomputed & settled in {settledTime} ms
              </div>
            )}
          </div>
        )}
      </div>

      {/* DAG Dependency Group */}
      <div className="tree-group" style={{ borderTop: '1px solid var(--line, var(--rule))' }}>
        <div
          className="tree-label"
          style={{ cursor: 'pointer', userSelect: 'none' }}
          onClick={() => setDagOpen(!dagOpen)}
        >
          {dagOpen ? <ChevronDown size={12} /> : <ChevronRight size={12} />}
          <GitBranch size={13} />
          Dependency DAG (Tiers)
          <span className="count">3</span>
        </div>

        {dagOpen && (
          <div style={{ padding: '4px 14px 10px', display: 'flex', flexDirection: 'column', gap: 7 }}>
            {/* Tier 0 */}
            <div style={{ padding: '6px 8px', background: 'var(--raised)', borderLeft: '3px solid #10b981', borderRadius: 2 }}>
              <div style={{ fontSize: 11.5, fontWeight: 600, color: 'var(--ink)' }}>Tier 0: Fast Nodes (0.1ms)</div>
              <div style={{ fontSize: 10.5, color: 'var(--ink-muted)' }}>Foundation Slab (IfcSlab) • Realtime Drag Grips</div>
              <div style={{ fontSize: 10, color: '#10b981', marginTop: 2 }}>✔ Interactive corner/edge grips in 3D</div>
            </div>

            {/* Tier 1 */}
            <div style={{ padding: '6px 8px', background: 'var(--raised)', borderLeft: '3px solid #f59e0b', borderRadius: 2 }}>
              <div style={{ fontSize: 11.5, fontWeight: 600, color: 'var(--ink)' }}>Tier 1: Procedural Brep (~200ms)</div>
              <div style={{ fontSize: 10.5, color: 'var(--ink-muted)' }}>28 Organic Logs • Moss Joints • CP-SAT Stack</div>
              <div style={{ fontSize: 10, color: computing ? '#f59e0b' : 'var(--ink-muted)', marginTop: 2 }}>
                {computing ? '⟳ Recomputing manifold meshes…' : '• 100% watertight carved Breps'}
              </div>
            </div>

            {/* Tier 2 */}
            <div style={{ padding: '6px 8px', background: 'var(--raised)', borderLeft: '3px solid var(--accent)', borderRadius: 2 }}>
              <div style={{ fontSize: 11.5, fontWeight: 600, color: 'var(--ink)' }}>Tier 2: Gated Reconciliations</div>
              <div style={{ fontSize: 10.5, color: 'var(--ink-muted)' }}>Top Elevation Z • Openings • Gable Roof</div>
              <div style={{ fontSize: 10, color: 'var(--accent)', marginTop: 2 }}>✔ Snapped to upper log elevation</div>
            </div>

            <div style={{ fontSize: 10.5, color: 'var(--ink-muted)', marginTop: 2 }}>
              Active in session: <strong>{entitiesCount} entities</strong>
            </div>
          </div>
        )}
      </div>

      {/* Multi-Format Code Inspector */}
      <div className="tree-group" style={{ borderTop: '1px solid var(--line, var(--rule))' }}>
        <div
          className="tree-label"
          style={{ cursor: 'pointer', userSelect: 'none' }}
          onClick={() => setCodeOpen(!codeOpen)}
        >
          {codeOpen ? <ChevronDown size={12} /> : <ChevronRight size={12} />}
          <Code2 size={13} />
          Multi-Format Source
          <span className="count">{codeFormat.toUpperCase()}</span>
        </div>

        {codeOpen && (
          <div style={{ padding: '4px 14px 12px' }}>
            {/* Format toggle pills */}
            <div style={{ display: 'flex', gap: 4, marginBottom: 8 }}>
              <button
                type="button"
                className={`browser-tab${codeFormat === 'py' ? ' on' : ''}`}
                style={{ padding: '2px 8px', fontSize: 11 }}
                onClick={() => setCodeFormat('py')}
              >
                Python (.py)
              </button>
              <button
                type="button"
                className={`browser-tab${codeFormat === 'lapin' ? ' on' : ''}`}
                style={{ padding: '2px 8px', fontSize: 11 }}
                onClick={() => setCodeFormat('lapin')}
              >
                Lapin JSON
              </button>
              <button
                type="button"
                className={`browser-tab${codeFormat === 'ifc' ? ' on' : ''}`}
                style={{ padding: '2px 8px', fontSize: 11 }}
                onClick={() => setCodeFormat('ifc')}
              >
                IFC4X3
              </button>
            </div>

            {/* Code container */}
            <div style={{ position: 'relative' }}>
              <pre
                style={{
                  margin: 0,
                  padding: 10,
                  background: 'var(--raised)',
                  border: '1px solid var(--rule)',
                  borderRadius: 3,
                  fontSize: 10.5,
                  fontFamily: 'var(--mono, monospace)',
                  lineHeight: 1.45,
                  overflowX: 'auto',
                  maxHeight: 220,
                  color: 'var(--ink)'
                }}
              >
                <code>
                  {codeFormat === 'py' && getPythonCode()}
                  {codeFormat === 'lapin' && getLapinJson()}
                  {codeFormat === 'ifc' && getIfcPreview()}
                </code>
              </pre>
              <button
                type="button"
                className="icon mini"
                title="Copy code"
                style={{ position: 'absolute', top: 4, right: 4 }}
                onClick={() => {
                  const text = codeFormat === 'py' ? getPythonCode() : codeFormat === 'lapin' ? getLapinJson() : getIfcPreview();
                  navigator.clipboard.writeText(text);
                  notify('Copied to clipboard');
                }}
              >
                <Copy size={11} />
              </button>
            </div>
          </div>
        )}
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
