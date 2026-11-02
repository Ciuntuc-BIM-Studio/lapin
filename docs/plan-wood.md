# Modulul `wood`: pereți pe cadre din lemn și CLT

Cerut de utilizator la 02.11.2026: un modul independent, numit `wood`, pentru construcția din lemn (timber framing și CLT), condus din configurator. Întrebarea grea e cum se tratează golurile de uși și ferestre, de aceea are capitolul ei, primul. Decizia 239 în `decisions-pending.md`: aprobată la 02.11.2026 cu propunerile de la sfârșit; etapa 1 făcută (vezi rândul ei).

## Granița modulului

- `wood` e un modul al lui, în `engine/src/Modules/Wood/Lapin.Wood` și `ui/src/modules/wood`, cu tabul **Wood**. `requires: core, architecture`: lucrează pe pereții, plăcile și acoperișurile arhitecturii, nu le înlocuiește.
- Pereții rămân `IfcWall` cu tipul și straturile lor (`IfcMaterialLayerSetUsage`). Ce aduce `wood` e **cum e construit** stratul-miez: un tip de perete primește `construction: timberFrame | clt` și regulile lui (ca etrierii pe tipurile de grinzi și stâlpi, decizia 215). Fără `wood`, același proiect se deschide și se desenează ca înainte: pereți cu straturi.
- Toate piesele (montanți, tălpi, buiandrugi, panouri CLT) sunt **derivate**, nestocate: se refac din perete, din golurile lui și din regulile tipului, ca șarpanta acoperișului (decizia 229). Se stochează doar regulile și excepțiile.
- Secțiunile, clasele de lemn și extrasul de debitare ale șarpantei (`Roof.Section`, `TimberGrades`, `RoofTimberSchedule`) trec într-un catalog comun de lemn. Șarpanta rămâne în arhitectură, dar folosește același catalog, iar extrasul devine unul singur: **Timber**, cu toate piesele de lemn ale proiectului.

## Golurile de uși și ferestre

### Principii

1. **Golul rămâne al arhitectului.** Ușa sau fereastra (`IfcDoor`, `IfcWindow`), cu `IfcOpeningElement` în perete și configuratorul de tâmplărie, nu se schimbă. `wood` doar le citește, cum citesc buiandrugii regula lor (`LintelRule`). Mutată sau schimbată, fereastra își reface cadrul singură.
2. **Golul brut (rough opening, RO)** e tâmplăria (`JoinerySpec`: lățimea, înălțimea, parapetul) plus jocurile de montaj din regulă: lateral, sus, jos. Implicit 10–15 mm de fiecare parte; la o ușă, 0 jos (pragul stă pe placă). Opțional, RO se rotunjește la un pas (5 sau 10 mm). Tot cadrul se măsoară de la RO, nu de la tâmplărie.
3. **Regula golurilor e pe tipul de perete**, pe trepte de lățime a RO (de exemplu ≤ 900, ≤ 1800, peste). Fiecare treaptă spune:
   - montanții întregi de lângă gol (*king studs*: 1 sau 2);
   - montanții de sprijin ai buiandrugului (*jack/trimmer*: 0, 1 sau 2; 0 cu piese metalice de rezemare);
   - buiandrugul (*header*): nimic (o riglă simplă, la un perete neportant), masiv simplu sau dublat, LVL, lemn lamelar, sau buiandrug-cutie izolat. Secțiunea e dată sau luată din tabelul de buiandrugi după deschidere. Rezemarea e lungimea pe jack-uri;
   - pervazul brut (*rough sill*: simplu sau dublu) și montanții scurți dedesubt (*cripples*);
   - montanții scurți deasupra buiandrugului până la talpa de sus;
   - riglele (*noggins/blocking*) peste buiandrug și sub pervaz, când e nevoie de prindere pentru plăci.
4. **Excepția stă pe gol.** O fereastră anume poate cere altceva: alt buiandrug, trei king-uri, fără pervaz. Suprascrierea e un atribut pe deschidere (`woodFraming`), legată de GlobalId-ul ei, ca să supraviețuiască exportului și reimportului IFC.
5. **Peretele portant sau neportant** se alege pe tip, cu suprascriere pe perete. Peretele neportant ia buiandrugul simplu. Cel portant ia tabelul de buiandrugi (editabil, pe secțiuni și clase). Verificarea EC5 a buiandrugului după încărcare e premium.

### Montanții și golul

6. **Modulul montanților** e o grilă la interaxul tipului (600, 625 sau 400 mm) pornită de la un punct de referință:
   - capătul peretelui (implicit);
   - colțul clădirii sau originea proiectului, ca toți pereții unei fațade să aibă același modul.
   
   Montanții care cad în RO dispar. Montanții scurți de deasupra și de dedesubt **păstrează modulul**, ca rosturile plăcilor de OSB și gips să cadă tot pe montanți; opțional, modulul reîncepe de la gol.
7. **Coliziuni.** Un montant de modul care ar sta la mai puțin de distanța minimă de un king (implicit 50 mm, din regulă) se contopește cu el sau dispare. Nu rămân două piese lipite fără rost.
8. **Golurile apropiate.** Dacă spaletul dintre două goluri e mai îngust decât minimul regulii (de obicei king + montant + king), cele două primesc un **buiandrug comun** și king-uri comune la capete. Se poate și cere explicit, pe gol.
9. **Golul lângă colț sau lângă un perete în T.** Dacă rămâne prea puțin pentru colț plus king, apare un **avertisment** în verificări, cu un clic pe gol. Golul nu se mută singur. Există o comandă *Pune golul pe modul* care îl deplasează, ca un pas de Undo, la fel la cerere: *aliniază toate golurile acestui perete*.
10. **Golurile mari** (uși glisante, vitrine): sunt aceleași reguli, cu buiandrugul din treapta cea mai mare (lemn lamelar, LVL). Dacă depășește tabelul, apare avertismentul „buiandrug de dimensionat” și o secțiune aleasă de mână.

### Straturile din jurul golului

11. **Plăcile** (OSB, plăci fibrolemnoase, gips-carton) se taie pe RO, cu retragerea din regulă. Plăcile se așază pe modul, cu rosturile pe montanți. Rostul de la colțul golului poate fi interzis (fisuri la gips): atunci placa se taie în L.
12. **Izolația** umple golurile dintre montanți, fără golurile de tâmplărie, și se numără ca volum.
13. Detaliile de montaj ale tâmplăriei (benzi, glafuri, solbancuri) rămân ale tâmplăriei (decizia 127).

### Golurile în CLT

14. Golul e **tăiat din panou** (CNC), iar buiandrugul e chiar panoul: nu sunt piese adăugate. Regulile tipului CLT:
    - distanța minimă de la gol la marginea panoului;
    - unde pot cădea rosturile dintre panouri: nu prin gol, de preferat aliniate cu marginile golului (un *panou de buiandrug* deasupra, unul de parapet dedesubt), sau gol decupat dintr-un panou întreg;
    - raza colțurilor golului (raza frezei, 10–20 mm);
    - jocurile de montaj, la fel ca la cadre.
15. Peste o anumită deschidere, apare avertismentul „verificare statică”. Dimensionarea e premium.

### Configuratorul de goluri

16. În configuratorul tipului de perete, tabul **Goluri** are:
    - tabelul treptelor (lățime RO → king, jack, buiandrug, pervaz, cripples);
    - jocurile de montaj și distanța minimă;
    - o **elevație de probă**: un perete de lungimea aleasă, cu o fereastră 1200 × 1500 la 900 și o ușă 900 × 2100, desenat de motor cu toate piesele cotate. Se schimbă la fiecare câmp, ca în configuratorul de acoperiș (`architecture.roofPreview`).
17. Pe o ușă sau fereastră selectată, panoul de proprietăți are secțiunea **Wood**: ce treaptă ia, ce iese (buiandrugul 2 × 45 × 195 C24, 2 king, 1 jack) și butonul *Suprascrie*, care deschide aceleași câmpuri doar pentru golul acela.

## Peretele pe cadre

- **Tipul** spune:
  - secțiunea montanților (45 × 145, 45 × 195…), interaxul și clasa lemnului;
  - talpa de jos (simplă; opțional peste o talpă de egalizare pe placă) și talpa de sus (simplă sau dublă, cu îmbinările decalate);
  - rândurile de rigle (la înălțimi date sau la jumătate);
  - stratul-miez, care devine „zona montanților” (izolația între ei).
- **Colțurile și îmbinările în T**, din regulă:
  - colț din trei montanți, colț „California” (izolabil) sau colț în L;
  - la T: montant dublu sau rigle-scară (*ladder blocking*) pentru peretele care vine.
  
  Îmbinările pereților există deja (`WallJoins`): `wood` află din ele cine trece și cine se oprește.
- **Capetele** de perete au montant dublu sau simplu.
- **Panourile prefabricate** (etapa a doua): peretele se împarte în panouri de transport (lungimea și înălțimea maximă din regulă), cu rosturile pe montanți, niciodată deasupra unui gol. Fiecare panou are marca lui și o elevație de atelier cotată.

## Planșeele, acoperișul și CLT

- **Planșeele pe grinzi** (grinzi masive sau în I, cu interax, centura perimetrală, rigle):
  - golurile de scară primesc grinzi de schimb (*trimmer*) și grinzi de capăt (*header*), după aceleași reguli de trepte;
  - golurile din placă (`IfcSlab`) le dau poziția.
- **Acoperișul** rămâne șarpanta din arhitectură (decizia 229), cu catalogul comun.
- **CLT**: pereți, planșee și acoperiș din panouri.
  - Tipul spune alcătuirea: 3, 5 sau 7 straturi, grosimea lamelelor, clasa (C24, T14), direcția straturilor exterioare.
  - Dimensiunile maxime ale panoului vin de la producător (de exemplu 3,5 × 16 m).
  - Îmbinările dintre panouri: cap la cap, cu falț pe jumătate sau cu lambă.
  - `wood` propune împărțirea în panouri; utilizatorul mută rosturile cu gripuri.
  - Conectorii (corniere, ancore, șuruburi) și exportul CNC sunt premium.

## IFC, extrase, vederi

- **IFC**: piesele unui perete pe cadre sunt `IfcMember` cu rolul lor (STUD, PLATE, MEMBER pentru rigle, USERDEFINED pentru buiandrug și pervaz), sub `IfcWall`. Un panou prefabricat e `IfcElementAssembly`. Un panou CLT e `IfcBuildingElementPart` cu materialul lui pe straturi, sub perete sau sub placă. Pe fiecare piesă: setul `Lapin_Timber` (de la șarpantă) plus marca panoului.
- **Extrase**:
  - **Timber**: lista de debitare comună, pe piesă, secțiune, clasă și lungime;
  - **Sheathing**: plăcile, cu numărul de foi întregi și pierderile;
  - **Insulation**: volumul izolației;
  - **CLT panels**: marcă, dimensiuni, alcătuire, arie, volum.
- **Vederi**:
  - *elevația de cadru* a unui perete, ca vedere a modulului, cu cote pe montanți și goluri;
  - în plan, la detaliu fin, montanții tăiați;
  - planul de panouri.

## Etape

1. **Peretele pe cadre, cu golurile**: regulile pe tip, montanții pe modul, tălpile și riglele, golurile după regulile 1–10, colțurile și T-urile, IFC, extrasul Timber, verificările. Plus configuratorul tipului cu elevația de probă și secțiunea Wood pe ușă și fereastră.
2. **Plăcile și izolația** (regulile 11–12), extrasele lor, elevația de cadru ca vedere și pe planșă.
3. **Planșeele pe grinzi**, cu golurile de scară.
4. **Panourile prefabricate** și elevațiile de atelier.
5. **CLT**: alcătuirile, împărțirea în panouri, golurile (regulile 14–15), extrasul de panouri.
6. **Premium**: dimensionarea EC5 a buiandrugilor și a montanților, conectorii, exportul CNC (BTLx), optimizarea tăierii.

## Stadiul (02.11.2026)

Etapa 1 făcută (decizia 239), colțurile, T-urile, plăcile și elevația de cadru (decizia 240). Etapele 3 și 4 trec deocamdată la premium (decizia 240): în ediția liberă planșeele pe grinzi și panourile se fac manual. Următoarea: CLT (etapa 5).

## De decis

- Ce e liber și ce e premium. Propunerea: etapele 1–5 libere (în `lapin`), etapa 6 premium, într-un depozit-frate ca `rebar-pro`.
- Practica de pornire pentru tabelele implicite (secțiuni, interaxe, buiandrugi): cea nordică, cea românească sau ambele, ca presetări.
- Punctul de referință implicit al modulului montanților: capătul peretelui sau colțul clădirii.
