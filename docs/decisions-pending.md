# Decizii de revizuit

Am luat aceste decizii singur în timpul implementării M1–M7, ca să nu opresc lucrul. Le corectăm împreună la final. Fiecare intrare spune ce am ales, ce alternative există și unde se aplică.

Ordinea de căutare a fost: Lapin-ul vechi, apoi BubbleBIM Pro, apoi BubbleBIM-Standalone.

## M1

| # | Ce am ales | Alternative | Unde |
|---|---|---|---|
| 1 | Un proiect nou pornește cu proiectul, situl, clădirea și un „Ground floor” la ±0,00, înalt de 3 m. | Un proiect gol, ca în BubbleBIM. | `Session.StartEmpty` |
| 2 | Tipul implicit de perete, când nu alegi altul: tencuială exterioară 20, cărămidă 250 (miezul), tencuială interioară 15 mm (exemplul din Lapin-ul vechi, N3b). | Alt tip implicit, sau obligația de a alege un tip. | `WallTypes.DefaultLayers` |
| 3 | Straturile se listează de la fața din dreapta direcției de desen spre fața din stânga, ca în Lapin-ul vechi. Usage-ul are `AXIS2`, sensul POSITIVE spre stânga. | De la exterior spre interior (cere să știm unde e exteriorul). | `Lapin.Model/MaterialLayers.cs` |
| 4 | Tipul e comun, usage-ul e al fiecărui perete. La schimbarea tipului, usage-ul rămâne același, cu axa pe miezul nou, iar flip-ul se păstrează. | Un usage comun pe tip (IFC permite asta). | `Walls.LayOut` |
| 5 | Grosimea unui perete tipizat se schimbă din tip (toți pereții tipului urmează). Regula veche („ultimul strat preia diferența”, pe elementul însuși) vine cu push/pull. | Un set de straturi propriu pe element, ca în Lapin-ul vechi. | `WallTypes.Update` |
| 6 | Mesajele motorului sunt cadre binare proprii (`LPF1`): un antet JSON și bufferele mesh-urilor. | MessagePack, Arrow IPC pe fir, sau JSON cu base64. | `BinaryFrame.cs`, `ui/src/engine/frame.ts` |
| 7 | O relație care doar câștigă sau pierde membri atinge relating-ul și acei membri, nu pe toți. Citirea atributelor unei entități și citirea relațiilor ei sunt dependențe separate. | — (regulă de performanță: 200 de pereți, de la 5,2 s la 0,36 s) | `Transaction.Concerned`, `TrackingView` |
| 8 | Jurnalul de recuperare nu stă în `.lapin`, ci în folderul de date al aplicației (`<app data>/recovery/*.jsonl`): un zip nu se poate completa la fiecare editare. Pornește din fișierul salvat (verificat prin SHA-256) sau din starea inițială a unui proiect nou. | `journal/ops.jsonl` în pachet, cum scria propunerea. | `Lapin.Host/Recovery.cs` |
| 9 | O închidere normală cu editări nesalvate păstrează jurnalul, iar la pornirea următoare ți se oferă recuperarea. Interfața trebuie să întrebe „Save / Don't save” la închidere. | Ștergerea jurnalului la închiderea normală. | `Recovery.Dispose` |
| 10 | Munca nesalvată se recuperează doar peste același fișier. Dacă proiectul s-a schimbat de atunci, recuperarea e refuzată. | Recuperarea oricum, cu un avertisment. | `Recovery.Restore` |
| 11 | Esc, ca în Revit: primul oprește lanțul de pereți, al doilea iese din unealtă. | Un singur Esc iese direct. | `PlanView` |
| 12 | Vederea de plan e deocamdată planul de lucru: conturul tăiat al fiecărui strat, din motor. Desenul 2D real (secțiune, linii ascunse, penițe) vine la M5. | — | `ui/src/viewport/PlanView.tsx` |
| 13 | La închiderea ferestrei cu modificări nesalvate, întrebarea are două butoane: „Save” și „Don't save” (fără „Cancel”, pe care dialogul nativ Tauri nu-l are în versiunea folosită). „Don't save” renunță și la jurnalul de recuperare. | Trei butoane, cu un dialog propriu. | `guardWindowClose` |
| 14 | Am scos secțiunile demonstrative „Phase” și „Quantities” din inspector, pentru că arătau cifre false pe elemente reale. Revin cu datele reale: cantitățile la M2, fazele la M6. | Păstrarea lor ca machete. | `ui/src/modules/phases`, `cost` |
| 15 | Proiectele recente se țin în browser (`localStorage`). Lista nu e partajată între calculatoare. | Un fișier în folderul de date al aplicației, prin motor. | `files.ts` |
| 16 | Editarea generică din inspector (`element.update`) schimbă doar câmpurile declarate de clasă, validate după tip, min și max. Câmpul „grosime fără straturi” nu mai apare la pereți. | O comandă de editare per clasă. | `CoreModule`, `PropertiesPanel` |

## M2 – pereții

| # | Ce am ales | Alternative | Unde |
|---|---|---|---|
| 17 | O îmbinare se stochează doar ca intenție (`IfcRelConnectsPathElements` cu tipul, stilul, conexiunile și prioritatea ca `Relating`). Tăieturile capetelor se calculează la fiecare reconstruire, din ambii pereți, deci nu există un profil rescris în model. | Scrierea profilului tăiat în model, ca în Lapin-ul vechi. | `Joins.cs`, `Walls.Geometry` |
| 18 | Pereții desenați se îmbină automat la capete: colț, perete în prelungire sau T pe traseul altuia. Un colț deja îmbinat nu primește automat un al treilea perete. | Îmbinare doar la cerere; îmbinări de trei pereți. | `Joins.Auto` |
| 19 | Tragerea capătului unui perete mută întotdeauna și capătul îmbinat al vecinului (colțul se mută întreg), oricare ar fi selecția. `moveJoinedEnds: false` ține colțul pe loc. Lapin-ul vechi desprindea vecinul când nu era și el selectat (`movesWith`). | Comportamentul vechi, după selecție. | `Joins.MoveEnds` |
| 20 | Stilul colțurilor noi (butt sau mitre) e o preferință ținută pe calculator, ca în Lapin-ul vechi. | O setare a proiectului. | `ui/src/modules/architecture/prefs.ts` |
| 21 | Tăietura unui capăt se împarte pe straturi liniar, între fața dreaptă și cea stângă. Straturile nu se „îmbină” între ele (miez la miez), ca în Lapin-ul vechi. Consecință: la un colț butt, miezul (și centura) peretelui care se oprește se termină la finisajul interior al celui care trece, deci rămâne o fâșie cât grosimea finisajului (15 mm) între miezuri. Îmbinarea straturilor după funcție (miez la miez, finisajele în jur, ca în Revit) e programată la M5, cu desenul pe materiale. | Îmbinarea straturilor după funcție acum. | `Walls.Geometry`, `WallZoneTests` |
| 22 | Zonele pe înălțime și straturile de finisaj sunt piese derivate ale peretelui (`zone:<cheie>`, `layer:<n>`), nu entități în model. Specificația zonelor stă pe tip. Piesele `IfcBuildingElementPart` și `IfcBeam` RING_BEAM, cu GlobalId-uri stabile derivate din perete și cheie, le scrie exportul IFC. | Piese ca entități agregate sub perete (`IfcRelAggregates`), ca în Lapin-ul vechi. | `WallZones.cs`, `Walls.Geometry` |
| 23 | Înălțimile zonelor sunt fixe, plus o singură zonă „rest”. Regulile după repere (sub grinda de deasupra, la parapet) nu sunt încă făcute. Peretele nu poate încă suprascrie înălțimea unei zone. | Reperele și suprascrierea locală acum. | `WallZones.Bands` |
| 24 | Cantitățile sunt calculate, nu stocate: setul elementului (`Qto_WallBaseQuantities`), al fiecărei zone (`Lapin_WallZoneQuantities`, `Qto_BeamBaseQuantities`) și al fiecărui strat (`Lapin_WallLayerQuantities`), cu volumele nete din piesele construite. Formulele de blocuri și mortar sunt cele vechi (mortarul nu coboară sub zero). | — | `Walls.Quantities`, `quantities.of` |
| 25 | Centura ocupă toată lățimea miezului. Retragerea pentru izolație și lățimea proprie vin mai târziu. | Lățime și retragere configurabile acum. | `WallZones` |

## M3 – goluri, uși și ferestre

| # | Ce am ales | Alternative | Unde |
|---|---|---|---|
| 26 | Golurile s-au făcut înaintea buiandrugilor și a priorităților de tăiere (pașii 7–8), pentru că acestea depind de ele. Configuratorul de tâmplărie (pasul 10) s-a făcut împreună cu golurile. | Ordinea din plan. | — |
| 27 | Un gol își ține poziția pe perete (`along`, centrul pe axă de la start, și `sill`). Lățimea și înălțimea vin din tipul care îl umple, deci un tip schimbat reîncadrează toate golurile lui, iar unul care nu mai încape e refuzat. | Dimensiunile copiate pe gol, rescrise la schimbarea tipului (ca în Lapin-ul vechi). | `Openings.cs` |
| 28 | Exteriorul unui perete e fața lui din dreapta (prima în lista de straturi, tencuiala exterioară la tipul implicit). Tâmplăria se așază cu +Y spre exterior, pe planul de mijloc al corpului peretelui. Flip „Hand” oglindește stânga ↔ dreapta, flip „Side” interior ↔ exterior. | Exteriorul ales pe perete sau după cameră. | `Openings.FillGeometry` |
| 29 | Golurile se taie din fiecare piesă a peretelui cu booleene (manifold). Fără biblioteca nativă (pentru Windows, la M7) peretele se desenează netăiat, cu un diagnostic. | Tăiere analitică, fără booleene, ca în Lapin-ul vechi. | `Walls.Geometry` |
| 30 | Planul taie la 1,20 m deasupra etajului (constantă până la domeniul vederii, M5). Golurile care trec prin tăietură lasă pauze în pereți. | — | `Openings.PlanCut` |
| 31 | Simbolurile de plan sunt deocamdată cele din `transfer.md`: foaia ușii deschisă la 90° spre interior, cu arc, și fereastra cu tocul și geamul dublu. Simbolurile complete (`simboluri-tamplarie.md`, grosimi, foi duble peste 1,4 m) vin la M5. | — | `Openings.Symbol` |
| 32 | Ușile și ferestrele primesc mărci automate (D1, D2, W1…) din marca tipului și numărul lor în proiect. | Mărci date de utilizator. | `Openings.AddFill` |
| 33 | Orice perete al cărui tip nu are regulă de buiandrug primește regula implicită veche: prefabricat, reazem 25 cm, înălțime 25 cm. Un tip de perete (un perete despărțitor, de exemplu) o poate pune pe „None”. | Fără buiandrugi până nu se cere o regulă. | `Walls.LintelRuleOf` |
| 34 | Buiandrugii și centura coborâtă sunt piese derivate ale peretelui (`lintel:<goluri>:<bucată>`, `downstand:…`), scăzute din miez. Nu sunt entități `IfcBeam`; exportul IFC le va scrie ca atare. | Entități agregate sub perete. | `Walls.Geometry` |
| 35 | Nu sunt încă făcute: îmbinarea buiandrugilor de la colțuri (cel mai lung îl taie pe cel scurt), catalogul de produse prefabricate cu import CSV și extrasul de buiandrugi (vine cu tabelele, la M6). | — | `Lintels.cs` |
| 36 | Tipul implicit de placă: finisaj 20, șapă 50, beton armat 150 mm (miezul), cu fața de sus la cota etajului. Straturile se listează de sus în jos (`AXIS3`, sens NEGATIVE). | Alt tip implicit, sau fața de jos la cota etajului. | `Slabs.DefaultLayers` |
| 37 | Prioritățile de tăiere sunt calculate, nu scrise ca `IfcRelInterferesElements`. Fiecare element își scade la construire „învelișurile” elementelor cu prioritate mai mare care îl ating. Exportul IFC va scrie relațiile. | Relații scrise la fiecare editare, ca în Lapin-ul vechi. | `ICutters`, `Cuts.Apply` |
| 38 | Un element caută tăietori doar în clasele cu prioritate mai mare decât a lui. Suprascrierea pe element (`cutPriority`) funcționează între clase diferite, nu între două elemente din aceeași clasă (un perete nu taie alt perete). | Suprascriere și în aceeași clasă (fiecare perete ar citi toți pereții). | `ModuleCatalog.For` |
| 39 | Plăcile nu au încă goluri proprii (gol de scară, de coș). | — | `Slabs` |

## M2 – structură

| # | Ce am ales | Alternative | Unde |
|---|---|---|---|
| 40 | Structure e modul de motor separat (`Lapin.Structure`), care cere doar platforma. O ediție doar de structură nu are pereți, dar are stâlpi, grinzi și fundații. Îmbinările au trecut în platformă, ca „elemente pe traseu”: pereți, grinzi, fundații continue. | Structura în modulul Architecture. | `engine/src/Modules/Structure` |
| 41 | Fundația continuă e o singură entitate `IfcFooting` STRIP_FOOTING, cu axă și tip (straturile peretelui de fundație și talpa). Peretele de fundație și talpa sunt piese derivate. Exportul IFC va scrie ansamblul (`IfcElementAssembly`, un `IfcWall` și un `IfcFooting`), ca în Lapin-ul vechi. | Ansamblu de entități, ca în Lapin-ul vechi. | `StripFootings` |
| 42 | Valorile implicite: stâlp 300 × 300 pe înălțimea etajului, grindă 250 × 400 cu fața de sus la nivelul următor, fundație continuă „SF 30” (cea veche) și fundație izolată în două trepte (1,8 și 1,0 m, cea veche). | Alte valori implicite. | `Columns`, `Beams`, `StripFootings`, `PadFootings` |
| 43 | La îmbinarea automată, elementul nou desenat trece prin colț (la grosimi egale). Swap inversează. | Primul desenat trece. | `Joins.Auto` |
| 44 | Grinzile se îmbină în plan (tăieturi verticale pe toată înălțimea), ca pereții. Îmbinarea 3D a elementelor liniare înclinate (căpriori) vine cu acoperișurile. Secțiunile sunt doar dreptunghi și cerc; I, L, T, U și țevile vin mai târziu. | Profilele din catalogul vechi acum. | `Beams`, `Section` |
| 45 | Proprietățile din inspector sunt deocamdată tipul și câteva câmpuri. Editarea completă a tipurilor structurale existente (nu doar crearea) vine mai târziu. | — | `StructureTypesDialog` |
