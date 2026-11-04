---
title: Devis
order: 98
summary: Le devis tiré du modèle : vos listes de prix importées de CSV ou d’Excel, des articles sur les types et des règles sur les matériaux, les quantités du métré multipliées par des prix unitaires ventilés en matériaux, main-d’œuvre, matériel et transport, frais généraux, bénéfice et TVA, en RON ou en EUR ; les formulaires roumains F3, F2 et C6 à C9 sur des feuilles, dans Excel et en PDF ; les plans de coûts IFC.
---

## L’onglet Coûts

![L’onglet Coûts](shots/ribbon-cost.webp)

L’onglet qui s’appelait **Métrés** s’appelle maintenant **Coûts**. Il garde les quantités et ajoute le devis :

- **Métré**, **Nomenclatures…** et **Exporter XLSX** : les quantités du modèle (voir [Nomenclatures et graphiques](schedules.md)).
- **Listes de prix…** : vos prix, importés d’un fichier CSV ou Excel (ci-dessous).
- **Affecter les articles…** : quel article chiffre chaque type et chaque matériau (ci-dessous).
- **Devis…** : ouvre le devis sous la vue (il est fait la première fois).
- **Formulaire sur feuille**, **Devis XLSX** et **Devis PDF** : le devis et ses formulaires sur une feuille, dans un classeur ou dans un PDF (ci-dessous).

Le devis est recalculé à partir du modèle à chaque changement. Un mur que vous dessinez maintenant est aussitôt dans le devis, avec ses quantités. Seuls les prix, les articles sur les types, les règles et vos propres lignes sont enregistrés.

## Les listes de prix

![Importer une liste de prix : le sens de chaque colonne deviné d’après son en-tête](shots/price-import.webp)

Les prix sont les vôtres : Lapin ne livre aucun catalogue de prix ni de normes. **Coûts → Listes de prix… → Importer…** lit un fichier CSV ou XLSX, par exemple un export de votre logiciel de devis ou la liste d’un fournisseur. Lapin montre les premières lignes du fichier sous leurs en-têtes. Au-dessus de chaque colonne figure ce que Lapin a deviné qu’elle contient, d’après l’en-tête, en anglais ou en roumain :

- **Code** et **Description** (*Cod*, *Simbol*, *Denumire*), **Unité** (*U.M.* : m³ ou mc, m² ou mp, m, pcs ou buc, kg, t, 100 m²).
- Le prix unitaire ventilé en **Matériau**, **Main-d’œuvre**, **Matériel** et **Transport** (*Material*, *Manoperă*, *Utilaj*, *Transport*), ou un seul **Prix unitaire (non ventilé)**, qui compte alors comme matériau.
- En option : **Heures-homme par unité** (*Normă de timp*) et **Métier** (*Meserie*), pour la liste de la main-d’œuvre (C6).
- En option : les ressources de l’article, des lignes qui nomment l’article auquel elles appartiennent (**Article auquel elle appartient**), leur **Type de ressource** (M, Man, U, T) et leur **Consommation par unité**. Un article donné seulement par ses ressources est chiffré par elles, et les listes de ressources (C7 à C9) les détaillent une à une.

Changez toute colonne mal devinée, choisissez la ligne des en-têtes et si les prix sont en RON ou en EUR, puis cliquez sur **Importer**. La liste entre dans le projet. Avec **Garder aussi dans la bibliothèque de cet ordinateur** coché, elle reste aussi sur cet ordinateur : sous **Sur cet ordinateur**, **Ajouter au projet** l’amène dans votre prochain projet.

**Modèle** enregistre un CSV avec les colonnes et quelques lignes des valeurs indicatives de Lapin, marquées *indicatif* : remplissez-le avec vos prix et importez-le. **Utiliser les valeurs indicatives** met ces valeurs dans le projet comme liste, pour essayer le devis avant d’avoir les vôtres. Remplacez-les avant d’envoyer un devis à qui que ce soit.

## Des articles sur les types, des règles sur les matériaux

![Affecter les articles : chaque type et matériau avec son article](shots/map-articles.webp)

Une ligne du devis vient d’un **article** : un code de votre liste de prix. Il y a deux façons de donner un article aux éléments :

- **Sur un type.** Chaque élément du type est chiffré par lui. L’article dit ce qu’il mesure : l’élément entier ou une de ses parties (le **noyau** d’un type multicouche, ou ses **finitions**), et selon quelle mesure (selon l’unité du prix, ou volume net ou brut, surface nette ou brute, longueur, nombre, poids). Il peut aussi ajouter un facteur et les pertes (%).
- **Sur un matériau (une règle).** Chaque couche de ce matériau, dans tout type, est chiffrée par elle : une seule règle pour l’enduit à la chaux chiffre l’enduit de tous vos murs.

L’article du type passe en premier. Une couche que les articles de son type mesurent déjà n’est pas chiffrée une deuxième fois par une règle. Un article sur l’élément entier le chiffre tout entier, et les règles s’effacent. Pour un mur multicouche, donnez au type un article sur son **noyau** (la maçonnerie au volume) et des règles pour ses finitions (l’enduit à la surface). Le mur est alors chiffré couche par couche : la maçonnerie sous **Murs**, l’enduit sous **Finitions et isolation**.

**Coûts → Affecter les articles…** liste les types du projet utilisés par des éléments, et les matériaux de leurs couches. Chaque ligne commence par l’article qu’elle a, ou celui que vous avez choisi pour un type ou un matériau du même nom dans un projet précédent, ou celui que Lapin propose : un code écrit dans son nom, ou l’article qui partage le plus de mots avec lui (*zidărie*, *BCA*, *tencuială*, *C25/30*). Pour un type multicouche, il propose le noyau. Tapez ou choisissez un code, choisissez la partie et la mesure, cochez les lignes voulues et cliquez sur **Appliquer** : une seule étape à annuler. Vos choix sont mémorisés sur cet ordinateur pour vos prochains projets.

Les types de coffrage et d’échafaudage de l’onglet **Installations de chantier** avaient déjà un code d’article (décision 152) : c’est un article sur le type, mesuré selon l’unité du prix.

## Le devis

![Le devis : chapitres, lignes, taux et totaux](shots/estimate.webp)

**Coûts → Devis…** ouvre le devis sous la vue, comme le Gantt. Ses lignes sont groupées en **chapitres** par corps d’état : terrassements, fondations, structure, murs, toiture, portes et fenêtres, finitions et isolation, installations, installations de chantier, autres. Un article peut nommer un chapitre à lui. **Par niveau et corps d’état** place le niveau avant chaque chapitre.

Chaque ligne montre le code, la description et l’unité de la liste de prix, la **quantité** tirée du modèle (la même que celle du métré), le prix unitaire ventilé en **Matériau**, **Main-d’œuvre**, **Matériel** et **Transport**, le prix unitaire et la **valeur**. Cliquez sur une ligne pour sélectionner ses éléments dans les vues. Sélectionnez des éléments dans une vue, et leurs lignes sont marquées dans le devis.

Sous les lignes viennent les totaux, disposés comme dans les devis roumains :

1. **Coûts directs** : la somme des lignes.
2. **Contribution sur le travail (CAM)** : 2,25 % de la main-d’œuvre, par défaut.
3. **Frais généraux** (*cheltuieli indirecte*) : 10 % des coûts directs avec la contribution, par défaut.
4. **Bénéfice** : 5 % des coûts directs et des frais généraux, par défaut.
5. **Total HT**, la **TVA** (21 % par défaut) et le **Total TTC**.

Changez tout taux dans la barre d’outils. Chaque valeur est arrondie à deux décimales, comme un devis l’imprime.

Le devis est en **RON** ou en **EUR**. **1 EUR =** est le taux du projet : une liste de prix dans l’autre monnaie est convertie à ce taux, et la case à côté affiche aussi chaque valeur dans l’autre monnaie.

La barre signale aussi ce qui n’est pas chiffré : **N type(s) sans prix** (cliquez dessus pour ouvrir Affecter les articles), et les codes qu’aucune liste de prix n’a. **Une ligne à vous** ajoute un travail que le modèle n’a pas (la clôture de chantier, une provision) : tapez son code, sa description, son unité, sa quantité et ses prix. Une ligne avec un code et sans prix les prend dans la liste de prix.

## Les formulaires roumains

![La liste des quantités de travaux (F3) du même devis](shots/estimate-f3.webp)

La liste au-dessus du devis montre le devis lui-même ou l’un de ses formulaires. Ce sont des regroupements du même devis, donc leurs totaux sont les mêmes. Leurs en-têtes sont en roumain, tels qu’ils sont déposés en Roumanie :

- **Liste des quantités de travaux (F3)** : *Lista cu cantitățile de lucrări pe categorii de lucrări*, chapitre par chapitre, les prix unitaires et les valeurs ventilés en matériaux, main-d’œuvre, matériel et transport, et les totaux dessous.
- **Devis par objet (F2)** : *Deviz pe obiect*, la valeur de chaque chapitre hors TVA, sa TVA et sa valeur TTC.
- **Main-d’œuvre (C6)** : les heures et la valeur par métier (d’après le métier et les heures-homme de la liste de prix, ou ses ressources de main-d’œuvre).
- **Matériaux (C7)**, **Matériel (C8)** et **Transport (C9)** : les ressources avec leurs quantités, prix unitaires et valeurs. Un article sans ressources compte comme une ressource à lui seul.

Les formulaires suivent la pratique courante selon la méthodologie de l’Ordre 863/2008. Vérifiez-les par rapport à ce que demande votre client ou le programme de financement : demandez-nous si vous avez besoin d’une autre mise en page.

**Placer sur une feuille** met le tableau affiché sur une nouvelle feuille, prête à imprimer avec les autres. **XLSX** enregistre le devis et chaque formulaire dans un seul classeur, une feuille chacun. **PDF** les enregistre en PDF, A4 paysage, prêts à imprimer.

## IFC

**Fichier → Exporter IFC…** écrit le devis comme **IfcCostSchedule** : ses chapitres et ses lignes comme **IfcCostItem**, la quantité de chaque ligne et ses prix unitaires comme quatre **IfcCostValue** (Matériau, Main-d’œuvre, Matériel, Transport), les éléments que chaque ligne mesure qui lui sont affectés (**IfcRelAssignsToControl**), la monnaie comme **IfcMonetaryUnit** du projet. Importé, un devis garde les lignes telles que le fichier les a, avec les mêmes totaux. Cochez **Lignes du modèle** pour ajouter les lignes que donne votre modèle.
