---
title: Librerie di profili
order: 57
summary: I profili che il tuo lavoro usa, qualunque sia la norma — il piccolo set EN di Lapin, le tue tabelle e i file IFC, le cartelle aziendali; disegnati con le proprietà calcolate e i valori del file verificati, copiati nel progetto man mano che si usano.
---

Lapin non può includere tutti i profili d'acciaio del mondo: porta un piccolo set europeo e legge il resto da ciò che hai già: un catalogo in foglio di calcolo (AISC Shapes Database, la tabella di un produttore, quella della tua azienda), una libreria IFC di profili (quella di Bonsai, per esempio) o qualsiasi modello IFC, un contorno chiuso disegnato in Lapin o in un DXF. **Gestisci → Librerie di profili** li raccoglie tutti.

![Librerie di profili](shots/profile-libraries.webp)

## Le librerie

- **Integrata**: *EN steel (Lapin)*: IPE, HEA, HEB, HEM, UPN, UPE, angolari a lati uguali e disuguali, SHS, RHS e CHS finiti a caldo nelle misure comuni, e C, Z e Σ generici formati a freddo. Solo dimensioni normate; tutte le proprietà sono calcolate da Lapin.
- **Su questo computer**: le librerie che importi, per ogni progetto. Anche la versione browser le conserva, in quel browser.
- **Cartelle aziendali**: nell'app desktop, quante cartelle locali, di rete o sincronizzate di file `.lapinprofiles` vuoi (**Aggiungi una cartella…**). Una cartella è in sola lettura finché non spunti **scrivibile**; allora il pulsante di salvataggio accanto vi scrive la libreria scelta, per i colleghi.
- **Nel progetto**: solo i profili usati vengono copiati, con il nome della loro libreria, così il file si apre ovunque.

Scrivi nella ricerca per cercare in tutte le librerie per nome, famiglia, norma o tipo; scegli un profilo per vederlo disegnato con dimensioni e proprietà.

## Proprietà

Dalle dimensioni Lapin calcola area, Iy, Iz, moduli elastici e plastici, raggi d'inerzia, assi principali e loro angolo (angolari, Z, ogni contorno asimmetrico), costante di torsione It e di ingobbamento Iw dove esiste una formula, massa per metro dalla densità del materiale e superficie da verniciare per metro. Concordano con le tabelle pubblicate entro circa l'1 % (It e Iw degli I laminati entro il 3 %; Iw di un profilo a U con ali inclinate solo entro il 20 %). Un contorno qualsiasi non ha It né Iw se il suo file non li dà.

I valori di un file sono conservati e mostrati accanto a quelli di Lapin. Una differenza oltre il 2 % (area, massa), 3 % (inerzie, moduli) o 10 % (It, Iw) è marcata e finisce nella lista di verifica dell'importazione; di una colonna intera sfasata di una potenza di dieci si dice che è in un'altra unità.

![Un valore del file che differisce da quello di Lapin](shots/profile-check.webp)

## Importare una tabella

**Tabella…** legge un CSV o XLSX, una riga per profilo. Significato e unità di ogni colonna sono dedotti dall'intestazione in inglese, rumeno, tedesco o francese (h, d, Höhe, hauteur, înălțime; tw, s, Stegdicke; A, Iy, Wel, Wpl, It, Iw, G, kg/m…), unità comprese (mm, cm, in, cm², cm⁴, in⁴, ×10³ cm⁶, lb/ft). Una tabella con Ix e senza Iz è letta all'americana: x l'asse forte, in pollici. Correggi nella finestra ciò che è sbagliato; Lapin ricorda la scelta per il prossimo file con la stessa disposizione.

![Le colonne di una tabella abbinate](shots/profile-table.webp)

Il tipo di una riga viene dalla sua colonna tipo (W, HP, UPN, HSS, PIPE…) o dal nome; per le altre sceglilo nella finestra.

## IFC

**IFC…** legge tutte le definizioni di profilo di qualsiasi file IFC: una libreria o un modello da Tekla, Revit o ArchiCAD, dove i profili usati dagli elementi prendono il nome dei loro tipi. I, I asimmetrico, U, T, L, C, Z, tubolari, rettangolo, cerchio ed ellisse conservano tipo e misure; quelli arbitrari, composti e derivati diventano contorni. Arrivano anche le proprietà meccaniche di `Pset_ProfileMechanical` (e quelle di IFC2x3); gli esportatori non concordano su unità e asse Y, quindi Lapin decide entrambi dalla geometria. I profili a linea media e a trapezio sono esclusi, e viene detto.

![Profili letti da un file IFC](shots/profile-ifc.webp)

**IFC** e **CSV** sotto la lista scrivono la libreria scelta (o i profili del progetto) come libreria di profili IFC4 o come tabella che Lapin rilegge.

Anche nell'IFC del modello travi, pilastri e profili d'acciaio sono scritti con il loro profilo parametrico (`IfcIShapeProfileDef`, `IfcLShapeProfileDef`, …, rettangoli e cerchi), con il nome del profilo e `Pset_ProfileMechanical`, così gli altri programmi vedono un IPE 300 e non una polilinea. Contorni, Σ e Z con labbri restano profili arbitrari; un elemento tagliato da un altro resta il solido che è.

## Un contorno come profilo

Disegna la sezione con Disegno come polilinee chiuse e cerchi (quelli dentro un altro sono fori), selezionali e premi **Dalla selezione**, oppure leggi il disegno di un produttore con **DXF…**. Dagli un nome, scegli materiale e libreria e conservalo. Così entrano i profili in alluminio e PVC, le sezioni prefabbricate e le forme in legno: lo stesso archivio, con il loro materiale e densità.

![Un contorno disegnato diventato profilo](shots/profile-outline.webp)

**Disegna il contorno** fa il contrario: il contorno del profilo scelto come polilinee chiuse nella vista attiva, perché Studio lo estruda o lo sposti lungo un percorso.

## Usare un profilo

In **Acciaio → Tipi in acciaio**, **Struttura → Tipi strutturali** (pilastri e travi) e nei profili del capannone a portali, scegli **Da una libreria di profili…** e scrivi per cercare.

![Il selettore cerca in tutte le librerie](shots/profile-picker.webp)

Il profilo viene copiato nel progetto e il tipo lo conserva. I corpi sono leggeri, a spigoli vivi, a ogni livello tranne uno: una vista a **LOD 400** (pianta, sezione, prospetto, tavola o 3D) disegna la sezione reale, con raccordi, raggi di punta e ali inclinate. Quantità ed esportazioni restano sul corpo a spigoli vivi; raccordi e ali inclinate contano nelle proprietà e nell'esportazione strutturale.

![Una trave e un pilastro d'acciaio in una vista 3D a LOD 400: raccordi disegnati](shots/profile-real-section.webp)

Altrove il profilo dà ciò che lo strumento può usare: modanature, scanalature e fondazioni continue prendono il suo esterno come profilo 2D (**Da una libreria di profili…** nel loro elenco), montanti e traversi delle facciate continue, telai e ante di porte e finestre prendono il suo contorno reale, con i vuoti, lungo ogni elemento, con larghezza e profondità; la x del profilo va di traverso all'elemento (verso il vetro in telaio e anta), la y verso l'esterno dell'edificio. **Semplice** toglie il contorno e mantiene la misura.

![Montanti e traversi di facciata continua da una sezione di alluminio](shots/profile-real-mullions.webp)

![Telaio e anta di una finestra dalla stessa sezione](shots/profile-real-frame.webp)

Una libreria può cambiare dopo l'uso. Un profilo del progetto diverso da quello con lo stesso nome nella sua libreria è marcato **più recente in libreria**; **Aggiorna dalla libreria** lo sostituisce e tutti i tipi fatti con esso lo seguono, in un solo passo di annullamento. Nulla cambia finché non lo chiedi. I progetti precedenti conservano i loro tipi.
