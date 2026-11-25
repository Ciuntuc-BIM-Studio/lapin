---
title: Niveluri și planuri
order: 60
summary: Adaugi niveluri, le setezi cotele și vezi alt nivel sub planul pe care desenezi.
---

<!-- video levels -->

## Niveluri

Navigatorul proiectului listează nivelurile sus, cel mai înalt primul. **+** de lângă **Niveluri** adaugă un nivel peste cel mai înalt.

**Mai multe niveluri deodată.** Butonul de lângă **+** (sau **Adnotări → Repere → Niveluri noi…**) deschide **Niveluri noi**. Scrie **Înălțimile de etaje** de jos în sus, despărțite prin spații, cum se scriu distanțele în Rețea de axe: `3*3000 2800` face patru etaje, trei de 3000 și unul de 2800. Primul stă pe **Cota de bază** (partea de sus a celui mai înalt nivel, dacă nu scrii alta); fiecare următor stă pe cel de dedesubt. **Nume** e un model în care `{n}` e numărul, numărat de la **Primul număr**: `Level {n}` de la 3 dă Level 3, Level 4…. Lista de alături arată fiecare nivel nou cu cota lui. Nivelurile care există rămân cum sunt, iar cele noi sunt un singur pas de anulare.

![Niveluri noi: patru etaje din înălțimile lor](shots/new-levels.webp)

Selectează un nivel ca să vezi **cota de bază** și **cota superioară**. Oricare poate fi deasupra sau dedesubtul cotei ±0,00. Înălțimea nivelului e distanța dintre ele. Când muți baza, partea de sus rămâne pe loc. Un proiect nou începe cu Foundations, Ground floor și First floor. Un nivel are cel mult 100 m înălțime și stă în ±10 km față de ±0,00: o valoare scrisă în altă unitate e refuzată. O scară care ar urca mai mult de 50 m până la nivelul de deasupra se oprește acolo și spune de ce.

![Proprietățile unui nivel: cota de bază, cota superioară și înălțimea](shots/level-properties.webp)

Fiecare nivel are planul lui sub **Planuri**. Dublu clic pe un plan ca să desenezi pe acel nivel. Nivelul pe care desenezi apare în caseta din stânga sus a vederii.

În secțiuni și fațade fiecare nivel e marcat în dreapta cu numele și cota lui. **Gestionare → Stiluri de obiecte → Linii ascunse** stabilește și **semnul cotei de nivel**: triunghi gol, triunghi cu jumătatea stângă neagră (cum îl desenează standardul românesc), triunghi negru sau liniuță, și mărimea lui pe hârtie.

Cotele de nivel se scriu în unitățile proiectului: în metri (+3.00) într-un proiect metric, cum le scriu planșele, și în picioare și țoli (+9'-10 1/8") într-unul imperial. Le urmează browserul, caseta din stânga sus a vederii, marcajele de nivel din secțiuni și fațade și cotele de nivel.

## Cote relative sau absolute

Cotele se scriu relativ la ±0,00, dacă nu spui altfel. **Gestionare → Amplasament → Niveluri** stabilește cum le scrie tot proiectul:

- **Cotele scrise ca**: **Relative la ±0.00** (+3.00, implicit), **Altitudini (absolute)** (+653.00, înălțimea față de nivelul mării, cum o cere secțiunea de racordare dintr-o autorizație de construire) sau **Ambele** (+3.00 (+653.00)).
- **Nivelul cotei zero**: nivelul a cărui cotă e ±0,00 pe planșe (parterul, de exemplu). **Niciunul** păstrează 0,00 al modelului. Modelul nu se mută.
- **±0.00 =**: altitudinea cotei ±0,00. Scrie 650.00 și cota punctului de bază se stabilește astfel încât ±0,00 să fie la 650,00; modelul nu se mută. Alături, nota de legare cum o scriu planșele: ±0.00 = +650.00.

![Gestionare → Amplasament → Niveluri: ambele, ±0.00 = 650.00 și notația](shots/level-datum.webp)

Altitudinea e întotdeauna cota punctului de bază; cota punctului topografic e doar informativă. Toate cotele o urmează: semnele de nivel din secțiuni și fațade, cotele de nivel, cotele punctuale, platformele, etichetele curbelor de nivel, cotele radierelor rigolelor, baza golurilor, jetoanele de cotă ale simbolurilor, documentele și indicatoarele.

![O secțiune cu semnele de nivel +0.00 (+650.00)](shots/level-datum-section.webp)

O vedere poate spune altfel: **Niveluri** în proprietățile vederii (ca proiectul, relative, absolute sau ambele). Pe o planșă, **Cotele în vederile ei** le setează pe toate vederile puse pe ea deodată, iar fiecare vedere păstrează alegerea. O singură cotă de nivel își alege reperul în **Scrisă ca**.

**Notația.** Sub **Niveluri**, blocul **Notație** stabilește textele: cota relativă `{level}`, altitudinea `{altitude}`, ambele `{level} ({altitude})`, nota de legare `{level} = {altitude}`, semnul zero ±, plusul, minusul și plusul dinaintea unei altitudini. Presetările sunt orientative, de verificat după practica după care lucrezi: **Franța (NGF)** scrie 653.00 NGF, fără plus; **România (MN75)** +653.00 MN75; **Germania (NHN)** 653.00 ü. NHN. Un simbol sau un indicator citește `{level.altitude}`, `{level.relative}`, `{level.tie}`, `{project.altitude}` și `{project.levelTie}`; un document `{{project.altitude}}`, `{{project.levelTie}}` și, pentru fiecare nivel, `{{storey.altitude}}` și `{{storey.level}}`.

**În IFC.** Exportul scrie altitudinea ca ElevationOfRefHeight al IfcBuilding (și, ca înainte, RefElevation al IfcSite și înălțimea conversiei de hartă); cotele nivelurilor rămân ale modelului. Modul și nivelul cotei zero merg cu datele proprii ale lui Lapin și revin când Lapin citește fișierul. Un IFC din alt program, fără conversie de hartă, dă unui proiect fără altitudine altitudinea lui, din ElevationOfRefHeight, altfel din RefElevation.

Nimic nu se salvează până nu schimbi ceva: un proiect făcut de o versiune mai veche își scrie cotele ca înainte.

## Nivelul de dedesubt

Ca să vezi alt nivel în timp ce desenezi, alege-l în lista **Fundal** din bara planului. Planul acelui nivel se desenează dedesubt, estompat și gri. Te poți prinde (snap) de el, dar nu-l poți selecta.

![Etajul desenat peste parter, cu parterul ca fundal](shots/underlay.webp)

Fundalul doar te ajută să desenezi; nu se tipărește. Ca să tipărești pe un plan elementele altor niveluri, deschide proprietățile vederii de plan și alege-le în **Etaje afișate**. Poți arăta câteva niveluri sau pe toate, în halftone dacă vrei.

## Copierea pe alte niveluri

Ca să repeți pereți, plăci sau un plan întreg pe alte niveluri, selectează-le (pereți cu ușile și ferestrele lor, plăci, încăperi, un grup…) și alege **Modifică → Copiază pe niveluri**. Poți și să scrii `COPYSTOREY` în linia de comandă sau să dai clic dreapta pe selecție într-un plan sau în vederea 3D. Bifează nivelurile pe care copiezi. **Toate deasupra** și **Toate dedesubt** bifează toate nivelurile de peste sau de sub nivelul selecției, iar Shift-clic bifează toate nivelurile dintre cel pe care dai clic și ultimul pe care ai dat clic. Nivelul pe care stă selecția nu se poate bifa.

![Copiază pe niveluri: două niveluri bifate și opțiunile](shots/copy-to-storeys.webp)

- **Relativ** (implicit): fiecare copie stă la aceeași înălțime peste nivelul ei nou ca originalul peste al lui. Un perete desenat la parter stă pe etaj.
- **Absolut**: fiecare copie rămâne unde e originalul în spațiu, pe nivelul nou. Decalajul bazei ei compensează diferența.
- **Și elementele găzduite**: ușile și ferestrele merg cu pereții lor, iar golurile cu ce taie. Etichetele și cotele elementelor copiate trec din planul nivelului lor în planul nivelului nou. O încăpere își ia finisajele, iar o scară spațiul de sub ea. Debifează opțiunea ca să copiezi pereții fără uși și ferestre.
- **Refă relațiile dintre copii**: pereții îmbinați într-un colț sau într-un T se îmbină la fel pe fiecare nivel nou. Un perete atașat de o placă sau de un acoperiș copiat împreună cu el se atașează de copia lui. Un grup devine un grup al copiilor, iar un ansamblu se copiază întreg. O scară urcă până la nivelul de deasupra celui nou și își face golul ei în placa de acolo.
- **Sari peste duplicate**: un nivel care are deja același element în același loc nu primește încă unul. Mesajul de jos spune câte au fost sărite.

Copiile folosesc aceleași tipuri ca originalele și primesc GlobalId-uri IFC noi. Toate copiile sunt un singur pas de anulare și rămân selectate după aceea, ca să le poți muta sau schimba împreună. Într-o sesiune comună, copiile unui invitat le anulează doar invitatul.

![Pereții și placa parterului copiate pe cele două niveluri de deasupra, în 3D](shots/copy-to-storeys-3d.webp)

Ca să muți selecția pe alt nivel, alege **Modifică → Mută pe nivel**. Păstrează înălțimile elementelor peste nivelul nou sau le lasă unde sunt în spațiu.

## Cota unui element

Un element de pe un nivel stă la cota nivelului și la un decalaj față de ea. **Proprietăți → Amplasare** arată **Nivel**, **Decalaj față de nivel** și **Cotă** față de ±0,00: baza unui perete sau stâlp, fața de sus a unei plăci sau grinzi (decalajul grinzii e față de fața de sus a nivelului, unde începe nivelul de deasupra, așa că apare ca **Decalaj sus (de la nivelul de deasupra)**), fața de jos a unei fundații (nivelul de jos al tipului ei sub nivel, plus decalajul). Scrie oricare dintre cifre și cealaltă o urmează: elementul se mută în toate vederile, într-un singur pas de anulare. Ca să pui un element pe alt nivel, schimbă-i **Nivel**. Ce desenezi într-un plan stă pe nivelul acelui plan.
