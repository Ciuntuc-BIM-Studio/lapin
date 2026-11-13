"""A planar Warren truss over a room, kept live: Lapin's example of external geometry (docs/plan-external-geometry.md).

Run it from VS Code or a terminal against the open Lapin (the desktop app, or ``pnpm dev``)::

    pip install lapinbim                # or: pip install -e ./python from a checkout
    python python/examples/truss_over_room.py           # build it, then follow the room until Ctrl+C
    python python/examples/truss_over_room.py --once    # build or update it, then stop

It takes the room selected in Lapin (else the project's first room), builds a truss spanning the room's short side, its
bottom chord on the room's ceiling, as one body (``element.createBody``, owner ``script:truss_over_room``), and watches
the room: when the room changes (its outline, its height, its storey's level), the truss is built again with
``element.setBody``, keeping its GlobalId. Each update is one undo step. The truss is an IfcMember for Lapin: drawn in
plans and sections, measured, exported to IFC; its shape is changed here, not in Lapin.

MIT, Lapin's own example (Ciuntuc BIM Studio).
"""

from __future__ import annotations

import argparse
import math
import sys
import threading

import lapinbim as lapin

OWNER = "script:truss_over_room"
DEPTH = 0.9        # the truss's depth, chord axis to chord axis (m)
BAR = 0.08         # each bar's square section (m)
PANEL = 1.5        # the longest panel (m)


def bar(a: tuple, b: tuple, size: float) -> tuple[list[float], list[int]]:
    """A closed square bar from point a to point b: 8 points, 12 triangles wound outwards."""
    d = [b[i] - a[i] for i in range(3)]
    length = math.sqrt(sum(x * x for x in d))
    u = [x / length for x in d]
    # Two directions across the bar, square to it and to each other.
    ref = (0.0, 0.0, 1.0) if abs(u[2]) < 0.9 else (1.0, 0.0, 0.0)
    v = [u[1] * ref[2] - u[2] * ref[1], u[2] * ref[0] - u[0] * ref[2], u[0] * ref[1] - u[1] * ref[0]]
    n = math.sqrt(sum(x * x for x in v))
    v = [x / n * size / 2 for x in v]
    w = [u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0]]
    corners = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    pts: list[float] = []
    for end in (a, b):
        for s, t in corners:
            pts += [end[i] + s * v[i] + t * w[i] for i in range(3)]
    # The ends (0-3 at a, 4-7 at b) and the four sides; a's end faces back along the bar, b's forward.
    tris = [0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7]
    for k in range(4):
        i, j = k, (k + 1) % 4
        tris += [i, j, j + 4, i, j + 4, i + 4]
    return pts, tris


def merge(parts: list[tuple[list[float], list[int]]]) -> dict:
    """Several meshes as one body's mesh."""
    positions: list[float] = []
    indices: list[int] = []
    for pts, tris in parts:
        base = len(positions) // 3
        positions += pts
        indices += [base + t for t in tris]
    return {"positions": positions, "indices": indices}


def warren(box: list) -> dict:
    """The truss over a room's box ``[[xmin, ymin, zmin], [xmax, ymax, zmax]]``: across its short side, in its middle,
    the bottom chord on its ceiling."""
    (x0, y0, _), (x1, y1, z1) = box
    along_x = (x1 - x0) <= (y1 - y0)
    span = (x1 - x0) if along_x else (y1 - y0)
    mid = (y0 + y1) / 2 if along_x else (x0 + x1) / 2
    start = x0 if along_x else y0
    bottom = z1 + BAR / 2
    top = bottom + DEPTH

    def at(s: float, z: float) -> tuple:
        return (start + s, mid, z) if along_x else (mid, start + s, z)

    panels = max(2, math.ceil(span / PANEL))
    step = span / panels
    lower = [at(i * step, bottom) for i in range(panels + 1)]
    upper = [at((i + 0.5) * step, top) for i in range(panels)]
    bars = [bar(lower[0], lower[-1], BAR), bar(upper[0], upper[-1], BAR)]
    for i in range(panels):
        bars.append(bar(lower[i], upper[i], BAR))
        bars.append(bar(upper[i], lower[i + 1], BAR))
    return merge(bars)


def the_room() -> lapin.Element:
    """The room selected in Lapin, else the project's first."""
    for item in lapin.selected():
        if item.ifc_class == "IfcSpace":
            return lapin.model[item.id]
    rooms = lapin.model.of("IfcSpace")
    if not rooms:
        sys.exit("The project has no room: draw one (Architecture → Room) and run this again.")
    return rooms[0]


def build(room: lapin.Element) -> str:
    """The truss made, or made again: its GlobalId comes from the room's, so running this again updates it."""
    gid = lapin.global_id(f"truss_over_room:{room.id}")
    box = lapin.measure_value(f"element.{room.id}.bbox")
    body = warren(box)
    try:
        lapin.model[gid]
    except KeyError:
        lapin.create_body("IfcMember", body, storey=room.storey, name=f"Truss over {room.name}", global_id=gid,
                          owner=OWNER, attrs={"room": room.id, "depth": DEPTH, "bar": BAR})
        print(f"Truss made over {room.name} ({gid}).")
        return gid
    lapin.set_body(gid, body)
    print(f"Truss over {room.name} made again ({gid}).")
    return gid


def main() -> None:
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("--once", action="store_true", help="build or update the truss, then stop")
    p.add_argument("--port", type=int, help="the engine's port (else LAPIN_PORT, or the open Lapin's)")
    args = p.parse_args()
    if args.port:
        lapin.connect(args.port)
    room = the_room()
    build(room)
    if args.once:
        return
    # Told when another's change touches the room (the user's, an undo); our own set_body never fires it.
    changed = threading.Event()
    lapin.watch(room.id)
    lapin.on("watch", lambda _: changed.set())
    print("Watching the room: change it in Lapin; Ctrl+C stops.")
    try:
        while True:
            changed.wait()
            changed.clear()
            try:
                build(lapin.model[room.id])
            except KeyError:
                print("The room is gone; stopping.")
                return
            except lapin.LapinError as e:
                print(f"Not built again: {e}")
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
