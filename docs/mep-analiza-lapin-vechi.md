# MEP rezidențial: analiza ramurii din vechiul Lapin

26.10.2026. Ramura `feature/residential-mep` a fork-ului ifc-lite (55 de commituri, ultimul `8c7bb244d`, 24.10.2026, nemerged în `lapin`): 265 de fișiere, ~22 000 de linii, toate create de noi (OWN), deci portabile prin rescriere. Documentul ei de proiectare: `docs/architecture/residential-mep.md` pe ramură. Modulul va fi premium (M9, ediția lui).

## Ce există

**Discipline:** încălzire (cazan, radiatoare, trasee tur/retur pereche, propuneri de radiatoare, sugestii de traseu, profil, izometrie), apă (tronsoane reci/calde, armături în linie, distribuitor, necesar, pierderi, tabel), canalizare (coloane, ramuri, curățitoare, ieșiri, ventilații, pantă după diametru), electric (tablou, circuite, prize, lumini, întrerupătoare, cabluri, tabel), ventilație (ventilator, tubulatură rotundă și dreptunghiulară, guri, ramuri, tabel), planul (benzi pe discipline, etichete, legendă, porturi neconectate, coturi prea strânse, export SVG/DXF/PDF/XLSX), verificare IDS.

**Modelul de date (cum e implementat, nu cum era proiectat):**
- Un traseu e un `IfcElementAssembly` cu specificația ca JSON în `Pset_LapinMEPRoute.Spec`: serviciu, mediu, pantă, punctele trunchiului (3D, locale nivelului), ramuri (prinse doar într-un vârf al trunchiului, nu la mijloc de tronson), conexiuni, diametru, plan (pardoseală/tavan/perete), reducții, pas de suporți, gardă, separație, izolație, rază minimă de cot. Fără id-uri de nod: punctele sunt adresate prin index.
- Pereche tur/retur sau rece/cald: un traseu desenat o dată, două țevi decalate ±separație/2 de la aceeași axă; nu pot diverge.
- Echipamentele: 15 feluri, fiecare o cutie cu poziție și rotație plus parametri; porturile nu sunt date, ci se calculează pe fel (cheie, mediu, sistem, sens, poziție, direcție). Echipamentele sunt produse IFC cu `Pset_LapinMEPEquipment` și `IfcDistributionPort` reale.
- Piesele generate (țevi, coturi, teuri, reducții, manșoane, suporți) sunt agregate sub traseu, identificate prin `Tag`, ca să supraviețuiască regenerării.
- Nu există un strat de domeniu separat: modelul IFC e singurul depozit; listarea traseelor parsează JSON-ul fiecărui assembly la fiecare apel. Un al doilea model MEP la nivel de aplicație (`viewer/mep/{types,network,parametric-route}.ts`) e cod mort.

**Fluxul impus de interfață:** ~43 de intrări în panglică (o unealtă de plasare pe fel de echipament, plus unelte de traseu pe discipline). Ordinea: plasezi echipamente → desenezi traseul (ortogonal, cu snap pe centrul porturilor; dublu-clic închide) → eventual ramuri și armături în linie → citești verificările și tabelele. Conectarea se face **doar în timpul desenării**, prin snap la port la primul sau ultimul vârf; nu există o unealtă de conectare ulterioară. Bara de opțiuni: lungime, unghi, diametru, separație, cotă; cu traseul selectat: plan, diametru, separație, izolație, cotă, pas suporți, gardă, cu avertismente de gardă. Dialoguri: propuneri de radiatoare, sugestii de traseu, tabele, porturi deschise, coturi strânse (liste fără selecție/zoom).

**Algoritmi:** piesele sunt deterministe (segment pe tronson, cot la fiecare vârf, teu la ramură, reducții din listă, manșoane la traversări de pereți, suporți la pas). Sugestiile de traseu: Dijkstra pe o grilă ortogonală din capete și colțuri de obstacole, patru moduri de cost, ordonate după penetrări, fitinguri, lungime; doar încălzire, doar primul cazan, doar în plan. Propunerile de radiatoare: sub fiecare fereastră a unui perete exterior, 50 W/m², altfel pe cel mai lung perete exterior. Apă: suma debitelor fără simultaneitate; diametrul din viteză (2 / 1,5 m/s) și 1 kPa/m. Canalizare: pante 1:40 … 1:200 după diametru, ramurile cad în joncțiune. Ventilație: 3 m/s pe ramuri, 5 m/s pe trunchi. Încălzire: niciun calcul de debit sau de țeavă. Coliziuni: distanțe segment–segment și față de pereți, ca note.

**IFC:** doar IFC4/4X3; țevi și tubulaturi ca `IfcSweptDiskSolid`; porturi prin `IfcRelConnectsPortToElement` doar la capetele conectate (nicio conectivitate între piesele unui traseu); un `IfcDistributionSystem` pe traseu, nu pe clădire; reimportul reface traseele editabile din JSON, dar regăsește echipamentele doar din 6 clase (coloane, ventilatoare, guri, electricele rămân cu referințe învechite). Probabil bug: o tubulatură conectată cade în sistemul HEATING și e scrisă ca „domestic water run”.

**Teste:** 28 de fișiere de domeniu (~85 de cazuri), ~41 de fișiere de interfață (~240), un Playwright care doar deschide casa demo.

## Ce e portabil și ce nu

Portabile în spirit, ca matematică și reguli: specificația traseului (validare, decalaje, generarea pieselor), urmărirea la mutare, geometria porturilor, dimensionările (apă, canalizare, ventilație), coturile, armăturile în linie, profilul, căutarea și sugestiile de traseu, propunerile de radiatoare, notele de coliziune. Legate de ifc-lite și de rescris pe contractul de modul al lapin-pro: depozitele (`*-store.ts`), conectivitatea IFC, tot ce listează traseele sau echipamentele din model, comenzile, snap-ul, dialogurile, undo-ul.

## Slăbiciunile modelului vechi

1. **Fără verticale, deci fără coloane de încălzire sau apă:** un tronson cu lungime zero în plan e refuzat; fiecare traseu stă pe un singur nivel. Doar canalizarea are verticalitate, prin coloana-echipament.
2. **Conexiunea e fragilă:** se face doar la desenare, tragerea unui capăt o rupe în tăcere, urmărirea la mutare mută un singur vârf (se pierde ortogonalitatea).
3. **Un traseu = o pereche de țevi; nu există rețea deasupra traseelor:** armăturile în linie taie traseele în bucăți; sistemul IFC e pe traseu.
4. **Mutarea pereților nu regenerează traseele:** manșoanele rămân vechi.
5. **Verificările amestecă nivelurile** (coordonate locale comparate între niveluri) și se recalculează la fiecare randare.
6. **Dimensionări incomplete:** fără debit/ΔT la încălzire, fără simultaneitate la apă.
7. **Performanță:** scanări întregi ale modelului și parsări JSON la fiecare listare; căutări liniare după GlobalId.

## Ce aș schimba la port (de discutat cu fluxul nou)

- **Rețea ca entitate de prim rang** (sistem → trasee → noduri cu id stabil), cu porturile ca date pe elemente, nu calculate; conexiunile ca relații IFC (`IfcRelConnectsPorts`), reconstruite din noduri, nu din indici.
- **Trasee 3D cu verticale** (riser-e între niveluri) pe `PathCurve`-like cu cote pe nod; perechea tur/retur ca două trasee care pot diverge, nu un decalaj fix.
- **Conectare după plasare**, nu doar la desenare: o unealtă „leagă la port” și re-legare la mutare care păstrează ortogonalitatea (mută ultimul tronson, nu doar vârful).
- **Regenerare pe dependențe**, ca restul motorului: traseul citește pereții pe care îi traversează, deci mutarea lor îl reconstruiește (manșoane, coliziuni).
- **Un sistem pe clădire și disciplină**, piesele cu porturi între ele.
- Dimensionările ca servicii separate, cu rezultatele etichetate „estimare”, cum cere documentul vechi.
