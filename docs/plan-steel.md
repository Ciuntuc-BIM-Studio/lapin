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
- vuta, la alegere: **tăiată din același profil** (un T tăiat oblic și sudat sub riglă) sau **din plăci sudate**; riglele cu inima variabilă rămân pentru mai târziu;
- cadrele de fronton: stâlpi de fronton cu riglă simplă, sau cadru curent la capăt;
- picioarele stâlpilor: articulate sau încastrate;
- panele și riglele de perete, contravântuirile (traveile alese), grinda de streașină.

Iese: axele și nivelurile, stâlpii, riglele cu vutele lor, nodurile cu tipurile implicite și elementele secundare, toate editabile după aceea.

## Nodurile

`LapinSteelNode`: stă unde se întâlnesc axele, știe ce bare leagă (principală și secundare) și are un **tip de nod** cu parametrii lui (configurat o dată, aplicat pe toate nodurile de același fel, ca etrierii pe tipurile de grinzi). Un nod **scrie capetele barelor** (retrageri, tăieturi, crestături), cum decid azi îmbinările colțurile pereților, și **adaugă piesele lui** într-un `IfcElementAssembly`: `IfcPlate`, `IfcMechanicalFastener` (șuruburi, ancore), `IfcFastener` (suduri).

Îmbinările obișnuite la halele cu vute, în ordinea în care vin:

1. **Picior de stâlp articulat**: placă de bază, 2 sau 4 ancore (drepte, cu cârlig sau cu placă de ancoraj, modelate și în fundație), fără rigidizări.
2. **Picior de stâlp încastrat**: placă de bază mai mare, 4–8 ancore, rigidizări (nervuri) pe tălpi și inimă.
3. **Nodul de streașină (colț de cadru)**: rigla cu vuta prinsă de stâlp cu **placă de capăt extinsă** și șuruburi; rigidizări în stâlp în dreptul tălpilor riglei și ale vutei; rigidizare de compresiune când trebuie.
4. **Nodul de coamă**: cele două rigle prinse cap la cap prin plăci de capăt, cu vuta de coamă.
5. **Înnădirea riglei** pentru transport: plăci de capăt cu șuruburi, la o stație aleasă.
6. **Stâlp de fronton – riglă**: placă de inimă sau corniere, cu gaură ovală pentru dilatare.
7. **Contravântuiri**: gușee pe stâlpi și pe rigle, pentru bare rotunde cu întinzătoare, corniere sau țevi.
8. **Pane și rigle de perete**: eclise (cleme) sudate sau prinse cu șuruburi pe rigle și stâlpi, cu suprapuneri sau manșoane la reazem; contrafișele de stabilitate (fly braces) de la pane la talpa inferioară.

Parametrii unui tip de nod: grosimea plăcilor, numărul, diametrul (M12–M30) și grupa șuruburilor (8.8, 10.9), distanțele la margini și între găuri, rigidizările, sudurile (grosimea).

## Etape

1. **Bara cu capete** — modulul `steel`, capetele configurabile, IFC cu tăieturi booleene, Convert to member din schiță (plan, secțiune, elevație), catalogul lărgit (RHS, CHS, Z, C). **Făcută (03.11.2026, decizia 268)**, vezi mai jos.
2. **Configuratorul de hală** — cadrele cu vute (vuta tăiată din profil), frontoanele, axele și nivelurile, previzualizarea.
3. **Nodurile principale** — `LapinSteelNode`, găsirea nodurilor din topologia axelor, tipurile de nod: picior articulat și încastrat, streașină, coamă, înnădire; plăcile, șuruburile și sudurile ca piese derivate.
4. **Elementele secundare** — pane și rigle de perete (Z, C), clemele lor, contravântuirile cu gușeele, grinda de streașină, contrafișele.
5. **Extrase și desene** — lista de profile (greutăți), plăci, șuruburi și ancore, lista de debitare cu unghiurile; elevația cadrului (ca `wood.frameView`) și **planul ancorelor** (plăcile de bază și ancorele cotate, pentru fundații).
6. **Premium (`steel-pro`)** — DSTV/NC, desenele de execuție pe piesă și pe ansamblu, numerotarea pentru atelier; verificările EC3 mai târziu.

Fiecare etapă: teste în engine, pagina ei din ghid în toate limbile și scena ei.

## Hotărât de utilizator (03.11.2026)

- **Profilele formate la rece**: forme generice cu dimensiuni (Z, C, Σ: înălțime, tălpi, grosime, îndoituri), fără cataloage de producător.
- **Vuta**: se poate alege, pe tipul de cadru și pe fiecare vută: **tăiată din același profil** (un T tăiat oblic, sudat sub riglă) sau **din plăci sudate** (inimă și talpă cu grosimile lor).
- **Ancorele**: toate trei felurile, alese pe tipul de picior: **tije drepte**, **cu cârlig** și **cu placă de ancoraj**; ancorele se **modelează și în fundație** (intră în fundația sub stâlp, cu lungimea de ancorare, și apar în planul ancorelor și în extrase).

## Etapa 1, făcută (03.11.2026, decizia 268)

- **Cine deține ce.** O clasă are un singur modul proprietar, iar Studio deține deja `IfcMember` pentru solidele lui clasate așa. De aceea bara metalică e clasa proprie **`LapinSteelMember`** (cu `LapinSteelMemberType`), scrisă în IFC ca `IfcMember` / `IfcMemberType` (`exportAs`, ca `LapinRailing`); proiectele vechi cu solide Studio `IfcMember` rămân neatinse. Grinzile și stâlpii rămân ai modulului `structure`: unul drept, dintr-un profil metalic, primește aceleași capete în atributul `steelEnds`, iar `steel`, ca `IGeometryFilter` (ca `wood`), îi reface corpul din ele în locul celui al structurii (axa grinzii pe deschiderea liberă dintre fețele stâlpilor, cu fața de sus pe axă; a stâlpului de la bază în sus). Fără capete, grinzile și stâlpii se desenează ca înainte.
- **Capătul** (`SteelEnd`): prelungire/retragere; tăiere dreaptă, după un plan (unghi în plan în jurul verticalei secțiunii, unghi în elevație în jurul laturii ei; un unghi pozitiv în elevație lasă capătul să se încline înapoi, partea de sus mai scurtă), la 45° cu altă bară (planul bisector prin punctul unde axele se apropie cel mai mult) sau ajustată pe fața celeilalte bare la care ajunge (din cutia secțiunii ei); crestătura sus și/sau jos (lungime, adâncime). Mitra și ajustarea țin bara cealaltă după id și o urmează; dacă e ștearsă, capătul redevine drept și spune de ce.
- **Geometria** (`SteelBody`): fiecare muchie laterală a secțiunii merge de la planul de început la cel de sfârșit, corpul se face direct (fețe laterale plane și capace în planele tăieturilor), fără booleene; doar crestăturile se scad cu manifold. `EndCut` din îmbinările pereților nu s-a potrivit (e o pereche de retrageri în plan, nu un plan în spațiu), deci capătul are planul lui.
- **IFC** (`IIfcSweptBodies` în platformă, folosit de `IfcExport`): `IfcExtrudedAreaSolid` (profil arbitrar, cu goluri pentru RHS/CHS) tăiat de `IfcBooleanClippingResult` cu `IfcHalfSpaceSolid` (reprezentarea „Clipping”), crestăturile ca `IfcOpeningElement` cu `IfcRelVoidsElement`. Testat și citit înapoi: volumul e cel al corpului din Lapin.
- **Din schiță** (`SketchBands`, `SketchMembers`, Steel → Convert to member, `steel.convertSketch`): două laturi lungi paralele (±0,5°) dau axa și lățimea, latura de la fiecare capăt tăierea; în plan lățimea e a tălpilor (profilul cu acea lățime din tipurile proiectului sau din catalog, cel mai ușor, altfel tipul curent), bara stă la nivelul de sus al etajului; pe o secțiune, o fațadă sau un plan de referință lățimea e înălțimea secțiunii, unghiurile sunt în elevație, iar o treaptă în unghi drept la capăt e o crestătură. Mai multe contururi într-un singur pas; schița e înlocuită (`keep: true` o păstrează); contururile care nu sunt benzi rămân și se spune de ce.
- **Catalogul** (`Section`): RHS, CHS și profilele generice formate la rece C, Z, Σ (înălțime, lățime, grosime, rebord; îndoituri în unghi drept; la Σ inima se frânge spre tălpi cu un sfert din lățime pe jumătatea din mijloc), cu ariile testate; profilele tubulare sunt goale și la grinzile și stâlpii structurii.
- **Interfața**: tabul **Steel** (Member, SM, cu tipul ales în Proprietăți și în Catalog; Convert to member; Mitre; Fit; Steel types, care face și tipuri de grindă și de stâlp din profile), secțiunea **Steel** în Proprietăți (capetele, înălțimile capetelor barei), mânere pe capetele barei în plan. Pagina ghidului `docs/user/steel.md` în toate limbile, scena `steel`.

**Rămas din etapa 1:** tăierea după un corp (ca tăieturile din Studio) și ajustarea pe planul axei altei bare; axa în arc; o crestătură sus și una jos de mărimi diferite la același capăt (acum au aceeași lungime și adâncime); unealta Bară desenează doar în plan (în secțiune sau 3D bara se ridică din înălțimile capetelor); mânere doar în plan; grinzile curbe nu primesc capete; razele interioare ale profilelor laminate și ale celor formate la rece nu sunt modelate.
