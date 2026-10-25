# IFC: export și import

Lapin modelează pe schema IFC, dar până acum un proiect nu putea ieși din el decât ca desen (PDF, DXF) sau ca pachet `.lapin`. Acest document spune ce scrie exportul IFC pentru fiecare entitate și relație din model, ce se refolosește din BubbleBIM Pro, cum se importă înapoi, și în ce ordine se face. Schimbul cu BubbleBIM trece prin același IFC: fișierul poartă și intenția de proiect (`plan.md`, „Compatibilitatea cu BubbleBIM”).

Întrebările au fost marcate **[Î1]** … **[Î6]**; răspunsurile stau lângă ele („Decis”) și în `decisions-pending.md` (#114). Toate sunt decise (25.10.2026). **Etapele 1 și 3 sunt implementate** (25.10.2026): `engine/src/Platform/Lapin.Ifc`, comenzile `project.exportIfc` și `project.importIfc`, meniul File → „Export IFC…” / „Import IFC…”, testele `IfcExportTests` și `IfcImportTests`. Ce diferă față de text e notat la „Așa s-a făcut”.

## Principii

1. **Fișierul se deschide oriunde.** Fiecare element are geometria lui IFC (corp B-rep), plasarea, materialul, proprietățile comune IFC și cantitățile cu numele IFC. Un program care nu știe nimic de Lapin vede o clădire corectă.
2. **Intenția călătorește în același fișier.** Ce face elementul editabil în Lapin (axa peretelui, straturile, regula de buiandrug, conturul și panta acoperișului, specificația tâmplăriei) se scrie ca reprezentare `Axis`, ca `IfcMaterialLayerSetUsage` și ca set de proprietăți de intenție. Lapin, și mai târziu BubbleBIM, reconstruiesc din ele elemente native.
3. **GlobalId-urile sunt stabile.** Entitățile modelului au deja GlobalId IFC ca id. Piesele derivate, relațiile și seturile de proprietăți primesc GlobalId-uri derivate determinist din GlobalId-ul părintelui și o cheie (`GlobalId.FromGuid(MD5(id + "\n" + cheie))`, ca `Step.Guid` din BubbleBIM). Două exporturi ale aceluiași proiect dau aceleași id-uri, deci un al doilea import actualizează în loc să dubleze.
4. **Geometria e cea pe care o calculează deja modulele.** Exportul nu are geometrie proprie: ia `ElementGeometry` (piesele `GeometryPart` cu `Solid`-ul lor B-rep) de la producătorii modulelor, prin sesiune, și o scrie ca `IfcFacetedBrep` (fețe plane cu găuri, cum le ține `Solid`). Nu se teselează în triunghiuri decât acolo unde B-rep-ul nu e închis.
5. **Metri, coordonatele proiectului.** Unitățile sunt metri, metri pătrați, metri cubi și radiani (în proprietăți unghiurile rămân în grade, ca în Lapin). Geometria stă în coordonatele proiectului, fiecare element plasat relativ la etajul lui (plasarea etajului e la cota lui, deci corpurile au z-ul scăzut cu cota); `TrueNorth` al contextului e +Y rotit cu `northAngle` în sens trigonometric; punctul de bază și cel topografic (`Location`) se scriu ca `IfcMapConversion` + `IfcProjectedCRS` când există un punct topografic, și ca `RefElevation`/`RefLatitude` pe sit.

## Ce se refolosește din BubbleBIM Pro

Din `vendor/bubblebim-pro/engine/src/Modules/Ifc/BubbleBim.Ifc`, copiate și adaptate (cod propriu al utilizatorului):

| Fișier | Ce e | Cum intră în Lapin |
|---|---|---|
| `Step.cs` (89 linii) | scriitorul STEP: numerotarea entităților, string-uri cu `\X2\`, reali, enumerări, liste, `Guid(seed, key)` | `Lapin.Ifc/Step.cs`, neschimbat în esență; `FILE_NAME` cu „Lapin” |
| `StepFile.cs` (213) | cititorul STEP: entități, referințe, string-uri decodate | `Lapin.Ifc/StepFile.cs`, pentru import |
| `IfcModel.cs` (689) | un fișier IFC citit și înțeles: unități, plasări, corpuri ca mesh-uri, conținere, psets, culori, goluri | `Lapin.Ifc/IfcModel.cs`, pentru importul ca referință și pentru citirea intenției |
| `IfcExport.cs` (287) | tiparul exportului: contexte, unități, arbore spațial, B-rep cu stiluri, psets comune, cantități, grilă | tiparul lui, rescris pe modelul Lapin (BubbleBIM are un graf de noduri cu `NodeTypeDescriptor`; Lapin are entități IFC și relații) |

Biblioteca nouă e `engine/src/Platform/Lapin.Ifc`, folosită de `core` (orice ediție exportă și importă). Comenzile: `project.exportIfc {path, schema?, detail?}` și `project.importIfc {path, mode}`; în interfață, meniul File are „Export IFC…” și „Import IFC…”.

## Schema **[Î1]**

**Decis (25.10.2026):** IFC4 implicit, IFC4X3 la cerere, din același cod (`schema` în comandă). Modelul Lapin e numit IFC4X3, dar aproape tot ce scrie există și în IFC4. **IFC4 (ADD2 TC1) implicit**, fiindcă îl citesc bine toate programele (Revit, ArchiCAD, Solibri, BIMcollab, Blender BIM), și **IFC4X3_ADD2 la cerere**, singurul care are terenul ca `IfcGeographicElement.TERRAIN` și platformele ca `IfcEarthworksCut`. Diferențele de scriere sunt puține (BubbleBIM le face deja: `IfcCartesianPointList3D` cu TagList, descrierea vederii). În IFC4, terenul iese ca `IfcGeographicElement` fără tip predefinit, iar platformele ca `IfcBuildingElementProxy` cu `ObjectType` „Earthworks cut”.

## Arborele spațial și proiectul

| Lapin | IFC | Note |
|---|---|---|
| `IfcProject` | `IFCPROJECT` | numele proiectului; `IfcUnitAssignment`; contextul `Model` și subcontextele `Body` și `Axis` |
| `IfcSite` | `IFCSITE` | `RefElevation` din cota măsurată a lui ±0,00 (`location.base.elevation`); latitudine/longitudine când vor exista |
| `IfcBuilding` | `IFCBUILDING` | |
| `IfcBuildingStorey` | `IFCBUILDINGSTOREY` | `Elevation`; plasarea etajului la cota lui, elementele plasate **relativ la etaj** |
| conținerea | `IFCRELAGGREGATES` (proiect → sit → clădire → etaje), `IFCRELCONTAINEDINSPATIALSTRUCTURE` (etaj → elemente) | id-urile relațiilor derivate din etaj |
| `Location` (punct de bază, topografic) | `IFCMAPCONVERSION` + `IFCPROJECTEDCRS` pe contextul `Model` | doar când există punctul topografic; nordul adevărat ca `XAxisAbscissa/Ordinate` |
| `drafting`, `hatchPatterns`, `graphicsSets`, vederi, planșe, extrase | nu se exportă | sunt ale Lapin-ului; rămân în `.lapin` |

## Elementele

Fiecare element: `GlobalId` = id-ul lui, `Name`, `ObjectType` = numele tipului (când are), `ObjectPlacement` relativ la etaj, `Representation` cu `Body` și, unde e cazul, `Axis`, `Tag` = id-ul Lapin. `PredefinedType` cel al entității.

| Lapin | IFC | Corp | Intenție | Note |
|---|---|---|---|---|
| `IfcWall` + `IfcWallType` + `IfcMaterialLayerSetUsage` | `IFCWALL` + `IFCWALLTYPE`, `IFCRELDEFINESBYTYPE`, `IFCRELASSOCIATESMATERIAL` cu `IFCMATERIALLAYERSETUSAGE` pe perete și `IFCMATERIALLAYERSET` pe tip | prisma straturilor, **netăiată de goluri** **[Î2]**; opțional piesele | `Axis`: `IFCPOLYLINE` cu cele două capete, în plasarea peretelui; înălțimea, offsetul de bază, stilul și prioritatea îmbinărilor în psetul de intenție | `Pset_WallCommon`: `IsExternal` (nu îl știm pe perete: nu se scrie), `LoadBearing` (din miez: stratul `Core`) |
| îmbinări (`IfcRelConnectsPathElements`) | `IFCRELCONNECTSPATHELEMENTS` | — | `RelatingConnectionType`, `RelatedConnectionType`, `Description` = stilul (`Mitre`), prioritatea în intenție | capetele tăiate sunt deja în corp |
| zone pe înălțime, centura, buiandrugii, centura coborâtă (piese `zone:*`, `lintel:*`, `downstand:*`) | **[Î2]**: ca `IFCBUILDINGELEMENTPART` (zone) și `IFCBEAM` RING_BEAM / LINTEL (centuri, buiandrugi), agregate sub perete cu `IFCRELAGGREGATES`; sau doar ca cantități și intenție. Așa s-a făcut: centura e `IFCBEAM` USERDEFINED cu `ObjectType` „Ring beam” (nici IFC4, nici 4.3 nu au RING_BEAM); buiandrugul LINTEL; zonele și straturile unui perete cu zone, `IFCBUILDINGELEMENTPART`. Un perete devine grup și când are doar buiandrugi (o ușă într-un perete cu regulă de buiandrug). | fiecare piesă cu corpul ei tăiat, cum e | specificația zonelor pe tip, regula de buiandrug pe tip sau pe tipul de tâmplărie | GlobalId din perete + cheia piesei |
| `IfcOpeningElement` | `IFCOPENINGELEMENT` + `IFCRELVOIDSELEMENT` | cutia golului (`Openings.Cutter`) | `along`, `sill` în intenție | |
| `IfcDoor` / `IfcWindow` + tipurile lor | `IFCDOOR` / `IFCWINDOW` (`OverallHeight`, `OverallWidth`, `OperationType` din panouri) + `IFCDOORTYPE` / `IFCWINDOWTYPE`, `IFCRELFILLSELEMENT` | piesele tâmplăriei (toc, cercevele, geam, foi) | specificația configuratorului (JSON) în intenție, pe tip; flip-urile pe element | `Pset_DoorCommon` / `Pset_WindowCommon`: `IsExternal` (de pe tip), `Reference` = marca; `IsExternal` e al tipului (#71) |
| `IfcSlab` + `IfcSlabType` + straturi | `IFCSLAB` FLOOR + `IFCSLABTYPE`, usage `AXIS3` | prisma straturilor, netăiată de golurile ei; golurile ca `IFCOPENINGELEMENT` | conturul în intenție | |
| `IfcRoof` | `IFCROOF` fără corp propriu, cu `IFCRELAGGREGATES` peste: `IFCSLAB` ROOF pe fiecare plan (învelitoarea tăiată pe plan), `IFCMEMBER` RAFTER/PURLIN/PLATE pentru șarpantă, `IFCPIPESEGMENT`/`IFCMEMBER` pentru jgheaburi și burlane, `IFCDISCRETEACCESSORY` pentru parazăpezi, `IFCWINDOW` SKYLIGHT pentru ferestrele de acoperiș, lucarnele ca `IFCWALL` + `IFCROOF` | piesele acoperișului | conturul, forma, panta, consolele, regulile pe laturi, stilul, structura, deschiderile în intenție | în prima etapă: un singur `IFCROOF` cu toate piesele ca un corp (vezi etapele) |
| `IfcColumn`, `IfcBeam` + tipurile | `IFCCOLUMN`, `IFCBEAM` + `IFCCOLUMNTYPE`, `IFCBEAMTYPE`; așa s-a făcut: tipul are materialul lui ca `IFCMATERIAL` direct, profilul stă în intenție (`IFCMATERIALPROFILESET` mai târziu) | corpul tăiat de priorități | `at` + `rotation` (grade), axa grinzii, în intenție | `Pset_ColumnCommon`/`Pset_BeamCommon`: `LoadBearing` = true |
| `IfcFooting` STRIP_FOOTING (fundație continuă) | Așa s-a făcut: `IFCFOOTING` STRIP_FOOTING părinte fără corp, cu `IFCRELAGGREGATES` peste straturile elevației (`IFCBUILDINGELEMENTPART`) și talpa (`IFCFOOTING` STRIP_FOOTING); nu `IFCELEMENTASSEMBLY`, ca un cititor să vadă o fundație | piesele | axa, tipul, în intenție | |
| `IfcFooting` PAD_FOOTING | `IFCFOOTING` PAD_FOOTING; cu mai multe trepte, părinte fără corp peste câte un `IFCFOOTING` PAD_FOOTING pe treaptă (Î2) | treptele | `at`, `rotation`, tipul | |
| prioritățile de tăiere | nu ca relații (sunt deja în corpuri) | — | `cutPriority` în intenție | #37 spunea „relațiile se vor scrie”; nu aduc nimic unui cititor, corpurile sunt deja tăiate **[Î2]** |
| Studio: solide și booleene (`IfcRelInterferesElements`) | elementul cu clasa lui (`exportAs` dă clasa reală: `IFCWALL`…); o uniune se scrie o dată, pe țintă; `IFCRELINTERFERESELEMENTS` doar între elemente care rămân amândouă | corpul rezultat | schița (contur, găuri, drum sau axă, pe ce vedere) în intenție | #92 |
| teren (`IfcGeographicElement` TERRAIN) | `IFCGEOGRAPHICELEMENT` (TERRAIN în 4.3) | TIN + marginea în jos | punctele topo, datumul, liniile de rupere în intenție, fiecare ca o proprietate `IfcText` cu JSON (**[Î5]** decis 25.10.2026: fără fișier atașat; STEP nu limitează lungimea textului, iar un singur fișier călătorește singur) | TERRAIN există și în IFC4 (`IfcGeographicElement` a apărut în IFC4) |
| platforme (`IfcEarthworksCut`) | `IFCEARTHWORKSCUT` în 4.3; `IFCBUILDINGELEMENTPROXY` USERDEFINED „Earthworks cut” în IFC4 | Așa s-a făcut: fără corp propriu (terenul îl desenează deja tăiat; platforma nu are producător de geometrie), scrisă lângă teren, cu intenția ei | conturul, cota | `IFCRELVOIDSELEMENT` teren → platformă doar în 4.3 (în IFC4 un proxy nu poate fi gol) |
| axe (`IfcGrid`) | `IFCGRID` pe etajul de referință, cu `IFCGRIDAXIS` pe fiecare axă | — | — | ca în BubbleBIM |
| adnotări, cote, texte, desen CAD (`IfcAnnotation`) | **[Î4]** decis (25.10.2026): nu se exportă în prima etapă; axele da | — | — | `IfcAnnotation` cu curbe 2D, mai târziu, când va cere cineva |
| faze | `Lapin_Phase` pe element: `Created`, `Demolished` (numele fazelor); lista fazelor pe proiect | — | — | #74; IFC nu are faze. **[Î6]** decis (25.10.2026): întâi tot proiectul; imediat după, filtrul pe fază (`phase` în comandă: ce există la sfârșitul fazei) |

## Materiale

| Lapin | IFC |
|---|---|
| `IfcMaterial` (nume, culoare, aspect pe tip de vedere) | `IFCMATERIAL` + `IFCMATERIALDEFINITIONREPRESENTATION` cu `IFCSTYLEDREPRESENTATION`: `IFCSURFACESTYLE` (culoarea 3D) și `IFCFILLAREASTYLE` cu `IFCFILLAREASTYLEHATCHING` (hașura secțiunii: unghi, distanță) (#52) |
| `IfcMaterialLayerSet`, `IfcMaterialLayerSetUsage` | `IFCMATERIALLAYERSET` (straturile cu grosime, `IsVentilated` nu, `Category` „LoadBearing” pe miez), `IFCMATERIALLAYERSETUSAGE` (`AXIS2`/`AXIS3`, sensul, offsetul) |
| `IfcMaterialProfileSet` (profile structurale) | `IFCMATERIALPROFILESET` cu `IFCRECTANGLEPROFILEDEF`, `IFCCIRCLEPROFILEDEF`, `IFCISHAPEPROFILEDEF`, `IFCUSHAPEPROFILEDEF`, `IFCTSHAPEPROFILEDEF`, `IFCLSHAPEPROFILEDEF` |

Culoarea fiecărei piese se scrie și pe corp (`IFCSTYLEDITEM`), ca în BubbleBIM, ca să o vadă și programele care ignoră materialele.

## Proprietăți și cantități

- **Seturile comune IFC** (`Pset_WallCommon`, `Pset_DoorCommon`…): doar proprietățile pe care Lapin le știe sigur (`Reference`, `IsExternal`, `LoadBearing`). Nimic ghicit.
- **Setul de intenție** **[Î3]** (decis 25.10.2026: numele e `Lapin_Intent`, pe element și pe tip; BubbleBIM va scrie același set. Până la 25.10.2026 s-a numit `Pset_Lapin_Intent`; prefixul `Pset_` e rezervat de IFC seturilor din standard (IfcPropertySet), iar serviciul de validare al bSI îl semnalează. Importul citește ambele nume. Seturile proprii sunt `Lapin_Intent`, `Lapin_Phase` și `Lapin_Parameters`): un `IFCPROPERTYSET` pe element (și unul pe tip), cu proprietățile care îl fac editabil în Lapin, exact atributele lui (`start`, `end`, `height`, `baseOffset`, `along`, `sill`, `contour`, `pitch`, `spec`…), ca `IFCPROPERTYSINGLEVALUE` tipizate (`IfcLengthMeasure` pentru câmpurile de lungime, `IfcReal` pentru restul numerelor, unghiurile în grade cu descrierea „degrees”, nu `IfcPlaneAngleMeasure`, care ar fi citit în radiani; `IfcLabel`, `IfcBoolean`) și, pentru liste și obiecte (contururi, specificații), ca `IfcText` cu JSON. Plus `LapinClass` (clasa Lapin, când diferă de cea IFC: un proxy Studio „exportAs”), `LapinPredefinedType` și `LapinVersion`. Pe tip, setul stă în `HasPropertySets`.
- **Cantitățile**: `IFCELEMENTQUANTITY` cu numele seturilor Lapin (`Qto_WallBaseQuantities`, `Qto_SlabBaseQuantities`…, exact numele IFC), cantitățile ca `IFCQUANTITYLENGTH/AREA/VOLUME/COUNT/WEIGHT` după `Kind`; seturile pe piesă (`Lapin_WallZoneQuantities`) pe piesa respectivă când e entitate, altfel pe element cu `Description` = piesa.

## GlobalId-uri derivate

| Ce | Cheie |
|---|---|
| piesă derivată | `{idElement}/{cheiaPiesei}` (`zone:masonry`, `lintel:D1:0`, `step:1`) |
| relație | relațiile modelului își păstrează id-ul; cele pe care exportul le face el: `{idPărinte}/parts`, `{idTip}/defines`, `{idMaterial}/associates`, `{idEtaj}/contains`, `{idProiect}/aggregates`, `{idSit}/aggregates/building`, `{idClădire}/aggregates/storeys` |
| pset, qto | `{idElement}/{numeleSetului}` (`…/common`, `…/Pset_Lapin_Intent`, `…/Pset_Lapin_Phase` — cheile vechi, păstrate după redenumirea seturilor, ca id-urile să rămână aceleași —, `…/qto/{set}` sau `…/qto/{set}/{piesă}`), relația `…-rel` |
| plan de acoperiș, element de șarpantă | `{idAcoperiș}/{cheia piesei}/{index}` |

Relațiile care există deja în model (`IfcRelVoidsElement`, `IfcRelFillsElement`, `IfcRelDefinesByType`, `IfcRelAssociatesMaterial`, `IfcRelConnectsPathElements`, `IfcRelInterferesElements`) își păstrează id-ul din model.

## Corpurile **[Î2]**

**Decis (25.10.2026):** golurile după regula IFC (corp netăiat de goluri, `IfcOpeningElement` cu corpul lui, `IfcRelVoidsElement`). Piesele: un perete care are doar straturi iese ca **un singur `IfcWall` cu un corp** și `IfcMaterialLayerSetUsage`; un perete cu zone, centuri sau buiandrugi iese ca **grup**: `IfcWall` părinte fără corp, zonele ca `IfcBuildingElementPart`, centura și buiandrugii ca `IfcBeam` RING_BEAM / LINTEL, legate cu `IfcRelAggregates`; golul voidează părintele. La fel acoperișul (`IfcRoof` peste planele și șarpanta lui), fundația continuă (ansamblul) și treptele fundației izolate. Fiecare piesă e în IFC un element de sine stătător; în Lapin rămân un grup parametric, refăcut la import din agregare și din intenție. Nivelul implicit al exportului e grupul; `detail: "simple"` dă un corp pe element, cu piesele doar ca intenție și cantități.

Regula IFC: un element voidat de goluri își scrie corpul **netăiat**, iar golul scrie corpul lui; cititorul scade golul. Lapin construiește corpurile deja tăiate (manifold). Pentru export, producătorii de geometrie primesc o opțiune `forExport` prin care pereții și plăcile dau prisma netăiată de goluri; tăieturile de îmbinare și de priorități rămân (ele nu sunt goluri IFC). Fără biblioteca de booleene (Windows, până la manifold) corpul e oricum netăiat.

Un element cu piese agregate sub el (zone, planele acoperișului, treptele) **nu are corp propriu**: piesele îl au. Așa cere `IfcRelAggregates` și așa evităm solidele duble. De aici cele două niveluri de detaliu din **[Î2]**.

## Importul

1. **Propriul IFC (round trip).** Făcut (25.10.2026). Un fișier cu setul de intenție Lapin (sau cel BubbleBIM, când va fi același) se reconstruiește **direct în model, într-un singur pas de undo** (`Session.Edit`), nu prin comenzi: proiectul (locația, fazele), etajele (după GlobalId, altfel după nume, altfel după cotă), materialele (cu `LapinId` din `IfcMaterialProperties`, ca să rămână valabile id-urile la care trimit tipurile și zonele), tipurile cu layer set-urile lor, elementele cu atributele din intenție, golurile, tâmplăria, usage-urile, îmbinările, axele. Piesele agregate sub un element nativ se sar (le reface elementul). Un element cu același GlobalId **actualizează** elementul existent; ce lipsește din fișier nu se șterge, se semnalează. Testele de round trip: export → import → același model (clase, atribute, relații, corpuri, cantități); al doilea import actualizează, nu dublează.
2. **Un IFC străin, ca referință.** Făcut (25.10.2026). Fără intenție, un element **își păstrează clasa IFC** (`IfcWall` rămâne `IfcWall`, o clasă pe care niciun modul nu o are rămâne străină) și primește corpul în atributul `body` (triunghiuri, metri, coordonatele proiectului, din `IfcModel`, cu golurile scăzute unde sunt booleenele) plus `reference`, `ifcClass`, `objectType` și seturile de proprietăți ca text. `StoredBodies` îl desenează în locul producătorului clasei; `element.update` și `element.transform` îl refuză, ștergerea merge. Modul `reference` al comenzii ține așa și fișierele Lapin. Lucrul serios cu IFC-uri importate rămâne al IfcSheet-ului.
3. **Recunoașterea** (după referință): elementele străine a căror reprezentare e parametrică devin elemente native. Perete: `Axis` (polilinie cu două puncte) + `IfcMaterialLayerSetUsage` (grosimi, offset, sens) + înălțimea corpului; fără `Axis`, un `IfcExtrudedAreaSolid` cu profil dreptunghiular sau un B-rep prismă cu talpă dreptunghiulară subțire dau axa din latura lungă. Placă: extrudare pe Z a unui profil arbitrar cu goluri, sau prismă B-rep, dau conturul, găurile și grosimea. Stâlp, grindă: extrudarea unui profil parametric (dreptunghi, cerc, I, U, L, T) dă secțiunea și axa. Gol: cutia `IfcOpeningElement` în cadrul peretelui dă `along`, `sill`, lățimea, înălțimea; ușa și fereastra iau `OverallWidth/Height` și un tip cu specificația implicită după nume. Fundație izolată: dreptunghi extrudat. Ce nu se recunoaște (sweep-uri, revoluții, mesh-uri triangulate, `IfcBooleanClippingResult` fără `Axis`, acoperișuri cu plane care nu formează un stil cunoscut) rămâne element de referință, cu corpul original păstrat și reemis identic la export. Materialele se potrivesc după nume cu cele ale proiectului; restul se creează.

## Etapele

1. **Exportul de bază** (făcut, 25.10.2026; cu filtrul pe fază: `phase` în comandă dă ce există la sfârșitul fazei): biblioteca `Lapin.Ifc` (Step, Guid-uri derivate, B-rep, stiluri), arborele spațial, pereți cu `Axis` și straturi, grupurile (zone, centuri, buiandrugi; planele și șarpanta acoperișului; ansamblul fundației continue; treptele), tipuri, goluri cu uși și ferestre, plăci, stâlpi, grinzi, fundații, teren și platforme, axe, materiale cu stiluri, seturile comune, setul de intenție, cantitățile, fazele, `IfcMapConversion`. Comanda și meniul. Teste: fișierul se parsează cu `StepFile`, numărul de entități pe clasă, GlobalId-urile stabile între două exporturi, deschiderea în BubbleBIM Pro (`IfcModel`) cu aceleași corpuri.
2. **Detaliul:** lucarnele ca `IfcWall` + `IfcRoof` sub acoperiș; `detail: "simple"` verificat în Revit și ArchiCAD; recunoașterea elementelor parametrice dintr-un IFC străin (vezi Importul, 3).
3. **Importul propriu** (round trip) și **importul ca referință**; apoi actualizarea după GlobalId la al doilea import.
4. **Schimbul cu BubbleBIM:** BubbleBIM scrie setul de intenție cu același nume; `bubbles-house` devine un caz al acestui IFC.

## Recunoașterea unui IFC străin (Importul, 3) în detaliu **[Î7–Î12]**

Cerința (25.10.2026): geometria importată să devină, pe cât se poate, geometrie nativă Lapin, editabilă: pereți și grinzi cu axele lor, în care se pot pune uși și ferestre, care se lungesc și se schimbă parametric; ușile și ferestrele recunoscute, cu tipul lor schimbabil sau mapat pe un tip al proiectului. Azi importul străin aduce mesh-uri (referințe), iar planurile din ele arată prost: fiecare triunghi își dă liniile, tăietura nu are umplutură.

### Ce arată fișierele reale

| Fișier | Pereți | Cum vin corpurile | Ce lipsește |
|---|---|---|---|
| `Bloc-P-4.ifc` (IFC4, Bubbles) | 506, din care 140 sunt finisaje exportate ca pereți separați („Finisaj exterior — Tencuială de var") | `IfcExtrudedAreaSolid` cu `IfcRectangleProfileDef` (2469), 160 `IfcBooleanClippingResult` (pereți sub acoperiș), 181 B-rep (scări, balustrade) | fără `Axis`, fără tipuri, fără `IfcMaterialLayerSetUsage`; ușile și ferestrele au `OverallWidth/Height` și `OperationType` |
| `BURKWIL_DX_B.ifc` (IFC2X3, ArchiCAD) | 540 | 4355 B-rep fațetate, 388 extrudări, 415 `IfcMappedItem` | `Axis` o singură dată; 29 `IfcWallType`, 3 `IfcMaterialLayerSetUsage`, 556 `IfcRelConnectsPathElements` |
| `Casa_Stil_Brancovenesc.ifc` (IFC4, BubbleBIM) | 6 | extrudări cu profil arbitrar (229), 14 `IfcTriangulatedFaceSet` | — |

Concluzia: `Axis` și straturile de material sunt rare. Recunoașterea trebuie să plece de la **corp**: extrudări cu profilul lor parametric, și prisme B-rep din care se reconstruiește profilul. Lapin vechi convertea doar ușile și ferestrele (golul + dimensiunile), nu pereții; BubbleBIM Standalone scria IFC, nu recunoștea. Deci recunoașterea e cod nou, pe semantica IFC, fără nimic preluat din ifc-lite sau web-ifc.

### Lanțul

1. **Forma canonică a corpului.** Pentru fiecare produs, înainte de triangulare, reprezentarea `Body` se reduce la una dintre formele: `Extrusion(profil 2D cu goluri, direcție, adâncime, plasare)`, din `IfcExtrudedAreaSolid` (și primul operand al unui `IfcBooleanClippingResult`, cu tăieturile reținute separat), sau `Prism(contur 2D cu goluri, axă, înălțime)`, dedus dintr-un B-rep sau un `IfcPolygonalFaceSet` ale cărui fețe sunt plane și se împart în două capace paralele congruente și fețe laterale perpendiculare pe ele. `IfcMappedItem` se dezleagă cu transformarea lui. Orice altceva (sweep, revoluție, mesh triangulat fără structură, mai multe corpuri disjuncte) rămâne **referință**.
2. **Clasa decide ce se citește din formă.**
   - **Perete** (`IfcWall`, `IfcWallStandardCase`): cu `Axis` prezent, axa e aceea; altfel, din conturul prismei verticale: perechea de laturi lungi paralele dă grosimea și direcția, mijlocul lor dă axa, extremele conturului proiectate pe axă dau capetele (un perete tăiat în unghi la îmbinare are capetele unde se termină conturul; Lapin îi reface îmbinarea). Înălțimea = adâncimea extrudării; `baseOffset` din cota corpului față de etaj. Straturile: `IfcMaterialLayerSetUsage` când există; altfel un singur strat de grosimea măsurată, cu materialul asociat (`IfcRelAssociatesMaterial`) sau fără. Pereții sub acoperiș (`IfcBooleanClippingResult`) se recunosc din primul operand și rămân cu înălțimea lui; atașarea la acoperiș vine când acoperișul e nativ.
   - **Placă** (`IfcSlab`): extrudare pe Z a unui profil arbitrar cu goluri sau prismă orizontală → contur, goluri (`addSlabOpening`), grosime, cotă.
   - **Stâlp, grindă** (`IfcColumn`, `IfcBeam`, `IfcMember`): profilul extrudării → secțiunea (dreptunghi, cerc, I/U/L/T parametrice, potrivite cu catalogul de profile metalice după dimensiuni) și axa (verticală pentru stâlp, orizontală pentru grindă, din plasare și direcție); un B-rep prismă dă același lucru.
   - **Fundații** (`IfcFooting`): dreptunghi extrudat → talpă izolată; prismă lungă → fundație continuă cu axa ca la perete.
   - **Goluri, uși, ferestre:** cutia `IfcOpeningElement` în cadrul peretelui-gazdă dă `along`, lățimea, înălțimea și pervazul (`sill`); ușa sau fereastra care îl umple (`IfcRelFillsElement`) ia `OverallWidth/Height` când le are, altfel cutia golului, plus `OperationType`/`PartitioningType` și tipul sursă (`IfcDoorType`/`IfcDoorStyle`). Un gol fără umplutură devine gol simplu.
   - **Acoperișuri, scări, balustrade, mobilier, spații:** referință (etapa B pentru acoperișurile din plane care formează un stil cunoscut).
3. **Tipurile.** Perete: un `IfcWallType` per nume de tip sursă, sau, fără tip, per grosime + material („Zidărie 25", „Imported 200"). Uși și ferestre: un tip per tip sursă, sau, fără tip, per lățime × înălțime + operare („Door 90×210 left"); specificația pornește din cea implicită Lapin cu dimensiunile sursei. Stâlpi și grinzi: un tip per secțiune. Materialele se potrivesc după nume cu cele ale proiectului; restul se creează, cu hașura implicită a clasei.
4. **Identitatea.** Elementul recunoscut păstrează **GlobalId-ul sursei ca id Lapin**, ca re-exportul să dea același GlobalId și un al doilea import să îl regăsească. Păstrează și `ifcClass`, `objectType`, numele și seturile de proprietăți ale sursei, ca text, pentru inspector.
5. **Raportul.** Importul întoarce ce a recunoscut și ce a rămas referință, pe clase, cu motivul (fără contur dreptunghiular, extrudare oblică, mai multe corpuri). Interfața îl arată la sfârșit.
6. **Ce rămâne referință se desenează corect.** Triunghiurile unui corp stocat se adună în fețe plane (`SolidFromMesh`) ca liniile din plan să fie muchiile reale, nu ale triunghiurilor; tăietura primește o umplutură neutră; liniile ascunse din interiorul corpului nu se desenează.

### Etapele

- **A (acum):** lanțul de mai sus pentru pereți, plăci, stâlpi, grinzi, fundații din extrudări și prisme; uși și ferestre cu tipuri implicite după sursă; raportul; desenul referințelor. Testat pe `Bloc-P-4.ifc` (toți pereții dreptunghiulari, 175 uși/ferestre) și pe `BURKWIL_DX_B.ifc` (prisme B-rep, tipuri de perete).
- **B:** dialogul „Map types" după import: fiecare tip importat poate fi înlocuit cu un tip existent al proiectului, alegerea memorată în proiect; finisajele exportate ca pereți separați absorbite ca strat al peretelui alăturat; golurile tăiate direct în corp (fără `IfcOpeningElement`) găsite în contur; acoperișurile din plane; „Convertește la nativ" la cerere pe o referință.
- **C:** actualizarea la reimport după GlobalId: ce s-a schimbat în sursă se schimbă în model, ce ai editat în Lapin se păstrează sau se semnalează.

### Așa s-a făcut (etapa A, 25.10.2026)

- **Contractul.** `IIfcRecognizer` în `Lapin.Platform` (`Recognition.cs`): importul citește fiecare element străin într-un `ForeignElement` (clasa, corpul ca prisme, golurile cu umpluturile lor, tipul, straturile sau materialul, uzajul, linia `Axis`, întinderile corpului în cadrul axei, piesele agregate) și îl oferă recunoscătorilor modulelor; primul care întoarce un id l-a făcut. Modulele se încarcă dinamic, deci `Lapin.Ifc` nu le poate apela direct: așa rămâne și cunoașterea atributelor în module.
- **Prismele** (`IfcModel.Prisms`, în `IfcShapes.cs`): extrudările direct (primul operand al unei tăieturi booleene, marcat `Clipped`); B-rep-urile și seturile de fețe poligonale prin perechi de capace paralele congruente cu laturi perpendiculare, după curățarea vârfurilor coliniare (alte programe le lasă unde se ating vecinii); capacul opus poate fi împărțit în mai multe fețe coplanare. O cutie dă trei prisme; clasa alege.
- **Pereții** (`Lapin.Architecture/ForeignElements.cs`), în ordine: corpul ca prismă verticală (talpa dă grosimea și, fără `Axis`, axa pe mijloc); piesele agregate ca straturi (ArchiCAD: `IfcBuildingElementPart` pe strat), din întinderile fiecărei piese de-a curmezișul axei, suprapunerile împărțite la mijloc; axa cu straturile din fișier și întinderile corpului (pereți tăiați de acoperiș, corpuri multiple). Uzajul fișierului așază straturile exact (`LayerSets.LayOut` cu offsetul și sensul lui); fără el, centrat. Golurile din cutia `IfcOpeningElement` în cadrul peretelui; ușile și ferestrele cu `OverallWidth/Height`, `flipHand` din `OperationType`; golurile fără umplutură ca goluri simple (`width`, `height` pe gol). Pereții se îmbină automat la sfârșit (`Joins.Auto`).
- **Plăcile:** conturul prismei, găurile ca goluri, grosimea din înălțime, cota din vârf. **Stâlpii:** dreptunghi (cu rotația) sau cerc. **Grinzile:** profil în plan vertical măturat orizontal, sau prismă verticală cu axa pe latura lungă. **Fundațiile izolate:** dreptunghi, o treaptă, `bottom` din bază.
- **Tipurile:** per nume de tip sursă, altfel per straturi (perete, placă), per secțiune (stâlp, grindă), per dimensiune și operare (uși, ferestre); refolosite în proiect după nume și dimensiune.
- **Identitatea:** GlobalId-ul sursei e id-ul Lapin al elementului, al golului și al ușii; `imported` ține clasa, numele, tipul și seturile de proprietăți ale sursei. Un al doilea import al aceluiași fișier reface elementul (cu golurile și uzajul lui), nu îl dublează; ce ai făcut tu cu același id rămâne.
- **Două treceri:** întâi nativ (intenție sau recunoaștere), apoi referințele, fără piesele unui element nativ și fără ușile făcute cu peretele lor.
- **Referințele** (`StoredBodies`): triunghiurile se adună în fețe plane (`SolidFromMesh`), ca planul să arate muchiile corpului.
- **Raportul:** `recognized`, `recognizedByClass`, `referencesByClass`, `skipped` (motive numărate) și note; interfața le arată în mesajul de import.

### Rămase pentru etapa B

Dialogul de mapare a tipurilor; finisajele exportate ca pereți separați absorbite ca strat; golurile tăiate direct în corp; plăcile și stâlpii B-rep care nu sunt prisme (conturul din fața de sus, întinderile); ușile peste înălțimea peretelui; fundațiile continue; acoperișurile din plane; „Convertește la nativ” pe o referință; actualizarea la reimport păstrând editările.

### Întrebări

- **[Î7]** Finisajele exportate ca pereți separați (Bubbles): absorbite ca strat al peretelui alăturat când sunt coplanare și adiacente pe toată lungimea, altfel pereți subțiri separați? (Recomand da, în etapa B.)
- **[Î8]** Tipurile de perete fără sursă: per grosime + material, cu numele materialului și grosimea în nume? (Recomand da.)
- **[Î9]** Ușile și ferestrele: tip per tip sursă, altfel per dimensiune + operare; maparea pe tipuri existente într-un dialog după import, memorată în proiect? (Recomand da; etapa A face tipurile, B dialogul.)
- **[Î10]** Recunoaștere nesigură: referință cu raport, nu „cel mai bun perete" forțat? (Recomand referință; conversia manuală vine în B.)
- **[Î11]** GlobalId-ul sursei devine id-ul Lapin al elementului recunoscut? (Recomand da.)
- **[Î12]** Ordinea A → B → C? (Recomand da; A începe acum.)
