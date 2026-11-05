---
title: Recherche de conflits
order: 58
summary: Trouvez où les éléments du modèle se traversent ou se rapprochent trop : jeux de recherche par règles, tests durs et de dégagement entre eux, conflits suivis d’un lancement à l’autre, montrés en 3D et envoyés en BCF.
---

**Gérer → Recherche de conflits** trouve où les éléments du modèle se traversent ou se rapprochent plus qu’ils ne devraient. Elle fonctionne de même sur les éléments de Lapin et sur les fichiers IFC et Fragments liés : elle coordonne donc aussi les modèles que vous envoient les autres corps de métier.

![La recherche de conflits, à côté de la vue 3D](shots/clash-dialog.webp)

## Tests et jeux

Un **test de conflit** compare deux **jeux de recherche** : le jeu A au jeu B, ou un jeu avec lui-même. Un projet commence avec trois jeux (Structure, Services, Architecture) et trois tests durs : Structure × Services, Services × Services et Architecture × Services.

- Les tests **durs** trouvent les éléments qui se traversent. Leur **tolérance** (5 mm par défaut) ignore les traversées aussi faibles, pour qu’un revêtement posé 3 mm dans un mur ne soit pas signalé.
- Les tests **de dégagement** trouvent les éléments plus proches qu’une distance (50 mm par défaut), et les traversées aussi.

Certaines paires ne sont jamais testées, parce qu’elles doivent se rencontrer : un mur et la porte ou la fenêtre de son ouverture, les parties d’un même assemblage, les murs joints en angle et les éléments attachés l’un à l’autre (une poutre posée sur un poteau, un mur attaché à un toit).

![Les réglages d’un test](shots/clash-test.webp)

## Lancer et suivre les conflits

**Lancer le test** lance le test affiché ; **Tout lancer** lance chaque test activé. Rien ne se lance tout seul : les résultats restent dans le projet jusqu’à ce que vous relanciez le test, et le dialogue dit quand le modèle a changé depuis.

Chaque conflit garde son numéro d’un lancement à l’autre. Un conflit trouvé pour la première fois est **Nouveau** ; trouvé à nouveau, il est **Actif**. Marquez-le à la main **Vérifié** ou **Approuvé**, attribuez-le à quelqu’un (**Attribué à**) et écrivez des **Notes**. Un conflit qu’un lancement ne trouve plus devient **Résolu** ; s’il revient, il est rouvert, avec le même numéro.

Cliquez sur un conflit pour le voir en 3D. Lapin sélectionne les deux éléments, place une boîte de coupe autour de leur rencontre et les regarde de côté, pour que vous voyiez le tuyau à travers le mur et non la face du mur. **seuls en 3D** cache le reste ; les flèches parcourent les conflits un par un. Le dialogue reste à droite, vous pouvez donc bouger la caméra pendant qu’il est ouvert.

![Un tuyau à travers un mur, cadré par le conflit](shots/clash-3d.webp)

**BCF** fait de chaque conflit ouvert (ou de ceux sélectionnés) une remarque de type Clash avec son point de vue, et les enregistre en fichier BCF pour d’autres outils BIM ; renvoyées, les mêmes remarques sont mises à jour. **CSV** enregistre la liste pour un tableur.

## Jeux de recherche

**Jeux de recherche** contient les règles qui choisissent les éléments d’un jeu. Une règle lit un champ de l’élément (sa classe, son type prédéfini, son type, son niveau, son matériau, son nom, le fichier lié ou n’importe quel paramètre) avec un opérateur : est, est l’un de, n’est pas, n’est aucun de, contient, ne contient pas, au moins, au plus, entre. Les nombres se lisent comme on les écrit : « 600 mm » vaut 600.

Un jeu prend les éléments qui remplissent une règle, ou toutes les règles. Une règle « n’est pas » retire toujours des éléments, et un jeu sans règle qui ajoute des éléments les prend tous. Le nombre à côté du jeu montre combien d’éléments prennent les règles pendant que vous tapez.

![Les règles d’un jeu de recherche](shots/clash-sets.webp)

> Les réseaux de Lapin peuvent percer eux-mêmes leurs ouvertures dans les murs et les planchers (Gérer → Ordre de coupe). Un tuyau à travers un mur n’est alors pas un conflit : le mur a son ouverture. **Gérer → Détection de conflits…** vérifie ces ouvertures.
