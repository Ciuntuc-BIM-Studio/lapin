---
title: Pranchas, PDF e DXF
order: 100
summary: Disponha vistas em pranchas com um carimbo e imprima-as em PDF ou DXF.
---

<!-- video sheets -->

## Criar uma prancha

Clique em **+** ao lado de **Pranchas** no navegador de projeto. Para colocar uma vista, arraste-a do navegador para a prancha, ou escolha-a em **Posicionar vista…** na barra da prancha. Cada vista mantém a sua escala e continua ligada ao modelo, assim a prancha muda quando o modelo muda. Na prancha, arraste uma vista para movê-la: a vista vai sozinha, com o título, enquanto a moldura, o carimbo e as outras vistas ficam; ela se ajusta às bordas e ao meio da moldura e às outras vistas, e as setas do teclado a deslocam 1 mm (10 mm com Shift).

![Uma prancha A3 com uma planta e um corte](shots/sheet.webp)

Selecione a prancha para preencher o **carimbo** no painel Propriedades: o projeto, o cliente, quem desenhou, quem verificou, a data e a revisão. O número, o papel e a orientação também são definidos ali. Se ficar vazio, o campo do projeto mostra o nome do projeto: o nome dado ao projeto ou, se não houver, o nome do arquivo.

## O que uma vista mostra

Uma vista em uma prancha mostra o que você escolhe, sem arrastá-la às cegas:

- **Recorte-a na prancha.** Clique em uma vista: aparecem alças nos cantos e nas bordas dela. Arraste-as e o quadro pega mais ou menos da vista, enquanto o desenho fica onde está no papel. Arraste com **Shift** dentro da vista para deslizar o desenho dentro do quadro.
- **Desenhe o limite dela na vista.** **Anotar → Limite de vista** (<kbd>VB</kbd>): desenhe um retângulo em uma planta, corte ou elevação e dê a ele um nome, único no projeto. Em **Posicionar vista…** a vista passa a ser oferecida com cada um dos seus limites ("Planta do térreo › Ala leste"), e uma vista colocada assim mostra exatamente aquele retângulo e o acompanha quando você o move ou redimensiona na vista. A mesma vista pode ir para várias pranchas, um limite em cada uma. Uma vista colocada alterna entre os seus limites, ou para um recorte livre, na lista do seu quadro. Os limites aparecem só na tela, nunca no papel.
- **Fixe-a.** O alfinete no quadro de uma vista a mantém onde está: ela ainda pode ser selecionada, mas não é movida, deslocada nem recortada por engano.

A moldura e o carimbo pertencem ao papel. Eles nunca são selecionados nem movidos, e um clique seleciona a menor vista sob o ponteiro, assim uma vista colocada sobre o carimbo sempre pode ser selecionada.

![Uma vista selecionada em uma prancha: as alças de recorte, o limite, o alfinete](shots/sheet-view-boundary.webp)

## Imprimir

Os botões **PDF** e **DXF** na barra da prancha exportam a prancha. Um PDF está pronto para imprimir ou enviar. Um DXF abre no AutoCAD e em outros programas CAD com as suas camadas, linhas e hachuras. **Imprimir** envia a prancha para uma impressora ou plotter no tamanho de papel dela, em 1:1, como linhas vetoriais: escolha o plotter e o papel na caixa de diálogo de impressão. Em uma impressora menor, escolha ajustar à página.
