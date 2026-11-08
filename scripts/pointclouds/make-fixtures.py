"""
The tiny point clouds the engine's tests read (engine/tests/Lapin.Engine.Tests/Fixtures/pointclouds), written by other
programs than Lapin so its readers are checked against them: LAZ by laspy with lazrs (Apache-2.0/MIT), E57 by pye57
(libE57Format, BSL-1.0). Dev only, never a dependency of Lapin:

    python3 -m venv /tmp/pcvenv && /tmp/pcvenv/bin/pip install "laspy[lazrs]" pye57 numpy
    /tmp/pcvenv/bin/python scripts/pointclouds/make-fixtures.py

Each file comes with <name>.expected.csv: every 7th point's x, y, z, intensity, class, red, green, blue as written.
"""
import os, sys
import numpy as np
import laspy

out = os.path.join(os.path.dirname(__file__), '..', '..', 'engine', 'tests', 'Lapin.Engine.Tests', 'Fixtures', 'pointclouds')
os.makedirs(out, exist_ok=True)
rng = np.random.default_rng(42)

# Chunks small enough that every file holds several: fixed ones of 1000 points, or variable ones (a chunk a write).
import lazrs, struct
from laspy._compression import lazrsbackend
_variable = {'on': False}
_init = lazrsbackend.LazrsPointWriter.__init__
def _patched_init(self, dest, point_format, parallel):
    _init(self, dest, point_format, False)
    if _variable['on']:
        self.vlr = lazrs.LazVlr.new_for_compression(point_format.id, point_format.num_extra_bytes, True)
    else:
        raw = bytearray(self.vlr.record_data())
        struct.pack_into('<I', raw, 12, 1000)
        self.vlr = lazrs.LazVlr(bytes(raw))
lazrsbackend.LazrsPointWriter.__init__ = _patched_init
_write = lazrsbackend.LazrsPointWriter.write_points
def _patched_write(self, points):
    _write(self, points)
    if _variable['on']: self.compressor.finish_current_chunk()
lazrsbackend.LazrsPointWriter.write_points = _patched_write

def cloud(n):
    # A room's floor and two walls, a few returns per pulse, GPS times in sequences, colours by position.
    t = rng.random(n)
    x = np.where(t < 0.5, rng.random(n) * 8, np.where(t < 0.75, rng.random(n) * 8, 0.0)) + 500000
    y = np.where(t < 0.5, rng.random(n) * 6, np.where(t < 0.75, 0.0, rng.random(n) * 6)) + 400000
    z = np.where(t < 0.5, rng.random(n) * 0.01, rng.random(n) * 3) + 120
    cls = np.where(t < 0.5, 2, 6).astype(np.uint8)
    cls[rng.random(n) < 0.05] = 1
    inten = (rng.random(n) * 60000).astype(np.uint16)
    red = ((x - 500000) / 8 * 65535).astype(np.uint16)
    green = ((y - 400000) / 6 * 65535).astype(np.uint16)
    blue = ((z - 120) / 3 * 65535).astype(np.uint16)
    gps = 1000 + np.cumsum(rng.integers(0, 3, n)) * 0.00001
    return x, y, z, cls, inten, red, green, blue, gps

def las_file(name, fmt, version, n, extra=False, variable=False):
    x, y, z, cls, inten, red, green, blue, gps = cloud(n)
    h = laspy.LasHeader(point_format=fmt, version=version)
    h.scales = [0.001, 0.001, 0.001]
    h.offsets = [500000, 400000, 0]
    if extra:
        h.add_extra_dim(laspy.ExtraBytesParams(name='amplitude', type=np.uint16))
    las = laspy.LasData(h)
    las.x, las.y, las.z = x, y, z
    las.intensity = inten
    las.classification = cls
    las.return_number = np.ones(n, dtype=np.uint8) + (rng.random(n) < 0.3)
    las.number_of_returns = np.maximum(las.return_number, 1 + (rng.random(n) < 0.4))
    if 'gps_time' in las.point_format.dimension_names: las.gps_time = gps
    if 'red' in las.point_format.dimension_names: las.red, las.green, las.blue = red, green, blue
    if 'nir' in las.point_format.dimension_names: las.nir = (inten // 2).astype(np.uint16)
    if 'scan_angle' in las.point_format.dimension_names: las.scan_angle = rng.integers(-3000, 3000, n).astype(np.int16)
    elif 'scan_angle_rank' in las.point_format.dimension_names: las.scan_angle_rank = rng.integers(-30, 30, n).astype(np.int8)
    las.point_source_id = (np.arange(n) // 500).astype(np.uint16)
    if extra: las.amplitude = rng.integers(0, 1000, n).astype(np.uint16)
    path = os.path.join(out, name)
    _variable['on'] = variable
    with laspy.open(path, mode='w', header=h, laz_backend=laspy.LazBackend.Lazrs) as w:
        for k in range(0, n, 700): w.write_points(las.points[k:k + 700])
    back = laspy.read(path)
    with open(path + '.expected.csv', 'w') as f:
        for i in range(0, n, 7):
            r = (back.red[i], back.green[i], back.blue[i]) if 'red' in back.point_format.dimension_names else (0, 0, 0)
            f.write(f"{back.x[i]:.3f},{back.y[i]:.3f},{back.z[i]:.3f},{back.intensity[i]},{back.classification[i]},{r[0]},{r[1]},{r[2]}\n")
    print(name, n, os.path.getsize(path), 'bytes')

las_file('f1.laz', 1, '1.2', 3000)
las_file('f3.laz', 3, '1.2', 3000, extra=True, variable=True)
las_file('f6.laz', 6, '1.4', 3000)
las_file('f7.laz', 7, '1.4', 3000, extra=True, variable=True)
las_file('f8.laz', 8, '1.4', 3000, variable=True)

# E57: three scans, the first with a pose (turned 90° and moved), cartesian with colour and intensity; one spherical.
import pye57
path = os.path.join(out, 'three-scans.e57')
e = pye57.E57(path, mode='w')
n = 1200
x, y, z, cls, inten, red, green, blue, gps = cloud(n)
x = x - 500000; y = y - 400000; z = z - 120
e.write_scan_raw({'cartesianX': x, 'cartesianY': y, 'cartesianZ': z, 'intensity': inten / 65535.0,
                  'colorRed': (red >> 8).astype(np.uint8), 'colorGreen': (green >> 8).astype(np.uint8), 'colorBlue': (blue >> 8).astype(np.uint8)},
                 name='scan one', rotation=np.array([0.7071067811865476, 0, 0, 0.7071067811865476]), translation=np.array([10.0, 20.0, 1.5]))
state = np.zeros(600, dtype=np.int8); state[::10] = 2
e.write_scan_raw({'cartesianX': x[:600], 'cartesianY': y[:600], 'cartesianZ': z[:600], 'cartesianInvalidState': state,
                  'rowIndex': np.arange(600, dtype=np.uint16), 'columnIndex': (np.arange(600) % 7).astype(np.uint16)}, name='scan two')
# A third, written through libE57Format's own nodes: spherical, its range a scaled integer (0.1 mm), intensity 12 bits.
L = pye57.libe57
imf = e.image_file
scan = L.StructureNode(imf)
scan.set('guid', L.StringNode(imf, '{00000000-0000-0000-0000-000000000003}'))
scan.set('name', L.StringNode(imf, 'scan three'))
proto = L.StructureNode(imf)
proto.set('sphericalRange', L.ScaledIntegerNode(imf, 0, 0, 1000000, 0.0001, 0.0))
proto.set('sphericalAzimuth', L.FloatNode(imf, 0.0, L.E57_DOUBLE, -4.0, 4.0))
proto.set('sphericalElevation', L.FloatNode(imf, 0.0, L.E57_DOUBLE, -2.0, 2.0))
proto.set('intensity', L.IntegerNode(imf, 0, 0, 4095))
proto.set('sphericalInvalidState', L.IntegerNode(imf, 0, 0, 2))
m = 500
rr = 2 + rng.random(m) * 50
az = (rng.random(m) - 0.5) * 6
el = (rng.random(m) - 0.5) * 3
it = rng.integers(0, 4096, m)
st = np.zeros(m, dtype=np.int8); st[3::25] = 2
arrays = {'sphericalRange': rr.astype(np.float64), 'sphericalAzimuth': az.astype(np.float64), 'sphericalElevation': el.astype(np.float64),
          'intensity': it.astype(np.uint16), 'sphericalInvalidState': st.astype(np.int8)}
buffers = L.VectorSourceDestBuffer()
for k in ['sphericalRange', 'sphericalAzimuth', 'sphericalElevation', 'intensity', 'sphericalInvalidState']:
    buffers.append(L.SourceDestBuffer(imf, k, arrays[k], m, True, True))
points = L.CompressedVectorNode(imf, proto, L.VectorNode(imf, True))
scan.set('points', points)
e.data3d.append(scan)
w = points.writer(buffers)
w.write(m)
w.close()
e.close()
with open(os.path.join(out, 'three-scans.e57.spherical.csv'), 'w') as f:
    for i in range(m):
        if st[i] == 2: continue
        r = round(rr[i] / 0.0001) * 0.0001
        f.write(f"{r * np.cos(el[i]) * np.cos(az[i]):.4f},{r * np.cos(el[i]) * np.sin(az[i]):.4f},{r * np.sin(el[i]):.4f},{it[i]}\n")
with open(path + '.expected.csv', 'w') as f:
    # Scan one in world coordinates: R(90° about z) · p + t.
    # Intensity as Lapin keeps it: normalised by the scan's limits to 16 bits.
    iv = inten / 65535.0
    lo, hi = np.min(iv), np.max(iv)
    for i in range(0, n, 7):
        wx, wy, wz = -y[i] + 10.0, x[i] + 20.0, z[i] + 1.5
        f.write(f"{wx:.4f},{wy:.4f},{wz:.4f},{round((iv[i] - lo) / (hi - lo) * 65535)},{0},{red[i] >> 8},{green[i] >> 8},{blue[i] >> 8}\n")
print('three-scans.e57', os.path.getsize(path), 'bytes')
