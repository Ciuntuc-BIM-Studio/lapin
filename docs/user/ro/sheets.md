---
title: Planșe, PDF și DXF
order: 100
summary: Așezi vederile pe planșe cu cartuș și le scoți în PDF sau DXF.
---

<!-- video sheets -->

## Cum faci o planșă

Apasă **+** lângă **Planșe** în navigatorul proiectului. Ca să pui o vedere, trage-o din navigator pe planșă sau alege-o în **Pune o vedere…** din bara planșei. Fiecare vedere își păstrează scara și rămâne legată de model, așa că planșa se schimbă odată cu modelul. Pe planșă, trage o vedere ca s-o muți: se mută doar ea, cu titlul ei, iar chenarul, cartușul și celelalte vederi rămân pe loc; se prinde de marginile și de mijlocul chenarului și de celelalte vederi, iar săgețile o împing cu 1 mm (10 mm cu Shift).

![O planșă A3 cu un plan și o secțiune](shots/sheet.webp)

Selectează planșa ca să-i completezi **cartușul** în panoul de proprietăți: proiectul, beneficiarul, cine a desenat, cine a verificat, data și revizia. Tot acolo se setează numărul, formatul și orientarea. Lăsat gol, câmpul proiectului arată numele proiectului: numele dat proiectului, altfel numele fișierului lui.

## Ce arată o vedere

O vedere de pe planșă arată exact ce alegi, fără s-o tragi pe ghicite:

- **Decupeaz-o pe planșă.** Clic pe o vedere: apar mânere la colțuri și pe laturi. Le tragi și chenarul cuprinde mai mult sau mai puțin din vedere, iar desenul rămâne pe loc pe hârtie. Cu **Shift**-tras înăuntru deplasezi desenul în chenar.
- **Desenează-i limita în vedere.** **Annotate → View boundary** (<kbd>VB</kbd>): desenezi un dreptunghi pe un plan, o secțiune sau o fațadă și îi dai un nume, unic în proiect. În **Place view…** vederea apare apoi cu fiecare limită a ei („Ground floor plan › Aripa est”), iar o vedere pusă așa arată exact dreptunghiul acela și îl urmează când îl muți sau îl redimensionezi în vedere. Aceeași vedere poate sta pe mai multe planșe, câte o limită pe fiecare. O vedere pusă trece de la o limită la alta, sau la decupare liberă, din lista de pe chenarul ei. Limitele se văd doar pe ecran, niciodată pe hârtie.
- **Fixeaz-o.** Pioneza de pe chenarul vederii o ține pe loc: se selectează, dar nu se mută, nu se împinge și nu se decupează din greșeală.

Chenarul planșei și cartușul sunt ale hârtiei. Nu se selectează și nu se mută, iar un clic alege cea mai mică vedere de sub cursor, așa că o vedere pusă peste cartuș se poate selecta oricând.

![O vedere selectată pe planșă: mânerele de decupare, limita ei, pioneza](shots/sheet-view-boundary.webp)

## Tipărirea

Butoanele **PDF** și **DXF** din bara planșei o exportă. Un PDF e gata de tipărit sau de trimis. Un DXF se deschide în AutoCAD și în alte programe CAD cu layerele, liniile și hașurile lui. **Tipărește** trimite planșa la imprimantă sau plotter la formatul ei, la 1:1, în linii vectoriale: alegi plotterul și hârtia în fereastra de tipărire. Pe o imprimantă mai mică, alegi încadrarea în pagină.
