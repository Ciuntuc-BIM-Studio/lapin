# Elevators (testers' reports #54 and #97)

The open `transport` module (`engine/src/Modules/Transport/Lapin.Transport`, `ui/src/modules/transport`), in the `lapin`
edition, requires `core` and `architecture`. Its tools sit in Architecture → Elevators.

## The element

- `IfcTransportElement`, PredefinedType `ELEVATOR` (IFC4's and IFC 4.3's `IfcTransportElementTypeEnum`), of an
  `IfcTransportElementType` made per kind, rated load and car (`key` on the type), shared by elevators alike; a type no
  elevator uses and that carries no `articles` goes.
- Contained in its lowest stop's storey; `topStoreyId` its highest stop; `unserved` the storeys between it passes
  without stopping (the lowest and highest are always served).
- `at` the middle of the shaft's clear footprint, `rotation` with its doors on its local −y.
- Its own attributes, every one written out (so `ShowIf` reads them): `drive` (traction, mrl, hydraulic, home), `load`,
  `persons`, `speed`, `cabinWidth/Depth/Height`, `glass`, `entrances` (front, through), `doorWidth/Height`,
  `doorOpening` (centre, side), `doorPanels`, `counterweight` (left, right, back; a hydraulic lift's `jackSide`),
  `accessible` (none, type1–3), `pit`, `headroom`, `machineRoom` (top, beside, none), `shaftWallTypeId` or
  `shaftThickness`, `shaftGlass`, `fireFighting`, `fireExit`; `style` and `kind`.

## Layout (`ElevatorLayout`)

The shaft's clear size is the car with its drive's clearances (side, counterweight or jack zone, front for the doors,
back; a through car has the front's at the back), widened when the landing doors need more wall to slide along (a
centre door 2 × its width, a side door its width × (1 + 1/panels)). Pit under the lowest stop and headroom over the
highest default to the drive and speed (`SuggestedPit`, `SuggestedHeadroom`) and follow a change of either until typed.
Parts: the shaft (pit floor, walls with the landings' openings, cap; cut in plans: `GeometryPart.CutInPlan`), the
machine room (on the cap, or a hydraulic lift's power unit room beside the shaft at the bottom), the car at the lowest
stop (glass sides and back when panoramic), car doors, landing doors, counterweight or jack.

## EN 81

`En81`: Table 6 of EN 81-20 (rated load → largest available car area, linear between rows, 0.16 m² per 100 kg above
2500 kg), Table 8 (persons → least area, 0.115 m² each above 20), persons = load / 75 kg within the area; EN 81-70's
type 1 (1000 × 1250, door 800), type 2 (1100 × 1400, door 800), type 3 (2000 × 1400, door 900). `ElevatorSpec.Checks`
says what does not comply; the catalogue's styles all comply. Where a report's size needs a larger load than it gave
(450 kg in 1100 × 1400; 800 kg in 1350 × 1600; 1400 × 2400 or 1500 × 2300 for 1275 kg), the style takes EN 81's size
for the load and says so in its description.

## The platform

- `IStoreySpans`: an element on several storeys drawn by each plan it reaches, with that storey's symbols.
- `IIfcElementSets`: standard sets a module answers in IFC4 and 4.3 (`Pset_TransportElementCommon` merged into the
  common set, `Pset_TransportElementElevator` its own); `StandardPsets` knows both.
- The floors' openings are `IfcOpeningElement`s of the slabs (`elevatorId`, `contour` the shaft's outer footprint), kept in
  step on place, configure, Properties, move/rotate/copy (`ITransformFollowers`), a slab changed, and deletion; a floor
  drawn after the elevator opens with `transport.syncOpenings` (Architecture → Shaft openings).

## Open

- A floor drawn after an elevator is not opened by itself.
- Escalators and moving walkways (the same class, other predefined types).
- Car and landing door finishes, guide rails, buffers, a machine room larger than the shaft.
- Traffic analysis (handling capacity, interval) to choose how many elevators.
