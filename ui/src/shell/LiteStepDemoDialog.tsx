import { useState, useEffect } from 'react';
import { execute, useSession } from '../engine/session';
import { useUi, notify } from '../app/store';
import { useCamera } from '../viewport/camera';
import { X, Play, RefreshCw, CheckCircle2 } from 'lucide-react';

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

export function LiteStepDemoDialog() {
  const [params, setParams] = useState<Record<string, number>>({
    CABIN_LENGTH: 6000.0,
    CABIN_WIDTH: 4000.0,
    NUM_LOGS_PER_WALL: 7,
    ROOF_PITCH: 35.0,
    RANDOM_SEED: 42
  });
  const [specs, setSpecs] = useState<ParamSpec[]>([]);
  const [computing, setComputing] = useState(false);
  const [activeTab, setActiveTab] = useState<'ui' | 'py' | 'lapin' | 'ifc'>('ui');
  const [pyCode, setPyCode] = useState<string>('');
  const [ifcPreview, setIfcPreview] = useState<string>('');
  const [settledTime, setSettledTime] = useState<number | null>(null);

  const entitiesCount = useSession((s: { nodes: Map<string, unknown> }) => s.nodes.size);

  // Load schema and current params on mount
  useEffect(() => {
    execute('litestep.schema', {})
      .then((res: unknown) => {
        const data = res as { parameters?: ParamSpec[] };
        if (data?.parameters) setSpecs(data.parameters);
      })
      .catch((e: unknown) => console.error('Failed to load litestep schema:', e));

    execute('litestep.getParams', {})
      .then((res: unknown) => {
        if (res && typeof res === 'object') setParams(res as Record<string, number>);
      })
      .catch((e: unknown) => console.error('Failed to get params:', e));
  }, []);

  const handleSliderChange = (id: string, value: number) => {
    setParams((prev) => ({ ...prev, [id]: value }));
  };

  const handleRecompute = async (overrides?: Record<string, number>) => {
    setComputing(true);
    const start = performance.now();
    try {
      const active = overrides || params;
      await execute('litestep.recompute', { params: active }, { timeoutMs: 120_000 });
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
      setComputing(false);
    }
  };

  const loadCodePreview = () => {
    const lines = [
      '# ── Lite-STEP Declarative Parameters (05_log_cabin.py) ──',
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
      'slab_elem = Element(ifc_class="IfcSlab", name="foundation_slab")',
      'roof = HippedRoof(shape="gable", pitch=ROOF_PITCH_DEG, eave_offset=wall_top_z)'
    ];
    setPyCode(lines.join('\n'));

    const ifcSample = [
      'ISO-10303-21;',
      'HEADER;',
      'FILE_DESCRIPTION((\'ViewDefinition [ReferenceView]\'),\'2;1\');',
      'FILE_SCHEMA((\'IFC4X3_ADD2\'));',
      'ENDSEC;',
      'DATA;',
      '#1=IFCPROJECT(\'30Mcw1vCXFc8vpDpqu96EI\',$,\'Laftehus Log Cabin\',$,$,$,$,(#10),#5);',
      `#14=IFCSLAB('2A$vQ...',$,'foundation_slab',$,$,#15,#16);  /* ${params.CABIN_LENGTH} x ${params.CABIN_WIDTH} mm */`,
      `#20=IFCPROPERTYSET('1P_...',$,'Lapin_Intent',$,(#21,#22)); /* outline: [${params.CABIN_LENGTH}, ${params.CABIN_WIDTH}] */`,
      '#40=IFCWALL(\'39K_...\',$,\'log_wall_south\',$,$,#41,#42);',
      `#50=IFCBUILDINGELEMENTPART('1X_...',$,'south_tier_1_id_5',$,$,#51,#52); /* Triangulated organic log mesh */`,
      `#51=IFCTRIANGULATEDFACESET(#60,((1,2,3),(3,2,4)...),$,$); /* Watertight Brep */`,
      `#70=IFCOPENINGELEMENT('2O_...',$,'opening_door_south',$,$,#71,#72);`,
      `#75=IFCDOOR('0D_...',$,'door_south',$,$,#76,#77);`,
      `#90=IFCROOF('3R_...',$,'cabin_roof',$,$,#91,#92); /* pitch: ${params.ROOF_PITCH} deg, shape: gable */`,
      'ENDSEC;',
      'END-ISO-10303-21;'
    ];
    setIfcPreview(ifcSample.join('\n'));
  };

  useEffect(() => {
    loadCodePreview();
  }, [params]);

  const close = () => useUi.setState({ dialog: null });

  return (
    <div className="dialog-host" role="dialog" aria-label="Lite-STEP & BubbleBIM Bridge">
      <div className="dialog litestep-dialog" style={{ width: 880, maxWidth: '95vw', maxHeight: '90vh', overflowY: 'auto' }}>
        <header className="dialog-head" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div>
            <span className="eyebrow" style={{ color: 'var(--accent, #6366f1)', fontWeight: 600 }}>Lite-STEP ⇄ BubbleBIM ⇄ IFC4X3</span>
            <h2 style={{ margin: 0, fontSize: '1.25rem' }}>3-Format Parametric & DAG Demo (Log Cabin)</h2>
          </div>
          <button type="button" className="icon" onClick={close}><X size={18} /></button>
        </header>

        <nav className="dialog-tabs" style={{ display: 'flex', gap: 8, borderBottom: '1px solid var(--line, #333)', margin: '16px 0 12px' }}>
          <button type="button" className={`tab ${activeTab === 'ui' ? 'on' : ''}`} onClick={() => setActiveTab('ui')}>
            Interactive Sliders & DAG
          </button>
          <button type="button" className={`tab ${activeTab === 'py' ? 'on' : ''}`} onClick={() => setActiveTab('py')}>
            1. Lite-STEP (.py)
          </button>
          <button type="button" className={`tab ${activeTab === 'lapin' ? 'on' : ''}`} onClick={() => setActiveTab('lapin')}>
            2. BubbleBIM / Lapin (.lapin)
          </button>
          <button type="button" className={`tab ${activeTab === 'ifc' ? 'on' : ''}`} onClick={() => setActiveTab('ifc')}>
            3. Standard IFC4X3 (.ifc)
          </button>
        </nav>

        {activeTab === 'ui' && (
          <div className="litestep-content" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 20 }}>
            <div className="params-box" style={{ background: 'var(--panel-bg, #1e1e24)', padding: 16, borderRadius: 8 }}>
              <h3 style={{ fontSize: '1rem', marginTop: 0, display: 'flex', alignItems: 'center', gap: 6 }}>
                Top-of-File Parameters (θ)
              </h3>
              <p className="muted small">Dragging sliders triggers instantaneous Tier 0 foundation updates while dispatching async log optimization.</p>

              <div style={{ display: 'flex', flexDirection: 'column', gap: 14, marginTop: 16 }}>
                {specs.map((s) => (
                  <div key={s.id} style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.85rem' }}>
                      <span style={{ fontWeight: 500 }}>{s.name} ({s.id})</span>
                      <span className="mono" style={{ color: 'var(--accent, #6366f1)' }}>
                        {params[s.id] ?? s.default} {s.unit}
                      </span>
                    </div>
                    <input
                      type="range"
                      min={s.min}
                      max={s.max}
                      step={s.step}
                      value={params[s.id] ?? s.default}
                      disabled={computing}
                      onChange={(e) => handleSliderChange(s.id, Number(e.target.value))}
                    />
                  </div>
                ))}
              </div>

              <div style={{ marginTop: 24, display: 'flex', gap: 10 }}>
                <button
                  type="button"
                  className="btn primary"
                  disabled={computing}
                  style={{ display: 'flex', alignItems: 'center', gap: 6 }}
                  onClick={() => handleRecompute()}
                >
                  {computing ? <RefreshCw size={14} className="spin" /> : <Play size={14} />}
                  {computing ? 'Solving Log Stack & Carves…' : 'Compile & Render in Lapin'}
                </button>
                <button
                  type="button"
                  className="btn"
                  disabled={computing}
                  onClick={() => {
                    const rnd = Math.floor(Math.random() * 9999) + 1;
                    handleSliderChange('RANDOM_SEED', rnd);
                    handleRecompute({ ...params, RANDOM_SEED: rnd });
                  }}
                >
                  Reroll Organic Logs
                </button>
              </div>

              {settledTime && (
                <div style={{ marginTop: 12, fontSize: '0.8rem', color: '#10b981', display: 'flex', alignItems: 'center', gap: 4 }}>
                  <CheckCircle2 size={14} /> Settled & rendered in {settledTime} ms
                </div>
              )}
            </div>

            <div className="dag-box" style={{ background: 'var(--panel-bg, #1e1e24)', padding: 16, borderRadius: 8 }}>
              <h3 style={{ fontSize: '1rem', marginTop: 0, display: 'flex', alignItems: 'center', gap: 6 }}>
                Extracted Dependency DAG
              </h3>
              <p className="muted small">Statically derived from Python AST (`ast_walker.py`). Guides 60 FPS optimistic updates.</p>

              <div className="dag-tree" style={{ display: 'flex', flexDirection: 'column', gap: 10, marginTop: 16 }}>
                <div style={{ padding: 10, background: 'rgba(255,255,255,0.04)', borderRadius: 6, borderLeft: '3px solid #10b981' }}>
                  <div style={{ fontWeight: 600, fontSize: '0.85rem' }}>Tier 0: Fast Parametric Nodes (0.1 - 1.0 ms)</div>
                  <div className="muted small">Foundation Slab (IfcSlab) • Bounding Box Gizmos • Ground Clearances</div>
                  <div style={{ fontSize: '0.75rem', marginTop: 4, color: '#10b981' }}>✔ Instant viewport resizing under mouse cursor</div>
                </div>

                <div style={{ padding: 10, background: 'rgba(255,255,255,0.04)', borderRadius: 6, borderLeft: '3px solid #f59e0b' }}>
                  <div style={{ fontWeight: 600, fontSize: '0.85rem' }}>Tier 1: Heavy Procedural Nodes (200 - 450 ms)</div>
                  <div className="muted small">28 Organic Logs (make_log) • OR-Tools Stack Optimizer • Moss Chinking</div>
                  <div style={{ fontSize: '0.75rem', marginTop: 4, color: computing ? '#f59e0b' : '#9ca3af' }}>
                    {computing ? '⟳ Running in background worker...' : '• Watertight Brep meshes calculated deterministically'}
                  </div>
                </div>

                <div style={{ padding: 10, background: 'rgba(255,255,255,0.04)', borderRadius: 6, borderLeft: '3px solid #6366f1' }}>
                  <div style={{ fontWeight: 600, fontSize: '0.85rem' }}>Tier 2: Gated Reconciliations (Instant upon Tier 1)</div>
                  <div className="muted small">Wall Top Elevation Z • Door & Window Carves • Gable Roof (IfcRoof)</div>
                  <div style={{ fontSize: '0.75rem', marginTop: 4, color: '#6366f1' }}>
                    {computing ? '⏳ Roof holding optimistic ghost until logs settle' : '✔ Snapped & seated at top log elevation'}
                  </div>
                </div>
              </div>

              <div style={{ marginTop: 18, padding: 10, background: 'rgba(99, 102, 241, 0.1)', borderRadius: 6, fontSize: '0.8rem' }}>
                <strong>Active Model in Lapin:</strong> {entitiesCount} entities loaded in current session.
              </div>
            </div>
          </div>
        )}

        {activeTab === 'py' && (
          <div>
            <p className="muted small">Single source of truth: pure Python script. Diffs in Git cleanly without binary lock-in.</p>
            <pre style={{ background: '#111', padding: 14, borderRadius: 6, fontSize: '0.85rem', overflowX: 'auto', maxHeight: 400 }}>
              <code>{pyCode}</code>
            </pre>
          </div>
        )}

        {activeTab === 'lapin' && (
          <div>
            <p className="muted small">BubbleBIM / Lapin package view: Native parametric archetypes (Slab, Roof) + StoredBodies (Procedural organic logs).</p>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
              <div style={{ background: '#111', padding: 12, borderRadius: 6 }}>
                <h4 style={{ margin: '0 0 8px 0', fontSize: '0.9rem', color: '#6366f1' }}>Native Lapin Entities</h4>
                <ul style={{ margin: 0, paddingLeft: 16, fontSize: '0.85rem' }}>
                  <li>IfcSlab: <code>foundation_slab</code> (Editable contour, 300 mm thickness)</li>
                  <li>IfcRoof: <code>cabin_roof</code> (Gable shape, {params.ROOF_PITCH}° pitch, timber rafters)</li>
                  <li>IfcOpeningElement: <code>door_south</code>, <code>win_south</code>, <code>win_north</code></li>
                  <li>IfcBuildingStorey: <code>Ground floor</code> (Elevation: ±0.00)</li>
                </ul>
              </div>
              <div style={{ background: '#111', padding: 12, borderRadius: 6 }}>
                <h4 style={{ margin: '0 0 8px 0', fontSize: '0.9rem', color: '#10b981' }}>Lite-STEP StoredBodies (Meshes)</h4>
                <ul style={{ margin: 0, paddingLeft: 16, fontSize: '0.85rem' }}>
                  <li>{Math.round(params.NUM_LOGS_PER_WALL || 7) * 4} Organic Timber Log Meshes (Camber, taper, bark)</li>
                  <li>Moss Chinking Strips (Sealed between log cavities)</li>
                  <li>Terrain Site Clearing Mesh (Synthetic heightmap)</li>
                  <li>Boolean carved faces around hosted openings</li>
                </ul>
              </div>
            </div>
          </div>
        )}

        {activeTab === 'ifc' && (
          <div>
            <p className="muted small">Standard ISO 10303-21 STEP physical file. Universal OpenBIM interchange format.</p>
            <pre style={{ background: '#111', padding: 14, borderRadius: 6, fontSize: '0.85rem', overflowX: 'auto', maxHeight: 400 }}>
              <code>{ifcPreview}</code>
            </pre>
          </div>
        )}

        <footer className="dialog-foot" style={{ marginTop: 20, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div style={{ fontSize: '0.8rem', color: '#9ca3af' }}>
            Model in the center: Lite-STEP (.py) ⇄ BubbleBIM (.lapin) ⇄ IFC4X3 (.ifc)
          </div>
          <button type="button" className="btn" onClick={close}>Close</button>
        </footer>
      </div>
    </div>
  );
}
