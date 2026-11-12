"""The engine's binary frames (engine/src/Platform/Lapin.Protocol/BinaryFrame.cs), read with the standard library.

::

    "LPF1"           4 bytes, the frame's mark
    header length    uint32 LE, the JSON header's bytes
    header           the message as JSON (UTF-8), padded with spaces to a multiple of 4
    buffers          every float / int array of the message, back to back, each 4-aligned

In the header an array is a reference, ``{"$buf": offset, "n": count, "t": "f32" | "i32"}``, its offset counted
from the start of the buffers. It is read as a ``memoryview`` on the frame (format ``"f"`` or ``"i"``), without
copying: ``len``, indexing and ``tolist()`` work, and ``numpy.frombuffer(v, numpy.float32)`` takes it as it is.
"""

from __future__ import annotations

import array
import json
import struct
import sys
from typing import Any

from ._names import camel

MARK = b"LPF1"
TYPES = {"f32": "f", "i32": "i"}


class Obj(dict):
    """A JSON object from the engine: a dict whose keys read as attributes too (``r.id``, ``node.storey_id``)."""

    __slots__ = ()

    def __getattr__(self, name: str) -> Any:
        if name in self:
            return self[name]
        c = camel(name)
        if c in self:
            return self[c]
        raise AttributeError(name)

    def __dir__(self):
        return list(super().__dir__()) + [k for k in self if isinstance(k, str) and k.isidentifier()]


def _array(frame: memoryview, base: int, ref: dict) -> Any:
    fmt = TYPES.get(ref["t"])
    if fmt is None:
        raise ValueError(f"Unknown buffer type {ref['t']!r}.")
    start = base + int(ref["$buf"])
    n = int(ref["n"])
    view = frame[start : start + 4 * n]
    if sys.byteorder == "little":
        return view.cast("f") if fmt == "f" else view.cast("i")
    out = array.array(fmt, bytes(view))
    out.byteswap()
    return out


def decode(data: str | bytes | bytearray | memoryview) -> Any:
    """A message as the engine sent it: an LPF1 frame (arrays as views on it), or a JSON text."""
    if isinstance(data, str):
        return json.loads(data, object_hook=Obj)
    raw = bytes(data) if not isinstance(data, bytes) else data
    if raw[:4] != MARK:
        return json.loads(raw.decode("utf-8"), object_hook=Obj)
    if len(raw) < 8:
        raise ValueError("A Lapin frame shorter than its header.")
    (n,) = struct.unpack_from("<I", raw, 4)
    base = 8 + n
    view = memoryview(raw)

    def hook(d: dict) -> Any:
        if "$buf" in d and "n" in d and "t" in d and len(d) == 3:
            return _array(view, base, d)
        return Obj(d)

    return json.loads(raw[8:base].decode("utf-8"), object_hook=hook)


def encode(header: Any, buffers: list[bytes] | tuple[bytes, ...] = ()) -> bytes:
    """A frame as the engine writes it (tests): the header padded to 4 bytes, then the buffers."""
    text = json.dumps(header).encode("utf-8")
    text += b" " * (-len(text) % 4)
    return MARK + struct.pack("<I", len(text)) + text + b"".join(buffers)
