---
title: Calcul structural
order: 52
summary: Rulează un calcul static liniar al cadrului sub încărcările tale, citește diagramele eforturilor și deformata în 3D și stabilește articulațiile capetelor barelor.
---

**Rulează calculul** din grupul **Încărcări** al tabului **Structură** (și din dialogul Încărcări) calculează barele, plăcile și pereții modelului sub încărcările și combinațiile din [Încărcări](loads.md) și arată eforturile din ele.

![Grupul Încărcări, cu Rulează calculul](shots/ribbon-analysis.webp)

> Rezultatele sunt **orientative**. Încărcările, combinațiile și calculul trebuie verificate de tine înainte să te bazezi pe ele.

## Ce se calculează

- **Barele** (stâlpi, grinzi, elemente de oțel și de lemn) intră cu rigiditatea lor, din material și secțiune. O bară al cărei material nu are modul de elasticitate este lăsată deoparte și listată.
- **Planșeele și plăcile de beton** sunt calculate ca **elemente de placă**: fiecare este împărțită în elemente mici (o plasă, implicit 0,5 m) care se încovoaie și se întind, cu nodurile comune cu grinzile, stâlpii și pereții pe care îi întâlnesc. Placa își duce singură încărcările la ce o susține, prin rigiditatea ei și a lor. O grindă sub o placă își păstrează axa dedesubt și lucrează cu placa ca o grindă în T.
- **Pereții portanți de beton** sunt tot elemente de placă: stau pe fundațiile lor (un perete cu nimic modelat sub el este ținut pe toată talpa) și duc planșeele de pe ei, precum și vântul sau alte încărcări laterale în planul lor.
- **Pereții de zidărie** și cei din alte materiale sunt reazeme: țin plăcile și barele de pe ei, dar nu sunt calculați ca pereți.
- **Planșeele din alte materiale** (un planșeu de lemn) își trec încărcările barelor și pereților de sub ele după **regula de 45°**: bisectoarele colțurilor împart placa în zonele pe care le duce fiecare latură, iar fiecare devine o încărcare trapezoidală sau triunghiulară pe bara de dedesubt.
- **Acoperișurile cu șarpantă** trec încărcarea fiecărui căprior după **lățimea lui aferentă**, până la jumătatea distanței față de vecini; zăpada se ia pe proiecția orizontală.
- **Tijele și tiranții** (o bară plină rotundă care contravântuiește, un tirant sau un cablu) lucrează axial: sunt articulați la capete, iar greutatea lor merge la capete, deci nu se încovoaie ca grinzile. Câmpul **Bară cu efort axial** din proprietățile **Analiză** ale unei bare le schimbă: **Din secțiune și utilizare**, **Da (tijă sau tirant)** sau **Nu (grindă)**.
- **Grinzile de înălțimi diferite** care se întâlnesc la un stâlp își păstrează fiecare cota; stâlpul are un singur nod, iar grinzile sunt legate de el printr-un braț rigid.
- **Vântul pe fațade** merge la grinzi, rigle și marginile planșeelor de-a lungul lor; fâșia de la baza lor merge direct în teren și e listată separat de sumele barelor.
- **Greutatea proprie** a barelor se ia de-a lungul lor, iar încărcările desenate de tine și cele derivate merg acolo unde găsesc ceva dedesubt. O încărcare care nu găsește nimic este lăsată deoparte și raportată.

## Rularea

Alege **Rulează calculul**. Se deschide dialogul **Rezultatele calculului**:

![Dialogul Rezultatele calculului](shots/analysis-results.webp)

- Rezumatul: barele, elementele de placă, nodurile și necunoscutele calculului și cât a durat.
- Pentru fiecare caz, **suma încărcărilor** față de **suma reacțiunilor** și dacă se **echilibrează** (bifă) sau nu (cruce: nu te baza pe acele rezultate). Încărcările nelegate sunt date în kN.
- Ce a fost **lăsat în afara** calculului și de ce.
- Valorile **cele mai mari și cele mai mici** ale eforturilor și momentelor, deplasarea și săgeata maximă (cu raportul față de deschidere, L/300) ale cazului, combinației sau înfășurătorii alese; sub ele, valorile cele mai mari și cele mai mici pe **plăci și pereți**, și unde sunt.
- **Elemente de placă**: ce plăci și pereți sunt calculate ca plăci (**Plăci și pereți**, **Doar plăcile**, **Doar pereții** sau **Niciunul**: plăcile după regula de 45° și pereții ca reazeme) și dimensiunea **Plasei** în metri. O plasă mai mică este mai exactă și mai lentă; o piesă îngustă primește mereu cel puțin patru elemente pe lățime. O schimbare este un singur pas de anulare; **Rulează din nou** o preia.

**Arată diagramele** închide dialogul și arată rezultatele în vederea 3D.

## Diagramele în 3D

Un card peste vederea 3D alege ce se desenează:

![Momentul încovoietor al înfășurătorii la starea limită ultimă](shots/analysis-moments.webp)

- **Ce**: un caz de încărcare, o combinație sau **înfășurătoarea** combinațiilor la starea limită ultimă (SLU) sau de serviciu (SLS): valoarea cea mai mare și cea mai mică în fiecare punct.
- **Mărime**: **N** efortul axial (întinderea pozitivă), **Vy** și **Vz** forțele tăietoare, **T** torsiunea, **My** și **Mz** momentele încovoietoare (**Mz** este axa tare a unei grinzi orizontale, pozitiv cu fibra întinsă jos) sau forma **Deformată**.
- **Scară**: mărimea diagramei pe ecran; modelul în sine nu se schimbă.
- **Reacțiuni**: săgeți la reazeme cu valorile lor în kN.

Fiecare bară are linia de bază, curba, hașuri între ele și valorile la extreme; roșu înseamnă pozitiv, albastru negativ. Momentele se desenează pe partea în care bara este întinsă.

![Forma deformată](shots/analysis-deformed.webp)

## Plăci și pereți

Când plăcile sau pereții sunt calculați ca elemente de placă, cardul are un al doilea rând de butoane, **Plăci și pereți**, iar elementele se colorează în benzi după mărimea aleasă (un al doilea clic pe ea scoate culorile):

![Momentul my al planșeului, în benzi](shots/analysis-slab.webp)

- **w**: deplasarea perpendiculară pe placă sau perete (în sus pozitivă pe o placă).
- **mx**, **my**: momentele încovoietoare pe direcțiile x și y ale plăcii (X și Y globale pe o placă; de-a lungul și în sus pe un perete), în kN·m pe metru. Pe o placă, **pozitiv înseamnă fața de jos întinsă** (în câmp), negativ fața de sus (peste reazem); pe un perete, fața din dreapta direcției lui.
- **mxy**: momentul de torsiune; **nx**, **ny**, **nxy**: eforturile în planul plăcii (întinderea pozitivă), în kN pe metru; **vx**, **vy**: forțele tăietoare transversale.

Legenda de pe card arată valorile fiecărei benzi și unitatea:

![Legenda curbelor de nivel](shots/analysis-legend.webp)

Aceleași benzi sunt desenate pe planul etajului plăcii, pentru ecran; planșele nu le tipăresc încă. Pentru o înfășurătoare, fiecare punct arată valoarea cu modulul mai mare dintre cea mai mică și cea mai mare.

## Articulațiile capetelor

Selectează un stâlp, o grindă sau un element: în proprietățile lui este o secțiune **Calcul** cu articulațiile **Început (i)** și **Sfârșit (j)**.

![Secțiunea Calcul a unei grinzi](shots/analysis-bar.webp)

- **Implicit**: ce spune îmbinarea (o eclisă, un guseu sau o contravântuire articulează elementul), un element de lemn al acoperișului articulat pe axa tare, orice altă bară încastrată.
- **Încastrat**: capătul preia momente. **Articulat**: se rotește liber în jurul ambelor axe de încovoiere.
- **Personalizat**: șase căsuțe, câte una pentru fiecare libertate (deplasarea pe bară, pe fiecare axă de forfecare, torsiunea, rotirea în jurul fiecărei axe de încovoiere); o libertate bifată este eliberată.

O schimbare este un singur pas de anulare.

## Când se schimbă modelul

Rezultatele se calculează din modelul așa cum este și nu se salvează. Când schimbi modelul, diagramele dispar din vedere și cardul spune asta:

![Cardul după o schimbare](shots/analysis-pick.webp)

**Rulează din nou** calculează noul model.

## Un mecanism

Dacă structura se poate mișca fără rezistență (prea multe articulații sau o parte pe care nimic n-o ține), calculul se oprește și spune unde:

![Un mecanism](shots/analysis-mechanism.webp)

Numește nodul, libertatea și barele în cauză; **Selectează elementele implicate** le selectează ca să le poți corecta, de obicei încastrând un capăt sau adăugând un reazem.
