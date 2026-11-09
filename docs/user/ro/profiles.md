---
title: Biblioteci de profile
order: 57
summary: Profilele de care ai nevoie, oricare ar fi standardul lor — setul EN mic al lui Lapin, tabelele și fișierele tale IFC, folderele firmei; desenate, cu proprietățile calculate și valorile din fișier verificate, copiate în proiect pe măsură ce le folosești.
---

Lapin nu poate livra toate profilele metalice folosite în lume, așa că vine cu un set european mic și citește restul din ce ai deja: un catalog ca foaie de calcul (AISC Shapes Database, tabelul unui producător, al firmei tale), o bibliotecă IFC de profile (a lui Bonsai, de exemplu) sau orice model IFC, un contur închis desenat în Lapin sau într-un DXF. **Gestionare → Biblioteci de profile** le ține pe toate.

![Biblioteci de profile](shots/profile-libraries.webp)

## Bibliotecile

- **Încorporată**: *EN steel (Lapin)*: IPE, HEA, HEB, HEM, UPN, UPE, corniere egale și inegale, SHS, RHS și CHS formate la cald în mărimi uzuale, C, Z și Σ generice formate la rece. Doar dimensiuni standard; toate proprietățile le calculează Lapin.
- **Pe acest calculator**: bibliotecile importate, pentru orice proiect. Și varianta din browser le păstrează, în acel browser.
- **Folderele firmei**: în aplicația desktop, oricâte foldere locale, din rețea sau sincronizate cu fișiere `.lapinprofiles` (**Adaugă un folder…**). Un folder e doar de citit până bifezi **de scris**; atunci butonul de salvare de lângă el scrie acolo biblioteca aleasă, pentru colegi.
- **În proiect**: doar profilele folosite se copiază în proiect, cu numele bibliotecii lor, ca fișierul să se deschidă oriunde.

Scrie în căutare ca să cauți în toate bibliotecile după nume, familie, standard sau fel; alege un profil ca să-l vezi desenat, cu dimensiunile și proprietățile lui.

## Proprietățile

Din dimensiunile unui profil, Lapin calculează aria, Iy, Iz, modulele elastice și plastice, razele de girație, axele principale și unghiul lor (corniere, Z, orice contur nesimetric), constanta de torsiune It și cea de deplanare Iw unde există o formulă, masa pe metru din densitatea materialului și suprafața de vopsit pe metru. Se potrivesc cu tabelele publicate cam la 1 % (It și Iw ale profilelor I laminate la 3 %; Iw al unui U cu tălpi înclinate doar la 20 %). Un contur arbitrar n-are It sau Iw decât dacă le dă fișierul lui.

Valorile date de un fișier se păstrează și se arată lângă ale lui Lapin. O diferență de peste 2 % (arie, masă), 3 % (inerții, module) sau 10 % (It, Iw) e marcată și intră în lista de verificare a importului; despre o coloană întreagă decalată cu o putere a lui 10 se spune că e în altă unitate.

![O valoare din fișier care diferă de a lui Lapin](shots/profile-check.webp)

## Importul unui tabel

**Tabel…** citește un CSV sau XLSX, un rând un profil. Sensul și unitatea fiecărei coloane sunt ghicite din titlu, în engleză, română, germană sau franceză (h, d, Höhe, hauteur, înălțime; tw, s, Stegdicke; A, Iy, Wel, Wpl, It, Iw, G, kg/m…), cu unități cu tot (mm, cm, in, cm², cm⁴, in⁴, ×10³ cm⁶, lb/ft). Un tabel cu Ix și fără Iz e citit american: x axa tare, în țoli. Corectează în dialog ce e greșit; Lapin ține minte alegerea pentru următorul fișier așezat la fel.

![Coloanele unui tabel potrivite](shots/profile-table.webp)

Felul unui rând vine din coloana lui de tip (W, HP, UPN, HSS, PIPE…) sau din nume; pentru celelalte alege felul în dialog.

## IFC

**IFC…** citește toate definițiile de profil dintr-un fișier IFC oarecare: o bibliotecă sau un model din Tekla, Revit ori ArchiCAD, unde profilele folosite de elemente iau numele tipurilor lor. I, I nesimetric, U, T, L, C, Z, țevile, dreptunghiul, cercul și elipsa își păstrează felul și dimensiunile; profilele arbitrare, compuse și derivate devin contururi. Vin și proprietățile mecanice din `Pset_ProfileMechanical` (și din proprietățile de profil IFC2x3); exportatoarele nu se înțeleg asupra unităților și asupra axei Y, așa că Lapin le hotărăște pe amândouă după geometrie. Profilele centre-line și trapez se lasă deoparte și se spune asta.

![Profile citite dintr-un fișier IFC](shots/profile-ifc.webp)

**IFC** și **CSV** de sub listă scriu biblioteca aleasă (sau profilele proiectului) ca bibliotecă IFC4 de profile ori ca tabel pe care Lapin îl citește înapoi.

## Un contur ca profil

Desenează secțiunea cu Desen, din polilinii închise și cercuri (cele din interiorul altuia sunt goluri), selectează-le și apasă **Din selecție**, sau citește desenul unui producător cu **DXF…**. Dă-i un nume, alege-i materialul și biblioteca și păstrează-l. Așa intră profilele de aluminiu și PVC, secțiunile prefabricate și formele de lemn: același depozit, cu materialul și densitatea lor.

![Un contur desenat făcut profil](shots/profile-outline.webp)

**Desenează-i conturul** face invers: conturul profilului ales ca polilinii închise în vederea din față, ca Studio să-l extrudeze sau să-l deruleze.

## Folosirea unui profil

În **Oțel → Tipuri metalice**, **Structură → Tipuri structurale** (stâlpi și grinzi) și la profilele halei cu cadre, alege **Dintr-o bibliotecă de profile…** și scrie ca să cauți.

![Alegătorul caută în toate bibliotecile](shots/profile-picker.webp)

Profilul se copiază în proiect, iar tipul îl păstrează. Corpurile sunt ușoare, cu colțuri drepte, la toate nivelurile în afară de unul: o vedere la **LOD 400** (plan, secțiune, elevație, planșă sau 3D) desenează secțiunea reală, racordări, raze la vârfuri, tălpi înclinate. Cantitățile și exporturile rămân pe corpul cu colțuri drepte; razele de racordare și tălpile înclinate contează în proprietăți și în exportul de structură.

În altă parte profilul dă ce poate folosi unealta: baghetele, șanțurile și fundațiile continue iau exteriorul lui ca profil 2D (**Dintr-o bibliotecă de profile…** în lista lor), iar montanții și traversele pereților cortină și tocurile ușilor și ferestrelor iau lățimea și adâncimea lui.

O bibliotecă se poate schimba după ce ai folosit-o. Un profil al proiectului diferit de cel cu același nume din biblioteca lui e marcat **mai nou în bibliotecă**; **Actualizează din bibliotecă** îl înlocuiește, iar toate tipurile făcute din el urmează, ca un singur pas de anulare. Nimic nu se schimbă până nu ceri. Proiectele făcute înainte își păstrează tipurile cum sunt.
