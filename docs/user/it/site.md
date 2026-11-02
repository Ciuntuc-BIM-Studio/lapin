---
title: Il sito e i suoi dati
order: 52
summary: Porta il rilievo, il lotto, gli edifici vicini e un’ortofoto in coordinate reali, da DXF, CSV, GeoJSON, CityJSON e da un’immagine georeferenziata.
---

La scheda **Sito** contiene il terreno su cui sorge l’edificio: il terreno, il lotto, gli edifici vicini e una foto aerea, ognuno dove si trova davvero. Il gruppo **Importa** li porta dai file che consegnano i topografi e il catasto.

![La scheda Sito](shots/ribbon-site.webp)

## Coordinate reali

Un rilievo arriva in coordinate cartografiche: est e nord, a centinaia di chilometri dallo zero. Lapin tiene il modello vicino alla sua origine e lo lega alla mappa tramite il **punto di rilievo**. La prima importazione in un progetto lo fa per te: il punto di rilievo va all’origine della pianta, sulle prime coordinate del file arrotondate al metro, e ogni importazione successiva arriva nello stesso posto.

**Gestisci → Posizione** mostra il **sistema di coordinate** in cui sono le coordinate est e nord, e il punto base e il punto di rilievo. Lapin conosce Stereo 70 (EPSG:3844, il sistema nazionale della Romania), i fusi UTM 34 e 35 (EPSG:32634, 32635), Web Mercator (EPSG:3857) e la longitudine e la latitudine (WGS 84, EPSG:4326). Un file in un altro di questi sistemi viene convertito quando entra: un GeoJSON in longitudine e latitudine arriva sulla pianta in Stereo 70 dove deve stare. L’esportazione IFC scrive il sistema e il punto di rilievo, così anche gli altri programmi collocano il modello sulla mappa.

![Gestisci → Posizione: il sistema di coordinate e il punto di rilievo](shots/site-location.webp)

## Importare

- **Terreno da punti** legge un file CSV o di testo di un rilievo: x y z, oppure numero nord est quota, un punto per riga.
- **Punti da DXF** legge un disegno di rilievo: entità POINT con le loro quote, blocchi inseriti a una quota o che la portano come attributo, quote scritte accanto ai punti ("245.37" vicino alla sua croce) e curve di livello alla loro quota. Quando il disegno ha punti su più layer, Lapin li elenca con quanti punti contiene ciascuno, e tu scegli quali prendere. I punti formano un terreno.
- **GeoJSON** porta lotti, linee e punti. Un file con un solo poligono dà il **lotto**; uno con più poligoni dà delle aree. Il lotto delimita anche il lotto del terreno, con la sua area. I file del catasto in Stereo 70 lo dichiarano nel file, e Lapin lo legge; un file che non dice nulla è in longitudine e latitudine, come richiede lo standard GeoJSON.
- **CityJSON** porta gli edifici vicini con le loro superfici, al loro posto e alla loro altezza. Sono riferimenti di sola lettura; proietteranno ombre nello studio del soleggiamento.
- **Ortofoto** stende una foto aerea sotto il sito. Scegli l’immagine (JPEG o PNG, fino a 8 MB) insieme al suo world file (`.jgw`, `.pgw` o `.wld`): il world file dice dove si trova l’immagine. La foto viene disegnata sotto la pianta e riveste il terreno in 3D.

![La planimetria: l’ortofoto, le curve di livello del terreno rilevato, il lotto e l’impronta dell’edificio vicino](shots/site-plan.webp)

![Lo stesso sito in 3D: la foto sul terreno, l’edificio vicino alla sua altezza](shots/site-3d.webp)

Tutto ciò che importi è conservato nel file del progetto. Un elemento prende il nome dal file (il numero catastale di un lotto, quando il file lo ha) e conserva con sé le altre proprietà del file; selezionalo per vederne l’area o la lunghezza.

## Strade, curve e linee CAD

Una **strada** o un **percorso** segue il suo asse. In **Corridoio stradale…** (CR), la scheda **Curve** dà a ogni vertice dell’asse un raggio con una clotoide in entrata e in uscita, come un progettista stradale traccia una curva; un vertice lasciato a 0 resta uno spigolo, e le curve che non stanno tra le loro vicine vengono rifiutate con il motivo. Le progressive, il profilo longitudinale e le sezioni trasversali seguono l’asse curvo, e IFC 4.3 lo scrive come tracciato.

Le linee CAD diventano elementi del sito: **Strada da linee CAD** (LR) e **Linea di discontinuità da linee CAD** (BL) prendono una linea o una polilinea disegnata o portata da un DXF, seguono ogni linea del suo layer che la incontra estremo a estremo, e ne fanno una strada lungo di essa o una linea di discontinuità del terreno. Quando le linee portano delle quote (le linee 3D di un DXF), danno il profilo della strada o le quote della linea di discontinuità. Queste linee sono conservate con le loro quote e possono essere mostrate in 3D (la categoria *linework*).

## Planimetrie e inquadramenti territoriali

Il browser di progetto ha un **Sito** sotto i livelli: i disegni propri del sito, che non appartengono a nessun piano. Un nuovo progetto inizia con due:

- la **Planimetria** (in rumeno *plan de situație*), a 1:500: l’intero modello visto dall’alto, tutti i piani insieme, tetti compresi, con il terreno, i percorsi, le piante, il lotto e gli edifici vicini. Ciò che sta interamente sotto ±0.00 (le fondazioni) è escluso, niente è tagliato e niente di nascosto è disegnato. I vertici del lotto sono numerati;
- l’**Inquadramento territoriale** (*plan de încadrare în zonă*), a 1:2000: lo stesso disegno per il sito nella sua zona, sopra l’ortofoto, senza i numeri dei vertici; gli edifici del progetto sono riempiti in rosso e il lotto è contornato in rosso, così il sito si distingue dai vicini.

**+** accanto a Sito ne crea un altro di uno dei due tipi; i pulsanti di copia ne duplicano uno. Entrambi portano una **freccia del nord** che punta al nord geografico (Gestisci → Posizione: l’angolo rispetto al nord geografico), una **scala grafica** e, quando il progetto è legato alla mappa, la **griglia delle coordinate**: croci dove si incontrano valori tondi di est e nord, ogni decimo della scala (50 m a 1:500, 200 m a 1:2000), con i loro valori. Seleziona la vista nel browser e impostali nelle sue proprietà, sotto **Impostazioni di vista**: che cos’è (planimetria o inquadramento territoriale), il passo della griglia (0: nessuna), la freccia, la scala grafica e **Evidenzia il progetto** (il riempimento e il contorno rossi, attivo per impostazione predefinita in un inquadramento territoriale, anche per una planimetria quando lo vuoi).

**La mappa sotto di essi.** L’ortofoto viene disegnata sotto ogni planimetria e ogni inquadramento territoriale, su qualunque piano sia stata importata, e si stampa con essi sulle tavole e nel PDF. Per la planimetria catastale o il disegno del topografo, apri la planimetria e usa **Importa DXF…** con **Posizionato: dalle coordinate della mappa**. Lapin lo propone da solo quando le coordinate del file sembrano quelle di una mappa, per esempio Stereo 70. Il disegno arriva allora dove dicono le sue coordinate est e nord, tramite il punto di rilievo del progetto e il suo angolo rispetto al nord geografico. Se il progetto non è ancora legato alla mappa, è il disegno a legarlo. Spunta **Come collegamento** per mantenerlo in sola lettura e ricaricarlo quando il topografo ne invia uno nuovo. Importalo una volta in ogni disegno che ne ha bisogno.

![Un inquadramento territoriale: la casa riempita in rosso, il lotto contornato in rosso, tra i vicini](shots/site-location-plan.webp)

![Una planimetria: la casa vista dall’alto sul suo lotto, i vertici numerati, la griglia della mappa, la freccia del nord e la scala grafica](shots/site-drawings.webp)

![Il Sito nel browser di progetto](shots/site-browser.webp)

L’abaco **Coordinate del lotto** (Abachi → + → Coordinate del lotto) è l’inventario delle coordinate del lotto: ogni vertice come lo numera la planimetria, il suo nord e il suo est nel sistema del progetto, il lato fino al vertice successivo e l’area. Come ogni abaco va su una tavola accanto alla planimetria, ed esce come CSV o XLSX.

## Alberi e arbusti

**Pianta (vegetale)** (PN) mette una pianta con un clic, **Area di piantumazione** (PZ) semina un contorno con un insieme di specie a una certa densità; seleziona l’una o l’altra e scegli la specie nelle proprietà. L’elenco delle specie è una libreria di piante autoctone o tradizionali in Romania, in gruppi: **alberi da frutto** (melo, pero, susino, ciliegio e amarena, albicocco, cotogno), **conifere** (abete bianco, abete rosso, pino silvestre e pino nero), **alberi a foglia caduca** (noce, tiglio, betulla, acero), **arbusti** (nocciolo, corniolo, sambuco, rosa canina, lillà, olivello spinoso, ribes rosso, tuia, bosso) e **tappezzanti**. Ognuna ha il suo nome latino, la sua altezza e la sua chioma abituali, e viene mostrata con il nome rumeno quando l’interfaccia è in rumeno. L’abaco delle piantumazioni elenca i nomi latino e rumeno accanto a ogni specie.

In pianta ogni pianta è disegnata dall’alto come la mostra un disegno di paesaggio, in nero, larga quanto la sua chioma: un albero da frutto come una chioma lobata con rami biforcati, un noce o un tiglio come una chioma smerlata attorno a un nucleo scuro, un abete come una stella di rametti di aghi, un pino come un ciuffo rotondo di aghi, un arbusto come una piccola nuvola. Lapin disegna ogni simbolo da sé, così gli alberi vicini differiscono un po’ e un’area non sembra mai stampata a timbro. A 1:1000 e a scale minori (un inquadramento territoriale) si disegnano solo i contorni delle chiome. In 3D le conifere sono coni, gli altri alberi hanno una chioma arrotondata sul tronco e gli arbusti una cupola bassa.

![Un piccolo frutteto in pianta: alberi da frutto, un noce, conifere e arbusti](shots/planting-symbols.webp)

## Regole urbanistiche e il sole (Lapin Urban)

L’edizione **Lapin Urban** aggiunge a questa scheda un gruppo **Urbanistica**: le regole urbanistiche del lotto (POT, CUT, le altezze massime, le distanze dalla strada, dai lati, dal retro e dall’asse stradale), il **volume edificabile** che lasciano, gli indicatori che il modello raggiunge rispetto a esse, e lo **studio del soleggiamento**: il sole e le sue ombre in 3D per qualsiasi giorno e ora nel luogo del progetto, e le piante delle ombre per l’equinozio e i solstizi.
