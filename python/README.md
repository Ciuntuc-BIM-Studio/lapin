# lapinbim

Drive [Lapin](https://lapin.ciuntucbimstudio.ro/), the free and open (MIT) BIM modeller on IFC, from Python.

Every Lapin tool is an engine command with named arguments and one undo step. `lapinbim` sends those commands to the Lapin that is open on your computer, so a script's walls show at once in the window and undo with Ctrl+Z like your own.

- Python 3.11 or newer, the standard library only: no dependencies.
- It talks to the Lapin desktop app (or `pnpm dev`) on `127.0.0.1` only.
- MIT, like Lapin.

## Install

```sh
pip install lapinbim
```

## Connect

Open Lapin, then:

```python
import lapinbim as lapin

print(lapin.info().product.name)
```

The package finds the open Lapin by the port file its engine writes (`host.json` in Lapin's user folder). `LAPIN_PORT=47810` names the port instead (the one `pnpm dev` uses), and `lapin.connect(port)` too.

## Add walls

```python
import lapinbim as lapin

storey = next(s.id for s in lapin.model.storeys if s.elevation == 0)   # the storey at ±0.00
w = lapin.architecture.add_wall(storey_id=storey, x1=0, y1=0, x2=5, y2=0, height=2.8)
print(w.id)                                   # its GlobalId: one undo step, named "Wall"
```

Lengths are metres and angles degrees, as in the engine. Command names are the engine's in snake_case (`architecture.addWall` is `lapin.architecture.add_wall`), and so are the arguments (`storeyId` is `storey_id`). The engine's own spelling works too: `lapin.execute("architecture.addWall", {"storeyId": storey, "x1": 0, ...})`.

## One undo step for many commands

```python
with lapin.batch("Ring of walls") as b:
    for (x1, y1), (x2, y2) in [((0, 0), (10, 0)), ((10, 0), (10, 8)), ((10, 8), (0, 8)), ((0, 8), (0, 0))]:
        lapin.architecture.add_wall(storey_id=storey, x1=x1, y1=y1, x2=x2, y2=y2)
print([r["id"] for r in b.results])
```

The commands go together when the block ends: one undo step named "Ring of walls", all or nothing.

## Read the model and its quantities

```python
for wall in lapin.model.walls:
    print(wall.id, wall.name, wall.height, wall.quantity("NetVolume"))   # m³
print(lapin.model.walls[0].quantities())      # every quantity set, as the properties panel shows it

floors = lapin.model.floors                   # IfcSlab, by its label; lapin.model.classes lists them
slabs = lapin.model.of("IfcSlab")
wall = lapin.model[w.id]
wall.update(height=3.0)                       # element.update: one undo step
```

## Undo, save, events

```python
lapin.undo()
lapin.redo()
lapin.save("/Users/me/Projects/house.lapin")
lapin.on("diff", lambda d: print(len(d.nodes), "entities changed"))
```

## Editor completion

The package carries type stubs for the commands of the Lapin it was published with. For the commands of the Lapin you have open (a newer one, or an edition with more modules):

```sh
python -m lapinbim stubs
```

## More

The full guide, with every call: [docs/python.md](https://github.com/Ciuntuc-BIM-Studio/lapin-pro/blob/main/docs/python.md).
