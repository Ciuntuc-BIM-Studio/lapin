# National packs (Sweden first): plan

Asked on 25.11.2026 in forum topic 261 (Slawomir: automating the design of houses to Swedish building standards; the same topic asked for Swedish, now added, and for a SpaceMouse, see docs/plan-3d-mouse.md).

**Order: 5 of 6** (angle lock, structural usage, level datums, leaning walls, national packs, 3D mouse). Stages N0 and N1 first; N2–N7 follow their own order.
**Status: proposed, not started.**

## What exists today

- **Rule packs.** `engine/src/Platform/Lapin.Platform/RulePacks.cs`: modules register an `IRuleSection` (key, default, `Check`) and an `IRuleCheck`. Four sections: stairs (`Architecture/Stairs/StairRuleSection.cs`), rebar, roofs, bolts. The built-in pack is `builtin:rules:ro`, "Romania (indicative)", read only. Users duplicate it, edit it, export it as `.lapinrules` (`lapin-rules` v1) and import it again. Checks list findings and never refuse. Guide: `docs/user/rules.md`.
- **Store.** `docs/plan-extensions.md` §3.1: kind `rules`, `"indicative": true` required, packages Ed25519-signed; submissions by pull request to `lapin-extensions` (`manifest.ts`/`packs.ts`, `tools/lib.mjs`).
- **Loads.** `Lapin.Analysis/CombinationSettings.cs` has two factor sets written in code, `en` and `cr0`: a third country means a code change. The imposed qk can be overridden per project; snow s0,k and wind qb are typed by hand, no maps shipped (`docs/user/loads.md`).
- `Thermal.cs` works out U-values from indicative λ; the standard psets carry `FireRating`, `ThermalTransmittance`, `Compartmentation`. Notation domains with country presets exist (`Notations.cs`, decision 861) but the presets are written in code. `StandardsLibrary` reads `.lapinstandards`. A Swedish interface exists (`ui/src/i18n/sv.ts`).

## Options for the mechanism

- **A (chosen).** Add sections to `.lapinrules` (loads, climate, accessibility, fire, energy) plus metadata (`country`, `edition`, `sources`, `language`): reuses the dialog, export, import, store kind, signing and checks.
- B. A new bundle kind (zip of rules, standards, types, notations): richer, but a new format, installer and CI rules. Later only if A proves too narrow.
- C. A script per country: code, not reviewable data, not editable in the dialog. Rejected.

## Stages

| Stage | Content | Effort |
|---|---|---|
| N0 | Docs only: a guide section "A pack for your country" (duplicate, edit, export, submit to the store as kind `rules`, licence CC0 or CC-BY-4.0, `languages: ["sv"]`). A Swedish user can already do this today for stairs, ramps, rebar, roofs and bolts | S |
| N1 | Loads as data (engine, Opus): a `loads` section (factor sets γG,sup, γG,inf, γQ, the expression, ξ, ψ per category, ψ by snow zone; the imposed qk table; a **γd per safety class**, new in the combinations, needed because EKS puts safety classes 1–3 on the load side). EN and CR 0 become built-in data with the same output (a golden test), so no upgrade; `CombinationSettings` reads the factor set from the pack in use | M |
| N2 | Climate: a `climate` section with a list of regions (for Sweden, the kommun) giving sk and vb,0; Loads → Snow and wind gains "Pick from the pack's list"; typing stays | M |
| N3 | Accessibility: least door clear width (from JoineryData), least corridor width, steepest ramp (the stair set `ramp`), turning circle; checks on doors, ramps, rooms | M |
| N4 | Fire: the offered `FireRating` classes as a pick list (EI/REI 30/60…) and the building class (Br0–Br3); check: an element with `Compartmentation` = true whose rating is below the requirement | M |
| N5 | Energy: U-limits per element category checked against `Thermal.Of` (envelope Umedel later, L) | M |
| N6 | Notation presets from a pack (data, not only code): for Sweden the decimal comma, a level sign, the rebar style `Ø12 s200`; expose `rules.section` and the check in the command catalogue for Python and graphs | S |
| N7 | The Swedish pack itself, to `lapin-extensions` by pull request: written by the Swedish user, or prefilled by us only from Boverket's free texts; Claude reviews, the user approves | S |

A first Swedish pack holds: stair and ramp limits from BBR, door and corridor widths, safety-class γd and ψ from EKS, sk and vb,0 per kommun, the fire classes offered, U-limits per element category. **Out of scope:** primary energy (EP), daylight, acoustics, fire design, Swedish capacity-side factors (γM), seismic, permit forms, map polygons.

## Legal notes (to confirm with the user's own advice)

- Swedish authority regulations (Boverket's BBR and EKS) are generally not protected by copyright (Upphovsrättslagen §9).
- The SS-EN Eurocodes and SS 91 42 21 (accessibility) are SIS-copyrighted: never copy their text or tables.
- Single values typed by users are facts; bulk-extracting a whole table can infringe the EU database right, so packs stay small and their sources are named.
- Every pack is marked indicative ("check against the edition in force"); norms and prices are the user's own (CLAUDE.md).
- Boverket is moving from BBR/EKS to new regulations with a transition period: a pack names its edition.

## Tests first

Each section's `Check` refuses bad values and fills missing ones from the defaults; golden `CombinationTests` (EN and CR 0 as data give exactly today's results); a Swedish example (6.10a/b with γd); the checks' findings; `.lapinrules` round trip; manifest validation identical in `manifest.ts` and `lib.mjs`; vitest for the new tabs.

## Guide and i18n

`docs/user/rules.md` and `loads.md` (EN and RO); a scene in `scripts/user-docs/scenes.mjs` for every new tab; strings in all languages of `ui/src/i18n`.

## The user's answers (25.11.2026)

The user accepted every recommendation:

1. Extend `.lapinrules` (not a new bundle kind). **Yes.**
2. The Swedish user enters the Swedish values; we provide the structure and at most values from Boverket's free texts, marked indicative. **Yes.**
3. Add the γd safety class to the combinations now, in N1. **Yes.**
4. Zones by a kommun list now; map polygons later if at all (open boundary data, its licence checked). **Yes.**
5. Energy: U-limits only; Umedel later; EP out of scope. **Yes.**
