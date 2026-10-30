---
title: Dalles et toits
order: 40
summary: Dessinez dalles et toits par leur contour, choisissez la forme et les rives d’un toit, et percez des trémies.
---

## Dalles

Choisissez **Architecture → Dalle** (<kbd>SB</kbd>), puis cliquez les angles de son contour. Fermez le contour sur le premier point, ou appuyez sur <kbd>Enter</kbd>. Une dalle est elle aussi faite de couches, définies dans **Architecture → Types de dalles**.

Pour changer le contour, sélectionnez la dalle et faites glisser ses angles. **Trémie de dalle** (<kbd>SH</kbd>) perce un trou dans une dalle, pour un escalier ou une gaine.

Une dalle dessinée hors des murs, devant une porte, est un perron. Pour les marches qui y mènent, voir [Escaliers](stairs.md).

## Toits

Choisissez **Architecture → Toit** (<kbd>RF</kbd>) et cliquez le contour des murs qu’il couvre. Lapin construit le toit sur ce contour, en haut de son niveau.

![Une maison avec un toit à deux pans dans la vue 3D](shots/house-3d.webp)

Sélectionnez le toit pour le modifier dans le panneau des propriétés :

- **Forme.** À croupes, à deux pans, en appentis, à la Mansart, en voûte ou plat. Un toit en voûte est une voûte courbe entre deux pignons ; **Voûte : flèche à la clé** règle la hauteur à laquelle il monte à la clé, au plus la moitié de sa portée.
- **Pente et débord** pour tout le toit.
- **Chaque rive** avec son propre type (égout ou pignon), sa pente et son débord. Un toit à la Mansart avec des rives en pignon est un comble à la hollandaise.
- **Style.** Les styles de toit donnent tout un aspect en un seul choix : la couverture, les égouts, les gouttières et les descentes.

![Les propriétés d’un toit](shots/roof-properties.webp)

Les murs du niveau du toit s’arrêtent dessous et montent d’eux-mêmes jusqu’à ses pignons, sous un toit courbe aussi. Un mur d’un autre niveau suit le toit après **Architecture → Attacher au toit** (<kbd>AR</kbd>) : sélectionnez les murs, puis cliquez le toit. Le même outil accepte tout autre corps au-dessus des murs : un solide Studio, une dalle, une poutre, un élément importé d’IFC. Les murs montent jusqu’à sa sous-face et sont coupés le long de celle-ci, quelle que soit sa forme. **Détacher du toit** l’annule.

**Roof window** et **Dormer** ajoutent des ouvertures à un toit.
