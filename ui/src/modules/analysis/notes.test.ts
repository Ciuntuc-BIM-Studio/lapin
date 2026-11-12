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
  'Indicative: EN 1990 Annex A1, the recommended values (γG 1.35, γQ 1.5, ψ by Table A1.1, snow for sites up to 1000 m).',
  'Not made yet: expressions 6.10a and 6.10b, snow with the imposed load on roofs (EN 1991-1-1 3.3.2), the seismic combination, CR 0-2012\'s values.',
  'No bars: the slabs\' loads go straight into the load-bearing walls under them, their reactions; the walls and slabs themselves are analysed from S2.',
  'No bars and no load-bearing walls: nothing carries the loads (S1 analyses bars; walls and slabs come with S2).',
  'Stage S1: the bars carry everything; the slabs only carry loads to them (the 45° rule), the load-bearing walls are line supports (every node of theirs held in translation, free to turn). Shells are analysed from S2.',
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
  'its section „Mine” gives no properties',
  'its material „Oak” has no E',
  'its type names no material',
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
});
