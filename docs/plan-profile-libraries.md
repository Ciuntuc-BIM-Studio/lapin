# Biblioteci de profile

Strategia aprobată de utilizator (08.11.2026): Lapin nu poate livra toate profilele metalice folosite în lume, așa că utilizatorii **își importă și își gestionează bibliotecile de profile** în formate universale, deschise și răspândite. Lapin livrează un set EN mic, calculat de el; restul vine de la utilizator (AISC Shapes Database, cataloagele ArcelorMittal sau Tata, tabelele EN 10365, bibliotecile IFC ale lui Bonsai, exporturile Tekla / Revit / ArchiCAD, tabelele firmei).

## Ce exista înainte

- `Section` (`Lapin.Structure/Frames.cs`): secțiunea unui stâlp, a unei grinzi sau a unei bare metalice: dreptunghi, cerc, poligon regulat, I (și H), U, T, L, RHS, CHS, C, Z, Σ (formate la rece), în metri, centrată pe cutia ei; `Section.Catalogue` ține ~40 de profile după nume (IPE 200…600, HEA, HEB, UPN, L, RHS, CHS, C, Z, Σ).
- `SectionProperties` (`Lapin.Platform/Analysis`): proprietățile unei secțiuni din geometria ei (integrale pe poligon, racordările de la rădăcină, axa plastică, axele principale verificate pe EN 10056-1), pentru exportul JG; razele de racordare doar pentru numele din catalog.
- `LapinProfile` (`Lapin.Platform/Profiles.cs`, decizia 297): profilele 2D închise ale proiectului pentru baghetele și șanțurile pereților și fundațiile continue (Gestionare → Profile…). Altceva: un contur cu punct de inserție, fără proprietăți de secțiune. Rămâne cum e.
- Importul listelor de prețuri (decizia 306): CSV/XLSX, coloanele ghicite din titluri și potrivite într-un dialog (`PriceLists.Guess`, `XlsxReader`). Modelul pentru importul tabelelor de profile.

## Profilul

`SectionProfile` (`Lapin.Platform/ProfileLibraries`): un profil de secțiune transversală, independent de material.

- **Felul** (`kind`): `I` (I și H, cu tălpi paralele sau înclinate), `asymI` (I nesimetric), `U` (UPN, UPE, C american), `T`, `L` (egal și inegal), `RHS` (și SHS), `CHS`, `C`, `Z`, `SIGMA` (formate la rece, o singură grosime, cu buze), `rectangle`, `circle`, `ellipse`, `outline` (contur arbitrar: inele, exteriorul în sens trigonometric și golurile invers; mai multe contururi exterioare pentru un profil compus).
- **Dimensiunile** (metri): `h`, `b`, `tw`, `tf`, `r1` (racordarea de la rădăcină; la RHS raza exterioară), `r2` (racordarea vârfurilor tălpilor sau ale aripilor), `lip` (buza), `slope` (panta feței interioare a tălpilor, 0,08 la UPN, 0,14 la IPN: `tf` e grosimea la mijlocul tălpii), `b2`, `tf2` (talpa de jos a unui I nesimetric).
- **Identitatea**: numele (designația: IPE 300, W14X90), familia (IPE, HEA, W, HSS…), standardul (EN 10365:2017, ASTM A6…), materialul (`steel`, `aluminium`, `timber`, `concrete`, `pvc`, `other`) și densitatea lui, categoria (laminat la cald, format la rece, lemn lamelat, tâmplărie, prefabricat).
- **Valorile date de fișier** (`props`, în SI): `A`, `Iy`, `Iz` (y axa tare, ca în EN), `Wely`, `Welz`, `Wply`, `Wplz`, `It`, `Iw`, `iy`, `iz`, `mass` (kg/m), `paint` (m²/m).

## Proprietățile

Din dimensiuni, Lapin calculează (`ProfileProps`, peste `SectionProperties`, lărgit cu razele, panta, I-ul nesimetric și conturul arbitrar):

- `A`, `Iy`, `Iz`, `Wel`, `Wpl`, razele de girație, centrul de greutate, axele principale și unghiul lor (cornierele, Z-urile, orice contur nesimetric), cu valorile față de axele geometrice păstrate;
- `It`: formula laminatelor I (cu racordarea), EN 10210-2 anexa B pentru RHS, exact pentru CHS și cerc, `Σ b·t³/3` pentru profilele deschise cu pereți subțiri; nu se calculează pentru un contur arbitrar;
- `Iw`: `Iz·(h − tf)²/4` la I, `hs²·I1·I2/(I1 + I2)` la I nesimetric, formula pereților subțiri la U, coordonata sectorială de-a lungul liniei mediane (polul ales în centrul de forfecare, prin minimizare) la C, Z, Σ; 0 la L, T (pereți care se întâlnesc într-un punct) și la secțiunile închise (neglijabil); nu se calculează la contur;
- masa pe metru din arie și densitatea materialului (oțel 7850, aluminiu 2700, lemn lamelat 470, beton 2500, PVC 1400 kg/m³), suprafața de vopsit pe metru din perimetrul exterior (cu racordările).

Când fișierul dă valori, Lapin le păstrează pe ale fișierului și le compară cu ale lui: o diferență mai mare de 2 % (arie, masă), 3 % (inerții, module) sau 10 % (`It`, `Iw`) intră în **lista de verificare** a importului și a profilului. Un raport aproape de o putere a lui 10 spune că o coloană e probabil în altă unitate.

## Formatele

1. **Tabele CSV/XLSX** (`ProfileTables`): un rând un profil; felul pe rând (o coloană Type/Shape/Tip: W, S, HP, M → I; C, MC, UPN, UPE → U; L; WT, MT, ST → T; HSS → RHS sau CHS după coloane; PIPE → CHS; IPE, HEA… după prefixul numelui) sau pentru tot fișierul (ales în dialog). Coloanele ghicite din titluri în engleză, română, germană și franceză (h/d/depth/Höhe/hauteur/înălțime, b/bf/B/width/Breite/largeur/lățime, tw/s/Stegdicke/âme/inimă, tf/t/Flanschdicke/aile/talpă, r/r1, r2, t/tdes/tnom, A, Iy/Ix, Iz/Iy, Wel/S, Wpl/Z, It/J, Iw/Cw, G/W/mass/Masse/masse/masă…), cu convenția axelor recunoscută (Ix, Sx, Zx fără Iz: tabel american, x axa tare) și unitățile recunoscute din titlu (mm, cm, m, in, ft, cm²/mm²/in², cm⁴/mm⁴/in⁴, cm⁶/dm⁶/in⁶, un factor ×10³ sau ×10⁻³, kg/m, lb/ft) sau, fără ele, din sistemul tabelului (mm și cm²/cm⁴ în EN, in și in²/in⁴ în SUA). Potrivirea se confirmă într-un dialog ca al listelor de prețuri (coloana, unitatea ei, felul, materialul), iar alegerea se ține minte pe calculator pentru aceeași așezare a coloanelor.
2. **IFC** (`Lapin.Ifc/IfcProfiles.cs`, cititorul STEP al lui Lapin, fără dependențe noi): toate subtipurile `IfcProfileDef` dintr-un fișier oarecare (bibliotecile lui Bonsai, exporturile Tekla, Revit, ArchiCAD; profilele folosite de elemente se numesc după tipul lor când n-au nume): `IfcIShapeProfileDef`, `IfcAsymmetricIShapeProfileDef` (IFC4 și IFC2x3), `IfcUShapeProfileDef`, `IfcTShapeProfileDef`, `IfcLShapeProfileDef`, `IfcCShapeProfileDef`, `IfcZShapeProfileDef`, `IfcRectangleHollowProfileDef`, `IfcCircleHollowProfileDef`, `IfcRectangleProfileDef`, `IfcCircleProfileDef`, `IfcEllipseProfileDef`, `IfcArbitraryClosedProfileDef`, `IfcArbitraryProfileDefWithVoids`, `IfcCompositeProfileDef`, `IfcDerivedProfileDef` (ultimele patru și orice profil cu o poziție neidentică devin contururi), cu valorile din `IfcProfileProperties` / `Pset_ProfileMechanical` (IFC4) și `IfcGeneralProfileProperties` / `IfcStructuralProfileProperties` / `IfcStructuralSteelProfileProperties` (IFC2x3), în unitățile proprietății, ale proiectului sau ale lungimii (alese după cea mai apropiată de geometrie); valorile Y și Z perechite cu axa tare și cea slabă după geometrie, oricare ar fi convenția exportatorului. **Exportul**: o bibliotecă sau profilele proiectului ca fișier IFC4 de bibliotecă (`IfcProjectLibrary` cu unitățile în mm, fiecare profil în subtipul lui parametric, conturul ca `IfcArbitraryClosedProfileDef` / `…WithVoids` / `IfcCompositeProfileDef`, `Pset_ProfileMechanical` cu valorile date sau calculate, `Lapin_Profile` cu familia, standardul, materialul și `r2`), citit înapoi identic.
3. **Un contur** desenat în Lapin (Drafting: o polilinie închisă, cu arce; cele dinăuntrul altuia sunt goluri) sau citit dintr-un DXF (polilinii închise, cercuri) → un profil `outline`, centrat pe cutia lui.
4. **Biblioteca Lapin** (`.lapinprofiles`, JSON: `format`, `version`, `name`, `material`, `standard` opțional, `profiles`): formatul în care se păstrează bibliotecile pe calculator și în folderele firmei; se exportă și ca CSV.

## Unde stau bibliotecile

- **Încorporată**: „EN steel (Lapin)”: IPE 80–600, HEA, HEB și HEM 100–600, UPN 80–400, UPE 80–400, corniere egale 20–200 și inegale uzuale, SHS/RHS și CHS formate la cald uzuale (EN 10210-2: raza exterioară 1,5 t, cea interioară t), C, Z și Σ generice; doar dimensiunile standard, proprietățile calculate de Lapin. Nume ca în catalogul de până acum (IPE 300, HEA 200, L 100×100×10, RHS 150×100×6, CHS 114.3×5), cu aceleași dimensiuni, deci tipurile existente rămân aceleași; `Section.From("HEB 240")` găsește acum și profilele bibliotecii încorporate.
- **Pe acest calculator**: bibliotecile importate, în spațiul aplicației (IndexedDB, mai încăpător decât `localStorage`); în desktop și în browser.
- **Folderele firmei** (desktop): oricâte foldere locale, din rețea sau sincronizate, fiecare doar de citit sau și de scris; Lapin citește fișierele `.lapinprofiles` din ele și scrie acolo bibliotecile salvate în folderele de scris.
- **Proiectul**: doar profilele folosite se copiază în proiect (`LapinSectionProfile`: profilul întreg, numele bibliotecii, sursa, ziua), ca proiectul să se deschidă oriunde. Un tip făcut dintr-un profil de bibliotecă ține `section.profileId`. Proiectele vechi: tipurile lor își păstrează secțiunea; cele care au dimensiunile unui profil al bibliotecii încorporate apar ca fiind din el (fără modificarea proiectului).
- **Actualizarea** e explicită: un profil al proiectului diferit de cel cu același nume din aceeași bibliotecă primește semnul „mai nou în bibliotecă”; Actualizează îl înlocuiește, iar tipurile care îl folosesc (și elementele lor) urmează, ca un singur pas de anulare.

## Interfața

- **Gestionare → Biblioteci de profile…**: bibliotecile în stânga (încorporată, pe calculator, folderele firmei, proiectul), profilele pe familii, standard și fel, cu căutare; profilul ales desenat cu cotele lui și tabelul proprietăților (calculat și din fișier, cu diferențele marcate); importul IFC, CSV/XLSX și contur (din desen sau DXF), exportul IFC și CSV, folderele firmei, Actualizează.
- **Alegătoarele de profile**: tipurile metalice (Oțel → Tipuri metalice), tipurile de stâlpi și grinzi (Structură → Tipuri), hala (profilele cadrului și cele secundare) caută în toate bibliotecile după nume și familie; pentru baghetele pereților și fundațiile continue (profilele 2D) un profil de bibliotecă se ia ca exterior, fără goluri; pentru Studio, „Desenează-i conturul” îl pune ca polilinii închise în vederea din față, de extrudat sau derulat; montanții și traversele pereților cortină, tocul și cerceveaua tâmplăriei iau conturul real al profilului ales (vezi mai jos, decizia 417).

## Dincolo de oțel

Același depozit, cu materialul și categoria: lemn lamelat și masiv (dreptunghiuri cu clasa lemnului), profile de aluminiu și PVC pentru tâmplărie și fațade (contururi cu goluri, din DXF-ul producătorului), profile formate la rece, secțiuni prefabricate de beton (contururi). Densitatea vine din material; verificările de rezistență rămân ale modulelor.

## Etape

- **A**: depozitul (profilul, bibliotecile, încorporata), proprietățile calculate și verificarea lor, importul CSV/XLSX cu potrivirea coloanelor, copia în proiect, Gestionare → Biblioteci de profile…, alegătorul în tipurile metalice și de structură.
- **B**: importul și exportul IFC al profilelor (toate subtipurile, proprietățile, unitățile), exportul CSV.
- **C**: folderele firmei, actualizarea din bibliotecă, conturul ca profil (desen, DXF), alegătoarele în hală, pereți cortină, tâmplărie, baghete și Studio.

## Secțiunea reală la LOD 400 (decizia 415)

Corpurile rămân ușoare, cu colțuri drepte, la toate nivelurile: `Session.Geometry` nu se schimbă, deci nici cantitățile, exporturile, proprietățile sau mesh-ul trimis interfeței. Doar o vedere la LOD 400 cere corpul fin: `ElementGeometry.FineShape` spune că producătorul știe mai mult, `GeometryOptions.Fine` îl construiește din `Section.Real()` (racordări, raze la vârfuri, tălpi înclinate, curbele după `Chords`; un nume din catalog cu aceleași dimensiuni ia razele și panta din biblioteca încorporată), iar `Session.FineGeometry` îl ține cât timp stă geometria elementului. Desenele (`Views.Gather`) îl iau la `lod >= 400`, fără o linie pe fiecare fațetă a racordării (`DrawPart.Smooth`, `HiddenLines.Draw(smooth)`: unghiul 3D-ului, `Tessellator.CreaseDeg`). 3D-ul îl cere doar la LOD 400 (`view.fine`, `ui/src/viewport/fineBodies.ts`), o dată pentru fiecare element așa cum e construit, și îl desenează în locul celui ușor; capacele secțiunii (`view.caps {fine}`) îl închid.

Costul măsurat (hala implicită, `MemberFineBodyTests`): 272 de bare, dintre care 94 cu secțiune reală (celelalte sunt formate la rece, îndoite drept, și rămân la fel): 11 326 de triunghiuri cu colțuri drepte, 18 816 la LOD 400 (×1,66), construite în 74 ms; la orice alt nivel, nimic în plus. Alegeri: exporturile și alegerea în plan rămân pe corpul ușor (cantitățile: vezi decizia 419); în 3D la LOD 400 alegerea lovește corpul desenat, același element.

## Profilul parametric în IFC-ul modelului (decizia 416)

`IfcSweep.Parametric`: grinzile și stâlpii drepți (`FrameIfcBodies`, oricare, și de beton), barele metalice și halele (`SteelBody.Swept`) se scriu ca `IfcExtrudedAreaSolid` din subtipul parametric (`ProfileIfc.Parametric`: I, I nesimetric, U, T, L, C cu buze, Z fără buze, RHS, CHS, dreptunghi, cerc, elipsă), cu `ProfileName` și `Pset_ProfileMechanical` (valorile fișierului dacă le-a dat, altfel ale lui Lapin; în SI, cu unitățile spuse pe proprietate), o singură definiție pentru tot ce o împarte. Contururile, poligoanele, Σ, Z cu buze și C fără buze rămân arbitrare, cu numele lor.

**Așezarea**: secțiunile lui Lapin sunt centrate pe cutia lor, ca profilele parametrice ale IFC, și L, U, C, Z stau la fel (inima la stânga, aripile la dreapta, T cu talpa sus): `Position` rămâne identitatea. O secțiune răsturnată (`flipped`) nu are imagine în oglindă în IFC: profilul se extrudă invers din capătul celălalt, cu x inversat, ceea ce e chiar oglinda (`Section.Sweep`).

**Corpul**: `FilletRadius`, `EdgeRadius` și `FlangeSlope` se scriu când profilul le are, ca adevăr despre profil; alte programe vor desena racordările. Cititorul lui Lapin (`IfcModel.Profile`) desenează profilele cu colțuri drepte, deci același corp ca elementul; excepție I-ul nesimetric, pe care Lapin îl ține ca un contur cu racordări și îl citește la fel. Corpurile stocate au coordonatele la 0,1 mm (`StoredBodies`). Un element tăiat (ordinea de tăiere, un stâlp prin grindă, o îmbinare în ungi) rămâne B-rep (`SweepIsBody`: un singur corp, același volum și aceeași cutie). O grindă rotundă rămâne B-rep: corpul ei e banda.

**Recunoașterea** nu se schimbă: dreptunghiurile și cercurile dintr-un fișier străin devin native și trec poarta de fidelitate; un I, L, U… rămâne referință cu corpul fișierului (grinzile și stâlpii profilați nu se recunosc încă în secțiuni native). `IfcCycleTests` cuprinde acum un L răsturnat, un UPN și un HEA rotit: fără derivă.

Tot aici: C și Z fără buze aveau lățimea mai mică cu o grosime decât spuneau; acum aripa ajunge la lățime (`Section.Centreline`, `SectionProperties`), deci `B` e a toată secțiunea și se citește înapoi la fel.

## Conturul real în fațade și tâmplărie (decizia 417)

`CurtainSection.Profile` (montanți, traverse) și `JoinerySpec.FrameSection`, `SashSection` țin secțiunea profilului (JSON-ul ei, cu `profileId`, deci urmează Actualizează din bibliotecă; lățimea și adâncimea se iau din ea la citire). Fără profil nimic nu se schimbă. `MemberOutline.Of` dă corpurile (exterior, goluri) cu racordările după `Chords`.

- **Orientarea**: x-ul profilului de-a curmezișul barei, spre geam la toc și cercevea (oglindit de la un montant la celălalt, sus și jos la fel), în planul peretelui la fațadă (în sus la traverse); y-ul spre exteriorul clădirii (`+Y` al tâmplăriei, dreapta peretelui la fațadă). Adâncimea profilului trece peste perete, ca a dreptunghiului.
- **Colțurile** ca până acum: montanții plini, traversele între ei; montanții de toc plini, buiandrugul și pragul între ei, tăiate drept.
- **Desenele**: planul și secțiunea taie corpul (fațadele), simbolurile tâmplăriei din plan și secțiune desenează conturul (`JoineryBox.Member`), 3D-ul prin `Openings.Box`. La orice nivel de dezvoltare, fiindcă e profilul ales de utilizator.
- **Cu sistemele de profile (decizia 370)**: rămân împreună. Sistemul dă mărimile de pornire și formele fine (opritor, buză, baghete, lăcrimar); un profil de bibliotecă înlocuiește bara lui și ce e forma ei reală (tocul: opritorul și lăcrimarul; cerceveaua: buza și lăcrimarul), baghetele de geam rămân, ca piese separate. Alegerea unui sistem șterge profilele din bibliotecă; alegerea unui profil păstrează sistemul pentru cealaltă bară.
- **Limite**: capetele curbe, ferestrele rotunde și panourile cu arc rămân o bandă simplă; montanții și traversele grilei tâmplăriei și foaia ușii rămân cutii; golul din fereastră se face după cutie, deci geamul se oprește la marginea interioară nominală. IFC-ul tâmplăriei și al fațadelor rămâne B-rep, cu forma reală.

## Cantitățile după aria reală (decizia 419)

`Section.Area` e aria reală (cu racordări, raze la vârfuri și pante, ca proprietățile: un IPE 300 are 53,81 cm², nu 51,88); `SharpArea` e a conturului cu colțuri drepte, cel din corpul desenat; `RealRatio` e raportul lor (1 fără raze). Secțiunea fără raze nu se schimbă. Stâlpii, grinzile și barele metalice măsoară `CrossSectionArea` și `GrossVolume` din aria reală, iar `NetVolume` (deci greutatea) din volumul corpului ușor × `RealRatio`: pentru o bară dreaptă netăiată e exact aria reală × lungimea, pentru una tăiată, în ungi sau cu copes, corpul așa cum e, scalat (aria secțiunii e singura care diferă). Din aceleași numere iau extrasul pe materiale, Lista barelor metalice (kg/m și greutatea fiecăreia), greutățile halei, devizul (articole pe volum) și durațiile planului (cantitate × normă); proprietățile analitice (JG) citeau deja racordările. Poarta de fidelitate compară geometria, nu cantitățile, și elementele recunoscute (dreptunghi, poligon, cerc) n-au raze: nu se schimbă nimic.

## Standardele (decizia 457)

`ProfileStandards`: grupul sub care apare un profil e standardul bibliotecii (`standard` în `.lapinprofiles`; cea încorporată e „EN”), altfel cel recunoscut din standardul profilului (`Group`: EN, DIN EN, SR EN, PN-EN → EN; ASTM, AISC → AISC / ASTM; BS, DIN, JIS, GB, AS/NZS, CSA, IS, GOST), altfel numele bibliotecii. Interfața are aceeași regulă (`standardGroup`, `groupOf` în `model.ts`, cu aceleași cazuri în teste). Alegătoarele: profilele proiectului, apoi fiecare standard sub titlul lui; butoanele din capul listei ascund un standard pentru utilizator (`profileStandardsHidden` în preferințe). Proiectul: `profileStandards` pe IfcProject (lipsă: toate), `profiles.setStandards`, bifat în Gestionare → Biblioteci de profile; un proiect nou îl primește din `newProjectProfileStandards` (Preferințe → Profile) prin `project.new {profileStandards}`. Copiile proiectului țin și standardul (`standard`). Proiectul nu încarcă biblioteci: doar profilele folosite se copiază (vezi mai sus), deci alegerea nu schimbă mărimea fișierului.

**AISC Shapes Database**: nu se livrează. AISC o pune la dispoziție gratuit, dar includerea într-o aplicație cere contractul lor de licență pentru dezvoltatori (cu condiții transmise utilizatorilor), incompatibil cu o distribuție MIT fără acord; utilizatorul o descarcă și o importă cu Tabel… (coloanele ei sunt recunoscute).

## Limite

- `It` și `Iw` nu se calculează pentru un contur arbitrar (rămân ale fișierului, dacă le dă); `Iw` la U e aproximarea pereților subțiri (UPN: cu 15 % peste tabel, din cauza tălpilor înclinate).
- Folderele firmei doar în desktop (browserul nu ajunge la foldere).
- Nicio bibliotecă a unui terț nu e în depozit: testele folosesc fișiere mici generate.
