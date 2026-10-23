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
