# Construcții metalice: hale cu vute

Cerut de utilizator la 03.11.2026, pornind de la o schiță în plan (un contur cu două laturi lungi paralele și capete tăiate oblic): un obiect generic pentru profile metalice, cu capete și noduri configurabile, din care să se genereze repede o structură detaliată. Răspunsurile utilizatorului: (1) îmbinările sunt **cele obișnuite la halele cu vute**; (2) exportul pentru fabricație (DSTV/NC) **nu e în varianta gratuită, ci în premium**; (3) ținta întâi sunt **halele metalice cu vute** (cadre portal). Decizia 266 în `decisions-pending.md`.

## Granița

- Modulul deschis **`steel`**, în acest depozit, `requires: core, structure`: bara cu capete configurabile, nodurile hale­lor cu vute și piesele lor (plăci, șuruburi, suduri), generatorul de hală, elementele secundare, extrasele și desenele de ansamblu. În ediția `lapin`, ca `wood`.
- Modulul premium **`steel-pro`**, într-un depozit privat alăturat (`../lapin-steel`, `requires: steel`, legat prin cârlige ca `mep-pro`): exportul pentru fabricație (DSTV/NC), desenele de execuție pe piesă și pe ansamblu, numerotarea pentru atelier; mai târziu, verificările îmbinărilor după EC3.
- Barele rămân `IfcBeam` / `IfcColumn` / `IfcMember` cu profilul din catalogul existent (`Section`: I, H, U, T, L), lărgit cu RHS, CHS și profilele formate la rece (Z, C, Σ) pentru pane și rigle.
- Ca la șarpantă și la `wood`: **se stochează regulile, piesele sunt derivate**. Un nod își păstrează tipul și parametrii; plăcile, șuruburile, tăieturile și crestăturile se refac din bare și din tipul nodului.

## Bara și capetele ei

O bară: axă (linie; arc mai târziu), profil, rotație în jurul axei, punct de inserție, și **două capete**. Un capăt e un set de parametri:

- **prelungire sau retragere** (jocul de montaj);
- **tăiere**: dreaptă; după un plan dat prin două unghiuri (în plan și în elevație); în unghi egal cu altă bară; **ajustată** pe fața sau planul altei bare;
- **crestătură** a tălpilor (lungime, adâncime, sus și/sau jos);
- **tăiere după un corp** (ca tăieturile din Studio).

Geometria: extrudarea profilului tăiată cu semispații. În IFC: `IfcExtrudedAreaSolid` în `IfcBooleanClippingResult` cu `IfcHalfSpaceSolid`, crestăturile ca `IfcOpeningElement`, cum le citesc Tekla, Advance Steel și Revit. Capătul reia `EndCut` din îmbinările pereților și grinzilor (`WallJoins.cs`), lărgit cu unghiul în elevație.

**Din schiță în bară** (schița utilizatorului): Modify → Convert to member pe unul sau mai multe contururi. Două laturi lungi paralele dau axa (linia de mijloc) și lățimea (talpa: profilul din catalog cu acea lățime sau tipul ales), laturile scurte dau tăieturile capetelor; un contur cu crestături dă și crestăturile. Desenat pe o secțiune sau pe o elevație, același contur dă tăieturile în elevație.

## Hala cu vute

**Configuratorul de hală**, ca al acoperișului (stiluri în stânga, filele în mijloc, previzualizarea engine-ului în dreapta, aplicat ca un singur pas):

- deschiderea, lungimea, traveile (egale sau date), înălțimea la streașină, acoperiș în două ape sau într-o apă, panta;
- stâlpii și riglele cadrului (profilele), **vuta la streașină** (lungime, adâncime) și **vuta la coamă**;
- vuta: **tăiată din același profil** (un T tăiat oblic și sudat sub riglă, implicit) sau **din plăci sudate**; riglele cu inima variabilă rămân pentru mai târziu;
- cadrele de fronton: stâlpi de fronton cu riglă simplă, sau cadru curent la capăt;
- picioarele stâlpilor: articulate sau încastrate;
- panele și riglele de perete, contravântuirile (traveile alese), grinda de streașină.

Iese: axele și nivelurile, stâlpii, riglele cu vutele lor, nodurile cu tipurile implicite și elementele secundare, toate editabile după aceea.

## Nodurile

`LapinSteelNode`: stă unde se întâlnesc axele, știe ce bare leagă (principală și secundare) și are un **tip de nod** cu parametrii lui (configurat o dată, aplicat pe toate nodurile de același fel, ca etrierii pe tipurile de grinzi). Un nod **scrie capetele barelor** (retrageri, tăieturi, crestături), cum decid azi îmbinările colțurile pereților, și **adaugă piesele lui** într-un `IfcElementAssembly`: `IfcPlate`, `IfcMechanicalFastener` (șuruburi, ancore), `IfcFastener` (suduri).

Îmbinările obișnuite la halele cu vute, în ordinea în care vin:

1. **Picior de stâlp articulat**: placă de bază, 2 sau 4 ancore, fără rigidizări.
2. **Picior de stâlp încastrat**: placă de bază mai mare, 4–8 ancore, rigidizări (nervuri) pe tălpi și inimă.
3. **Nodul de streașină (colț de cadru)**: rigla cu vuta prinsă de stâlp cu **placă de capăt extinsă** și șuruburi; rigidizări în stâlp în dreptul tălpilor riglei și ale vutei; rigidizare de compresiune când trebuie.
4. **Nodul de coamă**: cele două rigle prinse cap la cap prin plăci de capăt, cu vuta de coamă.
5. **Înnădirea riglei** pentru transport: plăci de capăt cu șuruburi, la o stație aleasă.
6. **Stâlp de fronton – riglă**: placă de inimă sau corniere, cu gaură ovală pentru dilatare.
7. **Contravântuiri**: gușee pe stâlpi și pe rigle, pentru bare rotunde cu întinzătoare, corniere sau țevi.
8. **Pane și rigle de perete**: eclise (cleme) sudate sau prinse cu șuruburi pe rigle și stâlpi, cu suprapuneri sau manșoane la reazem; contrafișele de stabilitate (fly braces) de la pane la talpa inferioară.

Parametrii unui tip de nod: grosimea plăcilor, numărul, diametrul (M12–M30) și grupa șuruburilor (8.8, 10.9), distanțele la margini și între găuri, rigidizările, sudurile (grosimea).

## Etape

1. **Bara cu capete** — modulul `steel`, capetele configurabile, IFC cu tăieturi booleene, Convert to member din schiță (plan, secțiune, elevație), catalogul lărgit (RHS, CHS, Z, C).
2. **Configuratorul de hală** — cadrele cu vute (vuta tăiată din profil), frontoanele, axele și nivelurile, previzualizarea.
3. **Nodurile principale** — `LapinSteelNode`, găsirea nodurilor din topologia axelor, tipurile de nod: picior articulat și încastrat, streașină, coamă, înnădire; plăcile, șuruburile și sudurile ca piese derivate.
4. **Elementele secundare** — pane și rigle de perete (Z, C), clemele lor, contravântuirile cu gușeele, grinda de streașină, contrafișele.
5. **Extrase și desene** — lista de profile (greutăți), plăci, șuruburi și ancore, lista de debitare cu unghiurile; elevația cadrului (ca `wood.frameView`) și **planul ancorelor** (plăcile de bază și ancorele cotate, pentru fundații).
6. **Premium (`steel-pro`)** — DSTV/NC, desenele de execuție pe piesă și pe ansamblu, numerotarea pentru atelier; verificările EC3 mai târziu.

Fiecare etapă: teste în engine, pagina ei din ghid în toate limbile și scena ei.

## De hotărât de utilizator

- Catalogul profilelor formate la rece: de la ce producători (sau doar forme generice Z/C cu dimensiuni)?
- Vuta implicită tăiată din același profil: bine așa, sau plăci sudate?
- Ancorele: tije drepte, cu cârlig, sau cu placă de ancoraj; se modelează și în fundație?
