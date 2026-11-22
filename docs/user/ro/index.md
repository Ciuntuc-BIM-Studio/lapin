---
title: Ghidul utilizatorului Lapin
order: 0
summary: Cum modelezi, desenezi și publici o clădire cu Lapin, pas cu pas, cu imagini luate chiar din aplicație.
---

# Ghidul utilizatorului Lapin

Lapin este un program BIM pentru arhitecți și ingineri. Desenezi în plan pereți, plăci, acoperișuri, uși și ferestre. Lapin construiește din ele modelul 3D și desenează secțiunile, fațadele și planșele pe care le tipărești. Fiecare element e un element IFC adevărat, așa că modelul ajunge în alte programe BIM așa cum este.

Lapin merge în două locuri:

- **Pe calculatorul tău.** Aplicația desktop pentru Windows, macOS și Linux e cea mai rapidă. Se actualizează singură.
- **În browser.** [lapinbim.com/app](https://lapinbim.com/app/) rulează aceeași aplicație fără să instalezi nimic. E puțin mai lentă.

Aplicația desktop se descarcă de pe [lapinbim.com/ro/download](https://lapinbim.com/ro/download/); aceleași fișiere sunt și pe [GitHub Releases](https://github.com/Ciuntuc-BIM-Studio/lapin/releases). Pe Linux, pachetul `.deb` pune Lapin în meniul de aplicații (la Grafică), cu o iconiță pe care tema ta de iconițe o poate înlocui, și deschide proiectele `.lapin` cu dublu clic. Pachetul `.rpm` face la fel pe Fedora, RHEL și openSUSE. AppImage-ul are aceeași intrare de meniu, pentru programele care pun AppImage-urile în meniu (AppImageLauncher, Gear Lever).

Ce pachet de Linux să alegi: Fedora, RHEL, openSUSE și derivatele lor, `.rpm`; Ubuntu, Debian, Linux Mint și derivatele lor, `.deb`; orice altceva, AppImage. Pachetele `.deb` și `.rpm` folosesc WebKitGTK-ul sistemului, mai nou și actualizat odată cu sistemul; AppImage-ul poartă un WebKitGTK mai vechi, al lui, iar vederea 3D poate fi mai lentă cu el, mai ales pe plăci NVIDIA sub Wayland. Pachetele `.deb` și `.rpm` cer parola administratorului când Lapin se actualizează; AppImage-ul se actualizează fără ea.

![Fereastra Lapin cu o casă mică în planul parterului](shots/workspace.webp)

> Imaginile din ghid sunt luate din Lapin la fiecare versiune publicată, așa că arată aplicația cum este azi.

## De unde începi

Ești nou în Lapin? Citește [Primii pași](getting-started.md), apoi [Pereți](walls.md). Celelalte pagini se pot citi în orice ordine. Preferi să vezi? Pagina [Video](videos.md) are câte un tutorial scurt pentru fiecare parte din Lapin.

<!-- pages -->

## Gratuit, cu cod deschis și helpware

Lapin e gratuit și are cod deschis, sub licența MIT; codul e pe [GitHub](https://github.com/Ciuntuc-BIM-Studio/lapin). Este și **helpware**: dacă Lapin te ajută cu ceva, te încurajăm, fără să te obligăm, să ajuți și tu pe oricine are nevoie de ajutor, în orice fel poți: cu timpul tău, cu ce știi să faci, cu o mână de ajutor pentru cineva de lângă tine, cu un dar pentru oameni sau o cauză în care ai încredere. Lapin nu cere nimic pentru sine și nu primește donații.

## Când te blochezi

Apasă **Ask Me**, iepurașul din dreapta sus, sau tasta <kbd>F1</kbd>, și scrie întrebarea cu cuvintele tale. Dacă nu are răspuns, îți trimite întrebarea la noi. Vezi [Ajutor și feedback](help.md).
