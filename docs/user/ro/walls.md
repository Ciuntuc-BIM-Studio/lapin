---
title: Pereți
order: 20
summary: Desenezi pereții colț cu colț, îi modifici în panoul de proprietăți și îți faci tipuri de pereți cu straturile lor.
---

<!-- video walls -->

## Desenarea pereților

Deschide tabul **Arhitectură** și alege **Perete**, sau scrie <kbd>WA</kbd>.

![Tabul Arhitectură](shots/ribbon-architecture.webp)

Dă clic unde începe peretele, apoi la fiecare colț. Peretele următor urmează mouse-ul. În timp ce desenezi, poți scrie o lungime și un unghi în căsuțele de deasupra vederii în loc să dai clic. <kbd>Enter</kbd> sau <kbd>Esc</kbd> oprește desenarea.

![Desenarea pereților: peretele următor urmează mouse-ul, cu lungimea și unghiul afișate](shots/wall-drawing.webp)

Unde se întâlnesc doi pereți, Lapin îi îmbină singur. Straturile lor intră unele în altele așa cum s-ar construi.

- **Perete curb** (<kbd>WC</kbd>) desenează un perete pe un arc.
- **Perete cortină** (<kbd>CW</kbd>) desenează un perete vitrat cu montanți și panouri.
- **Îmbină** (<kbd>JW</kbd>), **La 45°** și **Cap la cap** schimbă felul în care se întâlnesc doi pereți.
- **Inversează** schimbă care dintre cei doi pereți trece prin colț.

## Modificarea unui perete

Dă clic pe un perete ca să-l selectezi. Apar mânerele lui. Trage de capete ca să-l lungești sau să-l scurtezi, sau de crucea din mijloc ca să-l muți.

![Un perete selectat, cu mânerele lui](shots/wall-selected.webp)

Panoul de proprietăți arată peretele selectat. Sus sunt tipul și straturile lui. Dedesubt sunt lungimea, înălțimea, decalajul la bază, nivelul și faza. Schimbi orice valoare, și modelul o urmează.

![Proprietățile unui perete](shots/wall-properties.webp)

**Întoarce** mută straturile peretelui pe partea cealaltă. Poți da clic și pe săgețile mici de lângă un perete selectat în plan.

## Tipurile de pereți

Un tip de perete e un set de straturi, de la fața exterioară la cea interioară. Fiecare strat are un material și o grosime, iar unul dintre ele e **miezul**. Linia peretelui merge pe miez. Ca să editezi tipurile, alege **Arhitectură → Tipuri de pereți**.

![Tipuri de pereți: straturile unui perete de cărămidă](shots/wall-types.webp)

- **+ Layer** adaugă un strat. Săgețile mută un strat în sus sau în jos.
- **Zone pe înălțime** împarte miezul de jos în sus, de exemplu un prim rând, zidăria și o centură.
- **Buiandrugi** alege buiandrugii puși peste uși și ferestre: prefabricați sau turnați pe loc, cu rezemarea și înălțimea lor.
- **Desenează cu el** închide fereastra și începe desenarea pereților de acest tip.

O schimbare a tipului schimbă toți pereții de acel tip.

## Pereți sub acoperiș

Pereții de pe același nivel cu un acoperiș se opresc singuri sub el și urcă până la frontoanele lui. Pentru un perete de pe alt nivel, selectează-l și alege **Arhitectură → Atașează la acoperiș** (<kbd>AR</kbd>), apoi dă clic pe acoperiș, sau pe orice alt corp până la care trebuie să urce peretele: un solid din Studio, un element importat. Vezi [Plăci și acoperișuri](floors-roofs.md).
