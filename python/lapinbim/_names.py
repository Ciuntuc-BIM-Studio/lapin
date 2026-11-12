"""Names between the engine and Python: ``architecture.addWall`` is ``architecture.add_wall``, ``storeyId`` is ``storey_id``."""

from __future__ import annotations

import keyword
import re

_BOUNDARY = re.compile(r"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")


def snake(name: str) -> str:
    """``addWall`` → ``add_wall``, ``exportIFC`` → ``export_ifc``, ``cutOrder`` → ``cut_order``; dots and dashes become ``_``."""
    return _BOUNDARY.sub("_", name).replace(".", "_").replace("-", "_").lower()


def camel(name: str) -> str:
    """``storey_id`` → ``storeyId``; a name without ``_`` stays as it is; a trailing ``_`` (``from_``) is dropped."""
    name = name.rstrip("_") if name.endswith("_") and not name.startswith("_") else name
    head, *rest = name.split("_")
    return head + "".join(p[:1].upper() + p[1:] for p in rest)


def ident(name: str) -> str:
    """A Python identifier for ``name``: ``from`` → ``from_``, ``3d`` → ``_3d``."""
    # Soft keywords (match, case, type) are fine as names.
    if keyword.iskeyword(name):
        return name + "_"
    if name[:1].isdigit():
        return "_" + name
    return name


_UNCOUNTED = {"furniture", "equipment", "information", "data"}


def plural(word: str) -> str:
    """``wall`` → ``walls``, ``storey`` → ``storeys``, ``property`` → ``properties``, ``box`` → ``boxes``; a word plural
    already (``stages``, ``norms``) or uncountable (``furniture``) stays as it is."""
    last = word.rsplit("_", 1)[-1]
    if last in _UNCOUNTED or (last.endswith("s") and not last.endswith(("ss", "us", "is"))):
        return word
    if word.endswith("y") and len(word) > 1 and word[-2] not in "aeiou":
        return word[:-1] + "ies"
    if word.endswith(("s", "x", "z", "ch", "sh")):
        return word + "es"
    return word + "s"
