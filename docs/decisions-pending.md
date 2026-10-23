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
| 19 | Tragerea capătului unui perete mută și capătul îmbinat al vecinului (colțul se mută întreg). `moveJoinedEnds: false` ține colțul pe loc. Când selecția conține doar peretele tras, vecinul nu „se desprinde”, ca în Lapin-ul vechi (`movesWith`). | Desprinderea vecinului neselectat. | `Joins.MoveEnds` |
| 20 | Stilul colțurilor noi (butt sau mitre) e o preferință ținută pe calculator, ca în Lapin-ul vechi. | O setare a proiectului. | `ui/src/modules/architecture/prefs.ts` |
| 21 | Tăietura unui capăt se împarte pe straturi liniar, între fața dreaptă și cea stângă. Straturile nu se „îmbină” între ele (miez la miez); asta ține de desenul pe materiale, la M5. | Îmbinarea straturilor după prioritatea materialelor. | `Walls.Geometry` |
