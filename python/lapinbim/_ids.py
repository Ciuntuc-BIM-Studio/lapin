"""IFC GlobalIds: 22 characters of IFC's own base 64 for a 128-bit GUID (the IFC specification's compressed form)."""

from __future__ import annotations

import uuid

_ALPHABET = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$"

#: The namespace stable ids are made in when no other is given (a UUID of Lapin's own).
LAPIN_NAMESPACE = uuid.UUID("6f2c1a8e-5d3b-4c71-9a0e-2b8f4e7d1c55")


def compress(guid: uuid.UUID) -> str:
    """A GUID as an IFC GlobalId (the same characters Lapin's engine writes for it)."""
    n = guid.int
    out = [_ALPHABET[n >> 126]]
    for k in range(1, 22):
        out.append(_ALPHABET[(n >> (126 - 6 * k)) & 63])
    return "".join(out)


def global_id(name: str, namespace: uuid.UUID | str | None = None) -> str:
    """A GlobalId that is always the same for ``name`` (a UUID 5 of it, compressed): an element a program makes again
    from the same name keeps its GlobalId, so Lapin, IFC files and other tools keep knowing it. ``namespace`` (a UUID, or
    any text, itself hashed) keeps one program's names apart from another's."""
    if namespace is None:
        ns = LAPIN_NAMESPACE
    elif isinstance(namespace, uuid.UUID):
        ns = namespace
    else:
        ns = uuid.uuid5(LAPIN_NAMESPACE, str(namespace))
    return compress(uuid.uuid5(ns, name))
