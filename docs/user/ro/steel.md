---
title: Oțel
order: 56
summary: Bare metalice cu capete configurabile — prelungite sau retrase, tăiate după un plan, la 45°, ajustate pe altă bară, crestate — făcute și dintr-un contur desenat; profile tubulare și formate la rece; hale cu cadre portal și vute.
---

## Tabul Oțel

![Tabul Oțel](shots/ribbon-steel.webp)

- **Hală cu cadre portal…**: o hală metalică întreagă din câteva cifre, în configuratorul de hală (mai jos).
- **Bară** (<kbd>SM</kbd>): o bară metalică între două puncte, la nivelul de sus al etajului, cu fața de sus a secțiunii pe axă, ca o grindă. Tipul îl alegi în Proprietăți înainte de primul clic, sau din tabul **Catalog**, unde fiecare tip metalic e desenat după secțiunea lui.
- **Convertește în bară**: contururile desenate selectate devin bare (mai jos).
- **La 45°** și **Încadrează**: două bare îmbinate la capete (mai jos).
- **Tipuri metalice**: tipurile de bare ale proiectului și altele noi.

O bară e un IfcMember: axa ei merge oriunde în spațiu (o diagonală, un căprior, un montant), secțiunea vine din tipul ei, se poate roti în jurul axei (**Rotire**), iar axa poate trece prin centrul, fața de sus sau fața de jos a secțiunii (**Punct de inserție**). Grinzile și stâlpii structurii dintr-un profil metalic primesc aceleași capete.

## Capetele

Selectezi o bară, sau o grindă ori un stâlp drept dintr-un profil metalic: secțiunea **Oțel** din Proprietăți arată cele două capete, **Început** și **Sfârșit**. Fiecare capăt e un set de reguli; corpul le urmează.

![Capetele unei bare în Proprietăți](shots/steel-ends.webp)

- **Prelungire**: cât trece capătul de punctul lui; negativă, capătul e retras (un joc de montaj).
- **Tăietură**: **Perpendicular pe axă**; **După un plan**, rotit față de tăietura dreaptă **În plan** (în jurul verticalei secțiunii) și **În elevație** (în jurul laturii ei), în grade; **La 45°** cu altă bară, pe planul care împarte în două colțul dintre ele; **Ajustat pe o bară**, pe fața celeilalte bare la care ajunge (talpa unui stâlp, sau latura inimii lui când stâlpul e rotit).
- **Crestătură**: tălpile crestate **Sus** și/sau **Jos**, pe **Lungimea crestăturii** înapoi de la capăt și pe **Adâncimea crestăturii** de la marginea secțiunii, ca la o grindă prinsă de o grindă principală.

Îmbinarea la 45° și ajustarea numesc cealaltă bară: o muți, iar capătul o urmează; o ștergi, iar capătul redevine drept și spune de ce. **La 45°** din tab îmbină cele două bare selectate în colțul unde se întâlnesc; **Încadrează** ajustează prima bară selectată pe a doua.

## Din schiță în bară

Desenezi conturul barei ca o polilinie închisă sau un dreptunghi (**Desen**), apoi **Oțel → Convertește în bară** cu unul sau mai multe contururi selectate. Într-un singur pas fiecare contur e citit și înlocuit cu o bară:

![Două contururi schițate în plan](shots/steel-sketch.webp)

- cele **două laturi lungi paralele** (în limita unei jumătăți de grad) dau axa, linia lor de mijloc de la un capăt la altul, și **lățimea** dintre ele;
- **latura de la fiecare capăt** dă tăierea capătului: dreaptă, sau după un plan cu înclinarea laturii.

Pe un **plan**, conturul e bara văzută de sus: lățimea e a tălpilor, iar un profil cu acea lățime e căutat printre tipurile de bare ale proiectului și în catalog (altfel tipul ales pentru Bară); bara stă la nivelul de sus al etajului. Pe o **secțiune** sau o **fațadă**, conturul e bara văzută din lateral: lățimea e înălțimea secțiunii, un capăt înclinat e o tăiere în elevație, iar o **treaptă** în unghi drept la un capăt e o **crestătură** sus sau jos; bara stă unde e desenată. O treaptă desenată în plan e refuzată (crestătura se vede din lateral). Un contur care nu e o bandă (un triunghi, un L, laturi neparalele) rămâne cum e, iar Lapin spune de ce.

## Tipuri metalice

**Oțel → Tipuri metalice** listează tipurile de bare ale proiectului cu greutatea pe metru și face altele noi: un profil din catalog (IPE, HEA, HEB, UPN, L, **RHS**, **CHS** și profilele generice formate la rece **C**, **Z** și **Σ**) sau o secțiune tubulară ori formată la rece **cu dimensiunile ei** (înălțime, lățime, grosime, rebord). Tipul nou poate fi un tip de bară, sau un **tip de grindă** ori **tip de stâlp** al structurii, ca uneltele Grindă și Stâlp să pună și ele oțel. Un clic pe un tip din listă îl face pe cel pe care îl pune Bară.

![Dialogul Tipuri metalice](shots/steel-types.webp)

Profilele tubulare sunt goale în interior, în vederea 3D, în secțiuni și în cantități. Profilele formate la rece sunt forme generice cu dimensiuni, cu îndoiturile în unghi drept, fără catalog de producător.

![Bare în 3D](shots/steel-3d.webp)

## Hale cu cadre portal

**Oțel → Hală cu cadre portal…** deschide configuratorul de hală. Ca al acoperișului, are stilurile în stânga, câmpurile halei în file la mijloc și, în dreapta, hala așa cum ar construi-o Lapin: un cadru curent văzut de-a lungul halei, cu vutele lui, și un rezumat al greutăților.

![Configuratorul de hală](shots/steel-hall.webp)

- **Stilurile**: halele tipice din catalog (**Hală 20 m**, **Hală 30 m**, un **Șopron 10 m** într-o apă) și cele ale proiectului. Un stil ales completează toate câmpurile; oricare se poate schimba apoi. **Salvează ca stil…** păstrează hala așa cum e configurată ca stil al proiectului; **Actualizează stilul** o salvează din nou în stilul ei, iar toate halele stilului îl urmează.
- **Cadru**: **Deschiderea** (de la axa unui stâlp la a celuilalt), **Traveile** (egale ca `5 × 6`, sau fiecare pe rând: `6, 6, 7.5`), **Înălțimea la streașină** (fața de sus a riglelor deasupra feței exterioare a stâlpului), **Acoperișul** (în două ape sau într-o apă) și **Panta** lui, profilele **Stâlpilor** și ale **Riglelor**, și **Picioarele stâlpilor** (articulate sau încastrate, păstrate pentru nodurile care vin).
- **Vute**: **Vuta la streașină** și **Vuta la coamă**, fiecare cu **Lungimea** de-a lungul riglei de la fața stâlpului (sau de la coamă) și **Adâncimea** sub riglă acolo, și cum e **Executată**: **tăiată din profilul riglei** (un I tăiat oblic de-a lungul inimii, o jumătate sudată sub riglă: inima și talpa riglei) sau **din plăci sudate** (o **Placă a inimii** și o **Placă a tălpii** cu grosimile lor, și lățimea tălpii).

![Vutele din plăci sudate](shots/steel-hall-haunches.webp)

- **Frontoane**: **stâlpi de fronton sub o riglă simplă** (stâlpi de colț și stâlpi la **Distanța** aleasă, cu axa puternică perpendiculară pe peretele de fronton, sub o riglă mai ușoară care merge până la fața exterioară a pereților, stâlpii ajustați pe fața ei de jos), sau **un cadru curent** la fiecare capăt.

![Un fronton cu stâlpi](shots/steel-hall-gable.webp)

- **Elemente secundare**: **Distanța dintre pane**, **Distanța dintre riglele de perete**, **Traveile contravântuite** (numerele lor, de la prima) și o **Grindă de streașină**. Se păstrează cu hala pentru panele, riglele și contravântuirile care vin; încă nu se construiesc.
- **Amplasare**: unde se întâlnesc axa A și axa 1, și direcția deschiderii în plan (**Rotire**); previzualizarea arată axele halei de sus.

**Creează hala** o face într-un singur pas, pe care o anulare îl ia înapoi: **axele** ei proprii (axele 1, 2… la cadre, A și B la stâlpi), iar la fiecare cadru doi **stâlpi** (stâlpi ai structurii dintr-un profil metalic, cu inima în planul cadrului și capătul de sus tăiat după panta acoperișului) și două **rigle** îmbinate la 45° la coamă (una singură, de la stâlpul scund la cel înalt, la acoperișul într-o apă): bare metalice ajustate pe fețele stâlpilor, cu vutele lor. Hala stă pe etajul curent; streașina și coama sunt înălțimi ale cadrelor ei, nu etaje noi. Rezumatul dă cadrele, înălțimile și oțelul pe feluri: stâlpi, rigle, vute, stâlpi de fronton, în total și pe m² de pardoseală.

![Hala în 3D](shots/steel-hall-3d.webp)

Hala rămâne editabilă. Selectează una din barele ei: secțiunea **Oțel** arată hala și **Editează hala…**, care deschide configuratorul pe ea; **Aplică** reface hala peste aceleași bare, care își păstrează identitatea (o bară care nu mai trebuie dispare, una nouă se adaugă). Tot acolo sunt vutele riglei, fiecare schimbată separat (felul, lungimea, adâncimea, plăcile): o vută dată așa rămâne cum e când hala se reface; **×** o scoate.

![Vutele unei rigle în Proprietăți](shots/steel-hall-props.webp)

## IFC și cantități

O bară se exportă ca **IfcMember** cu **IfcMemberType**-ul ei. Corpul se scrie cum scriu oțelul celelalte programe (Tekla, Advance Steel, Revit): secțiunea **extrudată** (IfcExtrudedAreaSolid), **tăiată** de planele capetelor (IfcBooleanClippingResult cu IfcHalfSpaceSolid), iar fiecare crestătură un **IfcOpeningElement** care o golește. O grindă sau un stâlp metalic cu capete se scrie la fel. Cantitățile fiecărei bare dau lungimea dintre tăieturi, aria secțiunii, volumul și greutatea netă (7850 kg/m³).

O hală se exportă ca **IfcBuildingSystem** (LOADBEARING) căruia îi sunt atribuite barele ei, cu datele ei în setul de proprietăți `Lapin_SteelHall`, și cu propriul **IfcGrid**. O riglă cu vute e un **IfcElementAssembly** al riglei și al vutelor ei: o vută tăiată din profil e un **IfcMember**, una din plăci sudate două **IfcPlate**, fiecare extrudată (IfcExtrudedAreaSolid). Vutele nu intră în cantitățile riglei; rezumatul configuratorului le cântărește.
