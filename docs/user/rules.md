---
title: Rules
order: 53
summary: The limits and choices Lapin's tools propose from, kept as data you can read and change — stair limits, reinforcement steels, diameters and hooks, roofs' least pitches, bolt grades, load factors and imposed loads; a pack for your country; checked against the project, never forced on it.
---

Lapin's tools work from numbers: the most a riser may be, the steels a bar can be made of, the least pitch of a roof covering. **Manage → Rules…** shows those numbers as tables, so you can see them, change them to your country's norm or your office's habit, and keep them with the project.

![Manage → Rules](shots/rules-dialog.webp)

> Every value in Rules is **indicative**. The ones Lapin ships are written by Lapin, not taken from a norm book; the ones you set are yours. Check them against the norm in force before you rely on them.

## Nothing is refused

A rule is a proposal. The Stair tool aims at the riser the rules name, a new bar takes the steel they list, a roof under its covering's least pitch is warned of. You can always draw what you like: the tools propose and warn, they never refuse.

## The packs

A **pack** is a complete set of rules, with a name.

- **Romania (indicative)** is Lapin's own pack, marked *Lapin, indicative*. It holds the values the tools have always used (STAS 2965 and NP 063 for stairs, SR EN 1992-1-1 for bars). It is read only: **Duplicate** makes a copy in the project to change.
- **This project** lists the packs you made. They travel in the project file.

Choose a pack in the list on the left to see it. **Use in this project** makes it the one the tools read (*In use* marks it). **Duplicate** copies the pack shown, **Delete** removes one of yours (the project goes back to Lapin's if it was in use), and the **Name** and **Description** of your own packs can be edited, with what the pack says of itself: its **Country**, the **Edition of the rules** its values were read from (rules change: a pack names the one it follows), its **Language** (a two-letter code such as sv) and its **Sources, one a line**. They travel with the pack in its file.

**Export…** writes the pack as a `.lapinrules` file and **Import…** reads one back as a pack of the project: that is how a firm's rules, or a colleague's, travel between projects.

## What a pack holds

A tab for each part of the pack the project's modules read:

- **Stairs.** One column for each **Stair set** (residential, public, entrance and ramp, and the sets you add with **New set's name**): the riser, the tread and the step (2 risers + a tread), each with its least, aimed-at and most value, the least width, the headroom, the least and most risers in a flight and the steepest slope. A least value above its most is refused, saying so. A stair type takes its set in its **Rules** tab (see [Stairs](stairs.md)).
- **Reinforcement.** The **Steels offered** and the **Steel of a new bar**, the **Diameters offered**, the **Mandrel** (the bend diameter, a factor of Ø by the bar's diameter) and the **Hooks** (the straight tail, a factor of Ø by the hook's angle), and the **Least hook tail**. The Steel and Diameter lists of the bar tools and of Properties are these; a new bar takes the steel, the hook tails and, when the mandrel differs from Lapin's, the bend radius from here (see [Simple rebar](rebar.md)).
- **Roofs.** The **Least pitch** of each **Covering**: a roof under it is warned of, as it may not shed water.
- **Loads.** The **sets of factors** the load combinations read (see [Loads](loads.md)): Lapin's `en` (EN 1990 Annex A1, recommended values) and `cr0` (CR 0-2012), and the sets you add with **New set's name** (each started from the one shown). For the set chosen: its **Label**, γG,sup, γG,inf, γQ, ξ and the **Expression** (6.10, or 6.10a and 6.10b) a project starts with; **γd by safety class**, for rules that put the safety class on the load side (γd multiplies the unfavourable actions of the ultimate combinations; no class, γd 1); **ψ of snow by the site's s0,k**, for rules that give ψ of snow by the snow load (no zone: the table's ψ; with zones, the site's s0,k takes the last zone it reaches); **ψ by category**; and the **Imposed loads, qk** of each category of use. A project chooses its set and its safety class in Loads → Combinations. Lapin's two sets hold exactly the values the combinations have always used, so a project gives the same combinations with Lapin's pack as with a copy of it.

![The Loads tab of a pack](shots/rules-loads.webp)

- **Climate.** A list of regions, for Sweden the kommun, for another country its county or locality: **The regions are** names them (Kommun, County), and each row gives a region's **sk** (the characteristic ground snow load, kN/m²) and **vb,0** (the basic wind velocity, m/s; qb = ½ · 1.25 kg/m³ · vb,0² is shown beside it) or, where your rules give the pressure, its **qb** (kN/m²). Add a region with **New region's name**, or **Paste a list…** of lines copied from a spreadsheet (name, sk, vb,0, the columns split by tabs or semicolons; a header line is left out, a region already listed takes the pasted values). A project picks its region in Loads → Snow and wind (see [Loads](loads.md)). Lapin's own pack lists no regions.

![The Climate tab of a pack](shots/rules-climate.webp)

- **Accessibility.** The least clear width of a door (inside its frame, as its tags write `{clearWidth}`), the least width of a corridor (a room of the functions **Corridors are rooms of the functions** names, Circulation by default, at its narrowest, to its finishes), the **Turning circle, diameter** a room of the functions named must hold (living rooms, bedrooms, kitchens, baths, WCs and corridors by default), and whether **Ramps** are checked against the stairs' set ramp (its steepest slope and least width, so the limit is written once). An empty value is not checked: Lapin's own pack checks none of these, so a project's check stays as it was until a pack's values say otherwise.

![The Accessibility tab of a pack](shots/rules-accessibility.webp)

- **Bolts.** The bolt grades a steel connection can be given (Lapin's 4.6, 5.6, 8.8 and 10.9; yours, such as A325 or A490, added to the pack). The tab lists the grades as a column of names: change a name in place, **Take this grade off the list** with the × (one grade always stays), or type a **New grade** and press Enter or **Add the grade**. A steel connection's grade list, in its properties and in the hall's connections, is this list (a grade a connection already holds stays offered for it). Lapin's own pack is read only: Duplicate it to change the grades.

## Check the project

**Check the project** lists what the project has that differs from the rules in use, grouped by module: a stair type whose riser is not the pack's, a bar of a steel the pack does not offer, a roof under its least pitch, a steel connection with a bolt grade the pack lacks, and, when the pack's Accessibility says so, a door narrower than its least clear width, a ramp steeper (or narrower) than the ramp set, a corridor narrower than its least width, a room that cannot hold the turning circle. Click a line to select the element it speaks of. The check changes nothing.

## Apply to stair types

A stair type keeps the rules it was made with, so a change to the pack does not move a stair that is already drawn. **Apply to stair types** copies the rules in use into every stair type, each keeping its set, in one step of undo. Lapin says how many types changed, or that each already follows the rules.

![Check the project](shots/rules-check.webp)

## A pack for your country

Lapin ships one pack, Romania's, and its values are indicative. To work to another country's rules, make a pack of them:

1. **Duplicate** *Romania (indicative)* and name the copy after the country and the edition, such as *Sweden, EKS 11*. Fill in its **Country**, **Edition of the rules**, **Language** and **Sources, one a line** (the regulation, its year, a link).
2. Change the tabs to your rules: the stair, ramp and entrance sets, the steels, diameters and hooks, the roofs' least pitches, the bolt grades, the loads (a set of factors of your own with its γd by safety class and its ψ of snow by s0,k, the imposed loads), the climate (sk and vb,0 by kommun) and accessibility (door and corridor widths, the turning circle, ramps checked).
3. **Use in this project**, then choose the new set of factors in Loads → Combinations and the site's region in Loads → Snow and wind, and **Check the project**.

Type the values yourself, one by one, from the edition in force. Do not copy a norm's text or whole tables into a pack: some authorities' regulations are free to quote, but standards (EN and their national editions) are copyrighted, and a pack stays small, with its sources named. A pack is always marked indicative: its values are yours to check.

To share the pack, **Export…** it as a `.lapinrules` file for your colleagues, or offer it to every Lapin user through the [extension store](extensions.md): its manifest is of kind `rules`, says `"indicative": true`, gives a licence such as `CC0-1.0` or `CC-BY-4.0` and the languages of its texts (`"languages": ["sv"]`), and comes by a pull request on the catalogue. It is reviewed before it is listed.
