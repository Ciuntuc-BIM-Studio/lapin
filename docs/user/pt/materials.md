---
title: Materiais, texturas e hachuras
order: 90
summary: Defina a cor, as hachuras e as juntas de cada material, coloque nele a sua própria imagem e traga os seus próprios padrões de hachura .pat.
---

**Gerenciar → Materiais** lista os materiais do projeto. Escolha um para alterá-lo.

![A caixa de diálogo Materiais](shots/materials-dialog.webp)

## O que um material define

- **Cor (3D)** e as faces desenhadas sobre ele em 3D. O Lapin as deduz pelo nome, por exemplo as fiadas de um tijolo ou a grade de uma cerâmica. **Extremidades** e **Topo e base** podem ter aparência diferente das faces.
- **Hachura** para cada lugar em que ele é desenhado: cortado em um corte, cortado em uma planta e visto em uma elevação. Cada uma tem a sua escala, ângulo, alinhamento, preenchimento e pena.
- **Categoria**, por exemplo acabamento, isolamento ou estrutura. Os estilos de vista a usam para deixar os acabamentos fora de uma vista estrutural.
- **Membrana** desenha uma camada fina como uma linha tracejada em vez de uma faixa.
- **Desenhar juntas.** Por padrão, um material continua de um elemento para o próximo sem uma linha, como duas paredes alinhadas ou um reboco sobre dois pavimentos. Marque a opção para desenhar a junta.

## A sua própria imagem em um material

Um material pode levar uma imagem sua: uma foto do tijolo, da madeira ou da pedra de verdade.

1. Escolha o material e clique em **Imagem própria → Importar imagem…**. Escolha um arquivo PNG ou JPEG.
2. Defina **L** e **A**: a largura e a altura, em metros, de uma cópia da imagem no modelo. A imagem se repete pelas faces.

![A imagem própria de um material, aplicada em 1,2 × 1,2 m](shots/own-image.webp)

A imagem aparece na vista 3D e é salva dentro do arquivo do projeto. Ela também vai para as exportações 3D: **GLB**, **glTF** e **USDZ** a levam. **DAE** e **USDA** são arquivos únicos sem espaço para imagens, então ficam com a cor do material.

![A mesma casa com a imagem própria do material em 3D](shots/own-image-3d.webp)

Imagens maiores que 2048 pixels são reduzidas a esse tamanho quando você as importa. O botão da lixeira remove a imagem do material.

## Os seus próprios padrões de hachura (.pat)

**Importar .pat** lê um arquivo de padrões de hachura do AutoCAD. Cada padrão nele é adicionado ao projeto, pronto para ser escolhido como hachura de um material.

O Lapin também guarda o arquivo em **Meus padrões** no seu computador. Em qualquer outro projeto, **Adicionar a este projeto** traz esses padrões de novo. Um projeto guarda os padrões que usa, então ele abre igual em outro computador.

![Meus padrões: os arquivos .pat que você importou, guardados para todos os projetos](shots/my-patterns.webp)
