---
title: Materiais, texturas e hachuras
order: 90
summary: Defina a cor, as hachuras e as juntas de cada material, coloque nele a sua própria imagem e traga os seus próprios padrões de hachura .pat.
---

**Gerenciar → Materiais** lista os materiais do projeto. Escolha um para alterá-lo.

![A caixa de diálogo Materiais](shots/materials-dialog.webp)

## Aparência: como um material é renderizado

A aba **Aparência** define como o material aparece em 3D e nos programas de renderização para os quais você exporta. A aba **Gráficos 2D** mantém as hachuras e penas, como antes.

![A aparência de um material, com a sua pré-visualização ao vivo](shots/material-appearance.webp)

- **Cor** e **Textura**: uma textura desenhada (tijolo, tábuas de madeira, ladrilhos…) ou a sua própria imagem. **W** e **H** definem o tamanho de um ladrilho no modelo, em metros; **Rotação** o gira.
- **Rugosidade**: 0 é um espelho, 1 é giz. **Metalicidade**: 1 para metal nu (alumínio anodizado, aço, cromo), 0 para tudo o que é pintado ou pintado a pó, madeira e pedra.
- **Transparência**: quanto se vê através. **Vidro (transmissão)** e **IOR** (1,52 para o vidro) fazem o vidro refratar num programa de renderização.
- **Brilha** emite luz, como o difusor de uma luminária.

A esfera mostra o resultado na hora. O que você não define vem do nome: um material chamado *vidro* ou *glass* é transparente, *aço* ou *steel* metálico, *parquet* e *laminado* são tábuas de madeira. **Aparência pelo nome** retira de novo os seus valores.

## Da biblioteca

**Da biblioteca…** oferece materiais prontos: metais (alumínio RAL 7016 antracite, anodizado, aço, cobre…), vidro (incolor, azul, verde, cinza, fosco, refletivo), madeiras em tábuas, concreto, tijolo, reboco e pintura, revestimentos e pedra, plástico e tecido. Um clique adiciona um ao projeto; altere-o como qualquer outro.

![A biblioteca de materiais](shots/material-library.webp)

## Dados: descrição, parâmetros, como se mede

O separador **Dados** guarda o que o material diz de si próprio. Escreve-o uma vez; lê-o cada tipo em que o material está: a etiqueta de composição, o levantamento de quantidades, o orçamento, a exportação IFC.

![Os dados de um material: descrição, parâmetros, como se mede e espessuras de catálogo](shots/material-data.webp)

- **Descrição**: o texto da especificação, em várias linhas (*Placas de lã mineral, λ = 0,035 W/mK, fixadas com buchas*). Uma etiqueta de composição escreve-o no lugar do nome.
- **Parâmetros**: um nome, um tipo e um valor, como nos elementos. **Adicionar um habitual…** oferece **Condutividade térmica λ**, **Resistência ao vapor μ** e **Densidade**, que vão para o IFC com os seus nomes padrão (*Pset_MaterialThermal.ThermalConductivity*, *Pset_MaterialHygroscopic.UpperVaporResistanceFactor*, *Pset_MaterialCommon.MassDensity*, em unidades SI), e **Reação ao fogo** e **Resistência à compressão**, que ficam como seus. Os seus parâmetros vão para o IFC como *Lapin_Material*.
- **Medido por** diz como **Custos → Levantamento de quantidades** (por material) e o orçamento o contam: **área** (m², uma linha por espessura: placas, isolamentos, membranas), **volume** (m³: betão, brita, betonilha), **massa** (kg, pela densidade) ou **peças** (placas do tamanho indicado). Vazio, mede-se como os seus elementos.
- **Espessuras de catálogo (mm)**: as espessuras em que o fabricante o produz, por exemplo *50, 80, 100, 150, 200, 240*. As tabelas de camadas de **Tipos de parede** e **Tipos de piso** oferecem-nas ao lado da espessura (pode escrever outra) e uma camada de outra espessura é marcada com um aviso.

A exportação IFC escreve a descrição como Description do material e os parâmetros como os seus conjuntos de propriedades; a importação lê-os de volta, também do ficheiro de outro programa.

## Um material para todos os caixilhos e todo o vidro

As novas portas e janelas são feitas de materiais compartilhados, parte por parte: **Window frame**, **Door frame**, **Glass**, **Door leaf**, **Window board** e **Hardware**. Altere **Window frame** uma vez e todos os caixilhos de janela mudam.

Para usar outros materiais, abra **Arquitetura → Portas e janelas**, escolha para um tipo o **Material do caixilho**, o **Material do vidro** e os demais, e clique em **Usar em todas as janelas** (ou **Usar em todas as portas**). Todos os tipos desse gênero os recebem, num só passo de desfazer. As fachadas-cortina também têm um material de caixilho e um de vidro.

![Os materiais das partes no configurador de portas e janelas](shots/joinery-materials.webp)

## Duplicar, substituir, selecionar

- **Duplicar** copia um material com um novo nome.
- **Substituir por** um material e depois **Substituir**: cada camada, tipo, porta, janela e elemento que usava o primeiro passa a usar o segundo, num só passo de desfazer. **Substituir e excluir** também remove o primeiro. Caso contrário, um material em uso não pode ser excluído.
- **Selecionar elementos** seleciona todos os elementos com uma parte nesse material.
- **Pesquisar materiais** e a lista de categorias filtram a lista.

## Materiais para renderização

Twinmotion, Lumion, Enscape, Blender e outros programas de renderização trocam materiais pelo nome. O Lapin escreve cada material **uma só vez, com o seu próprio nome**, com cor, textura, rugosidade, metalicidade e vidro, e divide a malha de cada elemento por material.

1. Dê aos materiais o nome que quer no programa de renderização, por exemplo *Aluminium RAL 7016 anthracite* para todos os caixilhos e *Glass blue tinted* para todo o vidro.
2. **Arquivo → Exportar 3D (glTF, USD, DAE)…** e escolha **GLB** (recomendado: um arquivo com as texturas, os nomes exatos e o vidro), **USDZ** ou **OBJ + MTL** (um .zip com as texturas; os espaços nos nomes viram sublinhados).
3. Importe o arquivo no programa de renderização. Lá todos os caixilhos compartilham um material; troque-o uma vez por um metal do programa e todos mudam.

![Uma casa com caixilhos cinza-escuro e vidro azul](shots/materials-render-3d.webp)

A exportação IFC também nomeia o estilo de superfície de cada material como ele, com a sua transparência e a forma como reflete.

## O que um material define

- **Cor (3D)** e as faces desenhadas sobre ele em 3D. O Lapin as deduz pelo nome, por exemplo as fiadas de um tijolo ou a grade de uma cerâmica. **Extremidades** e **Topo e base** podem ter aparência diferente das faces.
- **Hachura** para cada lugar em que ele é desenhado: cortado em um corte, cortado em uma planta e visto em uma elevação. Cada uma tem a sua escala, ângulo, alinhamento, preenchimento e pena. Um alçado ou um corte tracejam as faces que veem com a hachura de elevação, onde nada mais próximo as esconde (Vistas, Tracejados de superfície); até lhe dar uma, o seu nome propõe uma (tijolo BRICK, pedra STONE, madeira BOARDS, revestimento metálico VERTICAL), nunca o BATT do isolamento.
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
