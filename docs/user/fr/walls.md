---
title: Murs
order: 20
summary: Dessinez les murs angle par angle, modifiez-les dans le panneau des propriétés et définissez des types de murs avec leurs couches.
---

<!-- video walls -->

## Dessiner des murs

Ouvrez l’onglet **Architecture** et choisissez **Mur**, ou tapez <kbd>WA</kbd>.

Tant que l’outil est actif, le panneau des propriétés montre **Nouvel élément** : le type avec lequel les murs sont dessinés. Choisissez-en un autre là avant le premier clic, et voyez ses couches, son épaisseur et combien de murs l’utilisent déjà ; **Modifier les types…** ouvre Wall types. Porte, fenêtre, dalle, faux plafond et mur-rideau montrent leurs types de la même façon.

![L’onglet Architecture](shots/ribbon-architecture.webp)

Cliquez là où le mur commence, puis cliquez à chaque angle. Le mur suivant suit la souris. Pendant que vous dessinez, vous pouvez taper une longueur et un angle dans les cases au-dessus de la vue au lieu de cliquer. Appuyez sur <kbd>Enter</kbd> ou <kbd>Esc</kbd> pour arrêter.

![Dessin de murs : le mur suivant suit la souris, avec sa longueur et son angle affichés](shots/wall-drawing.webp)

Là où deux murs se rencontrent, Lapin les joint de lui-même. Leurs couches se prolongent l’une dans l’autre comme elles seraient construites.

- **Mur courbe** (<kbd>WC</kbd>) dessine un mur le long d’un arc.
- **Mur-rideau** (<kbd>CW</kbd>) dessine un mur vitré avec des meneaux et des panneaux.
- Les murs se joignent d’eux-mêmes là où ils se rencontrent : un angle en onglet, un mur qui finit sur un autre en T, un mur qui en traverse un autre en X. À un T ou à un X, les couches se rejoignent comme on les construit : le noyau du mur qui finit traverse les finitions de l’autre jusqu’à son noyau, et ses finitions s’arrêtent à la face de l’autre, où elles rejoignent leurs semblables, si bien que le plan montre une seule composition, sans ligne au travers. **Joindre** (<kbd>JW</kbd>), **Onglet** et **Bout à bout** changent la façon dont deux murs se rencontrent quand ce n’est pas ce que vous voulez ; **Nouveaux angles en onglet** fait du bout à bout le réglage par défaut sur cet ordinateur. Dans les deux cas, chaque couche tourne l’angle d’elle-même : les finitions rejoignent leurs semblables et continuent sans rupture, et les âmes se rencontrent sur la diagonale (onglet) ou l’une va jusqu’à la face extérieure de l’autre (bout à bout), si bien que le plan des murs multicouches montre des angles nets. Chaque couche entre dans l'autre mur à travers ses couches de **Priorité** inférieure et s'arrête à la première de priorité égale ou supérieure (voir **Types de murs** plus bas) : par défaut, les noyaux rencontrent les noyaux et les finitions s'arrêtent aux faces ; à un angle, un noyau de priorité supérieure traverse celui de l'autre, en onglet comme en about. La **Priorité de coupe** d'un mur ne le fait jamais entrer dans un mur auquel il est joint : entre murs joints, elle décide seulement quel noyau traverse quand les deux noyaux ont la même priorité (à un angle en about ou à un croisement).
- **Permuter** change lequel des deux murs passe à travers l’autre.

**La ligne sur laquelle vous dessinez.** Tant que l’outil Wall est actif, la liste dans la barre de la ligne de commande choisit la ligne cliquée : **Axe du noyau** (l’axe, comme avant), **Noyau, face extérieure**, **Noyau, face intérieure**, **Face extérieure** ou **Face intérieure**. L’extérieur est la droite du mur dans le sens du tracé (tournez autour du bâtiment dans le sens antihoraire pour qu’il soit dehors). L’axe du mur reste sur son noyau, décalé de la ligne, et les murs tracés l’un après l’autre sur la même face se rejoignent à l’angle de cette face.

## Modifier un mur

Cliquez sur un mur pour le sélectionner. Ses poignées apparaissent. Faites glisser les extrémités pour l’allonger ou le raccourcir, ou faites glisser la croix au milieu pour le déplacer.

![Un mur sélectionné avec ses poignées](shots/wall-selected.webp)

Le panneau des propriétés affiche le mur sélectionné. Le haut du panneau contient son type et ses couches. En dessous se trouvent sa longueur, sa hauteur, son décalage de base, son niveau et sa phase. Changez une valeur et le modèle suit.

![Les propriétés d’un mur](shots/wall-properties.webp)

Plus bas, **Dépendances** montre ce qui est construit à partir du mur et ce à partir de quoi le mur est construit. **Utilisé par** liste les éléments qui changent quand le mur change : les murs qu’il raccorde, ses portes et fenêtres, un mur attaché à lui. Cliquez sur un nom pour sélectionner l’élément. **Construit à partir de** liste les éléments dont dépend le mur, puis son type et ses matériaux. Quand vous supprimez un élément dont d’autres dépendent, un message nomme ceux qui ont été reconstruits ; <kbd>Ctrl</kbd> <kbd>Z</kbd> ramène tout.

**Retourner** fait passer les couches du mur de l’autre côté. Vous pouvez aussi cliquer sur les petites flèches affichées à côté d’un mur sélectionné dans le plan.

## Types de murs

Un type de mur est un ensemble de couches, de la face à droite du sens dans lequel le mur est tracé (l’extérieur, si vous tracez autour d’un bâtiment dans le sens inverse des aiguilles d’une montre) à la face à sa gauche. Chaque couche a un matériau et une épaisseur, et une couche est le **noyau**. La ligne du mur suit le noyau. Pour modifier les types, choisissez **Architecture → Types de murs**. Une couche autre que l’âme peut avoir sa propre hauteur : **De** et **Jusqu’à** règlent jusqu’où elle monte au-dessus du pied du mur (une faïence jusqu’à 1500, un bardage à partir de 300) ; vide, elle court sur toute la hauteur.

La colonne **Priorité** (de 0 à 100) indique comment la couche rencontre les couches d'un autre mur là où les murs se joignent : elle traverse les couches de l'autre de priorité inférieure et s'arrête à la première de priorité égale ou supérieure. Vide, la couche prend la priorité de son matériau dans **Gérer → Ordre de coupe**, sinon 50 pour le noyau et 0 pour les autres couches (le nombre affiché en grisé). Ainsi, un noyau en béton de 100 n'est pas interrompu par une ossature métallique de 99 et traverse la cloison, et un enduit dur de 60 arrête le noyau d'un mur qui vient s'y terminer. L'export IFC l'écrit comme la Priority de la couche, et l'import la relit.

Une couche autre que le noyau peut aussi changer de matériau sur sa hauteur : le bouton **couches** au bout de sa ligne ajoute un morceau (carrelage, 2100 par défaut), depuis le bas de la couche vers le haut ; ajoutez-en d’autres pour une bande (peinture jusqu’à 900, carrelage jusqu’à 1500), et au-dessus du dernier morceau la couche continue dans son propre matériau jusqu’en haut. Chaque morceau est dessiné, coupé en plan et en coupe, et métré dans son propre matériau.

Une couche autre que le noyau peut avoir une épaisseur de 0, pour un matériau trop fin pour être modélisé mais qu’il faut métrer : une étanchéité, un pare-vapeur, une peinture. Elle ne forme aucun solide ; les plans et les coupes la dessinent en trait épais discontinu là où elle se trouve entre deux couches (sur une face, le trait de la face est le sien), le dessin des Compositions des types la nomme comme les autres, et elle est métrée en surface : la face de la couche sur laquelle elle repose, ouvertures déduites, dans les quantités du mur et dans le métré par matériau.

![Une couche d’enduit avec du carrelage jusqu’à 2100 et l’enduit au-dessus](shots/wall-layer-parts.webp)

![Types de murs : les couches d’un mur en brique](shots/wall-types.webp)

La légende au-dessus des couches montre le mur en plan comme le montre un mur sélectionné : l’axe en tirets avec une pointe de flèche dans le sens où il a été tracé, **D** sur sa face droite et **G** sur sa face gauche. Les couches sont numérotées de 1, sur la face D, à la dernière, sur la face G, avec les mêmes numéros que les lignes du tableau ; le noyau, qui porte la ligne du mur, est mis en évidence. Un mur retourné avec **Retourner** a ses couches dans l’autre sens, de G à D. Le message d’un enregistrement refusé disparaît dès que vous modifiez à nouveau le type.

- **+ Couche** ajoute une couche. Les flèches montent ou descendent une couche.
- **Zones en hauteur** divise le noyau de bas en haut, par exemple une première assise, la maçonnerie et un chaînage.
- **Linteaux** choisit les linteaux posés au-dessus des portes et des fenêtres : préfabriqués ou coulés en place, avec leur appui et leur hauteur.
- **Dessiner avec** ferme la boîte de dialogue et commence à dessiner des murs de ce type.

Un changement apporté à un type change chaque mur de ce type.

**Import…**, **Export CSV** et **Export JSON**, sous la liste des types, font passer les types de murs et de dalles d’un projet à un autre, ou vers un tableur et retour : le CSV a une ligne par couche (Kind, Type, Mark, Layer, Material, Thickness (mm), Core, From, To), les lignes d’un type à la suite, et les points-virgules et virgules décimales d’Excel sont lus aussi. À l’import, un type du même nom prend les couches du fichier et les autres sont créés, en une seule étape à annuler ; un matériau que le projet n’a pas est créé d’après son nom. La nomenclature **Compositions des types** (Schedules → + → Type assemblies) liste chaque type de mur et de dalle avec son repère, son épaisseur totale, le nombre d’éléments qui l’utilisent et ses couches dans l’ordre.

## Moulures et engravures de mur

**Architecture → Moulure de mur** (<kbd>WB</kbd>) fait courir un profil le long de la face d'un mur : une corniche, un bandeau, un soubassement, un appui. **Engravure de mur** (<kbd>RV</kbd>) le creuse dans le mur : une rainure, un joint creux. Choisissez le profil et la hauteur dans la barre de la ligne de commande, puis cliquez sur la face du mur. En 3D la moulure se place à la hauteur cliquée ; en plan, à la hauteur saisie, du côté cliqué. **depuis le haut** mesure la hauteur vers le bas depuis le sommet du mur, pour une corniche sous l'avant-toit. **Moulure sur les murs sélectionnés** et **Engravure sur les murs sélectionnés** posent la même chose sur chaque mur sélectionné, sur sa face extérieure.

Une moulure court sur toute la longueur de la face et s'interrompt aux portes et fenêtres qu'elle traverserait. Là où deux murs se rencontrent en angle et portent tous deux une moulure du même profil à la même hauteur sur les faces qui se rejoignent, les deux se rejoignent en onglet, une seule moulure qui tourne l'angle. Sélectionnez une moulure pour la modifier dans les Propriétés : son profil, son matériau, sa face (**Changer de côté**), sa hauteur, son décalage par rapport à la face, où elle commence et finit le long du mur, et si elle s'interrompt aux ouvertures. Les propriétés d'un mur listent les moulures et engravures qu'il porte.

Moulures et engravures suivent leur mur quand il est copié, déplacé vers un autre niveau ou supprimé. Elles sont métrées en longueur et en volume (pour une engravure, le volume retiré). En IFC, une moulure est un IfcCovering MOLDING et une engravure un IfcOpeningElement RECESS dans son mur ; les deux reviennent telles qu'elles étaient.

![Corniches, bandeaux et une engravure sur les murs extérieurs d'une maison](shots/wall-sweeps-3d.webp)

### Profils

**Gérer → Profils** contient les profils du projet, les contours selon lesquels sont balayés les moulures, les engravures et les semelles filantes. **Nouveau profil** part du catalogue avec ses dimensions : un rectangle, un bandeau chanfreiné, une corniche, un demi-rond, un nez d'appui, un bandeau avec goutte d'eau ; une rainure en V, rectangulaire ou ronde ; pour les semelles, rectangulaire, à 2 ou 3 redans, à dessus incliné, excentrée, avec voile. Ou choisissez **Sommets saisis** : chaque sommet u v en millimètres, séparés par un point-virgule, un troisième nombre faisant du côté qui part de ce sommet un arc. **À partir d'un contour dessiné** lit une polyligne fermée ou un rectangle dessiné avec Dessin et sélectionné dans sa vue. Le cercle de l'aperçu est le point d'insertion : pour une moulure, il est sur la face du mur à la hauteur de la moulure, u vers l'extérieur de la face (dans le mur pour une engravure). Modifiez un profil, et tout ce qui l'utilise suit.

![La boîte de dialogue Profils](shots/profiles.webp)

### Bardages

Modélisez les bardages avec les couches du mur lui-même, pas avec des moulures. Ajoutez une couche de finition à l'extérieur du type de mur (pierre, bardage bois, fibres-ciment, une ITE), avec son épaisseur et son matériau. **De** et **Jusqu’à** lui donnent une hauteur propre (un soubassement en pierre jusqu'à 600, de l'enduit au-dessus), et le bouton **couches** de la couche change son matériau par bandes sur sa hauteur. Pour un bardage qui se tient à distance du mur, dessinez un mur à part sur la face (l'option de ligne **Face extérieure** de l'outil mur) avec un type fait des couches du bardage. Le bardage posé lame par lame (lames verticales ou horizontales, tasseaux) n'est pas encore modélisé.

## Murs sous un toit

Les murs du même niveau qu’un toit s’arrêtent d’eux-mêmes dessous et montent jusqu’à ses pignons. Pour un mur d’un autre niveau, sélectionnez-le et choisissez **Architecture → Attacher au toit** (<kbd>AR</kbd>), puis cliquez le toit — ou tout autre corps jusqu’auquel le mur doit monter : un solide Studio, un élément importé. Voir [Dalles et toits](floors-roofs.md).
