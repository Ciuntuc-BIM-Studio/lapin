---
title: Furniture
order: 47
summary: Sofas, corner sofas, armchairs, chairs, beds, wardrobes, chests, bookcases, sideboards, tables, desks and straight kitchens, from parametric types, placed against walls with a click.
---

## Placing furniture

The **Furniture** tab has a tool for each kind of piece: **Sofa** (<kbd>SO</kbd>), **Corner sofa**, **Armchair** (<kbd>AC</kbd>), **Chair**, **Bed** (<kbd>BD</kbd>), **Bedside table**, **Wardrobe** (<kbd>WD</kbd>), **Chest of drawers**, **Bookcase**, **Sideboard**, **Table** (<kbd>TB</kbd>), **Coffee table** and **Desk**. Choose one and click in the plan.

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

**Furniture → Furniture types** lists the project's types by group (seating, beds, storage, tables, kitchen), each with how many pieces use it. Choose a kind and **New type**, or **Duplicate** a type to change a copy. A type holds:

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
  - a kitchen: base unit width, plinth, worktop, where the sink and hob go, wall units, tall unit;
- its **materials** by role: body, upholstery, top, fronts, linen, metal, glass. Type any material name; one the project does not have is made, and **Manage → Materials** sets its colour and hatches.

The preview beside the fields shows the type's plan symbol and its front as you change it. Every piece of a type follows any change to it.

![The Furniture types dialog with a sofa and its chaise longue](shots/furniture-types.webp)

## Schedules and IFC

**Furniture schedule** lists each type with its kind, size, how many pieces use it and the rooms they stand in, with the total. **Furniture by room** lists each room with what stands in it. Both are in the project browser under **Schedules**, and print and export like any schedule. A furniture type with a cost article is priced per piece in the estimate.

In IFC a piece is an **IfcFurniture**, with the predefined type of its kind (SOFA, CHAIR, BED, TABLE, DESK, SHELF, or USERDEFINED with Wardrobe, Kitchen…). Its **IfcFurnitureType** is attached by IfcRelDefinesByType, and its bodies carry their materials. Lapin reads its own furniture back as native, editable pieces. Furniture from other programs comes in as references.
