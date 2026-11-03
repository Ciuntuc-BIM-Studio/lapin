---
title: Stairs and ramps
order: 42
summary: Place a stair or a ramp between storeys or up to any height, set it up in its type, and make the few steps up to a porch or a door.
---

## Stairs

Choose **Architecture → Stair** (<kbd>ST</kbd>). Click the foot of the stair (the middle of its first riser), then a point in the direction it climbs. For an L or U stair, a third click left or right of the flight sets which way it turns. The stair climbs from its storey to the storey above. Lapin works out the number of risers, the riser and the tread from the height and the type's rules.

Select the stair to change it in the properties panel:

- **Stair type.** The shape, the rules, the construction and the railing come from the type, set up in **Architecture → Stair types**.
- **Width**, **Turn**, **Base offset** and **Top offset.**
- **Rise.** Leave it empty and the stair climbs to the storey above. A value gives the stair a rise of its own, whatever the storeys. Use it for a few steps between two levels on the same storey.
- **Fixed.** Risers, riser, tread and landing length, when you want them fixed. Leave them empty to let the rules decide.
- **Checks.** Each number against the type's rules, in green or red.

A stair asks the floor above for an opening over itself. Untick **Opening in the floor above** if the floor should stay whole.

## Stair shapes and ready-made types

In **Architecture → Stair types**, the **Shape** tab offers: **Straight**, **Straight with landing**, **L shape** and **U shape** (each turning on a landing or with **winders**; a U with no gap is a dog-leg stair), **Spiral** (round a pole, with its **Pole radius**), **Curved** (with its **Inner radius**) and **Split (bifurcated)**: a main flight up the middle to a landing, then two return flights on either side. For a spiral or curved stair, **Turns through (°)** sets how far round it goes; left empty, the risers and the tread decide, and given, the tread follows from it. Each stair can change it too in its properties.

The library adds ready-made types to the project: a dog-leg stair, a winder stair, a spiral stair, a curved stair, a cantilever stair and a split stair.

On the **Construction** tab, **Built as** chooses a waist slab, solid steps on the ground, **Open treads (cantilevered, floating)** (each step a plate fixed into the wall) or **Treads on stringers**.

The **Railing** has its **Design**: **Posts and a handrail**, **Balusters** (at most the **Baluster spacing** apart), **Glass panels**, **Cables** or a **Solid balustrade**; a square or round **Handrail**, its height, **In from the edge**, how far it **Runs on past the ends**, and its colour. In the properties panel, **Railing** puts it on other sides than the type for one stair.

![The spiral stair's type, its steps numbered](shots/stair-spiral.webp)

### Numbered steps

Under **In plan** on the **Shape** tab: **Number the steps** writes each step's number in a circle, from the first riser up; **Label** writes the risers, the riser and the tread, and the width by the foot; **"UP" at the foot of the arrow** marks where it starts. Plans and sheets show them.

![Numbered steps in plan, a split stair and a cantilever stair](shots/stair-numbers-plan.webp)

![A spiral, a split and a cantilever stair in 3D](shots/stair-shapes-3d.webp)

## Entrance steps (a porch)

The steps up to a front door have a rise of their own: from the ground, a few steps below the ground floor, up to ±0.00. Lapin has a ready-made stair type for them, **Entrance steps**: 2 m wide, three risers of 150 mm, solid concrete cast on the ground, with no railing.

![Entrance steps in front of a porch, in plan](shots/entrance-steps-plan.webp)

1. **The porch.** Draw the landing as a floor: choose **Architecture → Floor** (<kbd>SB</kbd>) and click its outline outside the wall, in front of the door. A floor's top is at its storey's level (±0.00 on the ground floor) and its type gives its thickness. To change the thickness, make a floor type for porches in **Architecture → Floor types**, for example 150 mm of concrete.
2. **The steps.** Choose **Architecture → Entrance steps**. Click the middle of the porch's edge, then click on the side the steps go out to. The steps come up to the porch edge and end at ±0.00. They start 450 mm lower: their **Base offset** is −450 mm.
3. **The height.** Select the steps. In **Rise**, type the real difference between the ground and the porch, for example 300. The top stays at the porch and the foot moves up: the base offset becomes −300 mm and the steps have two risers. **Width** makes them as wide as the porch, for example 2400.

![The entrance steps' properties: rise, base offset, levels](shots/entrance-steps-properties.webp)

**Levels** shows where the steps start and end, for example −0.45 → ±0.00. With one riser (a rise of 150 mm), the step is the porch edge itself and there is no tread to build. Give the steps two risers or more to have treads in front of the porch.

The type is added to the project the first time you use it. To change it, open **Architecture → Stair types** and pick **Entrance steps**:

- **Shape → Climbs → A height of its own.** The type's rise, climbed **Up to its storey's level, from below (an entrance)**; each stair can still change it with **Rise**.
- **Rules → Rule set → entrance steps.** Risers of 120–170 mm and treads of 300–400 mm, with a single riser allowed.
- **Construction → Built as.** **Solid, cast on the ground** fills the steps down to their base. **Waist slab under the steps** builds them like an indoor stair.

![The Entrance steps type in the stair configurator](shots/stair-types.webp)

To make any other stair type climb a height of its own, choose **Climbs → Up to → A height of its own** on its **Shape** tab (see the next section). To change a placed stair into entrance steps, pick **Entrance steps** as its **Stair type**: it moves down so that its top is at its storey's level.

![The house with its porch and entrance steps in 3D](shots/entrance-steps-3d.webp)

In IFC, the steps are an IfcStair of type STRAIGHT_RUN_STAIR. Its flight is an IfcStairFlight with its NumberOfRisers, NumberOfTreads, RiserHeight and TreadLength, and Pset_StairCommon carries the same numbers. The porch is an IfcSlab.

## A stair to any height

A stair does not have to go from one storey to the next. On a type's **Shape** tab, **Climbs → Up to** chooses **The storey above** or **A height of its own**, with its **Rise**, and **Starts** says where it starts:

- **Up to its storey's level, from below (an entrance).** It ends at the storey's level and starts the rise lower, as entrance steps do.
- **From its storey's level up (a terrace, a platform).** It starts at the storey's level and climbs the rise: 1.20 m up to a terrace or a loading dock, for example.

Each stair placed can climb another height: type it in **Rise** in its properties (empty: the type's). Climbing from the level, a new rise keeps the foot where it is; coming up from below, it keeps the top.

## Ramps

Choose **Architecture → Ramp** (<kbd>RU</kbd>). Click the middle of the ramp's lower end, then a point in the direction it climbs; for an L or a switchback, a third click left or right of the flight sets which way it turns. The first time, the project gets the library's **Ramp 6 %**: 1.20 m wide at 6 %, in flights of at most 9 m with landings of 1.50 m between them, a slab of reinforced concrete under a finish, handrails on both sides. It climbs to the storey above unless its type or its **Rise** says otherwise.

**Architecture → Entrance ramp** is placed like entrance steps: click the middle of the porch edge, then the side the ramp goes out to. It comes up 450 mm from the ground to ±0.00, solid on the ground, and **Rise** changes it as it does the steps.

Ramp types are made in the same configurator, **Architecture → Stair types**, with **New ramp type**, or from the library's **Switchback ramp** and **Entrance ramp**:

- **Shape.** **Straight**, **Straight, with landings**, **L shape** or **Switchback (back and forth)** with its **Gap between flights**; the **Width**, the **Slope** in per cent, the **Longest flight** (longer, the ramp is split into flights of equal length with landings between them) and the **Landing length**. The run in plan is the height over the slope: 3 m at 6 % is 50 m. **Climbs** works as for stairs.
- **Rules.** **Steepest slope** (8 % by default), **Minimum width** and **Headroom**. The checks show in red a slope steeper than the rules allow, a flight longer than the longest, and a landing shorter than 1.50 m or the ramp's width.
- **Construction.** **A sloping slab of reinforced concrete** of its **Slab thickness**, or **Solid, cast on the ground**; the finish and the railing as for stairs.

![The switchback ramp in the configurator](shots/ramp-types.webp)

Select a ramp to see its type, **Width**, **Turn**, **Base offset** and **Rise**, a **Slope** and a **Landing length** of its own (empty: the type's), its flights' lengths and its checks. Plans draw its flights and landings, the arrow up, and the slope in per cent along each flight. A ramp that climbs to the storey above asks that floor for an opening, as a stair does.

![The switchback ramp and the entrance ramp in plan, the slope along each flight](shots/ramps-plan.webp)

![The ramps in 3D](shots/ramps-3d.webp)

In IFC a ramp is an IfcRamp (STRAIGHT_RUN_RAMP, TWO_STRAIGHT_RUN_RAMP, QUARTER_TURN_RAMP or HALF_TURN_RAMP) typed by an IfcRampType, made of IfcRampFlights, landings (IfcSlab LANDING) and IfcRailings.

## Railings

**Architecture → Railing** (<kbd>RL</kbd>) draws a railing of its own along a balcony's edge, a terrace or a gallery: click its points, Enter finishes. It takes the stair railings' designs (posts and a handrail, balusters, glass, cables or a solid panel), its height (1 m by default), base offset, handrail profile and sizes and post spacing in its properties; its points are dragged by their grips (Ctrl/⌘-click an edge's middle adds one). In IFC it is an IfcRailing (GUARDRAIL).
