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
