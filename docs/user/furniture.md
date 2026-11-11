---
title: Furniture
order: 47
summary: Sofas, corner sofas, armchairs, chairs, beds, wardrobes, chests, bookcases, sideboards, tables, desks and kitchens in six layouts, from parametric types, placed against walls with a click.
---

## Placing furniture

The **Interior** tab has, after the finishes, a tool for each kind of piece: **Sofa** (<kbd>SO</kbd>), **Corner sofa**, **Armchair** (<kbd>AC</kbd>), **Chair**, **Bed** (<kbd>BD</kbd>), **Bedside table**, **Wardrobe** (<kbd>WD</kbd>), **Chest of drawers**, **Bookcase**, **Sideboard**, **Table** (<kbd>TB</kbd>), **Coffee table** and **Desk**. Choose one and click in the plan.

Click near a wall and the piece backs onto it. Its back sits on the face of the wall (on the plaster, not the core), its front faces the room, and it slides along the wall until it no longer runs into the wall across a corner. Click further from any wall and the piece stands where you clicked.

While a tool is on, the command line's bar shows:

- **the type** the tool puts down, chosen from the project's types of that kind (if there is none yet, the first is made as you place the piece);
- **the turn**, 0°, 90°, 180° or 270°, for a piece standing free;
- **Against walls**, which you can switch off to place a piece exactly where you click, even next to a wall.

The properties panel shows the same type before you click, with its size and **Edit types…**.

![A living room, a kitchen and a bedroom furnished, in plan](shots/furniture-plan.webp)

Plans draw furniture by its symbol, as a drawing convention, not cut:

- a sofa with its back, arms and cushions;
- a bed with its headboard, pillows and turned-down duvet;
- a wardrobe with its hanging rail and its doors;
- a kitchen with its sink and hob, and its wall units dashed.

Elevations and sections draw the bodies, with their materials' hatches. In 3D the pieces are low-poly bodies made by Lapin.

![The furnished house in 3D](shots/furniture-3d.webp)

## Chairs round a table, and the kitchen

**Chairs round a table:** click a table or a desk. Chairs go along its long sides, about one every 600 mm, and one at each end when the end is wide enough; a round table gets them in a circle. Each chair faces the table. A desk gets chairs on its front side only. The chairs are of the type chosen for the **Chair** tool.

**Kitchen** (<kbd>KT</kbd>) is drawn with two points along a wall:

- the line's length is the kitchen's length, and its back sits on the wall's face;
- the base units are split evenly at about 600 mm;
- it has a worktop, a sink and a hob (where they stand along the run is set on the type), wall units above, and a tall unit for the fridge at one end.

A kitchen's **Length** stays editable in its properties; the units are split again.

### Kitchen layouts

A kitchen type has a **Layout**: **One wall** (the straight run), **Galley** (two parallel runs with an aisle between), **L-shaped**, **U-shaped**, **Island** (a run on the back wall and a separate island with its aisle) or **Peninsula** (an L with a run reaching out from its leg, its worktop overhanging for stools). Choosing one starts the type from that layout’s sizes, and each stays editable beside the preview: the leg lengths from the back wall, the aisle between runs, the opposite run’s length, the island’s length, depth and place, the peninsula’s length and overhang, and for an L or a peninsula the corner on the left or the right.

![The six layouts in plan: one wall, galley, L-shaped, U-shaped, island and peninsula](shots/kitchen-layouts.webp)

**Sink on** and **Hob on** put them on the main run or on the other part (the leg, the opposite run, the island, the peninsula), **Sink at** and **Hob at** say where along it, and the **Tall unit (fridge)** stands at a free end. The corner units are blind; wall units are only on runs against a wall, not on an island or a peninsula.

![A kitchen type: L-shaped](shots/kitchen-layout-types.webp)

**Kitchen** (<kbd>KT</kbd>) draws any layout with two points along the wall the main run is against: the line is its length, its back on the wall’s face, and an L, a peninsula, a U or a galley is slid into the corner or between the walls it is drawn near. **Flip** gives an L or a peninsula its corner on the other side. The furniture schedule names the layout and lists the footprint’s depth. In IFC it stays an IfcFurniture of a user-defined Kitchen type.

### Kitchen units and appliances

A kitchen type can list its own **units** under its parameters in **Furniture types**, in two lines:

- the **Base line**: the main run's units from the left as seen from the front (after a blind corner);
- the **Wall line**: the units hung above it.

**Add** puts a unit of the chosen kind at the end of a line. **Typical run** starts the base line from a sink, a dishwasher, a unit of drawers and a hob over an oven. The arrows move a unit left or right, and the bin takes it out.

Each unit has a **width**. A cabinet takes any width from 100 to 800 mm: choose 150, 200, 300, 400, 450, 500, 600 or 800, or type another. An appliance takes the sizes it comes in. The units on the base line are:

- **Base unit**, whose **Front** is a drawer over a door, doors (one or two, with the side a single door is **hinged on**), drawers (1 to 4), pan drawers (a shallow drawer over two deep ones) or open shelves;
- **Sink unit**: a single sink, a 1½ bowl sink, a double sink (from 800) or a corner sink, with a standard tap, a pull-out tap or none. A corner sink reaches into the blind corner when it stands next to it.
- **Hob over an oven**, 600, 800 or 900 wide, induction or gas, with or without the oven under it;
- **Freestanding cooker**, 500, 600 or 900 wide, induction or gas. The worktop stops on either side of it.
- **Dishwasher**, integrated, 450 or 600 wide, behind a full-height front that matches the others;
- **Fridge**: under the counter, tall and integrated (a fridge door over a freezer door), or freestanding;
- **Oven tall unit**, with drawers under the oven and a door above it;
- **Tall unit**, with doors or open shelves;
- **Filler** and **Gap**.

A tall unit, a fridge or an oven unit has its own **Height**. On the wall line, a **Wall unit** has doors, glazed doors or open shelves, its own **Height** and its own height **From the floor**. Leave either empty to take the run's values. A **Hood** is a chimney hood (650 above the worktop, with its flue up to the wall units' top) or an integrated hood in a wall unit. A **Gap** leaves the wall bare. Wall units step past a tall unit.

With no wall line listed, each base unit gets a wall unit of its width, and a hob or a cooker gets a chimney hood. On an island or a peninsula, **Hood over the hob there** hangs a hood over the hob that stands on it.

What the units leave of the run is split evenly as before, and less than 100 mm becomes a filler. Units that do not fit the run are left out, and the preview and the element say so. **Fit to units** in a kitchen's properties sets its length to what its units need: theirs, plus a blind corner and the tall unit at its end. A type with no units of its own is built as before.

In plan, each unit has its own symbol:

- base doors' swings, dashed and only at the finest detail;
- the sink's bowls and its tap;
- the hob's zones or burners;
- tall units crossed;
- **DW** on the dishwasher, **F** on a fridge and **OV** on an oven unit;
- wall units and hoods overhead.

Elevations and 3D show each front, handle and appliance.

The materials have two more roles: **Plinth** and **Handles**, besides the fronts, the carcass (body), the worktop (top), metal and glass. A kitchen type made before this takes its body's material for the plinth and its metal's for the handles.

![A kitchen type's units: the base line and the wall line, with the engine's preview](shots/kitchen-units-types.webp)

![A kitchen of mixed units and appliances, in 3D](shots/kitchen-units-3d.webp)

**Interior → Kitchen units** makes a schedule of each kitchen's units, from the left, base line then wall line, with their width, height and front or appliance. In IFC the kitchen stays one IfcFurniture of its type, and the units travel with the type, so Lapin reads them back.

## A piece's properties

Select a piece to see:

- **Type**, its kind and size;
- **Rotation**;
- **Above the floor**, for a piece raised on a podium;
- **Length**, for a kitchen.

**Flip** gives a piece its other hand where it stands: the chaise longue on the other side, the fridge at the other end, the corner sofa's corner on the other side. Its front stays where it was. **Modify → Mirror** does the same.

Drag the grip in the middle of a piece to move it; drag the grip in front of it to turn its front towards the cursor.

![A sofa's properties](shots/furniture-properties.webp)

## Furniture types

**Interior → Furniture types** lists the project's types by group (seating, beds, storage, tables, kitchen), each with how many pieces use it. Choose a kind and **New type**, or **Duplicate** a type to change a copy. A type holds:

- its **name**, **width**, **depth** and **height** (a sofa's height is its back's, a bed's its headboard's, a kitchen's its worktop's);
- its kind's own parameters:
  - sofas and armchairs: seats, arms, arm width, seat height, a chaise longue on the left or right and its length, legs;
  - a corner sofa: the side its corner is on and that side's length;
  - a bed: the mattress top, headboard, footboard, pillows, a turned-down duvet;
  - a wardrobe: doors, hinged or sliding, drawers at the bottom, plinth;
  - a chest of drawers: rows and columns of drawers;
  - a bookcase: shelves, and doors at the bottom up to a height;
  - a sideboard: doors and drawers;
  - a table: rectangular, round or oval, on four legs or a pedestal;
  - a desk: a drawer pedestal on the left or right;
  - a kitchen: base unit width, plinth, worktop, where the sink and hob go, wall units, tall unit, and its own units (above);
- its **materials** by role: body, upholstery, top, fronts, linen, metal, glass, and a kitchen's plinth and handles. Type any material name; one the project does not have is made, and **Manage → Materials** sets its colour and hatches.

The preview beside the fields shows the type's plan symbol and its front as you change it. Every piece of a type follows any change to it.

![The Furniture types dialog with a sofa and its chaise longue](shots/furniture-types.webp)

## Schedules and IFC

**Furniture schedule** lists each type with its kind, size, how many pieces use it and the rooms they stand in, with the total. **Furniture by room** lists each room with what stands in it. Both are in the project browser under **Schedules**, and print and export like any schedule. A furniture type with a cost article is priced per piece in the estimate.

In IFC a piece is an **IfcFurniture**, with the predefined type of its kind (SOFA, CHAIR, BED, TABLE, DESK, SHELF, or USERDEFINED with Wardrobe, Kitchen…). Its **IfcFurnitureType** is attached by IfcRelDefinesByType, and its bodies carry their materials. Lapin reads its own furniture back as native, editable pieces. Furniture from other programs comes in as references.
