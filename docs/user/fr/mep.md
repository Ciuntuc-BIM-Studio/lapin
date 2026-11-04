---
title: Réseaux de fluides
order: 57
summary: Dessinez les réseaux de chauffage, d’eau, d’évacuation, d’électricité et de ventilation en schémas sur le plan, avec leurs sources, leurs terminaux et les tronçons qui les relient, et exportez-les en réseau IFC.
---

L’onglet **Fluides** dessine les installations du bâtiment comme un ingénieur les esquisse : un système par installation, sa **source** (une chaudière, un compteur d’eau, une colonne de chute, un tableau électrique, un ventilateur), ses **terminaux** (radiateurs, lavabos, WC, prises, luminaires, diffuseurs) et les **tronçons** qui les relient. Chaque nœud est un véritable élément IFC avec ses ports ; chaque tronçon est un segment de tuyau, de gaine ou de câble ; les connexions tiennent quand vous déplacez les éléments.

![L’onglet Fluides](shots/ribbon-mep.webp)

## Un système en un clic

Sous **Nouveau système**, choisissez l’installation (**Chauffage**, **Eau**, **Eau chaude**, **Évacuation**, **Électricité**, **Ventilation**) et cliquez dans le plan. Lapin place le système entier : la source là où vous avez cliqué, un premier terminal à quelques mètres, et le tronçon qui les relie. Le système s’ouvre en modification.

![Un système de chauffage sur le plan du rez-de-chaussée : la chaudière, deux radiateurs sous les fenêtres et les tronçons qui les relient](shots/mep-plan.webp)

En mode modification, tout ce qui n’appartient pas au système est estompé, et seuls les nœuds et les tronçons du système peuvent être sélectionnés. Faites glisser la source et le terminal à leur place : leur tronçon suit. Ajoutez des terminaux avec **+ Radiateur**, **+ Lavabo**… dans le panneau des propriétés, ou avec les outils sous **Chauffage**, **Plomberie**, **Électricité** et **Air** : chaque nouveau terminal rejoint le système par un tronçon qui lui est propre, depuis le port libre le plus proche de la source, ou depuis le tronçon le plus proche (un té). **Terminé** (ou <kbd>Esc</kbd>) quitte le mode modification et vous indique combien de coudes et de tés compte le système et ce qui reste ouvert. **Modifier le système** (<kbd>ES</kbd>) le rouvre.

## Tronçons

**Tronçon** (<kbd>RN</kbd>) dessine un tronçon point par point ; appuyez sur <kbd>Enter</kbd> pour terminer. Une extrémité qui tombe sur un port libre s’y connecte ; une extrémité qui tombe sur un tronçon du même système crée une dérivation.

**Connecter** relie l’extrémité d’un tronçon à un nœud ou à un autre tronçon ; **Déconnecter** la libère. **Ports libres** sélectionne tout ce qui n’est pas encore connecté.

## Remodeler un tronçon

Sélectionnez un tronçon : ses poignées sont posées sur la ligne telle qu’elle est dessinée, même après que vous avez déplacé les nœuds qu’il relie.

- **Faites glisser une poignée** pour déplacer librement cet angle.
- **Faites glisser le milieu d’un segment** pour le faire coulisser latéralement ; les segments voisins s’allongent, de sorte qu’un tracé à angles droits reste à angles droits. Faites coulisser le premier ou le dernier segment, et Lapin y ajoute un décrochement, pour que l’extrémité reste sur son port.
- **Ctrl-clic** sur une poignée la retire, sur le milieu d’un segment en ajoute une ; **Ajouter une poignée** en place une là où vous cliquez.
- **Dévoiement** (<kbd>DU</kbd>) fait contourner un obstacle à un tronçon : cliquez sur le tronçon là où le dévoiement commence, cliquez là où il finit sur le même segment, puis déplacez-vous du côté où il passe et cliquez (ou tapez la distance). Le tronçon sort, longe et revient, avec quatre nouveaux angles ; il garde ses connexions.
- **Redessiner le tracé** (<kbd>RP</kbd>) donne un nouveau tracé à un tronçon : sélectionnez-le (ou commencez dessus) et dessinez le tracé point par point, <kbd>Enter</kbd> pour terminer. Le tronçon garde son système, sa taille et ses connexions, et ses extrémités reviennent sur leurs ports.

![Un tronçon de radiateur qui contourne un obstacle avec Dévoiement : il sort, longe et revient](shots/mep-detour.webp)


## Nœuds

Chaque nœud a un **rôle** : une chaudière, un radiateur, un collecteur, un compteur d’eau, un lavabo, un évier, un WC, une douche, une baignoire, une colonne de chute, un tampon de dégorgement, un tableau électrique, une prise, un luminaire, un interrupteur, un ventilateur, un diffuseur, une grille d’extraction, une vanne, une montée / descente, un bouchon. Le rôle lui donne sa classe IFC, ses ports, sa taille et son symbole sur le plan. Une vanne, un compteur ou un interrupteur déposé sur un tronçon le scinde et se place au milieu. Une **montée / descente** mène un tronçon à une autre altitude ou au niveau supérieur.

![Les propriétés d’un radiateur : son système, son rôle et ses ports, sa taille et sa puissance](shots/mep-radiator-properties.webp)

Dans le panneau des propriétés, un nœud montre son système, son rôle et ses ports (libres ou connectés) ; un tronçon montre ses deux extrémités. Les deux prennent les valeurs par défaut du système, sauf si vous leur donnez une valeur : la taille, l’altitude, le plan dans lequel ils passent (sol, plafond, mur), en paire ou simple, le matériau, la pente.

## Systèmes

**Systèmes…** liste les systèmes du projet et les valeurs par défaut du système courant : la taille, le plan et les altitudes de chaque plan, la pente (évacuation), si les tronçons sont dessinés en paire (chauffage : aller et retour) et à quel écartement, le matériau et l’isolation. Modifiez une valeur par défaut, et chaque nœud et chaque tronçon sans valeur propre la suit.

![La boîte de dialogue Systèmes](shots/mep-systems.webp)

## Plans, listes et IFC

**Plan des fluides** crée un plan de la discipline Fluides pour le niveau, rangé dans l’ensemble Fluides de l’arborescence. La **Liste des équipements** et la **Liste des tronçons** sous **Système** sont des nomenclatures comme les autres : elles se placent sur les feuilles et s’exportent avec elles.

Dans un export IFC, chaque système est un IfcDistributionSystem, chaque nœud et chaque tronçon a ses IfcDistributionPorts, et les connexions sont écrites entre les ports, de sorte que le réseau arrive dans un autre logiciel connecté de la source jusqu’au dernier terminal.

## Gaz et fioul

**Gaz** et **Fioul** sont des services à part sous **Nouveau système**. Un système de gaz dit son gaz (**Gaz naturel** ou **GPL**) et sa **Classe de pression** (basse dans la maison, moyenne dans la rue) dans **Systèmes…** ; ses tubes sont par défaut en acier noir sur les murs à 2,10 m, et peuvent être en cuivre, multicouche ou **PE 100** pour ce qui est enterré. Le fioul passe en cuivre.

L’installation de gaz d’une maison part du **coffret de comptage** sur la façade. Clique **Nouveau système → Gaz** : le coffret va où tu cliques et une **chaudière gaz** à l’intérieur, reliés par une conduite. En chemin, les outils de **Gaz** posent sur la conduite le **détendeur de gaz**, le **compteur de gaz**, le **robinet général de gaz**, les **robinets d’appareil** et les **raccords flexibles**, qui la coupent comme tout raccord ; la **cuisinière à gaz** et le **chauffe-eau à gaz** rejoignent l’installation par une conduite à eux. Là où un tube de gaz ou de fioul traverse un mur, il passe dans un **fourreau**, dessiné et compté avec la conduite. Une chaudière gaz dessinée dans le système de chauffage comme sa source rejoint l’installation de gaz depuis ses propriétés (le bouton **+** avec le nom du système de gaz).

![L’installation de gaz d’une maison : le coffret, le détendeur et le compteur dehors, la chaudière et la cuisinière dans la cuisine](shots/mep-gas-plan.webp)

Chaque appareil garde sa **consommation** (m³/h) et son **type d’appareil** selon son évacuation (A sans conduit, B raccordé, C étanche), pour les listes et le dimensionnement.

**Fioul** pose une **cuve à fioul** (intérieure, extérieure ou enterrée) et une **chaudière fioul** ; la **bouche de remplissage** et le **chapeau d’évent** rejoignent le remplissage et l’évent de la cuve, et le **filtre à fioul** se place sur l’aller.

### L’alimentation dans la rue

Sous **Rue**, **Conduite de gaz le long de…** (<kbd>GM</kbd>) pose une conduite de gaz le long de ce que tu cliques — une route, un axe, une ligne dessinée, un mur — à la **distance à la ligne** et à la **profondeur (couverture)** de la barre de la ligne de commande ; sous un terrain la profondeur se prend depuis le sol, sinon depuis le niveau. **Conduite de gaz** la dessine point par point. La conduite va dans un système de gaz moyenne pression, en tube PE, avec son **grillage avertisseur**.

**Branchement** (<kbd>GS</kbd>) : clique la conduite, puis le coffret. Lapin pose une **prise de branchement** sur la conduite, pose le tube de branchement d’équerre à sa profondeur jusqu’au coffret, et y remonte dans le coffret.

![Le branchement de la conduite de la rue au coffret, sélectionné](shots/mep-gas-street.webp)

![La conduite et le branchement sous terre, l’installation sur la façade](shots/mep-gas-3d.webp)

![Les propriétés d’un branchement : de la prise de branchement au coffret, sa couverture et son diamètre](shots/mep-gas-service-properties.webp)

![Les valeurs par défaut d’un système de gaz : son gaz et sa classe de pression](shots/mep-gas-systems.webp)

**Métré gaz et fioul** sous **Système** liste, système par système, les tubes par matériau et diamètre avec leur longueur (et dedans les colonnes montantes), les coudes, tés et fourreaux, le grillage avertisseur, les robinets, compteurs et coffrets, et les appareils avec leur consommation et leur puissance installées. En IFC un système de gaz est un IfcDistributionSystem de type GAS et un système de fioul FUEL ; le coffret est un IfcDistributionChamberElement, le compteur un IfcFlowMeter GASMETER, les robinets des IfcValve, la cuisinière un IfcBurner, la cuve un IfcTank.

![Le métré gaz et fioul](shots/mep-gas-list.webp)

Dimensionner les tubes d’après la consommation des appareils et vérifier la perte de charge relèvent de l’ingénierie, dans l’édition **Lapin MEP**.

## MEP Pro

L’ingénierie du réseau — le débit que porte chaque tronçon et la taille qui le supporte, les vérifications (extrémités ouvertes, coudes trop serrés, pentes d’évacuation, sources surchargées), les règles de pose par matériau (le cuivre par pas de 45°, le PVC tournant au plus de 45°), les radiateurs proposés sous les fenêtres, les tracés suggérés le long des murs, le profil d’un tronçon et l’isométrie d’un système — se trouve dans l’édition **Lapin MEP**, qui ajoute les groupes **Ingénierie** et **Assistants** à cet onglet.
