"""The model as Python reads it: the entities of ``doc.snapshot`` (JSON, read once and kept until a diff comes)."""

from __future__ import annotations

from typing import TYPE_CHECKING, Any, Iterator

from ._names import camel, snake

if TYPE_CHECKING:
    from ._client import Lapin


class Element:
    """One entity: its ``id``, IFC ``type`` (class), ``name``, ``storey`` (id), ``module`` and ``attrs``.

    An attribute reads by its name, in snake_case or as the engine spells it (``wall.height``, ``wall.type_id``), and
    an assignment changes it: ``wall.height = 3`` is ``wall.update(height=3)``, one undo step (in a batch or a step,
    part of it). What changes it goes through the engine's commands: ``update``, ``rename``, ``set_param``, ``delete``.
    The element is what the model was when read (an assignment updates its own copy too); ``refresh()`` reads it again.
    """

    __slots__ = ("_client", "id", "type", "name", "storey", "module", "parent", "attrs")

    def __init__(self, client: "Lapin", node: dict):
        self._client = client
        self.id: str = node.get("id") or ""
        self.type: str = node.get("type") or ""
        self.name: str = node.get("name") or ""
        self.storey: str | None = node.get("storeyId")
        self.module: str | None = node.get("module")
        self.parent: str | None = node.get("parentId")
        self.attrs: dict = node.get("props") or {}

    def __getattr__(self, name: str) -> Any:
        attrs = object.__getattribute__(self, "attrs")
        if name in attrs:
            return attrs[name]
        c = camel(name)
        if c in attrs:
            return attrs[c]
        raise AttributeError(f"{object.__getattribute__(self, 'type')} {object.__getattribute__(self, 'id')} has no attribute {name!r}")

    def __setattr__(self, name: str, value: Any) -> None:
        if name in Element.__slots__:
            object.__setattr__(self, name, value)
            return
        if name.startswith("_"):
            raise AttributeError(f"{name!r} is not a field of an element.")
        # The engine's element.update: it checks the field (and refuses what the class does not have, or cannot change).
        self.update(**{name: value})
        attrs = object.__getattribute__(self, "attrs")
        key = name if name in attrs else camel(name)
        if value is None:
            attrs.pop(key, None)
        else:
            attrs[key] = value

    def __dir__(self):
        return [*super().__dir__(), *(snake(k) for k in self.attrs if isinstance(k, str))]

    def refresh(self) -> "Element":
        """The element read again from the engine (its fields as they are now); KeyError when it is gone."""
        fresh = self._client.model[self.id]
        for k in ("type", "name", "storey", "module", "parent", "attrs"):
            object.__setattr__(self, k, getattr(fresh, k))
        return self

    @property
    def fields(self) -> dict[str, str]:
        """Its class's fields as the engine describes them (``session.info``): each key and its label."""
        cls = self._client.catalogue().classes.get(self.type) or {}
        return {f["key"]: f.get("label") or f["key"] for f in cls.get("fields") or [] if f.get("key")}

    def __repr__(self) -> str:
        return f"<{self.type} {self.id} {self.name!r}>"

    def __eq__(self, other: object) -> bool:
        return isinstance(other, Element) and other.id == self.id

    def __hash__(self) -> int:
        return hash(self.id)

    def quantities(self) -> Any:
        """Its quantity sets (lengths, areas, volumes; its layers'), as the properties panel shows them."""
        return self._client.quantities(self.id)

    def quantity(self, name: str, default: Any = None) -> Any:
        """One quantity's value by its IFC name, from whichever set has it: ``wall.quantity("NetVolume")`` (m³)."""
        for qset in self.quantities() or []:
            for q in qset.get("quantities") or []:
                if q.get("name") == name:
                    return q.get("value")
        return default

    def update(self, **attrs: Any) -> Any:
        """Its fields changed (``element.update``): ``wall.update(height=3)``; one undo step."""
        return self._client.execute("element.update", {"id": self.id, "attrs": {camel(k): v for k, v in attrs.items()}})

    def rename(self, name: str) -> Any:
        return self._client.execute("element.rename", {"id": self.id, "name": name})

    def set_param(self, name: str, value: Any, kind: str | None = None, pset: str | None = None) -> Any:
        """One of the user's parameters (``element.setParam``): a new one needs its ``kind`` (text, number, length, bool…)."""
        args: dict[str, Any] = {"id": self.id, "name": name, "value": value}
        if kind:
            args["kind"] = kind
        if pset:
            args["pset"] = pset
        return self._client.execute("element.setParam", args)

    def delete(self) -> Any:
        return self._client.execute("element.delete", {"ids": [self.id]})


class Model:
    """The open model's entities: ``model.walls``, ``model.of("IfcSlab")``, ``model["e41"]``, ``len(model)``, ``for e in model``.

    A plural reads a class by its label (``walls`` → IfcWall, ``storeys`` → IfcBuildingStorey); ``model.classes`` lists them.
    The entities are read from the engine once and kept until the engine sends a change (any diff: an edit, an undo, the
    window's own); then the next read asks again.
    """

    def __init__(self, client: "Lapin"):
        self._client = client

    def _elements(self) -> dict[str, Element]:
        return self._client._elements()

    def all(self) -> list[Element]:
        return list(self._elements().values())

    def of(self, *classes: str) -> list[Element]:
        """The entities of these IFC classes (``model.of("IfcWall", "IfcCurtainWall")``)."""
        want = set(classes)
        return [e for e in self._elements().values() if e.type in want]

    def get(self, id: str, default: Any = None) -> Element | Any:
        return self._elements().get(id, default)

    def __getitem__(self, id: str) -> Element:
        try:
            return self._elements()[id]
        except KeyError:
            raise KeyError(f"The model has no entity {id!r}.") from None

    def find(self, cls: str, name: str) -> Element:
        """The first entity of an IFC class with this name (``model.find("IfcWallType", "Brick 250")``): how a recorded
        script names a type or a material, so it finds the one of the same name in another project. KeyError when none."""
        for e in self._elements().values():
            if e.type == cls and e.name == name:
                return e
        raise KeyError(f"The model has no {cls} named {name!r}.")

    def storey(self, name: str | None = None, elevation: float | None = None) -> Element:
        """A storey by its name, else by its elevation (within 1 mm), else (neither given) the ground floor: the lowest
        at or above ±0.00. ``model.storey("Ground floor", 0.0)`` is how a recorded script names the storey it drew on,
        so it draws on the same one in a new project. KeyError when there is none."""
        storeys = sorted(self.of("IfcBuildingStorey"), key=lambda s: float(s.attrs.get("elevation") or 0))
        if name is not None:
            for s in storeys:
                if s.name == name:
                    return s
        if elevation is not None:
            for s in storeys:
                if abs(float(s.attrs.get("elevation") or 0) - float(elevation)) < 1e-3:
                    return s
        elif name is None:
            ground = [s for s in storeys if float(s.attrs.get("elevation") or 0) >= -1e-6]
            if ground or storeys:
                return (ground or storeys)[0]
        raise KeyError(f"The model has no storey {name!r}" + (f" nor one at {elevation} m." if elevation is not None else "."))

    def __contains__(self, id: object) -> bool:
        return id in self._elements()

    def __iter__(self) -> Iterator[Element]:
        return iter(self.all())

    def __len__(self) -> int:
        return len(self._elements())

    @property
    def classes(self) -> dict[str, str]:
        """Each plural this model reads (``walls``) and the IFC class it stands for."""
        return dict(self._client.catalogue().plurals)

    def __getattr__(self, name: str) -> list[Element]:
        if name.startswith("_"):
            raise AttributeError(name)
        cls = self._client.catalogue().plurals.get(name)
        if cls is None:
            raise AttributeError(f"The model has no class read as {name!r}; model.classes lists them, model.of('IfcWall') reads one by its IFC name.")
        return self.of(cls)

    def __dir__(self):
        return [*super().__dir__(), *self._client.catalogue().plurals]

    def __repr__(self) -> str:
        return f"<lapin model: {len(self)} entities>"
