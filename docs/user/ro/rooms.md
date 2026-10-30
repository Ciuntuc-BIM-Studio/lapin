---
title: Încăperi și culorile lor
order: 45
summary: Pui încăperi cu un clic între pereți, le dai nume și numere și le colorezi sau hașurezi pe plan după funcțiune sau departament, cu o legendă.
---

## Cum pui o încăpere

Alege **Arhitectură → Încăpere** (<kbd>RM</kbd>) și dă clic într-un spațiu închis de pereți. Lapin găsește conturul încăperii între fețele pereților, ale pereților cortină și ale stâlpilor din jurul punctului. Ușile nu deschid încăperea: conturul se închide peste ele. Un stâlp din încăpere nu intră în aria ei.

Încăperea se ține după pereți. Muți un perete și încăperea crește sau scade odată cu el. Dacă un perete e șters și încăperea nu mai e închisă de nimic, păstrează conturul pe care îl avea, iar eticheta ei se face roșie. **Arhitectură → Actualizează încăperile** salvează conturul pe care îl găsesc încăperile acum.

Pentru un spațiu care nu e închis de pereți peste tot, cum e o terasă sau un capăt al unui spațiu deschis, folosește **Încăpere după contur** și dă clic în colțurile lui. Încăperea aceasta păstrează conturul pe care îl desenezi.

![Un plan cu două încăperi colorate după funcțiune și legenda culorilor](shots/rooms-plan.webp)

## Proprietățile unei încăperi

Selectezi o încăpere cu un clic pe eticheta ei sau oriunde în interiorul ei. În panoul de proprietăți:

- **Nume** și **Număr.** Încăperile noi sunt numerotate pe niveluri: 101, 102… la parter.
- **Funcțiune**, **Departament** și **Finisajul pardoselii.** Scrii ce vrei sau alegi o valoare pe care o au deja alte încăperi.
- **Înălțime.** Gol înseamnă înălțimea nivelului.
- **Culoare proprie** și **Hașură proprie** înlocuiesc schema de culori doar pentru această încăpere.

Cantitățile de dedesubt arată aria netă a încăperii (fără stâlpi), perimetrul și volumul ei. Ca să muți eticheta, trage de punctul de pe ea.

![Proprietățile unei încăperi](shots/room-properties.webp)

## Culorile încăperilor

**Vedere → Culorile încăperilor** colorează încăperile unui plan. Alege o schemă, alege în **Colorează după** ce anume colorează (**Funcțiune**, **Nume**, **Departament**, **Număr** sau **Finisajul pardoselii**) și alege schema la **Planul acesta arată**. Fiecare valoare pe care o au încăperile primește un rând cu:

- o **Culoare**,
- o **Hașură** din modelele proiectului, cu scara ei (**Scalează**) și culoarea liniilor (**Cerneală**),
- și o previzualizare a felului în care arată.

**Completează culorile lipsă** dă fiecărei valori fără culoare una dintr-o paletă discretă. **Nou** și **Duplică** fac alte scheme, de exemplu una pentru prezentarea la client și una pe departamente.

![Dialogul Culorile încăperilor](shots/room-colors.webp)

Culorile se desenează sub pereți, sub mobilier și sub deschiderile ușilor, așa că planul rămâne ușor de citit. Se tipăresc pe planșe și intră în exporturile PDF și DXF. Un plan nou nu arată culorile încăperilor până nu îi alegi o schemă, în **Culorile încăperilor** sau în proprietățile planului.

## Legenda culorilor

**Adnotări → Legenda culorilor**: dă clic pe plan unde vrei legenda. Ea listează culorile pe care le folosesc încăperile planului, cu numele lor, și urmează orice schimbare a schemei. O muți cu **Mută**.

## Încăperile în 3D și în IFC

Încăperile sunt ascunse în vederea 3D, ca să nu acopere clădirea. **Vizibilitate / Grafică** în vederea 3D le poate arăta.

La exportul IFC fiecare încăpere e un IfcSpace, cu numărul, numele, funcțiunea, ariile și volumul ei.

## Încăperi dintr-un fișier IFC

**Fișier → Importă IFC…** aduce spațiile fișierului ca încăperi, oricare ar fi programul care le-a scris (Revit, ArchiCAD, Allplan, Bonsai…):

- fiecare ajunge pe **nivelul ei**, cel în care o pune fișierul;
- **conturul** ei vine din forma ei (o extrudare, un B-rep sau o rețea de fețe, ori amprenta desenată lângă ea); îl păstrează, nu urmează pereții;
- **numele** ei e numele lung al spațiului, iar **numărul** e numele lui (așa le scriu Revit și ArchiCAD); **înălțimea** și, când începe deasupra nivelului (o galerie, o pardoseală înălțată), **decalajul la bază** vin din forma ei;
- **funcțiunea**, **departamentul** și **finisajul pardoselii** vin din fișier, acolo unde el le spune (tipul obiectului sau categoria spațiului, o proprietate Department, pardoseala), așa că o schemă de culori după funcțiune sau după departament le colorează imediat;
- tot restul pe care spațiul îl avea în fișier, seturile de proprietăți și cantitățile lui (Pset_SpaceCommon, BaseQuantities, seturile proprii ale programului), se păstrează și e listat, needitabil, la **Proprietăți IFC** în proprietățile ei, cu un filtru când sunt multe.

Mesajul importului spune câte încăperi au intrat. Le găsești pe planurile nivelurilor lor (fiecare își arată eticheta), într-un extras **Încăperi** (**+** de lângă **Extrase** în navigator) sau în tabul IFC al navigatorului; vederea 3D ascunde încăperile.

Un spațiu a cărui formă nu dă un contur intră totuși, cu numele și proprietățile lui, ca încăpere fără contur: apare în extras, dar nu se desenează nicăieri. Selectează-l (din tabul IFC al navigatorului), deschide planul nivelului lui, apoi **Găsește printr-un clic** (clic între pereții lui) sau **Desenează conturul** în proprietățile lui.
