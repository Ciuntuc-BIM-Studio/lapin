---
title: Feuilles, PDF et DXF
order: 100
summary: Disposez les vues sur des feuilles avec un cartouche, et imprimez-les en PDF ou en DXF.
---

<!-- video sheets -->

## Créer une feuille

Cliquez sur **+** à côté de **Feuilles** dans l’arborescence du projet. Pour placer une vue, faites-la glisser de l’arborescence sur la feuille, ou choisissez-la dans **Placer une vue…** dans la barre de la feuille. Chaque vue garde sa propre échelle et reste liée au modèle : la feuille change quand le modèle change. Sur la feuille, faites glisser une vue pour la déplacer : elle seule bouge, avec son titre, tandis que le cadre, le cartouche et les autres vues restent en place ; elle s’accroche aux bords et au milieu du cadre et aux autres vues, et les flèches la décalent de 1 mm (10 mm avec Maj).

![Une feuille A3 avec un plan et une coupe](shots/sheet.webp)

Sélectionnez la feuille pour remplir son **cartouche** dans le panneau des propriétés : le projet, le client, qui l’a dessinée, qui l’a vérifiée, la date et l’indice de révision. Son numéro, son format de papier et son orientation se règlent aussi à cet endroit. Laissé vide, le champ du projet affiche le nom du projet : le nom qui lui a été donné, sinon le nom de son fichier.

## Ce que montre une vue

Une vue sur la feuille montre ce que vous choisissez, sans la faire glisser à l’aveugle :

- **La recadrer sur la feuille.** Cliquez une vue : des poignées apparaissent aux coins et sur les bords. Faites-les glisser et le cadre prend plus ou moins de la vue, tandis que le dessin reste en place sur le papier. **Maj**-glisser à l’intérieur fait glisser le dessin dans son cadre.
- **Dessiner sa limite dans la vue.** **Annotate → View boundary** (<kbd>VB</kbd>) : dessinez un rectangle sur un plan, une coupe ou une façade et donnez-lui un nom, unique dans le projet. Dans **Place view…** la vue est alors proposée avec chacune de ses limites (« Ground floor plan › Aile est »), et une vue placée ainsi montre exactement ce rectangle et le suit quand vous le déplacez ou le redimensionnez dans la vue. La même vue peut aller sur plusieurs feuilles, une limite chacune. Une vue placée passe d’une limite à l’autre, ou au recadrage libre, dans la liste de son cadre. Les limites se voient à l’écran seulement, jamais sur le papier.
- **L’épingler.** L’épingle sur le cadre la garde en place : elle se sélectionne, mais ne se déplace ni ne se recadre par erreur.

Le cadre et le cartouche appartiennent au papier. Ils ne se sélectionnent ni ne se déplacent jamais, et un clic prend la plus petite vue sous le pointeur : une vue posée sur le cartouche se sélectionne toujours.

![Une vue sélectionnée sur la feuille : ses poignées de recadrage, sa limite, l’épingle](shots/sheet-view-boundary.webp)

## Votre propre cartouche

**Gérer → Cartouches…** liste les cartouches : ceux de Lapin (**Lapin standard**, celui de toutes les feuilles jusqu’ici, le **cartuș** roumain et un cartouche **ISO 7200**), ceux du projet et ceux de la bibliothèque de cet ordinateur, proposés dans tous ses projets. Cliquez-en un pour le voir avec les valeurs du projet. **Dupliquer dans le projet** en fait une copie modifiable, **Nouveau** un vide ; **Définir par défaut pour le projet** le donne à toutes les feuilles qui n’en choisissent pas, et une feuille choisit le sien sous **Cartouche** dans ses propriétés.

![Cartouches : ceux de Lapin, du projet et de cet ordinateur, celui choisi avec les valeurs du projet](shots/title-blocks.webp)

**Modifier…** ouvre l’éditeur. Le cartouche est un cadre en millimètres papier placé dans un coin du cadre de la feuille (en bas à droite par défaut) : le même cartouche convient du A4 au A0, en portrait ou en paysage ; les marges du cadre se règlent ici. Dessinez avec **Ligne**, **Rectangle**, **Polyligne**, **Texte**, **Image** (votre logo, PNG ou JPEG) et **Tableau des révisions** ; un rectangle ou une polyligne fermée prend un remplissage ou une hachure, un texte une hauteur ou un style de texte des Standards de dessin. Les clics s’accrochent aux extrémités, coins et milieux des éléments et à la grille ; **Maj** garde la ligne droite. Glissez un élément pour le déplacer, ses poignées pour le remodeler ; les flèches le décalent, **Suppr** le supprime, **Ctrl+Z** annule.

La liste **Champs** contient le texte vivant : du projet (nom, numéro, client, terrain, permis, concepteur, personnes et vos propres paramètres), de la feuille (numéro, nom, échelle, format, révision, statut, date, dessiné, vérifié et approuvé par, phase, feuille x sur y) et du système (date d’impression, nom du fichier). Un clic met le champ dans le texte sélectionné ou le place comme nouveau texte ; un texte peut mêler mots et champs, `Projet : {project.name}`. L’éditeur montre la valeur de chaque champ sur une feuille d’exemple ; un champ sans valeur apparaît en gris avec son nom, un champ inconnu de Lapin en rouge (imprimé comme `<champ>`, et **Gérer → Vérifier** le signale). **Enregistrer** garde le cartouche comme une seule étape à annuler.

![L’éditeur : un cartouche de style finlandais, ses champs avec leurs valeurs, un champ ajouté sous le numéro de la feuille](shots/title-block-designer.webp)

Les feuilles, leur PDF, l’impression et l’émission le dessinent avec les valeurs de chaque feuille. En DXF, c’est un bloc avec des attributs, un par texte vivant (`PROJECT_NAME`, `SHEET_NUMBER`…), comme les cartouches d’AutoCAD. **Exporter…** enregistre un cartouche en fichier `.json`, avec ses images, et **Importer…** le lit dans le projet ou la bibliothèque : une agence transporte ses cartouches d’un ordinateur à l’autre. Les anciens projets gardent Lapin standard et se dessinent exactement comme avant.

![La feuille avec son propre cartouche](shots/sheet-title-block.webp)

## Imprimer

Les boutons **PDF** et **DXF** de la barre de la feuille exportent la feuille. Un PDF est prêt à imprimer ou à envoyer. Un DXF s’ouvre dans AutoCAD et dans les autres logiciels de DAO, avec ses calques, ses lignes et ses hachures. **Imprimer** envoie la feuille à une imprimante ou un traceur à son propre format, à 1:1, en traits vectoriels : choisissez le traceur et le papier dans la boîte d’impression. Sur une imprimante plus petite, choisissez l’ajustement à la page.
