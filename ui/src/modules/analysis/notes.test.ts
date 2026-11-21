/**
 * The analysis' notes and its cases' default names come from the engine in English (docs/plan-fem.md §9.9): every language
 * has each of them, by phrase or by pattern, keeping the numbers, the codes and the names they carry.
 */
import { describe, expect, it } from 'vitest';
import { loadAllLanguages, translate, type Lang } from '../../i18n';

await loadAllLanguages();

/** One of each note as the engine writes it (Lapin.Analysis: DerivedLoads, Combinations, ClimateLoads, LoadMapping, AnalysisRun). */
export const ENGINE_NOTES = [
  '3 members have no density or section, their self weight left out: Beam 1, Beam 2, Beam 3.',
  '1 member has no density or section, their self weight left out: Beam 1.',
  '2 materials have no density, left out of the floors\' finishes and the partitions: Screed (3 elements), Brick (1 element).',
  '4 rooms of category B are in no case: analysis.addMissingCases makes Q-B.',
  'No case carries the self weight: give one permanent case selfWeight.',
  'The floors\' finishes are in no case: give one case derives finishes.',
  'The partitions are in no case: give one case derives partitions.',
  'Imposed loads indicative: EN 1991-1-1 Tables 6.2 and 6.10, recommended values; set the national annex\'s with analysis.setImposedLoad.',
  '„W” holds nothing: left out of the combinations.',
  '„A1” is an accidental action: the accidental combination is not made yet.',
  '„Q1” has no category: its ψ are category A\'s (0.7, 0.5, 0.3).',
  'The arrangements of the snow and the directions of the wind are alternatives: each combination takes one of them (87 combinations).',
  'Indicative: CR 0-2012 (γG 1.35, γQ 1.5 by its Table 7.2, ψ by its Table 7.1: snow 0.7, 0.5, 0.4; wind 0.7, 0.2, 0).',
  'Indicative: EN 1990 Annex A1, recommended values, with the project\'s own factors: γG,sup 1.4, γG,inf 1, γQ 1.5.',
  'Indicative: CR 0-2012, with the project\'s own factors: γG,sup 1.35, γG,inf 1, γQ 1.6.',
  'Expressions 6.10a and 6.10b (EN 1990), ξ = 0.85: the project\'s choice; CR 0-2012 itself has only its expression 6.9 (EN\'s 6.10).',
  'Expressions 6.10a and 6.10b (EN 1990), ξ = 0.85, instead of 6.10.',
  'Not made yet: snow with the imposed load on roofs (EN 1991-1-1 3.3.2); the seismic combination comes with the seismic parameters (its own envelope).',
  '„Maintenance” names a load case that is gone: left out of the results until its factors are set again.',
  '„Maintenance” names 2 load cases that are gone: left out of the results until its factors are set again.',
  '„Quake” is a seismic combination: its factors ± AEd, in the seismic and ULS envelopes once the seismic parameters are given.',
  '„Blast” is an accidental combination: checked as an ultimate one, with the persistent situation\'s γM (on the safe side).',
  '1 combination switched off: left out of the results, the envelopes, the checks and the export.',
  '4 combinations switched off: left out of the results, the envelopes, the checks and the export.',
  'Indicative: EN 1990 Annex A1, the recommended values (γG 1.35, γQ 1.5, ψ by Table A1.1, snow for sites up to 1000 m).',
  'Not made yet: expressions 6.10a and 6.10b, snow with the imposed load on roofs (EN 1991-1-1 3.3.2), CR 0-2012\'s values; the seismic combination comes with the seismic parameters (its own envelope).',
  'No bars and no shells: the slabs\' loads go straight into the load-bearing walls under them, their reactions.',
  'No bars, no shells and no load-bearing walls: nothing carries the loads.',
  'No shells: the bars carry everything; the slabs only carry loads to them (the 45° rule), the load-bearing walls are line supports (every node of theirs held in translation, free to turn).',
  '4 beam pieces under a slab analysed as a shell: on the slab\'s nodes, their own axis below by rigid arms (the slab and the beam work as a T).',
  '1 beam piece under a slab analysed as a shell: on the slab\'s nodes, its own axis below by rigid arms (the slab and the beam work as a T).',
  'The options leave the slabs out of the shells: their loads go to what holds them by the 45° rule (as in S1).',
  'The options leave the walls out of the shells: the load-bearing walls are line supports (as in S1).',
  'Slabs not of concrete (Floor C24, Deck 2): not shells; their loads go to what holds them by the 45° rule.',
  'Walls not of concrete (Wall 3): line supports.',
  'Walls that say nothing of their load taken as load-bearing (masonry or concrete, a core of 10 cm or more): Wall 1, Brick 25. Set Pset_WallCommon.LoadBearing to false on a partition to leave it out.',
  'Walls that say nothing of their load, thinner than 10 cm, taken as partitions (their weight on the floors): Brick 7.5. Set Pset_WallCommon.LoadBearing to true on one that carries.',
  'No E for Floor 20: not shells (as in S1).',
  '2 shell pieces with no area, not plane or not convex: left out of the mesh (3kQ#2, 3kQ#5).',
  'The shells could not be meshed (A piece turning against its normal.): the slabs and walls are taken as in S1.',
  '3 mesh elements with no area left out.',
  '36 mesh nodes along walls that are line supports held as the walls\' own nodes are.',
  'Shell walls with nothing modelled under their foot (Wall 4): their foot held as a line support (translations).',
  'Shells (S2): 96 elements (90 MITC4 quadrilaterals, 6 MITC3 triangles) on 1 surface, mesh 0.5 m (at least 4 elements across a narrow piece); the slabs\' loads stay on them, the 45° rule only where a slab is not a shell.',
  'Shells (S2): 336 elements (336 MITC4 quadrilaterals, 0 MITC3 triangles) on 5 surfaces, mesh 0.25 m (at least 4 elements across a narrow piece); the slabs\' loads stay on them, the 45° rule only where a slab is not a shell.',
  'Bars as Timoshenko beams, their shear areas from the section (EN 1993-1-1 §6.2.6(3) for steel, 5/6·A for a rectangle).',
  '4 beam ends of another depth meet a column at its joint: their own level kept, a rigid arm to the joint.',
  '310 bars with eccentric ends, joined to their nodes by rigid arms.',
  '242 bar end pieces pinned by default (the members a connection joins); analysis.setReleases changes them.',
  '31 timber roof member pieces pinned at their ends about their strong axis by default, held across it (S1 has no deck to hold the roof sideways); analysis.setReleases changes them.',
  '2 nodes free to turn without moving anything (only pinned ends meet there): held by a spring that takes no force.',
  '„S”: 140 kN of 1 load find nothing under them (no bar, slab or wall within reach) and are left out: Area load.',
  '„W+X/s”: 12.35 kN of the wind on the façades goes straight to the ground (the strip at their foot), outside the bars\' sums.',
  '„G1”: the load-bearing walls\' own weight (215.6 kN) goes straight to what holds them in S1, outside the bars\' sums.',
  '3 stretches of line loads on a slab with no bar under them: shared by the slab piece\'s corners as the point loads are, integrated along them.',
  '1 point load on a slab with no bar under it: shared by the slab piece\'s corners (barycentric, bilinear on a quadrilateral), statically consistent.',
  'Area loads on a framed roof: each rafter (or truss top chord; purlins where there are none) takes the load over its tributary width, half way to its neighbours (an edge one to the roof\'s edge); snow per horizontal projection (q·w·cos α along a sloping bar), other loads per true area.',
  '„Q-A”: the reactions do not balance the loads (off by 2.5e-4 of them).',
  'No snow on the roofs: the site\'s ground snow load s0,k is not set (Loads → Snow and wind, analysis.setClimate).',
  '„S+X” is the undrifted snow here (no roof plane faces the wind towards +X): left out.',
  'Snow on 2 roof planes and uncovered floor pieces: s = γIs·μ1·Ce·Ct·s0,k = 1.15 × μ1 × 1.2 × 1 × 2 kN/m² per m² of plan, μ1 0.8 to 30°, 0.8(60 − α)/30 to 60°, 0 past; drifted with 0.5 μ1 on the planes facing the wind; indicative: CR 1-1-3/2012 and CR 1-1-4/2012 (EN 1991-1-3, EN 1991-1-4, their recommended values); the site\'s s0,k and qb are yours.',
  'Not derived yet: snow drifts at parapets and changes of level (EN 1991-1-3 §6.2), snow overhanging the eaves (§6.3), the exceptional snow loads.',
  'No wind: the site\'s reference wind pressure qb is not set (Loads → Snow and wind, analysis.setClimate).',
  'No wind: the model has no walls or columns to make a building\'s outline of.',
  '„W+Y/p” is „W+Y/s” here (no roof whose pressure coefficients differ): left out.',
  'Wind: qp(ze) = ce(ze)·qb, ce from terrain category II (z0 = 0.05 m), qb = 0.5 kN/m², γIw = 1; the building 7.8 m high from its lowest node, its outline the hull of its walls and columns; cpe,10 by EN 1991-1-4 Tables 7.1–7.4; indicative: CR 1-1-3/2012 and CR 1-1-4/2012 (EN 1991-1-3, EN 1991-1-4, their recommended values); the site\'s s0,k and qb are yours.',
  'Wind on the façades goes to the beams, rails and floor edges in their plane by vertical strips (half way to the next), its lowest strip to the ground; wind on a roof to its rafters or purlins as the snow does, per area of the roof.',
  'No wind derived on Roof 1, Roof 2: hipped roofs and roofs of more than two planes (EN 1991-1-4 Table 7.5) are not made yet.',
  'Not derived yet: the internal pressure cpi, friction, canopies and free-standing walls, the dynamic factor of tall or slender buildings.',
  '2 bars are refused, the analysis does not start: Beam 1: its type names no material; Beam 2: its material „Oak” has no E.',
  'Timber (C24): G = E0,mean / 16, as EN 338\'s tables give it (no Poisson ratio for an orthotropic material).',
  '182 bars of an unsymmetric section (a T, a channel, an angle): their axis through the centroid, joined to the box centre\'s nodes by rigid arms.',
  'No shear area for My outline: Euler–Bernoulli there (shear deformation left out).',
  'No Poisson ratio for Glass: ν = 0.3 assumed for G.',
  '12 rods or ties work axially: pinned at their own ends, their weight carried to them (no bending under it); analysisAxial on an element changes it.',
  '1 rod or tie works axially: pinned at its own ends, its weight carried to them (no bending under it); analysisAxial on an element changes it.',
  'A mechanism at node N12 (5, 4, 2.85), uz: nothing holds it (Beam 1, Beam 2).',
  'A mechanism at node N3 (0, 0, 3), rx: nothing holds it (no bar reaches it).',
  // Seismic and modal analysis (S3: Lapin.Analysis/Seismic, Solver/AnalysisRun, Solver/ShellProblem)
  'Masonry walls as shells (Wall 1: Clay unit, E = 4.47 GPa, Wall 2: AAC, E = 2.35 GPa): linear elastic, isotropic (E = KE·fk, G = 0.4 E: EN 1996-1-1 §3.7, as CR 6-2013 takes it; indicative unless the material gives its own Lapin_Masonry values), no tension cut-off (masonry carries no tension: not modelled in S3).',
  'Say what to set: code, ag, tc, ground, spectrum, importance, q, damping, modal, directions, torsion, modes, cracked, diaphragm, drift, snowPsi2.',
  '„agg” is not a seismic parameter: code, ag, tc, ground, spectrum, importance, q, damping, modal, directions, torsion, modes, cracked, diaphragm, drift, snowPsi2.',
  'The behaviour factor q is a number from 1 to 8.',
  'The design ground acceleration ag (a fraction of g) is a number from 0.01 to 1.5.',
  'The ground type is one of A, B, C, D, E.',
  'The seismic code is one of P100-1/2013, EN 1998-1.',
  'cracked is {concrete, masonry}.',
  'Cracked stiffness: concrete × 0.5, masonry × 0.5 (EN 1998-1 4.3.1(7), P100-1/2013 ch. 8: half the uncracked stiffness unless worked out); steel and timber as they are.',
  '3 nodes with an upward net gravity load: no mass there.',
  '1 node with an upward net gravity load: no mass there.',
  'The subspace iteration did not converge in 40 steps: the highest modes are approximate.',
  '12 modes by subspace iteration on the sparse Cholesky (7 steps); effective masses 91.2 % along X, 90.4 % along Y of the mass on the free nodes (310.52 t).',
  '1 mode by subspace iteration on the sparse Cholesky (5 steps); effective masses 61.2 % along X, 0.4 % along Y of the mass on the free nodes (12.5 t).',
  'The modes\' limit (60) stops before 90 % of the mass along both directions (P100-1/2013 §4.5.3.3.1, EN 1998-1 §4.3.3.3.1): raise it, or check with √n modes as the codes allow.',
  '12.5 t of the mass is on the supports (it does not move).',
  'Modes combined by CQC (Der Kiureghian 1981, ξ = 0.05); 2 pairs of neighbouring modes within 10 % of each other\'s period.',
  'Modes combined by CQC (Der Kiureghian 1981, ξ = 0.05); 1 pair of neighbouring modes within 10 % of each other\'s period.',
  'Modes combined by SRSS; 0 pairs of neighbouring modes within 10 % of each other\'s period.',
  'Modes combined by SRSS; 2 pairs of neighbouring modes within 10 % of each other\'s period: CQC would be right for them (EN 1998-1 4.3.3.3.2).',
  'Seismic combination: G1 + 0.3 Q-A ± AEd (CR 0-2012, EN 1990 6.4.3.4); the static results of the gravity loads with the uncracked stiffness, the seismic action with the cracked.',
  'Indicative: P100-1/2013; ag = 0.2 g, the site\'s values yours.',
  'Indicative: EN 1998-1, the recommended values; ag = 0.15 g, the site\'s values yours.',
  'Rigid diaphragm on 2 levels without a shell floor (a penalty tying each level\'s nodes in its plane).',
  'Rigid diaphragm on 1 level without a shell floor (a penalty tying each level\'s nodes in its plane).',
  '1 level has a shell floor: no diaphragm imposed there (the slab gives it).',
  '3 levels have a shell floor: no diaphragm imposed there (the slab gives it).',
  'Accidental torsion: each storey\'s seismic force times ±0.05 of its width across the direction (P100-1/2013 §4.5.2.2, EN 1998-1 §4.3.2), as a static torque on its masses; its effects added in absolute value.',
  'There is no mode 99: 1 to 12.',
  'Give the site\'s ag (and its TC, or its ground type) to turn the seismic analysis on: the modes, the response spectrum, the storeys\' shears and drifts, and the seismic combination.',
  'The seismic values are indicative: the site\'s ag and TC, the behaviour factor and the code\'s values are yours to check.',
  'of the mass along X and Y: less than 90 %. Raise the modes\' limit.',
  'No seismic parameters: give the site\'s design ground acceleration ag (analysis.setSeismic), as the code\'s map gives it.',
  'The static analysis did not run: analysis.run says why.',
  'The modal model meets a mechanism: analysis.run says where.',
  'The modal model could not be assembled.',
  'No mass: the seismic combination\'s gravity loads are nothing.',
  'Masonry is linear elastic in S3: no tension cut-off, no cracking; the forces are those of the design spectrum (q).',
  'The two horizontal directions by the 30 % rule: E = max(Ex + 0.3 Ey, 0.3 Ex + Ey) (P100-1/2013 §4.5.3.6, EN 1998-1 4.3.3.5.1); no vertical component.',
  'The two horizontal directions by SRSS: E = √(Ex² + Ey²); no vertical component.',
  'The modal analysis did not run: analysis.modal says why.',
  'Give the seismic parameters first (analysis.setSeismic).',
  'The seismic results are asked through the analysis queries.',
  'The seismic analysis did not run: analysis.modal says why.',
  'An envelope is „uls”, „sls” or „seismic”.',
  'Indicative checks (docs/plan-fem.md §12): the Eurocodes\' recommended values (kept by the Romanian annexes where Lapin could read them) and CR 6-2013; the signed design remains the engineer\'s.',
  'There are no ULS combinations: nothing to check.',
  'The analysis did not run: analysis.run says why.',
  'Laterally restrained by the slab it carries: no lateral torsional buckling.',
  'No fy or no section properties: not checked.',
  'No fck (the material\'s name has no class such as C25/30): not checked.',
  'No strength class (C24, GL24h…) in the material\'s name: not checked.',
  'No cut through the wall\'s mesh: not checked.',
  'its section „Mine” gives no properties',
  'its material „Oak” has no E',
  'its type names no material',
  // Second order, buckling, support springs and one-way members (S5: Lapin.Analysis/Solver/AnalysisRun.Nonlinear, SupportSprings, Buckling, SupportSettings)
  'Select the footings, column bases or slabs on the ground.',
  'A slab on the ground takes Winkler\'s ks (winkler), not springs.',
  'A column base takes springs, not Winkler\'s ks: it has no footprint.',
  'The modulus of subgrade reaction ks is a number of kN/m³ above 0.',
  'A spring\'s stiffness is a number from 0 up (kN/m, kN·m/rad).',
  'Only a bar works one way.',
  'A buckling mode is 1 to 12.',
  'No buckling under that combination: no bar is in compression.',
  'Second order (P-Δ): each static combination solved with the bars\' geometric stiffness of their axial forces, iterated; the cases stay first order. Along a bar the moment keeps its first-order shape between its ends: its own buckling is its check\'s (EN 1993-1-1 6.3).',
  'The global imperfections (EN 1993-1-1 5.3.2) are not added yet: give them as loads (equivalent horizontal forces φ·NEd) where the design calls for them.',
  'The shells have no geometric stiffness: second order and buckling are the bars\' only.',
  'Second order amplifies the sway most in „ULS 3”: × 4.2.',
  'Each static combination solved on its own (one-way members, supports that lift): the cases stay linear, every member in, every support seated.',
  'Second order: unstable, the loads are past the critical ones (αcr < 1).',
  'A mechanism once 2 members went slack: nothing holds node N12 (uz).',
  'A mechanism once 2 members went slack and 3 supports lifted: nothing holds node N12 (uz).',
  'A mechanism at node N12 (uz).',
  'The one-way members and supports did not settle: they go back to states already tried.',
  'Not settled in 2 iterations.',
  '„ULS 3”: Second order: unstable, the loads are past the critical ones (αcr < 1). Left out of the envelopes and the checks.',
  '„ULS 3”: A mechanism once 2 members went slack and 3 supports lifted: nothing holds node N12 (uz). Left out of the envelopes and the checks.',
  '„ULS 3”: The one-way members and supports did not settle: they go back to states already tried. Left out of the envelopes and the checks.',
  '„ULS 3”: The one-way members and supports did not settle: they go back to states already tried. Its last iteration is taken.',
  '„ULS 3”: Not settled in 2 iterations. Its last iteration is taken.',
  'One-way members slack in 2 combinations: Brace 1, Brace 2.',
  'Supports lifted in 2 combinations: 3 nodes.',
  '2 supports on springs (Brace 1, Brace 2): 12 nodes; the springs\' forces are reactions.',
  '2 supports on springs (Brace 1, Brace 2): 12 nodes, 4 on Winkler\'s bed (kz = ks·A, the footprint\'s ks·I against turning; across and along a strip each node its length); the springs\' forces are reactions.',
  '2 slabs on the ground on Winkler\'s bed: 96 nodes, kz = ks × each node\'s share of the area; ux and uy held there (the ground\'s friction).',
  '2 support nodes in compression only: each combination solved again where one would pull, lifted (free).',
  'Winkler\'s ks on Brace 1, Brace 2: no footprint to take it on (a column base); give its springs instead.',
  '„ULS 3”: a slab takes Winkler\'s ks only; its other support values do nothing.',
  'Winkler\'s bed under Brace 1, Brace 2: the slab is not analysed as a shell (the analysis options), so the bed does nothing.',
  'The modal problem is linear: every one-way member in, every support that may lift seated.',
  'Second order (P-Δ) on: the steel members\' forces are second order; their own buckling (EN 1993-1-1 6.3.1) stays with their buckling lengths, the global imperfections (5.3.2) are not added yet.',
  'EN 1993-1-1 5.2.1(3): αcr = 4.2 under „ULS 3”, below 10: second-order effects must be taken (analysis.setOptions secondOrder, or the sway effects × 1/(1 − 1/αcr) when αcr ≥ 3); analysis.buckling gives every combination\'s.',
  'EN 1993-1-1 5.2.1(3): αcr ≥ 4.2 under „ULS 3”, below 10: second-order effects must be taken (analysis.setOptions secondOrder, or the sway effects × 1/(1 − 1/αcr) when αcr ≥ 3); analysis.buckling gives every combination\'s.',
  'Linear buckling of the bars (the shells have no geometric stiffness): (K + αcr·Kg(NEd)) φ = 0, NEd of each ultimate combination; αcr ≥ 10: first order will do, 3 to 10: second order or the sway effects × 1/(1 − 1/αcr), below 3: second order (EN 1993-1-1 5.2.1(3), 5.2.2(5B)).',
  'Only the compressed bars\' geometric stiffness is taken (the tension\'s stiffening left out, on the safe side). A mode one member holds more than half of is that member\'s own buckling (its check\'s, EN 1993-1-1 6.3): the class is the frame\'s first other mode\'s.',
  'Concrete bars at their uncracked stiffness: EN 1992-1-1 5.8.2(6) asks for second-order effects over 10 %, judged with the cracked stiffness.',
  'No buckling under these loads: the geometric stiffness only stiffens.',
  'No buckling mode: the compressed bars are held by what is round them.',
  'A mechanism in its last states.',
  '1 support on springs (Footing 1): 4 nodes; the springs\' forces are reactions.',
  'A mechanism once 1 member went slack: nothing holds node N3 (ux).',
  'One-way members slack in 1 combination: Brace 1.',
  'Supports lifted in 1 combination: 1 node.',
];

/** The cases' default names (Lapin.Analysis: LoadCases, Climate). */
export const CASE_NAMES = ['Self weight', 'Finishes and partitions', 'Imposed, category A', 'Imposed, category C1', 'Snow', 'Wind',
  'Snow, drifted by wind towards +X', 'Snow, drifted by wind towards -Y', 'Wind towards +X, roof suction', 'Wind towards -Y, roof pressure'];

const LANGS: Lang[] = ['ro', 'es', 'de', 'fr', 'da', 'it', 'pl', 'pt', 'lo', 'zh'];

describe('the analysis\' notes and cases in every language', () => {
  for (const lang of LANGS) {
    it(`are in ${lang}, keeping their numbers and codes`, () => {
      for (const s of [...ENGINE_NOTES, ...CASE_NAMES]) {
        const out = translate(s, lang);
        expect(out, `${lang}: ${s}`).not.toBeNull();
        // A word may read the same (German Wind): it is still there.
        for (const x of s.match(/[+-][XY]\b|\d+(?:\.\d+)?(?:e-?\d+)?/g) ?? []) expect(out, `${lang}: ${s} → ${out}`).toContain(x);
      }
    });
  }

  it('in Romanian, names and codes kept as they are', () => {
    expect(translate('„W+X/s”: 12.35 kN of the wind on the façades goes straight to the ground (the strip at their foot), outside the bars\' sums.', 'ro'))
      .toBe('„W+X/s”: 12.35 kN din vântul pe fațade se duc direct în teren (fâșia de la baza lor), în afara sumelor barelor.');
    expect(translate('Wind towards -Y, roof pressure', 'ro')).toBe('Vânt spre -Y, presiune pe acoperiș');
    expect(translate('A mechanism at node N3 (0, 0, 3), rx: nothing holds it (no bar reaches it).', 'ro')).toBe('Un mecanism la nodul N3 (0, 0, 3), rx: nimic nu-l ține (nicio bară nu ajunge la el).');
  });

  it('in Romanian, the seismic messages name their parameter and count properly', () => {
    expect(translate('The behaviour factor q is a number from 1 to 8.', 'ro')).toBe('Factorul de comportare q este un număr de la 1 la 8.');
    expect(translate('1 mode by subspace iteration on the sparse Cholesky (5 steps); effective masses 61.2 % along X, 0.4 % along Y of the mass on the free nodes (12.5 t).', 'ro'))
      .toBe('1 mod prin iterație în subspațiu pe Cholesky rar (5 pași); mase efective 61.2 % pe X, 0.4 % pe Y din masa nodurilor libere (12.5 t).');
    expect(translate('3 levels have a shell floor: no diaphragm imposed there (the slab gives it).', 'ro')).toBe('3 niveluri au planșeu-placă: nu se impune diafragmă acolo (o dă placa).');
    expect(translate('An envelope is „uls”, „sls” or „seismic”.', 'ro')).toBe('O înfășurătoare este „uls”, „sls” sau „seismic”.');
  });

  it('in Romanian, the combination notes keep the names and the counts', () => {
    expect(translate('„Maintenance” names 2 load cases that are gone: left out of the results until its factors are set again.', 'ro'))
      .toBe('„Maintenance” numește 2 cazuri de încărcare care nu mai există: lăsată în afara rezultatelor până când i se dau din nou coeficienții.');
    expect(translate('4 combinations switched off: left out of the results, the envelopes, the checks and the export.', 'ro'))
      .toBe('4 combinații oprite: lăsate în afara rezultatelor, a înfășurătoarelor, a verificărilor și a exportului.');
  });
});
