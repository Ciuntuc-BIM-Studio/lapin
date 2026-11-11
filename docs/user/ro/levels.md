---
title: Niveluri și planuri
order: 60
summary: Adaugi niveluri, le setezi cotele și vezi alt nivel sub planul pe care desenezi.
---

<!-- video levels -->

## Niveluri

Navigatorul proiectului listează nivelurile sus, cel mai înalt primul. **+** de lângă **Niveluri** adaugă un nivel peste cel mai înalt.

Selectează un nivel ca să vezi **cota de bază** și **cota superioară**. Oricare poate fi deasupra sau dedesubtul cotei ±0,00. Înălțimea nivelului e distanța dintre ele. Când muți baza, partea de sus rămâne pe loc. Un proiect nou începe cu Foundations, Ground floor și First floor. Un nivel are cel mult 100 m înălțime și stă în ±10 km față de ±0,00: o valoare scrisă în altă unitate e refuzată. O scară care ar urca mai mult de 50 m până la nivelul de deasupra se oprește acolo și spune de ce.

![Proprietățile unui nivel: cota de bază, cota superioară și înălțimea](shots/level-properties.webp)

Fiecare nivel are planul lui sub **Planuri**. Dublu clic pe un plan ca să desenezi pe acel nivel. Nivelul pe care desenezi apare în caseta din stânga sus a vederii.

În secțiuni și fațade fiecare nivel e marcat în dreapta cu numele și cota lui. **Gestionare → Stiluri de obiecte → Linii ascunse** stabilește și **semnul cotei de nivel**: triunghi gol, triunghi cu jumătatea stângă neagră (cum îl desenează standardul românesc), triunghi negru sau liniuță, și mărimea lui pe hârtie.

Cotele de nivel se scriu în unitățile proiectului: în metri (+3.00) într-un proiect metric, cum le scriu planșele, și în picioare și țoli (+9'-10 1/8") într-unul imperial. Le urmează browserul, caseta din stânga sus a vederii, marcajele de nivel din secțiuni și fațade și cotele de nivel.

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

Un element de pe un nivel stă la cota nivelului și la un decalaj față de ea. **Proprietăți → Amplasare** arată **Nivel**, **Decalaj față de nivel** și **Cotă** față de ±0,00: baza unui perete sau stâlp, fața de sus a unei plăci sau grinzi (decalajul grinzii e față de fața de sus a nivelului), fața de jos a unei fundații (nivelul de jos al tipului ei sub nivel, plus decalajul). Scrie oricare dintre cifre și cealaltă o urmează: elementul se mută în toate vederile, într-un singur pas de anulare. Ca să pui un element pe alt nivel, schimbă-i **Nivel**. Ce desenezi într-un plan stă pe nivelul acelui plan.
