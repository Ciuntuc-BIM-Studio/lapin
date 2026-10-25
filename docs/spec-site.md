# Uneltele de teren și peisagistică (tabul Site)

Lapin are terenul ca TIN din puncte topo sau LandXML, curbele de nivel și platformele nivelate în el, cu săpătura și umplutura lor (`spec-terrain.md`). Acest document adaugă uneltele de teren și de peisagistică pe care le aveau Lapin-ul vechi și BubbleBIM Standalone (codul tău, nu ifc-lite): sculptarea terenului, rigolele și șanțurile, căile de circulație, zidurile de sprijin și vegetația. Vocabularul e cel din Standalone (`TerrainWorkspace`: sursă, modelare, pensulă, analiză, peisagistică), implementarea e nouă, în C#, pe TIN-ul lui Lapin. Nimic din ifc-lite.

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

## Etape

1. Teren plat, pensula (patru moduri, cu raza și cantitatea în bara de unelte), șanțul, aleea, zidul de sprijin, planta și zona de plantare; secțiunile din inspector; cantitățile; IFC; teste.
2. Analiza colorată (pante, săpătură/umplutură), cotele punctuale pe plan, tabelul de plantare, bordurile ca piese, exportul LandXML, umplutura din spatele zidului, rigola cu fund la cotă.
