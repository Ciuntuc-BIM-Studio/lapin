---
title: Steel
order: 56
summary: Steel members with configurable ends — extended or set back, cut by a plane, mitred, fitted to another member, coped — made from a drafted outline too; hollow and cold-formed sections.
---

## The Steel tab

![The Steel tab](shots/ribbon-steel.webp)

- **Member** (SM): a steel member along two points, on the storey's top with its section's top on the axis, as a beam. Its type is chosen in Properties before the first click, or from the **Catalog** tab, where each steel type is drawn by its section.
- **Convert to member**: the selected drafted outlines made members (below).
- **Mitre** and **Fit**: two members joined at their ends (below).
- **Steel types**: the project's member types, and new ones.

A member is an IfcMember: its axis runs anywhere in space (a brace, a rafter, a post), its section comes from its type, it can be turned about its axis (**Rotation**) and its axis can run through the section's centre, top or bottom (**Insertion point**). The structure's beams and columns of a steel profile take the same ends.

## Ends

Select a member, or a straight beam or column of a steel profile: its **Steel** section in Properties shows its two ends, **Start** and **End**. Each end is a set of rules; the body follows them.

![A member's ends in Properties](shots/steel-ends.webp)

- **Extension**: how far the end reaches past its point; negative, it is set back (a fitting gap).
- **Cut**: **Square to the axis**; **By a plane**, turned from square **In plan** (about the section's up) and **In elevation** (about its side), in degrees; **Mitre** with another member, on the plane halving the corner between them; **Fit to a member**, on the face of the other member it comes to (a column's flange, or its web's side when the column is turned).
- **Cope**: the flanges notched at the **Top** and/or the **Bottom**, its **Cope length** back from the end and its **Cope depth** from the section's edge, as for a beam framing into a girder.

A mitre and a fit name the other member: move it, and the end follows; delete it, and the end is square again and says so. **Mitre** on the tab mitres the two members selected at the corner where they meet; **Fit** fits the first selected to the second.

## From a sketch to a member

Draw the member's outline as a closed polyline or a rectangle (**Draft**), then **Steel → Convert to member** with one or more outlines selected. In one step each outline is read and replaced by a member:

![Two outlines sketched on the plan](shots/steel-sketch.webp)

- its **two long parallel edges** (within half a degree) give the axis, their midline from end to end, and the **width** between them;
- the **edge across each end** gives that end's cut: square, or a plane at the edge's slant.

On a **plan** the outline is the member seen from above: its width is the flanges', and a profile of that width is found among the project's member types and the catalogue (else the type chosen for Member); the member lies on the storey's top. On a **section** or an **elevation** the outline is the member seen from the side: its width is the section's depth, a slanted end is a cut in elevation, and a square **notch** at an end is a **cope** at the top or the bottom; the member lies where it is drawn. A notch drawn on a plan is refused (a cope is seen from the side). An outline that is not a band (a triangle, an L, edges that are not parallel) is left as it is, and Lapin says so.

## Steel types

**Steel → Steel types** lists the project's member types with their weight per metre, and makes new ones: a profile from the catalogue (IPE, HEA, HEB, UPN, L, **RHS**, **CHS** and the generic cold-formed **C**, **Z** and **Σ**) or a hollow or cold-formed section **of its own dimensions** (depth, width, thickness, lips). The new type can be a member type, or a **beam type** or **column type** of the structure, so the Beam and Column tools place steel too. A click on a type in the list makes it the one Member places.

![The Steel types dialog](shots/steel-types.webp)

Hollow sections are open inside, in the 3D view, the sections and the quantities. The cold-formed sections are generic shapes with dimensions, their bends sharp, without a manufacturer's catalogue.

![Members in 3D](shots/steel-3d.webp)

## IFC and quantities

A member exports as an **IfcMember** with its **IfcMemberType**. Its body is written as the other programs write steel (Tekla, Advance Steel, Revit): its section **extruded** (IfcExtrudedAreaSolid), **clipped** by its ends' planes (IfcBooleanClippingResult with IfcHalfSpaceSolid), and each cope an **IfcOpeningElement** voiding it. A steel beam or column with ends is written the same way. Each member's quantities give its length between its cuts, its section's area, its volume and its net weight (7850 kg/m³).
