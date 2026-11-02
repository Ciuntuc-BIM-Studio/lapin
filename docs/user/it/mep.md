---
title: Impianti MEP
order: 57
summary: Disegna impianti di riscaldamento, idrici, di scarico, elettrici e di ventilazione come schemi sulla pianta, con sorgenti, terminali e i tratti tra di essi, ed esportali come rete IFC.
---

La scheda **Impianti** disegna gli impianti dell’edificio come li abbozza un progettista: un sistema per ogni impianto, la sua **sorgente** (una caldaia, un contatore dell’acqua, una colonna di scarico, un quadro elettrico, un ventilatore), i suoi **terminali** (radiatori, lavabi, WC, prese, punti luce, diffusori) e i **tratti** tra di essi. Ogni nodo è un vero elemento IFC con i suoi attacchi; ogni tratto è un segmento di tubo, di canale o di cavo; i collegamenti restano quando sposti le cose.

![La scheda Impianti](shots/ribbon-mep.webp)

## Un sistema con un clic

Sotto **Nuovo sistema**, scegli l’impianto (**Riscaldamento**, **Acqua**, **Acqua calda**, **Scarico**, **Elettrico**, **Ventilazione**) e fai clic sulla pianta. Lapin posiziona l’intero sistema: la sorgente dove hai fatto clic, un primo terminale a qualche metro di distanza e il tratto tra di essi. Il sistema si apre per la modifica.

![Un impianto di riscaldamento sulla pianta del piano terra: la caldaia, due radiatori sotto le finestre e i tratti tra di essi](shots/mep-plan.webp)

In modalità di modifica tutto ciò che non fa parte del sistema è sbiadito, e si possono selezionare solo i nodi e i tratti del sistema. Trascina la sorgente e il terminale dove vanno: il loro tratto li segue. Aggiungi terminali con **+ Radiatore**, **+ Lavabo**… nel pannello Proprietà, oppure con gli strumenti sotto **Riscaldamento**, **Idraulico**, **Elettrico** e **Aria**: ogni nuovo terminale si unisce al sistema con un tratto proprio, dall’attacco libero più vicino della sorgente, oppure dal tratto più vicino (una derivazione a T). **Fatto** (o <kbd>Esc</kbd>) esce dalla modalità di modifica e ti dice quante curve e derivazioni a T forma il sistema e cosa resta ancora aperto. **Modifica sistema** (<kbd>ES</kbd>) lo riapre.

## Tratti

**Tratto** (<kbd>RN</kbd>) disegna un tratto punto per punto; premi <kbd>Enter</kbd> per terminare. Un’estremità che cade su un attacco libero vi si collega; un’estremità che cade su un tratto dello stesso sistema si dirama da esso.

**Collega** unisce l’estremità di un tratto a un nodo o a un altro tratto; **Scollega** la libera. **Attacchi liberi** seleziona tutto ciò che non è ancora collegato.

## Rimodellare un tratto

Seleziona un tratto: le sue maniglie stanno sulla linea così come è disegnata, anche dopo che hai spostato i nodi che collega.

- **Trascina una maniglia** per spostare liberamente quell’angolo.
- **Trascina il centro di un segmento** per farlo scorrere lateralmente; i segmenti accanto si allungano, così un percorso ortogonale resta ortogonale. Fai scorrere il primo o l’ultimo segmento e Lapin vi aggiunge una sfalsatura, così l’estremità resta sul suo attacco.
- **Ctrl-clic** su una maniglia per toglierla, o sul centro di un segmento per aggiungerne una; **Aggiungi maniglia** ne mette una dove fai clic.
- **Deviazione** (<kbd>DU</kbd>) fa passare un tratto attorno a un ostacolo: fai clic sul tratto dove inizia la deviazione, fai clic dove finisce sullo stesso segmento, poi spostati dal lato verso cui va e fai clic (o digita la distanza). Il tratto esce, prosegue e rientra, con quattro nuovi angoli; mantiene i suoi collegamenti.
- **Ridisegna percorso** (<kbd>RP</kbd>) dà a un tratto un nuovo percorso: selezionalo (oppure parti da esso) e disegna il percorso punto per punto, <kbd>Enter</kbd> per terminare. Il tratto mantiene il suo sistema, la sua dimensione e i suoi collegamenti, e le sue estremità tornano sui loro attacchi.

![Un tratto di radiatore fatto passare attorno a un ostacolo con Deviazione: esce, prosegue e rientra](shots/mep-detour.webp)


## Nodi

Ogni nodo ha un **ruolo**: una caldaia, un radiatore, un collettore, un contatore dell’acqua, un lavabo, un lavello, un WC, una doccia, una vasca, una colonna di scarico, un tappo d’ispezione, un quadro elettrico, una presa, un punto luce, un interruttore, un ventilatore, un diffusore, una griglia di ripresa, una valvola, una salita / discesa, un tappo. Il ruolo gli dà la sua classe IFC, i suoi attacchi, la sua dimensione e il suo simbolo sulla pianta. Una valvola, un contatore o un interruttore posato su un tratto lo divide e si mette nel mezzo. Una **salita / discesa** porta un tratto a un’altra quota o al piano superiore.

![Le proprietà di un radiatore: il suo sistema, il suo ruolo e i suoi attacchi, la sua dimensione e la sua potenza](shots/mep-radiator-properties.webp)

Nel pannello Proprietà un nodo mostra il suo sistema, il suo ruolo e i suoi attacchi (liberi o collegati); un tratto mostra le sue due estremità. Entrambi prendono i valori predefiniti del sistema, a meno che tu non imposti un valore su di essi: dimensione, quota, il piano su cui corrono (pavimento, soffitto, parete), doppio o singolo, materiale, pendenza.

## Sistemi

**Sistemi…** elenca i sistemi del progetto e i valori predefiniti di quello corrente: la dimensione, il piano di posa e le quote di ogni piano, la pendenza (scarico), se i tratti sono disegnati in coppia (riscaldamento: mandata e ritorno) e a quale distanza, il materiale e l’isolamento. Cambia un valore predefinito e ogni nodo e tratto senza un valore proprio si adegua.

![La finestra di dialogo Sistemi](shots/mep-systems.webp)

## Piante, elenchi e IFC

**Pianta impianti** crea una pianta della disciplina impiantistica per il livello, archiviata sotto il gruppo Impianti nel browser. L’**Elenco apparecchiature** e l’**Elenco tratti** sotto **Sistema** sono abachi come gli altri: vanno sulle tavole e si esportano con esse.

In un’esportazione IFC ogni sistema è un IfcDistributionSystem, ogni nodo e tratto ha i suoi IfcDistributionPorts, e i collegamenti vengono scritti tra gli attacchi, così la rete arriva in un altro programma collegata dalla sorgente all’ultimo terminale.

## MEP Pro

La parte ingegneristica sulla rete (la portata che ogni tratto trasporta e la dimensione che la regge, le verifiche: estremità aperte, curve strette, pendenze di scarico, sorgenti sovraccariche; le regole di posa per materiale: rame a passi di 45°, PVC con curve di 45° al massimo; radiatori proposti sotto le finestre, percorsi suggeriti lungo i muri, il profilo di un tratto e l’assonometria di un sistema) si trova nell’edizione **Lapin MEP**, che aggiunge a questa scheda i gruppi **Ingegneria** e **Assistenti**.
