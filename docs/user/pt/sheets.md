---
title: Pranchas, PDF e DXF
order: 100
summary: Disponha vistas em pranchas com um carimbo e imprima-as em PDF ou DXF.
---

<!-- video sheets -->

## Criar uma prancha

Clique em **+** ao lado de **Pranchas** no navegador de projeto. Para colocar uma vista, arraste-a do navegador para a prancha, ou escolha-a em **Posicionar vista…** na barra da prancha. Cada vista mantém a sua escala e continua ligada ao modelo, assim a prancha muda quando o modelo muda. Na prancha, arraste uma vista para movê-la: a vista vai sozinha, com o título, enquanto a moldura, o carimbo e as outras vistas ficam; ela se ajusta às bordas e ao meio da moldura e às outras vistas, e as setas do teclado a deslocam 1 mm (10 mm com Shift).

![Uma prancha A3 com uma planta e um corte](shots/sheet.webp)

Selecione a prancha para preencher o **carimbo** no painel Propriedades: o projeto, o cliente, quem desenhou, quem verificou, a data e a revisão. O número, o papel e a orientação também são definidos ali. O papel é um formato A (A0 a A4), uma das folhas alongadas ISO (A0×2, A1×3, A2×3, A3×3, A3×4, A4×3), um formato norte-americano (Letter, Tabloid, ARCH D, ARCH E) ou **personalizado**, com a sua **Largura do papel** e **Altura do papel** em milímetros. Se ficar vazio, o campo do projeto mostra o nome do projeto: o nome dado ao projeto ou, se não houver, o nome do arquivo.

## O que uma vista mostra

Uma vista em uma prancha mostra o que você escolhe, sem arrastá-la às cegas:

- **Recorte-a na prancha.** Clique em uma vista: aparecem alças nos cantos e nas bordas dela. Arraste-as e o quadro pega mais ou menos da vista, enquanto o desenho fica onde está no papel. Arraste com **Shift** dentro da vista para deslizar o desenho dentro do quadro.
- **Desenhe o limite dela na vista.** **Anotar → Limite de vista** (<kbd>VB</kbd>): desenhe um retângulo em uma planta, corte ou elevação e dê a ele um nome, único no projeto. Em **Posicionar vista…** a vista passa a ser oferecida com cada um dos seus limites ("Planta do térreo › Ala leste"), e uma vista colocada assim mostra exatamente aquele retângulo e o acompanha quando você o move ou redimensiona na vista. A mesma vista pode ir para várias pranchas, um limite em cada uma. Uma vista colocada alterna entre os seus limites, ou para um recorte livre, na lista do seu quadro. Os limites aparecem só na tela, nunca no papel.
- **Fixe-a.** O alfinete no quadro de uma vista a mantém onde está: ela ainda pode ser selecionada, mas não é movida, deslocada nem recortada por engano.

A moldura e o carimbo pertencem ao papel. Eles nunca são selecionados nem movidos, e um clique seleciona a menor vista sob o ponteiro, assim uma vista colocada sobre o carimbo sempre pode ser selecionada.

![Uma vista selecionada em uma prancha: as alças de recorte, o limite, o alfinete](shots/sheet-view-boundary.webp)

## Seu próprio carimbo

**Gerenciar → Carimbos…** lista os carimbos: os do Lapin (**Lapin standard**, o que todas as folhas tinham até agora, o **cartuș** romeno e um **ISO 7200**), os do projeto e os da biblioteca deste computador, oferecidos em todos os seus projetos. Clique num para vê-lo com os valores do projeto. **Duplicar no projeto** faz uma cópia editável, **Novo** um vazio; **Definir como padrão do projeto** o dá a todas as folhas que não escolhem o seu, e uma folha escolhe o seu em **Carimbo** nas propriedades.

![Carimbos: os do Lapin, do projeto e deste computador, o escolhido com os valores do projeto](shots/title-blocks.webp)

**Editar…** abre o editor. O carimbo é uma caixa em milímetros de papel que fica num canto da moldura (inferior direito por padrão), então o mesmo carimbo serve do A4 ao A0, retrato ou paisagem; as margens da moldura se ajustam aqui. Desenhe com **Linha**, **Retângulo**, **Polilinha**, **Texto**, **Imagem** (seu logotipo, PNG ou JPEG) e **Tabela de revisões**; um retângulo ou uma polilinha fechada recebe preenchimento ou hachura, um texto uma altura ou um estilo de texto dos Padrões de desenho. Os cliques se ajustam às pontas, cantos e meios dos itens e à grade; **Shift** mantém a linha reta. Arraste um item para movê-lo, suas alças para remodelá-lo; as setas o deslocam, **Delete** o exclui, **Ctrl+Z** desfaz.

A lista **Campos** tem o texto vivo: do projeto (nome, número, cliente, terreno, alvará, projetista, pessoas e seus próprios parâmetros), da folha (número, nome, escala, papel, revisão, estado, data, desenhado, verificado e aprovado por, fase, folha x de y) e do sistema (data de impressão, nome do arquivo). Um clique põe o campo no texto selecionado ou o coloca como texto novo; um texto pode misturar palavras e campos, `Projeto: {project.name}`. O editor mostra o valor de cada campo numa folha de exemplo; um campo sem valor aparece em cinza com o nome, e um que o Lapin não conhece em vermelho (impresso como `<campo>`, e **Gerenciar → Verificar** o aponta). **Salvar** guarda o carimbo como um só passo a desfazer.

![O editor: um carimbo de estilo finlandês, seus campos com os valores, um campo adicionado sob o número da folha](shots/title-block-designer.webp)

As folhas, seu PDF, Imprimir e a emissão o desenham com os valores de cada folha. Em DXF é um bloco com atributos, um por texto vivo (`PROJECT_NAME`, `SHEET_NUMBER`…), como os carimbos do AutoCAD. **Exportar…** salva um carimbo como arquivo `.json`, com as imagens, e **Importar…** o lê no projeto ou na biblioteca, para que um escritório leve seus carimbos de um computador a outro. Projetos antigos mantêm o Lapin standard e são desenhados exatamente como antes.

![A folha com seu próprio carimbo](shots/sheet-title-block.webp)

## Imprimir

Os botões **PDF** e **DXF** na barra da prancha exportam a prancha. Um PDF está pronto para imprimir ou enviar. Um DXF abre no AutoCAD e em outros programas CAD com as suas camadas, linhas e hachuras. **Imprimir** envia a prancha para uma impressora ou plotter no tamanho de papel dela, em 1:1, como linhas vetoriais: escolha o plotter e o papel na caixa de diálogo de impressão. Em uma impressora menor, escolha ajustar à página.
