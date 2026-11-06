---
title: Primii pași
order: 10
summary: Ecranul de start, un proiect nou, părțile ferestrei, cum te miști într-un desen și cum alegi unitățile.
---

## Ecranul de start

Când pornește, Lapin arată ecranul de start. **Proiect nou** începe o clădire goală. **Deschide un proiect…** deschide un fișier `.lapin`. Proiectele deschise înainte sunt sub **Recente**.

**Pornește de la** dă un proiect nou pregătit pentru lucrul lui: **Casă parter** (fundații și parter), **Casă P+1** (și un etaj), **Bloc de locuințe** (parter și patru etaje), **Hală sau structură** (un nivel înalt de 6 m, în spațiul de lucru Structură și armare), **Doar desen 2D** (un nivel, în spațiul de lucru Desen 2D) și **Pornește de la un IFC** (un proiect nou, apoi fișierul IFC de importat). Fiecare își alege și spațiul de lucru; nivelurile se pot redenumi, adăuga sau șterge apoi, ca în orice proiect.

![Ecranul de start](shots/start-screen.webp)

Un proiect nou are deja trei niveluri: **Foundations** (−1,50), **Ground floor** (±0,00) și **First floor** (+3,00). Le poți redenumi. Proiectul se deschide pe planul parterului, gata pentru primul perete.

## Fereastra

![Părțile ferestrei Lapin](shots/workspace.webp)

1. **Taburile panglicii.** Desen, Arhitectură, Interior, Structură, Teren, Studio, Adnotări, Gestionare, Execuție și Vedere își grupează fiecare uneltele. **Fișier** deschide, salvează, importă și exportă. **Anulează** și **Refă** sunt la capătul din dreapta al aceleiași bare (<kbd>Ctrl</kbd> <kbd>Z</kbd>, <kbd>Ctrl</kbd> <kbd>Y</kbd>).
2. **Panglica.** Are uneltele tabului curent. Ține mouse-ul peste o unealtă ca să vezi numele și literele ei de la tastatură.
3. **Navigatorul proiectului.** Listează nivelurile, planurile, secțiunile, fațadele, vederile 3D, planșele și extrasele. Dublu clic pe una o deschide într-un tab. Un proiect se deschide pe planul parterului, cu vederea 3D într-un tab alături.
4. **Vederea.** Aici desenezi: un plan, o secțiune, o vedere 3D sau o planșă. Poți deschide mai multe vederi alăturate cu cele două butoane din dreapta sus a vederii.
5. **Bara rapidă.** Are uneltele cele mai folosite pe tastele <kbd>1</kbd>–<kbd>9</kbd>.
6. **Linia de comandă.** Scrie aici literele unei unelte și apasă <kbd>Enter</kbd>. Cât lucrează o unealtă, linia îți spune ce îi trebuie mai departe. Vezi [Desen și linia de comandă](drafting.md).
7. **Panoul de proprietăți.** Arată ce e selectat. Când nu e nimic selectat, arată vederea din față.
8. **Ask Me.** Asistentul de ajutor al Lapin. Vezi [Ajutor și feedback](help.md).

Vrei mai mult loc pentru desen? **Vedere → Interfață…** alege o fereastră mai compactă, iar panourile laterale se pliază. Vezi [Interfața și scurtăturile](interface.md).

## Cum te miști

- **Zoom.** Rotița mouse-ului sau ciupit pe trackpad. Lapin face zoom unde e cursorul.
- **Deplasare.** Trage cu butonul din mijloc sau din dreapta, sau cu două degete pe trackpad.
- **Rotire în 3D.** Trage cu butonul stâng. Cu elemente selectate, vederea se rotește în jurul lor; altfel, în jurul punctului modelului de sub cursor.
- **Arată tot.** Butonul **Încadrează**, comanda <kbd>ZE</kbd> sau dublu clic cu butonul din mijloc.

## Unitățile

Lapin lucrează în milimetri dacă nu alegi altceva. Ca să schimbi unitățile, deschide **Gestionare → Standarde de desen → Proiect → Unități**, sau apasă pe **Unități** în bara de stare de jos. Poți alege milimetri, centimetri, metri, picioare și țoli, sau țoli.

![Standarde de desen, tabul Proiect: unitățile proiectului](shots/standards-project.webp)

Toate lungimile urmează setarea: panourile, valorile pe care le scrii când desenezi, linia de comandă și cotele. Modelul în sine e ținut în metri, așa că schimbarea unităților nu-ți schimbă niciodată clădirea. Nivelurile și axele o urmează și ele.

În milimetri, centimetri și metri, lungimile sunt ținute și arătate la o zecime de milimetru: o placă de 12,5 mm rămâne 12.5 mm (1.25 cm), iar valorile întregi se scriu fără zecimale. Picioarele și țolii merg la cea mai apropiată 1/16". Cotele de pe desene se rotunjesc cum spune stilul lor de cotare.

## Salvarea

**Fișier → Salvează** (<kbd>Ctrl</kbd> <kbd>S</kbd>) scrie un fișier `.lapin`. Vezi [Fișiere, versiuni și recuperare](files.md).
