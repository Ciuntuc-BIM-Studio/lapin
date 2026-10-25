# Uneltele de teren și peisagistică (tabul Site)

Lapin are terenul ca TIN din puncte topo sau LandXML, curbele de nivel și platformele nivelate în el, cu săpătura și umplutura lor (`spec-terrain.md`). Acest document adaugă uneltele de teren și de peisagistică pe care le aveau Lapin-ul vechi și BubbleBIM Standalone (codul tău, nu ifc-lite): sculptarea terenului, rigolele și șanțurile, căile de circulație, zidurile de sprijin și vegetația. Vocabularul e cel din Standalone (`TerrainWorkspace`: sursă, modelare, pensulă, analiză, peisagistică), implementarea e nouă, în C#, pe TIN-ul lui Lapin. Nimic din ifc-lite.

## Așa s-a făcut (etapa 1, 25.10.2026)

- Cod în `engine/src/Modules/Civil/Lapin.Civil/`: `Sculpt.cs` (tușele, aplicate în `Terrains.Of` peste ridicare), `Ditches.cs`, `Paths.cs`, `RetainingWalls.cs`, `Plantings.cs`, `SiteGeometry.cs` (loft între două secțiuni, prismă orientată, îndesirea traseului, random cu sămânță), `SiteCommands.cs`; teste `SiteTests` (6). Interfața în `ui/src/modules/civil/` (`SiteSections`, `BrushDialog`, `prefs`).
- Comenzi: `civil.addFlatTerrain`, `civil.sculpt {kind, x, y, radius, amount|level|strength}`, `civil.clearSculpt {id, last?}`, `civil.addDitch/updateDitch`, `civil.addPath/updatePath`, `civil.addRetainingWall/updateRetainingWall`, `civil.addPlanting/updatePlanting`.
- Tabul Site: Terrain (din puncte, plat; LandXML), Earthworks (platformă, șanț, ridică, coboară; nivelează, netezește, pensula…), Landscape (alee, zid de sprijin, plantă, zonă de plantare). Pensula are raza, cantitatea, cota de nivelare și tăria într-un dialog; fiecare clic e o tușă.
- Șanțurile și aleile sunt legate de teren prin `IfcRelVoidsElement`, ca platformele, iar terenul le scade corpul și le măsoară săpătura în `Lapin_EarthworkQuantities`. Zidul și plantarea stau pe terenul etajului lor fără relație.
- Planul: aleea și plantarea sunt simbolice (conturul, axul, cercurile coroanelor); zidul se taie ca orice solid; șanțul se desenează pe planul terenului (ax și margini).
- IFC: `IfcPavement` FLEXIBLE în 4.3 și `IfcSlab` USERDEFINED „Pavement” în IFC4; șanțul `IfcEarthworksCut` TRENCH, în IFC4 proxy „Trench”; zidul `IfcWall` USERDEFINED „RETAININGWALL”; plantarea `IfcGeographicElement` VEGETATION în 4.3, USERDEFINED în IFC4. Relația de gol teren → element se scrie doar pentru `IfcEarthworksCut`, în 4.3.

## Principii

1. **Terenul rămâne sursa.** Fiecare unealtă scrie un element parametric (sau o operație pe teren) din care se refac corpul, planul, secțiunile și cantitățile. Nimic nu „coace” geometrie în puncte pe care nu le mai poți schimba.
2. **Elementele se așază pe teren singure.** Aleea, zidul, planta citesc cota terenului unde stau; mutate, se așază din nou.
3. **Săpătura se măsoară.** Tot ce taie terenul (platformă, șanț, alee) dă volumul de săpătură și de umplutură pe element, în `Lapin_EarthworkQuantities`, și totalul pe teren.
4. **IFC-native cât se poate.** Terenul `IfcGeographicElement` TERRAIN, șanțul și platforma `IfcEarthworksCut`, aleea `IfcPavement` (IFC4X3; în IFC4 `IfcSlab` USERDEFINED „Pavement”), zidul de sprijin `IfcWall` RETAININGWALL (în IFC4 USERDEFINED), vegetația `IfcGeographicElement` VEGETATION (în IFC4 USERDEFINED). În model, zidul și plantarea sunt clase Lapin (`LapinRetainingWall`, `LapinPlanting`) cu `exportAs`, fiindcă `IfcWall` și `IfcGeographicElement` au deja câte un proprietar cu altă geometrie.

## Uneltele

| Grup | Unealtă | Element | Ce ține | Geometrie |
|---|---|---|---|---|
| Teren | Teren din puncte, din LandXML (există) | `IfcGeographicElement` TERRAIN | punctele, liniile de rupere, datumul | TIN închis într-un solid |
| Teren | **Teren plat** | idem | patru colțuri la o cotă | pentru proiecte fără ridicare topo |
| Modelare | Platformă (există) | `IfcEarthworksCut` USERDEFINED | conturul, cota | taie și umple terenul |
| Modelare | **Șanț / rigolă** | `IfcEarthworksCut` TRENCH | traseul, adâncimea sub teren, lățimea la fund, panta taluzului 1:n | o secțiune trapez urmărește terenul de-a lungul traseului și se scade din el; volumul de săpătură |
| Pensulă | **Ridică, Coboară, Nivelează, Netezește** | operații pe teren (`sculpts` pe `IfcGeographicElement`) | centrul, raza, cât (m) sau la ce cotă, tăria | fiecare tușă îndesește TIN-ul în cerc (pas rază/4) și mută cotele cu o pondere lină spre margine; tușele se aplică în ordine peste ridicarea topo, deci se pot anula și reface |
| Peisagistică | **Alee / pavaj** | `IfcPavement` | traseul, lățimea, stratul de uzură și stratul de bază (grosimi, materiale), borduri | fâșii cu partea de sus la cota terenului pe ax, pe segmente; terenul e scăzut cu volumul lor; arie, volume, lungime, borduri |
| Peisagistică | **Zid de sprijin** | `LapinRetainingWall` → `IfcWall` | traseul, grosimea, partea înaltă (stânga/dreapta), înălțimea peste terenul înalt sau cota absolută a coronamentului, încastrarea sub terenul jos, talpa (lățime, grosime), materialul | pe fiecare segment, secțiunea în T inversat extrudată; coronamentul în trepte pe teren înclinat; beton, cofraj, lungime |
| Peisagistică | **Plantă** și **Zonă de plantare** | `LapinPlanting` → `IfcGeographicElement` VEGETATION | un punct sau un contur, speciile (nume, fel: arbore / arbust / gazon, înălțime, coroană, pondere), densitatea la 100 m², sămânța | exemplarele se așază determinist (sămânța) în contur și pe cota terenului; arbore = trunchi + coroană stilizată, arbust = coroană joasă, gazon = covor; numărătoarea pe specii |

Analiza (pante, săpătură/umplutură colorate, cote punctuale), tabelul de plantare și exportul LandXML vin într-o etapă următoare.

## Decizii luate implicit (de confirmat)

- Tușele pensulei sunt operații parametrice pe teren, nu puncte modificate: se anulează cu Undo, se șterg din inspector („Clear sculpting”).
- Șanțul taie terenul cu adâncimea sub terenul existent în fiecare punct al traseului (fundul urmărește terenul); o rigolă cu fund la cotă fixă vine după.
- Aleea stă la cota terenului pe ax (plană pe lățime); terenul e scăzut cu corpul ei ca să nu se vadă prin pavaj.
- Zidul de sprijin are coronamentul pe segment la cota terenului înalt plus înălțimea (sau absolut), deci în trepte pe teren înclinat; umplutura din spate nu se modelează încă.
- Plantele sunt stilizate (trunchi și coroană din cilindri), în culoarea speciei; biblioteca de modele 3D vine cu cloud-ul.
- În plan: șanțul și platformele se desenează pe planul terenului (contur „heavy”, ax); aleea și zidul prin corpurile lor; plantele ca cercuri la raza coroanei.

## Coridoarele (configuratorul elementelor pe traseu, decizia 128)

Aleea sau drumul, zidul de sprijin și șanțul sunt elemente pe traseu și au același configurator (selectezi elementul, apoi „Corridor…” din Site sau „Configurator…” din inspector). Modelul e coridorul din Civil 3D și drumurile din BubbleBIM, redus la ce trebuie unei curți sau unui parc; drumurile mari rămân în modulul premium.

- **Stații.** Poziția kilometrică a începutului (`stationStart`) și pasul (`interval`); etichete 0+000.00. Stațiile, vârfurile traseului și rosturile se văd pe profilul longitudinal.
- **Profilul longitudinal.** Linia roșie urmează terenul de sub ax, natural (ridicarea topo) sau propus (după pensulă), sau e proiectată din PVI-uri (stație, cotă, lungimea racordării parabolice). „Fit to natural / proposed ground” pune PVI-urile pe teren (eșantion la 5 m, simplificare Douglas–Peucker la 10 cm, racordări cât încap). Pe zid linia roșie e coronamentul, pe șanț e fundul, pe alee e partea de sus a carosabilului pe ax. PVI-urile se trag cu mausul sau se scriu în tabel; Shift-clic adaugă unul.
- **Profilul transversal.** Aleea e o fâșie de lățimea ei, drumul are două benzi; panta transversală cade spre dreapta, spre stânga sau din coronament; stratul de uzură și cel de bază. Pe fiecare parte, spre exterior: rigolă (lățime, adâncime), bordură (lățime, cât iese deasupra, cât intră sub), trotuar (lățime, panta 2 % spre drum). Zidul și șanțul își păstrează secțiunile lor.
- **Colțurile** se rezolvă singure, în mitră: secțiunea stă pe bisectoarea colțului și se întinde cu 1/cos(jumătate din unghi), cel mult de 4 ori; fâșiile, zidurile și șanțurile trec neîntrerupte.
- **Rosturile** există doar la cerere: la fiecare n metri și/sau la stații date, fiecare cât de lat e cerut (implicit 2 cm). Se văd în plan ca linii transversale și se numără în cantități.
- **Secțiunile.** Configuratorul arată secțiunea transversală la orice stație peste terenul natural (punctat) și propus. Stațiile alese („Section here” sau scrise) și toate stațiile pot deveni vederi de secțiune Lapin, perpendiculare pe ax, la 1:50, cu adâncimea 0,5 m, desenate ca orice secțiune.
- Comenzi: `civil.setCorridor {id, stationStart, interval, sections, joints {every, width, at}, profile {mode, ground, pvis}}`, `civil.profileFromGround {id, ground}`; secțiunea aleii prin `civil.addPath/updatePath {template, laneWidth, crossfall, fall, left, right}`. Interogări: `civil.corridor {id}`, `civil.crossSection {id, station}`.

## Etape

1. **Făcut (25.10.2026).** Teren plat, pensula (patru moduri, cu raza și cantitatea în bara de unelte), șanțul, aleea, zidul de sprijin, planta și zona de plantare; secțiunile din inspector; cantitățile; IFC; teste.
2. **Făcut (25.10.2026):** analiza colorată (pante, săpătură/umplutură, cote în benzi) ca film peste teren în 3D și ca umpleri în plan, cu legenda și ariile pe benzi în inspector (`civil.updateTerrain {analysis}`, `civil.analysis`); cotele punctuale pe plan (`civil.addSpot/removeSpot`, cota terenului proiectat: platformă, șanț, pensulă); parcela pe teren, cu aria și perimetrul în cantități (`civil.setParcel`); exportul LandXML 1.2 al terenurilor (`civil.landXml`, butonul din inspector și din tab); tabelul de plantare ca extras (`IScheduleSource` în platformă: un modul oferă un tabel, extrasele îl arată, îl pun pe planșă și îl exportă CSV/XLSX; `architecture.createSchedule {kind: "planting"}`).
3. **Făcut (25.10.2026), decizia 128:** coridoarele (mai sus): stații, profil longitudinal, profil transversal cu rigole, borduri și trotuare ca piese separate, colțuri în mitră, rosturi, secțiuni la stații; rigola cu fund la cotă e șanțul cu profil proiectat.
4. Rămase: umplutura din spatele zidului, intersecțiile dintre alei, racordarea la teren cu taluzuri a drumului.
