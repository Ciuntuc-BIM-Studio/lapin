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

**Editar…** abre o carimbo numa prancheta: uma vista de desenho própria, em milímetros de papel, com a caixa do carimbo tracejada. O carimbo é uma caixa num canto da moldura, então o mesmo serve do A4 ao A0, retrato ou paisagem. Ele é desenhado com as mesmas ferramentas de **Desenho** de qualquer vista (linha, polilinha, retângulo, círculo, arco, texto, hachura e as ferramentas de edição); os encaixes e as alças funcionam como de costume. Os textos mantêm seus campos vivos como foram escritos, `{project.name}`, `{sheet.number}`. O logotipo é uma imagem: **Logotipo ou imagem…** no painel ou **Desenho → Importar imagem**. A tabela de revisões aparece como a sua caixa; mova-a ou redimensione-a. Um painel ao lado tem o **Nome**, a largura e a altura da caixa (mm), o canto da folha, **Moldura em volta da folha**, onde fica a tabela de revisões e os campos para copiar. **Terminar esboço** grava o carimbo num só passo a desfazer, e as suas folhas o acompanham; sem alterações, ele fica exatamente como era. **Cancelar** descarta a edição.

A lista **Campos** tem o texto vivo: do projeto (nome, número, cliente, terreno, alvará, projetista, pessoas e seus próprios parâmetros), da folha (número, nome, escala, papel, revisão, estado, data, desenhado, verificado e aprovado por, fase, folha x de y) e do sistema (data de impressão, nome do arquivo). Um clique copia um campo, para colar num texto escrito com **Texto**; um texto pode misturar palavras e campos, `Projeto: {project.name}`. Um campo que o Lapin não conhece é impresso como `<campo>`, e **Gerenciar → Verificar** o aponta.

![O carimbo na prancheta: a sua caixa tracejada, o desenho e o painel com os campos para copiar](shots/title-block-designer.webp)

As folhas, seu PDF, Imprimir e a emissão o desenham com os valores de cada folha. Em DXF é um bloco com atributos, um por texto vivo (`PROJECT_NAME`, `SHEET_NUMBER`…), como os carimbos do AutoCAD. **Exportar…** salva um carimbo como arquivo `.json`, com as imagens, e **Importar…** o lê no projeto ou na biblioteca, para que um escritório leve seus carimbos de um computador a outro. Projetos antigos mantêm o Lapin standard e são desenhados exatamente como antes.

![A folha com seu próprio carimbo](shots/sheet-title-block.webp)

## Desenhar na prancha

Notas, marcas e legendas que pertencem à prancha e não a uma das suas vistas desenham-se na própria prancha. Clique em **Anotar** na barra da prancha, ou dê um duplo clique no papel fora das vistas (**Anotar → Anotar prancha** faz o mesmo). A prancha abre-se no painel de desenho como o seu papel, em 1:1, com as vistas, a moldura e o carimbo por baixo, e a barra diz *desenho na prancha*. Todas as ferramentas de **Desenho** e **Anotar** funcionam ali como numa vista, em milímetros de papel: digite 50 e uma linha mede 50 mm no papel, e a altura de um texto é a sua altura no papel. Os pontos encaixam na moldura, no carimbo, nas caixas das vistas (azul tracejado) e no que as vistas desenham. **Prancha** na barra volta à disposição das vistas.

- Os **textos** podem ter campos dinâmicos, como os de um carimbo: `Prancha {sheet.number} de {sheet.count}`, `{project.name}`, `{sheet.date}`. Numa vista, um texto fica como foi digitado.
- **Nota com chamada** (Anotar → Texto): clique onde a seta aponta, depois onde fica o texto, e digite-o. Movida, a nota leva a seta consigo; **Linha de chamada → remover** nas suas propriedades tira a seta. Selecionada, a ponta da seta e cada dobra têm alças próprias: arraste uma para movê-la sozinha.
- **Linhas, polilinhas, retângulos, círculos, arcos, hachuras, símbolos e imagens** são os das ferramentas de Desenho, com as suas alças, camadas e ferramentas de modificação. Uma seta de norte é um dos símbolos do Lapin (**Anotar → Símbolo**). Os símbolos colocados com **Colocar símbolo…** na barra da prancha também se escolhem aqui com um clique e movem-se em milímetros de papel.
- **Nuvem de revisão** (Desenho → Preenchimento, Anotar → Pranchas ou REVCLOUD na linha de comando): dois cantos, ou **P** para um polígono fechado com Enter. **Nuvem de revisão** nas propriedades de qualquer polilinha fechada ou círculo transforma-a em nuvem ou de volta, e define o seu arco no papel.
- **Legenda** (Anotar → Pranchas): uma lista dinâmica do que as vistas da prancha desenham: os materiais com que os seus cortes são hachurados e preenchidos ou, com **Listas** nas suas propriedades, as suas linhas por função, tipo e espessura. Muda com as vistas e com o modelo, e o seu título também aceita campos. As suas próprias palavras (o título, as funções e os tipos das linhas) são escritas no idioma que a interface tinha quando foi colocada; **Idioma** nas propriedades muda-as.
- **Escala gráfica** (Anotar → Pranchas): na escala da primeira vista da prancha ou na escala dada nas suas propriedades, com um comprimento redondo de cerca de 80 mm ou um que escolher. Numa planta, corte ou elevação, a escala gráfica toma a escala da vista.

O que é desenhado na prancha imprime-se nela, por cima das vistas, exatamente como aparece: no seu PDF, em Imprimir e no seu DXF. Pertence à prancha: **Duplicar a prancha** (o seu botão no navegador do projeto) copia-o, com os símbolos colocados na prancha, e apagar a prancha apaga-o. Cada desenho é um passo de desfazer. As pranchas feitas antes não têm nada desenhado e imprimem-se como antes. Uma emissão também o conta: uma prancha alterada só pelo que está desenhado no seu papel ou pelos seus símbolos é indicada como alterada desde a última emissão (Gerir → Verificar, CDE).

![Desenho numa prancha: uma nota com chamada, uma nuvem de revisão, um texto dinâmico, uma legenda dos materiais da planta e uma escala gráfica](shots/sheet-annotations.webp)

![A mesma prancha, como se imprime](shots/sheet-annotated.webp)

## Imprimir

Os botões **PDF** e **DXF** na barra da prancha exportam a prancha. Um PDF está pronto para imprimir ou enviar. Um DXF abre no AutoCAD e em outros programas CAD com as suas camadas, linhas e hachuras; círculos, arcos e elipses (desenhados, ou as bordas curvas de lajes e outros contornos numa planta) são escritos como entidades CIRCLE, ARC e ELLIPSE verdadeiras, não como linhas curtas, e voltam como curvas quando o DXF é importado de novo. **Imprimir** envia a prancha para uma impressora ou plotter no tamanho de papel dela, em 1:1, como linhas vetoriais: escolha o plotter e o papel na caixa de diálogo de impressão. Em uma impressora menor, escolha ajustar à página.
