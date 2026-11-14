# Construcții metalice: hale cu vute

Cerut de utilizator la 03.11.2026, pornind de la o schiță în plan (un contur cu două laturi lungi paralele și capete tăiate oblic): un obiect generic pentru profile metalice, cu capete și noduri configurabile, din care să se genereze repede o structură detaliată. Răspunsurile utilizatorului: (1) îmbinările sunt **cele obișnuite la halele cu vute**; (2) exportul pentru fabricație (DSTV/NC) **nu e în varianta gratuită, ci în premium**; (3) ținta întâi sunt **halele metalice cu vute** (cadre portal). Decizia 266 în `decisions-pending.md`.

## Granița

- Modulul deschis **`steel`**, în acest depozit, `requires: core, structure`: bara cu capete configurabile, nodurile hale­lor cu vute și piesele lor (plăci, șuruburi, suduri), generatorul de hală, elementele secundare, extrasele și desenele de ansamblu. În ediția `lapin`, ca `wood`.
- Modulul premium **`steel-pro`**, într-un depozit privat alăturat (`../lapin-steel`, `requires: steel`, legat prin cârlige ca `mep-pro`): exportul pentru fabricație (DSTV/NC), convențiile de numerotare pentru atelier (serii pe faze, numerotare blocată între faze), simbolurile de sudură ISO 2553 pe desene; mai târziu, verificările îmbinărilor după EC3. Desenele de execuție pe piesă și pe ansamblu și mărcile pieselor sunt libere din 14.11.2026, generale pentru toate modulele (decizia 579, `docs/plan-shop-drawings.md`).
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
2. **Configuratorul de hală** — cadrele cu vute (vuta tăiată din profil), frontoanele, axele și nivelurile, previzualizarea. **Făcută (03.11.2026, decizia 270)**, vezi mai jos.
3. **Nodurile principale** — `LapinSteelNode`, găsirea nodurilor din topologia axelor, tipurile de nod: picior articulat și încastrat, streașină, coamă, înnădire; plăcile, șuruburile și sudurile ca piese derivate. **Făcută (03.11.2026, decizia 273)**, vezi mai jos.
4. **Elementele secundare** — pane și rigle de perete (Z, C), clemele lor, contravântuirile cu gușeele, grinda de streașină, contrafișele. **Făcută (03.11.2026, decizia 275)**, vezi mai jos.
5. **Extrase și desene** — lista de profile (greutăți), plăci, șuruburi și ancore, lista de debitare cu unghiurile; elevația cadrului (ca `wood.frameView`) și **planul ancorelor** (plăcile de bază și ancorele cotate, pentru fundații). **Făcută (03.11.2026, decizia 276)**, vezi mai jos.
6. **Premium (`steel-pro`)** — DSTV/NC, numerotarea pe faze pentru atelier, simbolurile de sudură ISO 2553; verificările EC3 mai târziu. (Desenele de execuție și mărcile sunt libere, generale, decizia 579.)

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

## Etapa 2, făcută (03.11.2026, decizia 270)

- **Hala** (`LapinSteelHall`, `Halls`, `HallSpec`, `HallCommands`): o resursă fără corp care ține specificația și agregă barele (`IfcRelAggregates`), fiecare cu rolul ei (`hallRole`), deci refacerea păstrează id-urile; axele ei într-un `IfcGrid` propriu (1, 2… la cadre, A și B la stâlpi, cu 1,5 m peste hală). **Niveluri**: nu se fac etaje noi — hala stă pe etajul curent, streașina și coama sunt înălțimi ale cadrelor (în Lapin un etaj e un nivel cu planuri și elemente, nu o cotă a unui cadru).
- **Înălțimea la streașină**: fața de sus a riglei deasupra feței exterioare a stâlpului; deschiderea de la axa unui stâlp la a celuilalt; travei egale sau date.
- **Cadrul**: stâlpii ai structurii (`IfcColumn`, profil metalic, inima în planul cadrului, capătul de sus tăiat după pantă prin fața de sus a riglei pe axa lui), riglele bare `RAFTER` cu axa pe fața de sus, ajustate pe fețele stâlpilor, la 45° la coamă (sau una singură, într-o apă, ajustată pe ambii stâlpi).
- **Vuta** (`Haunch`, `Haunches`): regulă pe capătul barei, lungime de-a lungul barei de la planul capătului, adâncime sub ea (perpendicular pe bară), capătul ei în planul capătului barei (fața stâlpului, planul de la coamă); **tăiată din profil** (inima și talpa riglei) sau **din plăci sudate** (grosimi date, lățimea tălpii a riglei dacă nu e dată); pe hală (streașină, coamă) și separat pe fiecare vută (`own`, păstrată la refacere). Greutatea vutelor nu intră în cantitățile riglei, ci în rezumatul configuratorului.
- **Frontoanele**: stâlpi de fronton (de colț și la distanța aleasă, axa tare perpendiculară pe perete, ajustați pe fața de jos a riglei de fronton, care merge până la fața exterioară a pereților, tăiată vertical) sau un cadru curent.
- **IFC**: hala ca `IfcBuildingSystem` LOADBEARING cu barele atribuite (`IIfcSystems`), setul `Lapin_SteelHall`, `IfcRelServicesBuildings`; o riglă cu vute ca `IfcElementAssembly` (riglă + vute): vuta tăiată un `IfcMember`, cea din plăci două `IfcPlate`, extrudate (`IfcSweptBody.Parts`). Testat și citit înapoi cu volumele din Lapin.
- **Previzualizarea** (`steel.hallPreview`): hala construită într-un model separat (un etaj la 0), elevația unui cadru curent și a frontonului (contururile barelor văzute de-a lungul halei), planul, rezumatul (cadre, înălțimi, oțel pe feluri, kg/m²).
- **Stiluri**: Hală 20 m, Hală 30 m (vute din plăci, cadru la fronton, încastrat), Șopron 10 m (într-o apă) și ale proiectului (`hallStyles` pe proiect; salvate, redenumite, șterse; halele stilului îl urmează). Catalogul lărgit cu IPE 330, 450, 500, 550, 600 și HEA 400.

**Rămas din etapa 2:** elementele secundare (pane, rigle de perete, contravântuiri, grinda de streașină) sunt doar păstrate pe hală, pentru etapa 4; riglele cu inimă variabilă; înnădirea riglei; halele cu mai multe deschideri; o hală mutată sau rotită doar din câmpurile ei (fără mânere). (Picioarele și ștergerea halei: etapa 3.)

## Etapa 3, făcută (03.11.2026, decizia 273)

- **Nodul** (`LapinSteelNode`, `SteelNodes`, `NodeParams`, `NodeCommands`): ține barele după id, fiecare cu capătul care ajunge la nod (sau fără: bara de care se prinde pe lateral), felul (`base`, `eaves`, `apex`, `splice`, `post`) și parametrii; ce nu e dat se alege după profilul barei prinse (`NodeSpec.Resolve`: șuruburi M16–M27 după înălțime, placa de capăt groasă cât șurubul rotunjit la 5 mm, găuri +2/+3 mm, margini 1,5 și distanțe 3 găuri; picioare 20–40 mm, ancore M20–M30, 30/25/20 de diametre în beton după fel, subturnare 30 mm). Parametrii dați în Proprietăți (`steel.updateNode`) sunt ai nodului (`own`) și hala refăcută îi păstrează.
- **Capetele barelor** le scrie nodul prin `SteelEnd.Extension` (un mecanism existent, nu unul nou): rigla retrasă cu grosimea plăcii de capăt (la streașină pe fața stâlpului, la coamă și la înnădire de la planul de îmbinare), stâlpul cu placa de bază (baza stâlpului e fața de jos a plăcii), stâlpul de fronton cu rostul (de-a lungul normalei la riglă). Nodul șters le dă înapoi; o bară ștearsă își ia nodurile.
- **Piesele** (derivate, `Nodes.cs`, `Fasteners.cs`): plăcile ca prisme; șuruburile și ancorele ca un singur corp din inele suprapuse (cap hexagonal, tijă, șaibă, piuliță; ancora cu cârlig îndoită în unghi drept, cea cu placă de ancoraj cu o placă pătrată la capăt). **Picior**: articulat — 2 ancore de o parte și de alta a inimii sau 4 între tălpi; încastrat — 4 (6: și 2 între tălpi; 8: două rânduri) în afara tălpilor, două nervuri în planul inimii. Ancorele intră în fundația din `structure` aflată sub stâlp (cea mai înaltă a cărei cutie conține punctul stâlpului, cu fața de sus sub bază), lungimea lor sub fața ei, oprite la 50 mm de fund (și se spune); fără fundație, sub subturnare. **Streașină**: placă de capăt extinsă (un rând de șuruburi deasupra riglei), rânduri între tălpi (câte încap la două distanțe, dacă nu se dau), 2 sau 4 pe rând, șuruburile prin placă și talpa stâlpului; rigidizări în stâlp de o parte și de alta a inimii, în dreptul tălpii de sus a riglei și al tălpii vutei (cea de sus coborâtă sub capătul stâlpului). **Coamă / înnădire**: plăcile de capăt ale celor două bare, șuruburile prin amândouă. **Stâlp de fronton**: placă de prindere sudată sub riglă, lângă inima stâlpului, două șuruburi prin inimă (găuri ovale, ca dată).
- **Sudurile** rămân date (unde, grosimea, lungimea), afișate și numărate, nu corpuri.
- **Topologia** (`steel.findNodes`, Steel → Find connections): picior sub fiecare bară verticală; streașină unde o bară e ajustată pe o bară verticală; stâlp de fronton unde o bară verticală e ajustată sub alta; coamă unde două bare sunt tăiate în unghi una cu alta; înnădire unde două bare merg în prelungire (±0,5°), capăt la capăt (5 mm), tăiate drept. Doar ce lipsește.
- **Hala**: fila **Nodes** a configuratorului (nodurile pornite/oprite, picioare articulate/încastrate, felul, numărul și lungimea ancorelor, placa, șuruburile, placa extinsă și rigidizările la streașină, placa și șuruburile la coamă; `nodes`, `anchor`, `nodeParams` pe hală); nodurile hălii au locul lor (`hallNode`: „eaves@f1:raf:a@start”) și se refac peste aceleași id-uri; picioarele stâlpilor de fronton sunt articulate. Previzualizarea desenează nodurile cadrului curent și rezumatul numără nodurile, șuruburile și ancorele, cu greutatea lor în total. **Delete hall** ia hala cu barele, nodurile și axele ei, într-un pas (`IDeletion`, ca o cutie Studio).
- **IFC**: nodul ca `IfcElementAssembly` (USERDEFINED, felul ca ObjectType) de `IfcPlate`, `IfcMechanicalFastener` BOLT și ANCHORBOLT, `IfcDiscreteAccessory` ANCHORPLATE, fiecare cu mărimea și grupa în `Lapin_SteelPart`; `IfcRelConnectsWithRealizingElements` între barele legate (piciorul, între fundație și stâlp, când e una), prin serviciul nou `IIfcConnections`; nodurile atribuite și ele sistemului halei. Testat și citit înapoi cu volumele din Lapin.
- **Extrasul Piese metalice** (`steelParts`, Steel → Steel parts): plăci, șuruburi, ancore și plăci de ancoraj după mărime și grupă, cu greutatea; sudurile după grosime, cu lungimea.
- **Interfața**: secțiunea **Connection** în Proprietăți (barele, parametrii, piesele, sudurile, greutatea, fundația; Edit hall…); titlul vutei din Proprietăți („Vută la început”) ocupă tot rândul, nu mai e tăiat.

**Rămas din etapa 3:** înnădirea riglei la o stație aleasă de hală (bara împărțită în două barelor halei; înnădirea între două bare desenate în prelungire există); sudurile ca corpuri; găurile în plăci și bare (șuruburile trec prin material); placa de capăt a streașinei la o riglă ajustată pe inima stâlpului (acum se presupune talpa); nodurile contravântuirilor, ale panelor și riglelor de perete (etapa 4); planul ancorelor (etapa 5); verificările EC3 (premium).

## Etapa 4, făcută (03.11.2026, decizia 275)

- **Pe hală** (`HallSecondary`, `Secondary.cs`): `secondary` (pornit implicit; halele vechi le primesc la prima refacere), profilele (`purlin`: Z 200×65×2, `rail` și `eavesBeamProfile`: C 200×65×2, `brace`: bară rotundă Ø20), `purlinOverhang` (0,15 m peste frontoane), `purlinRun` (`bay`: câte una pe travee, sau `continuous`), `railBottom` (0,3 m), `roofBracing`, `wallBracing`, `flyBraces`. Toate sunt bare ale halei cu rol (`pur:a:3:2`, `rail:b:1:4`, `grail:0:2:1`, `eb:a:3`, `rbr:2:a:1:1`, `wbr:2:a:0:2`, `fly:1:a:3:p`, `cfly:1:a:3:m`), deci refacerea păstrează id-urile.
- **Panele**: pe fața de sus a riglelor, inima perpendiculară pe acoperiș, de la 150 mm în interiorul feței stâlpilor până la 200 mm de coamă, șirurile egale la cel mult distanța dată pe pantă; `PURLIN`. **Riglele de perete**: pe fața exterioară a stâlpilor, inima orizontală și buzele în jos (C întors), de la `railBottom` până sub streașină (sub grinda de streașină cu o distanță), pe travee; pe frontoane între stâlpi, cât le lasă rigla de deasupra; `USERDEFINED` „Side rail”. **Grinda de streașină**: o riglă în vârful pereților lungi, „Eaves beam”.
- **Contravântuirile**: în traveile alese, X în planul acoperișului (sub talpa de sus a riglelor) și în planul pereților lungi (prin axele stâlpilor, până sub vută), în panouri cam cât travea; cele două diagonale de o parte și de alta a gușeului, capetele retrase de la inimă; `BRACE`. **Contrafișele**: de la fiecare a doua pană și riglă (în afara vutelor) la talpa de jos a riglei și la talpa interioară a stâlpului, de ambele părți, L 50×50×5.
- **Nodurile noi** (`SteelNodes`, fără retrageri: primul membru e cel de care se prind): **eclisa** (`cleat`) — o placă sudată pe bara traversată, lângă inima panei (în afara unui C, pe partea unui Z opusă tălpii de jos), două șuruburi pe capăt de pană prin inimă (M12 până la 250 mm, M16 peste; placa 8/10 mm), mai înaltă cât golul la un stâlp de colț mai îngust; un profil care nu e format la rece e prins prin talpă; **gușeul** (`gusset`) — o placă în planul contravântuirii, sudată pe inima riglei sau a stâlpului (pe talpă dacă planul e al inimii), adâncimea ei sub axă păstrată pe nod, două șuruburi la o cornieră sau o țeavă, nimic la o bară rotundă; **contrafișa** (`fly`) — câte un șurub la fiecare capăt. Intră în extrasul Steel parts și în sistemul IFC al halei.
- **Configuratorul**: fila **Secondary steel** (profile, distanțe, lungimi, prima riglă, grinda de streașină, traveile, profilul contravântuirilor, acoperiș/pereți, contrafișe), previzualizarea în plan, rezumatul cu numărul și greutatea fiecărui fel. Profilele catalogului: L 50, 60, 70 și CHS 60,3×3,2.
- **IFC**: panele `IfcMember` PURLIN, contravântuirile și contrafișele BRACE, riglele și grinda de streașină USERDEFINED, nodurile ca ansambluri; testat și citit înapoi.

**Rămas din etapa 4:** manșoanele și suprapunerile panelor la reazem (acum una pe travee sau continuă, fără eclise de continuitate); golurile de uși în riglele de perete; contravântuirile frontoanelor și grinzile de vânt verticale la stâlpii de fronton; întinzătoarele barelor rotunde; contrafișele în zona vutelor.

## Etapa 5, făcută (03.11.2026, decizia 276)

- **Extrasul Steel members** (`SteelMembersSchedule`, `steelMembers`): barele, grinzile și stâlpii dintr-un profil metalic, pe hale (cele fără hală la sfârșit), o **marcă** pentru barele de un fel cu același profil, aceeași lungime (la mm, între tăieturi) și aceleași tăieturi (C, R, GP, P, SR, EB, B, FB, M, BM; numerotate de la profilul cel mai greu și cea mai lungă); coloanele: profilul, lungimea, tăietura la început și la sfârșit (cât se rotește planul capătului față de drept, și crestătura — unghiurile listei de debitare), numărul, kg/m, greutatea bucății și totală, suprafața de vopsit (perimetrul secțiunii pe lungime); un total pe hală (când sunt mai multe) și unul general. Mărcile sunt calculate, nu păstrate pe bare (numerotarea pentru atelier rămâne în `steel-pro`). Extrasul **Steel parts** cuprinde acum și eclisele, gușeele și șuruburile etapei 4.
- **Planul ancorelor** (`steel.anchorPlan`, Steel → Anchor plan): o vedere în plan a etajului halei, la 1:50, tăiată la 0,12 m (peste plăcile de bază), cu cheia `steelAnchors`; ceruta din nou, se deschide aceeași. Adnotările (`IViewAnnotations`): fiecare ancoră încercuită cu cruce, distanța ancorelor până la axă pe ambele direcții (± o singură dată când sunt simetrice, altfel lanț), fiecare picior cu marca tipului său (**B1**, **B2**…: aceeași placă, ancore, grupă, subturnare), traveile și deschiderea axelor, tabelul tipurilor (număr, fel, placă, ancore, grupă, subturnare) lângă hală.
- **Elevația cadrului** (`steel.frameView`, Steel → Frame elevation): o secțiune la 0,5 m înaintea cadrului, privind spre el, adâncime 1 m, 1:50, cheile `steelFrame` și `steelFrameIndex`; cadrul curent sau al barei selectate; cotele deschiderii (și până la coamă), înălțimilor la streașină și la coamă, titlul cu profilele și panta.
- Amândouă sunt vederi obișnuite: se pun pe planșe, se tipăresc; urmează hala.

**Rămas din etapa 5:** detaliul unui picior (o secțiune prin placa de bază și fundație); planul ancorelor nu arată încă fundațiile (sunt sub tăietură); cotele la axe ale picioarelor de fronton dintr-un cadru rotit sunt în coordonatele halei, textele fixe în engleză (ca celelalte desene generate); numerotarea pentru atelier și desenele de execuție: `steel-pro`.
