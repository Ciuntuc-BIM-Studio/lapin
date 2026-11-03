---
title: Oțel
order: 56
summary: Bare metalice cu capete configurabile — prelungite sau retrase, tăiate după un plan, la 45°, ajustate pe altă bară, crestate — făcute și dintr-un contur desenat; profile tubulare și formate la rece.
---

## Tabul Oțel

![Tabul Oțel](shots/ribbon-steel.webp)

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

## IFC și cantități

O bară se exportă ca **IfcMember** cu **IfcMemberType**-ul ei. Corpul se scrie cum scriu oțelul celelalte programe (Tekla, Advance Steel, Revit): secțiunea **extrudată** (IfcExtrudedAreaSolid), **tăiată** de planele capetelor (IfcBooleanClippingResult cu IfcHalfSpaceSolid), iar fiecare crestătură un **IfcOpeningElement** care o golește. O grindă sau un stâlp metalic cu capete se scrie la fel. Cantitățile fiecărei bare dau lungimea dintre tăieturi, aria secțiunii, volumul și greutatea netă (7850 kg/m³).
