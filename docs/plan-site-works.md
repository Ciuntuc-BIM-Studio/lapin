# Plan: organizarea de șantier (tabul Site works)

Cerut pe 27.10.2026: un tab nou în ediția open (`products/lapin.json`). În el se modelează, cu configuratoare, schelele de fațadă și sistemele de cofraj. Cofrajele se așază pe trasee și pe repere ale elementelor: fața de jos a plăcii, fața de jos a grinzii, fețele laterale și muchiile ei.

Planul continuă decizia din 17.10.2026 (`Lapin/docs/pereti-compusi-si-executie.md`, etapele V5 și V9). Execuția face parte din proiectare: cofrajele și schelele rezultă din model, se configurează pe tipuri, iar în model sunt elemente temporare.

## Așa s-a făcut (27.10.2026)

Deciziile S-1…S-5 confirmate de utilizator; ordinea cerută: schela, placa, grinda, desenele.

- **Platforma.** `IElementFaces` (`ElementFaces.cs`): rolurile fețelor declarate de module din atribute (`ArchitectureFaces`: placa și peretele drept; `StructureFaces`: grinda dreaptă și stâlpul). Interogările `core.faces {id}` și `core.faceAt {id, point, normal?}`. `IGround` (Civil: terenul cu tușele pensulei) și `IPhaseService` (Phases: faza de execuție, adăugată și făcută activă) în `SiteServices.cs`.
- **Interfața.** Gestul `face`: în 3D, fața de sub cursor se colorează și apare eticheta „element · față”; clicul dă punctul și normala. În plan, un clic pe element dă punctul, iar motorul alege latura cea mai apropiată sau intradosul.
- **Modulul `sitework`** (`engine/src/Modules/Construction/Lapin.SiteWorks`, `ui/src/modules/sitework`, în `products/lapin.json`), tabul Site works:
  - **Scaffold**: pe o polilinie.
  - **Scaffold on walls**: pe pereții selectați; îi urmează.
  - **Formwork**: pe plăcile și grinzile selectate.
  - **Formwork on face**.
  - **Formwork plan**: planul etajului activ în faza de execuție, secționat la 1 m.
  - **Formwork schedule** și **Scaffold schedule**.
  - Configuratoarele **Scaffold types…** și **Formwork types…**, cu previzualizarea calculată de motor (`sitework.scaffoldPreview`, `sitework.formworkPreview`).
- **Comenzi**: `sitework.createScaffoldType/updateScaffoldType`, `createFormworkType/updateFormworkType`, `addScaffold`, `scaffoldFromWalls`, `updateScaffold`, `addFormwork {hostIds, faces?}`, `updateFormwork`, `formworkPlan`.
- **Reguli**:
  - **Traveile** sunt cele mai puține piese din catalog care ajung la capăt; 30 cm de prisos cântăresc cât o piesă.
  - **Nivelurile** urcă până când podina de sus e la cel mult `reach` (1,50 m) sub cota de lucru.
  - **Colțurile**: la colțul exterior tronsonul continuă cu o lățime, la cel interior se oprește cu o lățime mai devreme.
  - **Tălpile schelei** stau pe teren; dacă terenul coboară pe un tronson mai mult decât preiau tălpile reglabile, apare un avertisment.
  - **Cofrajul plăcii** stă pe etajul de sub intradosul ei, ca să-l deseneze planul acelui etaj.
  - **Aria de contact** se calculează exact (benzi între evenimente), fără booleane.
- **IFC**:
  - `IfcElementAssembly` USERDEFINED FORMWORK sau SCAFFOLD, cu piesele ca `IfcMember`, `IfcPlate` și `IfcDiscreteAccessory`;
  - `Status` TEMPORARY în setul de faze;
  - `IfcConstructionProductResource` prin `IfcRelAssignsToProduct`;
  - `IfcRelConnectsElements` între gazdă și cofraj.
- **Teste**: `SiteWorksTests` (11). **Ghidul**: `docs/user/site-works.md` (și `ro/`), cu scena `site-works`.
- **Rămase după prima zi**: toate făcute pe 27.10.2026, a doua tură (mai jos).

### A doua tură (27.10.2026)

- **Vederea de jos.** Orice plan e văzut de sus sau de jos (`look: "up"`, în proprietățile vederii și în View range): de jos, secțiunea și ce e deasupra ei până la marginea de sus a intervalului, ca linii văzute, întoarse ca să stea ca planul (convenția planului de plafon). Planul de cofraj e implicit văzut de jos; „Formwork plan from above” îl face văzut de sus, cu cofrajul întrerupt.
- **Elemente curbe.** Grinzile curbe se cofrează de-a lungul arcului: axa poligonală (coarde de cel mult 5°), fețele laterale decalate în mitră, cea exterioară mai lungă cu w·θ, capetele la fețele de capăt. Pereții curbi au fețele ca linii de-a lungul arcului; schela îi urmează pe coarde de cel mult 3 m. Colțurile schelei se închid cu lățimea × tan(jumătate din unghi).
- **S6: pereți, stâlpi, centuri, fundații** (tipul `upright`):
  - **Fețele.** Pereții au golurile ca găuri în fețe, capetele lor și fețele zonelor turnate (`Zone[key].Left/Right`). Fundațiile au treptele fundației izolate, talpa și elevația celei continue.
  - **Ce se cofrează.** „Auto” înseamnă: la stâlp, fețele laterale; la peretele compus, centurile, altfel ambele fețe și capetele libere; la fundație, fețele ei.
  - **Ce se scade.** Ce stă lipit de o față nu se cofrează, iar ce e turnat deasupra o oprește.
  - **Tipul** dă panourile (lățimi × înălțimi), tijele prin pereți, jugurile stâlpilor și sprijinirile.
- **Refolosirea și devizul.** Extrasul de cofraje are, pe fiecare tip, setul (cel mai mult turnat deodată: pe fază de execuție dacă sunt mai multe, altfel pe etaj) și ce e de cumpărat (setul, sau totalul împărțit la refolosiri, dacă e mai mult). Tipul ține articolul de deviz (codul din norme și textul, date de utilizator; Lapin nu inventează coduri), arătat pe fiecare rând.
- **Rămâne.** Cataloagele de producător, ca biblioteci.

## Ce există deja

- **Fazele** (`Lapin.Phases`) au felul `execution` și starea `temporary`, adică un element creat și demolat în aceeași fază. Filtrul „Show execution” arată elementele temporare. Cofrajul și schela sunt exact cazul pentru care starea a fost gândită.
- **Elementele pe traseu**:
  - `IPathElements` și `Joins` (`PathJoins.cs`) dau axul și corpul pereților și ale fundațiilor continue, cu colțurile lor.
  - Studio face sweep pe un traseu, cu poziția profilului față de traseu (centru, stânga, dreapta, exterior, interior) și cu un decalaj.
  - Coridoarele din Civil (`spec-site.md`) au deja stații, mitre la colțuri și rosturi la n metri.
- **Configuratoarele** au un model de urmat: peretele cortină (`CurtainWallDialog`) și tâmplăria (`JoineryDialog`). Amândouă au un tip cu reguli, o previzualizare și instanțe care urmează tipul.
- **Cantitățile de cofraj** sunt calculate azi doar ca arie, pe zidul de sprijin (`RetainingWalls.cs`, `FormworkArea`) și pe scară (`StairBuilder.cs`). Nu există încă un element de cofraj.
- **Extrasele** au `IScheduleSource`: un modul oferă un tabel, extrasele îl arată, îl pun pe planșă și îl exportă. Devizul e în modulul `cost`.
- **Ce lipsește**:
  - Alegerea unei **fețe** a unui element, în 3D sau în plan. Azi un pick întoarce elementul, nu fața lui.
  - Un **nume stabil al feței**: „fața de jos a grinzii” trebuie să rămână aceeași față după ce grinda se mută sau își schimbă secțiunea.

## Principii

1. **Elementul gazdă rămâne sursa.** Un cofraj ține reperul (gazda și rolul feței) și tipul lui de sistem. Desfășurarea, piesele și cantitățile se refac din gazdă de fiecare dată, ca la goluri și la buiandrugi: o grindă mutată își mută cofrajul.
2. **Reperele sunt semantice, nu indici de față.** Un rol se cere modulului gazdei, după datele lui parametrice. Exemple de roluri:
   - placa: `Soffit`, `Top`, `Edge[i]`, cu `i` latura conturului;
   - grinda: `Bottom`, `Side.Left`, `Side.Right`, `Edge.BottomLeft`, `Edge.BottomRight`, `End.Start`, `End.End`;
   - stâlpul: `Side[i]`;
   - peretele și centura: `Face.Exterior`, `Face.Interior`, `Top`.

   Un index de față din B-rep s-ar schimba la prima boolean.
3. **Aria de contact se calculează din geometrie.** Contactul e fața gazdei minus ce acoperă alte elemente. Exemple: fața laterală a grinzii minus placa turnată odată cu ea și minus stâlpii; fața de jos a plăcii minus grinzile și golurile. Este aria din deviz.
4. **Configuratorul ține sistemul, nu piesele.** Un tip de cofraj sau de schelă spune:
   - ce elemente are sistemul: panouri, grinzi, popi, cadre, podine;
   - dimensiunile din catalog;
   - distanțele maxime;
   - regulile de completare.

   Piesele se generează din tip, pe reper.
5. **Temporar prin faze.** Un cofraj sau o schelă nouă primește faza de execuție activă: e creată și demolată în ea. Se vede doar într-o vedere de execuție și nu intră în cantitățile permanente. În IFC are statusul `TEMPORARY`.
6. **Generic în ediția open.** Sistemele implicite sunt generice: cadre de fațadă, sistem modular cu rozetă, grinzi de lemn H20 pe popi, panouri-cadru. Cataloagele de producător vin mai târziu, ca date în biblioteci (cloud), nu în cod, și fără mărcile lor în ediția de bază.

## Arhitectura

- **Un modul nou, `sitework`.**
  - Motorul: `engine/src/Modules/Construction/Lapin.SiteWorks`, cu `module.json` care cere `architecture`, `structure` și `phases`.
  - Interfața: `ui/src/modules/sitework`.
  - Se adaugă în `products/lapin.json`.
  - Tabul se numește „Site works” (RO: „Organizare de șantier”). Vine după Structure și înaintea lui Quantities, ca să nu se confunde cu tabul Site din Civil, care e despre teren.
- **Un serviciu nou în platformă, `IElementFaces`**, în același stil ca `IPathElements`.
  - Fiecare modul declară rolurile fețelor pentru clasele lui. Pentru fiecare rol dă: conturul plan al feței (un poligon 3D cu normala lui), axul în lungul feței (pentru grinzi și pereți) și cine îl acoperă.
  - Placa și peretele le declară în `architecture`, grinda, stâlpul și fundația în `structure`.
  - Serviciul răspunde la două întrebări: `FacesOf(entity)` și `Face(entity, role)`.
  - Interogarea `core.faceAt {id, point, normal}` traduce un pick 3D (elementul, punctul și normala triunghiului lovit) în rolul feței.
- **Pick pe față în UI.** Un pas nou al liniei de comandă, `want: 'face'`.
  - În 3D, fața de sub cursor se colorează la hover, iar eticheta îi spune rolul („Beam B12 · bottom”).
  - În plan, se poate alege o latură a plăcii sau a peretelui (rolul `Edge[i]` sau `Face.*`).
  - Raycaster-ul trebuie să întoarcă și normala, nu doar elementul. Asta e o schimbare mică în `viewport`.
- **Clasele din model**, ca clase Lapin cu `exportAs`, ca `LapinRetainingWall`:
  - `LapinFormwork`, exportat ca `IfcElementAssembly` USERDEFINED „FORMWORK”. Ține tipul, reperele (`[{host, role}]`), opțiunile instanței (direcția grinzilor, decalajele, cota de sprijin) și piesele generate.
  - `LapinScaffold`, exportat ca `IfcElementAssembly` USERDEFINED „SCAFFOLD”. Ține tipul, traseul (o polilinie în plan sau reperele fețelor de perete), cota de jos (teren sau etaj) și cota de sus (cota de lucru plus parapet).
  - Tipurile, ca `IfcElementAssemblyType`, cu regulile într-un set de proprietăți Lapin (`Lapin_FormworkSystem`, `Lapin_ScaffoldSystem`). Nu încep cu `Pset_`.
  - Legătura cu gazda este un `IfcRelConnectsElements` gazdă → cofraj, cu rolul în `Lapin_Intent`.
  - Resursele intră în IFC ca `IfcConstructionProductResource` FORMWORK, legat prin `IfcRelAssignsToProduct`. Așa citesc programele de planificare.
- **Piesele** sunt părți ale elementului (`ElementGeometry.Parts`), nu entități separate, ca să nu umfle modelul. O placă de 200 m² are sute de popi.
  - La exportul IFC, opțiunea „components” le scrie ca `IfcMember`, `IfcPlate` și `IfcDiscreteAccessory` în ansamblu (`IfcRelAggregates`).
  - Fără opțiune, ansamblul are un singur corp.
- **Nivelul de detaliu.**
  - Schematic (implicit): suprafețele de cofraj ca plăci subțiri, popii ca bare și cadrele ca linii groase. Este V9 din planul de execuție.
  - Detaliat: profilele reale ale grinzilor, popii cu tub și furcă, rozetele.
  - Planurile folosesc schematicul.

## Uneltele

### 1. Cofrajul plăcii (fața de jos și marginile)

- **Așezarea**, în trei feluri:
  - selectezi plăcile și apeși „Slab formwork”;
  - alegi fața de jos în 3D;
  - comanda `SLABFORM`.

  Marginile se adaugă singure (opțiunea „edges”) sau se aleg una câte una.
- **Configuratorul tipului:**
  - sistemul: grinzi și popi, mese sau panouri;
  - placajul (grosimea, formatul, de exemplu 2,50 × 0,50 m);
  - grinzile secundare (profilul H20, lungimile din catalog 2,45 / 2,90 / 3,30 / 3,90 / 4,90 m, distanța maximă);
  - grinzile principale (distanța maximă);
  - popii (clasa, domeniul de înălțime, distanța maximă);
  - capetele (furcă, cap de decofrare rapidă).

  Distanțele maxime se dau pe grosimi de placă, de exemplu 0,18 / 0,25 / 0,30 m. Configuratorul le alege după grosimea gazdei.
- **Desfășurarea:**
  - Conturul de calcul este fața de jos a plăcii minus grinzile de sub ea și golurile.
  - Grinzile secundare merg pe direcția instanței (implicit deschiderea scurtă).
  - Grinzile principale merg perpendicular pe ele. Popii stau în nodurile principalelor.
  - Înălțimea popului se măsoară de la fața de sus a elementului de dedesubt (placa etajului inferior, radierul sau terenul) până la fața de jos a plăcii, minus placaj și grinzi. Fața de dedesubt e tot un reper, găsit singur pe verticală.
  - Lungimile de grindă se aleg din catalog, cu suprapuneri. Ce rămâne se completează la margini.
- **Marginea:** o fâșie verticală de înălțimea plăcii plus o supraînălțare. Are opțiunea de margine pierdută și opțiunea de a urma conturul cu colțuri închise.
- **Cantitățile:**
  - aria de contact, separat fața de jos și marginile;
  - numărul de popi, pe domenii de înălțime;
  - lungimile de grindă, pe lungimi de catalog;
  - aria de placaj;
  - numărul de refolosiri ale tipului, pentru deviz.

### 2. Cofrajul grinzii (fața de jos, fețele laterale, muchiile)

- **Așezarea:** selectezi grinzile și apeși „Beam formwork”. Sau alegi fețe și muchii: fața de jos, o față laterală sau o muchie de jos (pentru șanfren).
- **Configuratorul tipului:**
  - fundul: panou sau placaj pe grinzi longitudinale;
  - fețele laterale: panouri sau placaj cu rigle;
  - juguri sau cleme, la o distanță maximă după înălțimea grinzii;
  - popii sub fund (distanța);
  - sprijinul lateral;
  - șanfrenul muchiilor: o baghetă triunghiulară de 15 sau 20 mm, pe una, pe amândouă sau pe niciuna dintre muchiile de jos.
- **Contactul:**
  - O față laterală are înălțimea grinzii minus grosimea plăcii turnate odată cu ea. Fața de jos a plăcii e un reper găsit singur prin intersecție.
  - Capetele se scad unde grinda intră în stâlp sau în altă grindă.
  - O grindă la margine are fața exterioară pe toată înălțimea, plus fâșia plăcii.
- **Împreună cu placa:**
  - Cofrajul plăcii vede grinzile de dedesubt și se oprește pe fețele lor laterale.
  - Popii grinzii și ai plăcii sunt numărați separat, dar desenați împreună.
- **Cantitățile:** aria de jos, aria laterală, lungimea de șanfren, popii, jugurile.

### 3. Schela de fațadă

- **Traseul**, în trei feluri:
  - îl desenezi în plan, ca o polilinie;
  - alegi fețele exterioare ale pereților, iar traseul le urmează la distanța de perete a tipului, cu colțurile închise;
  - comanda `SCAFFOLD`.

  Cota de jos este terenul sub fiecare picior (citit din TIN-ul din Civil, cu talpă reglabilă) sau cota unui etaj. Cota de sus este cota de lucru (cornișa, aticul, streașina) plus parapetul.
- **Configuratorul tipului:**
  - sistemul: cadre sau modular;
  - lungimile de travee din catalog (de exemplu 3,07 / 2,57 / 2,07 / 1,57 / 1,09 / 0,73 m);
  - lățimea (0,73 / 1,09 m);
  - înălțimea nivelului de lucru (2,00 m);
  - distanța de perete (0,20–0,30 m);
  - podinele (pe fiecare nivel sau doar pe nivelurile de lucru);
  - parapetul (balustradă dublă, bordură);
  - ancorele (o rețea, de exemplu la 4 m pe orizontală și la două niveluri pe verticală);
  - consolele spre perete;
  - turnul de acces (o travee cu scară, la poziția aleasă);
  - plasa de protecție;
  - colțurile: exterior cu travee de colț sau cu prelungire, interior cu suprapunere.
- **Desfășurarea:**
  - Fiecare segment de traseu se împarte în travei de catalog. Algoritmul alege cât mai puține piese, cu restul mai mic decât cea mai scurtă travee, iar restul se pune la colț.
  - Nivelurile se urcă de la cota de jos, ca la scară.
  - Picioarele urmează terenul, cu talpa reglabilă în domeniul ei. Unde domeniul nu ajunge, se pune un nivel de compensare.
  - Golurile din fațadă (uși, ferestre) nu schimbă schela, dar ancorele le ocolesc.
- **Cantitățile:**
  - aria de schelă (lungimea traseului × înălțimea, cum se măsoară în deviz);
  - piesele: montanți pe lungimi, longrine, traverse, diagonale, podine, tălpi, ancore, console;
  - lungimea de plasă.

## Desenele și cantitățile

- **Vederile de execuție.**
  - Un plan sau un 3D cu filtrul „Show execution” arată cofrajele și schelele.
  - Un stil de vedere nou, „Execution”, le desenează cu creionul lor: placajul cu hașura de cofraj, popii ca puncte sau cruci, schela cu traveile numerotate.
- **Planul de cofraj** este un plan privit de jos, la cota feței de jos a plăcii. Arată grinzile principale și secundare, popii, cotele lor și eticheta sistemului. Tipul de vedere „ceiling plan” (privit în sus) intră în platformă odată cu el.
- **Vederea schelei** e o fațadă cu traveile, nivelurile și ancorele.
- **Extrasele** vin prin `IScheduleSource`: „Formwork” (pe gazdă și pe rol: aria de contact) și „Scaffold components”, pe planșă și în CSV/XLSX.
- **Devizul:** cofrajul și schela devin articole, cu cantitatea din model. Normele RO de cofraj și de schelă se leagă în `cost`, ca celelalte articole.

## Etapele

| Etapa | Ce | Depinde de |
|---|---|---|
| **S1** | Modulul `sitework`, cu tabul și faza de execuție (o creează dacă proiectul nu are una). `IElementFaces` în platformă, cu rolurile plăcii, grinzii, stâlpului și peretelui. Pick-ul pe față în 3D și pe latură în plan, cu `core.faceAt`. | — |
| **S2** | Cofrajul plăcii: tipul cu configuratorul și previzualizarea, desfășurarea grinzi și popi, marginile, cantitățile, IFC (ansamblul, statusul TEMPORARY, resursa). | S1 |
| **S3** | Cofrajul grinzii: fundul, fețele laterale, șanfrenul muchiilor, contactul cu plăcile și stâlpii, desenat împreună cu cofrajul plăcii. | S2 |
| **S4** | Schela de fațadă: traseul desenat sau din pereți, configuratorul, colțurile, terenul, ancorele, turnul de acces, cantitățile. | S1 |
| **S5** | Desenele: stilul Execution, planul de cofraj privit de jos, vederea schelei, extrasele, articolele de deviz. | S2–S4 |
| **S6** | Mai târziu: cofrajele stâlpilor, pereților, centurilor și fundațiilor (panouri și tije), refolosirea pe etaje (seturi și cicluri de turnare legate de faze), cataloagele de producător ca biblioteci. | S3 |

Fiecare etapă vine cu teste în `Lapin.Engine.Tests` (`SiteWorksTests`), cu numere calculate de mână. Exemple:

- O placă de 6 × 4 m, groasă de 0,20 m, cu fața de jos la +2,80 peste o placă la 0,00 dă 24 m² de contact jos și 4 m² de margini.
- Cu grinzile secundare la 0,50 m și principalele la 1,50 m, numărul de popi se verifică pe rețea. Înălțimea popului este 2,80 − placajul − grinzile.
- O grindă de 0,30 × 0,50 m, lungă de 5 m, sub o placă de 0,15 m, dă 1,50 m² jos și 2 × 0,35 × 5 = 3,50 m² lateral. Dacă e grindă de margine, are 0,50 × 5 pe fața exterioară.
- O schelă pe un traseu drept de 10 m pe teren plat, cu traveile de 3,07 / 2,57…, dă numărul de travei și restul, nivelurile și ancorele numărate.

## Decizii luate implicit (de confirmat)

- **S-1.** Modulul e în ediția open, gratuit. Cataloagele de producător, dacă vor fi, vin ca biblioteci.
- **S-2.** Piesele sunt părți ale elementului, nu entități. În IFC sunt componente doar la cerere.
- **S-3.** Reperul este un rol semantic de față, declarat de modulul gazdei, nu o față a B-rep-ului.
- **S-4.** Clasa IFC este `IfcElementAssembly` USERDEFINED (FORMWORK / SCAFFOLD), cu statusul TEMPORARY, plus resursa `IfcConstructionProductResource`. IFC nu are o clasă dedicată pentru cofraj sau schelă.
- **S-5.** Cofrajele și schelele intră în faza de execuție activă. Dacă nu există una, tabul o creează („Execution”) la prima unealtă.
- **S-6.** Ordinea, aleasă de utilizator: schelă → placă → grindă → desene.
