/**
 * Interactive online Map Picker dialog (Site → Import → From map…):
 * search an address or coordinates, pan/zoom OSM or Satellite imagery, frame a site rectangle,
 * and import 3D terrain (AWS Terrarium elevation tiles → adaptive TIN) + draped orthophoto in one step.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { Globe, Layers, Minus, Plus, Search, X } from 'lucide-react';
import { backdropClose } from '../../shell/Dialog';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import {
  basemapTileUrl,
  boundsMetricSize,
  fetchOrthophoto,
  fetchOsmBuildings,
  fetchTerrariumGrid,
  lonLatToTile,
  mercatorToLonLat,
  lonLatToMercator,
  searchLocation,
  type BasemapKind,
  type GeoBounds,
} from './mapTiles';

const MAP_W = 580;
const MAP_H = 360;
const PRESETS_M = [150, 300, 500, 1000];

export function MapImportDialog() {
  const close = () => useUi.setState({ dialog: null });
  const nodes = useSession((s) => s.nodes);
  const existingTerrains = useMemo(
    () => [...nodes.values()].filter((n) => n.type === 'IfcGeographicElement' && n.props.predefinedType === 'TERRAIN'),
    [nodes],
  );

  // Default to Bucharest (consistent with Sun study default); user can search any address or lat,lon.
  const [center, setCenter] = useState<{ lat: number; lon: number }>({ lat: 44.4268, lon: 26.1025 });
  const [zoom, setZoom] = useState(15);
  const [basemap, setBasemap] = useState<BasemapKind>('satellite');
  const [searchText, setSearchText] = useState('');
  const [searching, setSearching] = useState(false);

  // Selection box in metres around the map centre (or dragged on the map).
  const [boxW, setBoxW] = useState(300);
  const [boxH, setBoxH] = useState(300);
  const [wantTerrain, setWantTerrain] = useState(true);
  const [wantOrtho, setWantOrtho] = useState(true);
  const [wantBuildings, setWantBuildings] = useState(true);
  const [maxError, setMaxError] = useState(0.25);
  const [maxPoints, setMaxPoints] = useState(3000);
  const [refineId, setRefineId] = useState('');
  const [status, setStatus] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // Panning state.
  const dragRef = useRef<{ x: number; y: number; lat: number; lon: number } | null>(null);

  const bounds: GeoBounds = useMemo(() => {
    const [cx, cy] = lonLatToMercator(center.lon, center.lat);
    const cosLat = Math.max(0.1, Math.cos((center.lat * Math.PI) / 180));
    const halfX = (boxW / 2) / cosLat;
    const halfY = (boxH / 2) / cosLat;
    const [minLon, minLat] = mercatorToLonLat(cx - halfX, cy - halfY);
    const [maxLon, maxLat] = mercatorToLonLat(cx + halfX, cy + halfY);
    return { minLon, minLat, maxLon, maxLat };
  }, [center, boxW, boxH]);

  const metric = useMemo(() => boundsMetricSize(bounds), [bounds]);

  // Visible slippy-map tiles covering MAP_W × MAP_H.
  const tiles = useMemo(() => {
    const [cx, cy] = lonLatToTile(center.lon, center.lat, zoom);
    const halfCols = MAP_W / 512 + 1;
    const halfRows = MAP_H / 512 + 1;
    const x0 = Math.floor(cx - halfCols), x1 = Math.ceil(cx + halfCols);
    const y0 = Math.floor(cy - halfRows), y1 = Math.ceil(cy + halfRows);
    const n = 2 ** zoom;
    const list: { key: string; url: string; left: number; top: number }[] = [];
    for (let y = y0; y <= y1; y++) {
      if (y < 0 || y >= n) continue;
      for (let x = x0; x <= x1; x++) {
        const wx = ((x % n) + n) % n;
        const left = MAP_W / 2 + (x - cx) * 256;
        const top = MAP_H / 2 + (y - cy) * 256;
        list.push({ key: `${basemap}-${zoom}-${x}-${y}`, url: basemapTileUrl(basemap, zoom, wx, y), left, top });
      }
    }
    return list;
  }, [center, zoom, basemap]);

  // Selection box pixel rectangle on the map viewport.
  const boxPx = useMemo(() => {
    const [cx, cy] = lonLatToTile(center.lon, center.lat, zoom);
    const [tx0, ty0] = lonLatToTile(bounds.minLon, bounds.maxLat, zoom);
    const [tx1, ty1] = lonLatToTile(bounds.maxLon, bounds.minLat, zoom);
    const left = MAP_W / 2 + (tx0 - cx) * 256;
    const top = MAP_H / 2 + (ty0 - cy) * 256;
    const width = Math.max(8, (tx1 - tx0) * 256);
    const height = Math.max(8, (ty1 - ty0) * 256);
    return { left, top, width, height };
  }, [center, zoom, bounds]);

  // Drag to pan.
  useEffect(() => {
    const onMove = (e: MouseEvent) => {
      const d = dragRef.current;
      if (!d) return;
      const dx = e.clientX - d.x;
      const dy = e.clientY - d.y;
      const [cx, cy] = lonLatToTile(d.lon, d.lat, zoom);
      const n = 2 ** zoom;
      const nx = cx - dx / 256;
      const ny = cy - dy / 256;
      const lon = (nx / n) * 360 - 180;
      const latRad = Math.atan(Math.sinh(Math.PI * (1 - (2 * ny) / n)));
      const lat = Math.max(-80, Math.min(80, (latRad * 180) / Math.PI));
      setCenter({ lat, lon });
    };
    const onUp = () => { dragRef.current = null; };
    window.addEventListener('mousemove', onMove);
    window.addEventListener('mouseup', onUp);
    return () => {
      window.removeEventListener('mousemove', onMove);
      window.removeEventListener('mouseup', onUp);
    };
  }, [zoom]);

  const doSearch = async () => {
    if (!searchText.trim()) return;
    setSearching(true);
    try {
      const hit = await searchLocation(searchText);
      if (hit) {
        setCenter({ lat: hit.lat, lon: hit.lon });
        setZoom((z) => Math.max(z, 15));
      } else {
        notify('Location not found. Try a city name, street address, or "lat, lon".');
      }
    } catch {
      notify('Could not reach the geocoding service.');
    } finally {
      setSearching(false);
    }
  };

  const doImport = async () => {
    if (!wantTerrain && !wantOrtho && !wantBuildings) { notify('Choose at least terrain, orthophoto, or buildings.'); return; }
    setBusy(true);
    const storeyId = useUi.getState().storeyId;
    try {
      let terrainPts = 0;
      let buildingCount = 0;
      if (wantTerrain) {
        setStatus('Downloading elevation tiles…');
        const dem = await fetchTerrariumGrid(bounds, 256);
        setStatus('Building 3D terrain…');
        const r = await execute('civil.importDem', {
          width: dem.width,
          height: dem.height,
          heightsF32: dem.heightsF32,
          corners: dem.corners,
          crs: 'EPSG:3857',
          maxError,
          maxPoints,
          name: `Map terrain (${Math.round(metric.widthM)}×${Math.round(metric.heightM)} m)`,
          ...(refineId ? { terrainId: refineId, replaceInside: true } : {}),
          ...(storeyId ? { storeyId } : {}),
        }) as { points?: number } | null;
        terrainPts = r?.points ?? 0;
      }
      if (wantOrtho) {
        setStatus('Downloading orthophoto…');
        const ortho = await fetchOrthophoto(bounds, basemap, 1536);
        await execute('civil.addOrthophoto', {
          data: ortho.data,
          mime: ortho.mime,
          corners: ortho.corners,
          crs: 'EPSG:3857',
          name: `Map ${basemap}`,
          ...(storeyId ? { storeyId } : {}),
        });
      }
      if (wantBuildings) {
        setStatus('Downloading neighbouring buildings (OSM)…');
        try {
          const osm = await fetchOsmBuildings(bounds);
          if (osm.length > 0) {
            setStatus(`Extruding ${osm.length} buildings…`);
            const br = await execute('civil.importContextBuildings', {
              buildings: osm,
              crs: 'EPSG:4326',
              source: 'OpenStreetMap',
              ...(storeyId ? { storeyId } : {}),
            }) as { count?: number } | null;
            buildingCount = br?.count ?? 0;
          }
        } catch {
          notify('OpenStreetMap buildings could not be fetched; terrain and orthophoto were kept.');
        }
      }
      const parts: string[] = [];
      if (wantTerrain) parts.push(`terrain (${terrainPts} points)`);
      if (wantOrtho) parts.push('orthophoto');
      if (wantBuildings && buildingCount > 0) parts.push(`${buildingCount} buildings`);
      notify(`Imported ${parts.join(', ')} from map.`);
      close();
    } catch (e) {
      notify(e instanceof Error ? e.message : 'Map import failed.');
    } finally {
      setBusy(false);
      setStatus(null);
    }
  };

  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog" style={{ width: 620, maxWidth: '95vw' }} role="dialog" aria-label="Import from map" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head">
          <span className="panel-title"><Globe size={14} style={{ verticalAlign: -2, marginRight: 6 }} />Import terrain &amp; orthophoto from map</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body" style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          {/* Search + basemap switcher */}
          <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
            <input
              type="text"
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
              onKeyDown={(e) => { if (e.key === 'Enter') void doSearch(); }}
              placeholder="Search address or lat, lon (e.g. 44.4268, 26.1025)"
              style={{ flex: 1 }}
            />
            <button type="button" className="btn" disabled={searching} onClick={() => void doSearch()}>
              <Search size={13} /> {searching ? '…' : 'Find'}
            </button>
            <button
              type="button"
              className="btn"
              title="Switch between Satellite and OpenStreetMap"
              onClick={() => setBasemap((b) => (b === 'satellite' ? 'osm' : 'satellite'))}
            >
              <Layers size={13} /> {basemap === 'satellite' ? 'Satellite' : 'OSM'}
            </button>
          </div>

          {/* Interactive slippy map viewport */}
          <div
            style={{
              position: 'relative',
              width: MAP_W,
              height: MAP_H,
              maxWidth: '100%',
              overflow: 'hidden',
              borderRadius: 6,
              border: '1px solid var(--border)',
              background: '#1c242b',
              cursor: 'grab',
              userSelect: 'none',
            }}
            onMouseDown={(e) => {
              dragRef.current = { x: e.clientX, y: e.clientY, lat: center.lat, lon: center.lon };
            }}
            onWheel={(e) => {
              e.preventDefault();
              setZoom((z) => Math.max(8, Math.min(18, z + (e.deltaY < 0 ? 1 : -1))));
            }}
          >
            {tiles.map((t) => (
              <img
                key={t.key}
                src={t.url}
                alt=""
                draggable={false}
                style={{ position: 'absolute', left: t.left, top: t.top, width: 256, height: 256, pointerEvents: 'none' }}
              />
            ))}
            {/* Site bounding box overlay */}
            <div
              style={{
                position: 'absolute',
                left: boxPx.left,
                top: boxPx.top,
                width: boxPx.width,
                height: boxPx.height,
                border: '2px solid #f59e0b',
                background: 'rgba(245, 158, 11, 0.14)',
                pointerEvents: 'none',
                boxSizing: 'border-box',
              }}
            />
            {/* Zoom controls */}
            <div style={{ position: 'absolute', right: 8, top: 8, display: 'flex', flexDirection: 'column', gap: 4 }}>
              <button type="button" className="btn" style={{ padding: '4px 6px' }} onClick={() => setZoom((z) => Math.min(18, z + 1))} title="Zoom in"><Plus size={13} /></button>
              <button type="button" className="btn" style={{ padding: '4px 6px' }} onClick={() => setZoom((z) => Math.max(8, z - 1))} title="Zoom out"><Minus size={13} /></button>
            </div>
            {/* Coordinate readout */}
            <div style={{
              position: 'absolute', left: 8, bottom: 8, padding: '2px 7px', borderRadius: 4,
              background: 'rgba(0,0,0,0.65)', color: '#fff', fontSize: 11, pointerEvents: 'none',
            }}>
              {center.lat.toFixed(5)}, {center.lon.toFixed(5)} · {Math.round(metric.widthM)} × {Math.round(metric.heightM)} m · z{zoom}
            </div>
          </div>

          {/* Area size presets & options */}
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10, alignItems: 'center', fontSize: 12 }}>
            <span className="muted">Area (m):</span>
            <input type="number" min={50} max={5000} step={50} value={boxW} onChange={(e) => setBoxW(Math.max(50, Math.min(5000, Number(e.target.value) || 200)))} style={{ width: 68 }} />
            <span>×</span>
            <input type="number" min={50} max={5000} step={50} value={boxH} onChange={(e) => setBoxH(Math.max(50, Math.min(5000, Number(e.target.value) || 200)))} style={{ width: 68 }} />
            <div style={{ display: 'flex', gap: 4 }}>
              {PRESETS_M.map((m) => (
                <button key={m} type="button" className="link small" onClick={() => { setBoxW(m); setBoxH(m); }}>{m}m</button>
              ))}
            </div>
          </div>

          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 14, alignItems: 'center', fontSize: 12 }}>
            <label style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}>
              <input type="checkbox" checked={wantTerrain} onChange={(e) => setWantTerrain(e.target.checked)} />
              3D Terrain (elevation tiles)
            </label>
            <label style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}>
              <input type="checkbox" checked={wantOrtho} onChange={(e) => setWantOrtho(e.target.checked)} />
              Orthophoto (draped in 3D)
            </label>
            <label style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}>
              <input type="checkbox" checked={wantBuildings} onChange={(e) => setWantBuildings(e.target.checked)} />
              Neighbouring buildings (OSM)
            </label>
          </div>

          {wantTerrain && (
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12, alignItems: 'center', fontSize: 12 }}>
              <label style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}>
                <span className="muted">Tolerance (m):</span>
                <input type="number" min={0.05} max={5} step={0.05} value={maxError} onChange={(e) => setMaxError(Math.max(0.05, Number(e.target.value) || 0.25))} style={{ width: 60 }} />
              </label>
              <label style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}>
                <span className="muted">Max points:</span>
                <input type="number" min={100} max={15000} step={500} value={maxPoints} onChange={(e) => setMaxPoints(Math.max(100, Math.min(15000, Number(e.target.value) || 3000)))} style={{ width: 72 }} />
              </label>
              {existingTerrains.length > 0 && (
                <label style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}>
                  <span className="muted">Target:</span>
                  <select value={refineId} onChange={(e) => setRefineId(e.target.value)}>
                    <option value="">New terrain</option>
                    {existingTerrains.map((t) => <option key={t.id} value={t.id}>Refine {t.name}</option>)}
                  </select>
                </label>
              )}
            </div>
          )}

          <p className="muted small" style={{ margin: 0 }}>
            Drag the map to pan, scroll to zoom. Elevation tiles (~30 m global resolution) are best for concept, site context and sun studies; refine the parcel with a DXF survey, GeoTIFF DTM or LiDAR point cloud for earthwork precision.
          </p>
        </div>
        <footer className="dialog-foot">
          {status && <span className="muted small">{status}</span>}
          <span className="grow" />
          <button type="button" className="btn" disabled={busy} onClick={close}>Cancel</button>
          <button type="button" className="btn primary" disabled={busy} onClick={() => void doImport()}>
            {busy ? 'Importing…' : 'Import'}
          </button>
        </footer>
      </div>
    </div>
  );
}
