---
title: A interface e os atalhos
order: 15
summary: Os modos Confortável, Compacta e Foco, como recolher os painéis laterais e os atalhos de teclado que você pode alterar.
---

<!-- video interface -->

## Espaços de trabalho

Um espaço de trabalho mostra só as guias da faixa de opções de que o seu trabalho precisa, na ordem em que esse trabalho as usa. Escolha-o no botão ao lado de **Arquivo**, que mostra o espaço de trabalho em que você está, ou na tela inicial, no alto de **Vista → Interface…** ou em **Comandos…** (Espaço de trabalho: …): **Tudo** (todas as guias da sua edição), **Arquitetura**, **Estrutura e armaduras**, **Instalações**, **Implantação e canteiro** ou **Desenho 2D**. O projeto continua sendo um só: as ferramentas de uma guia oculta continuam em Comandos… e nos atalhos de teclado, e um modelo feito em um espaço de trabalho abre inteiro em outro.

![Os espaços de trabalho em Vista → Interface…](shots/workspaces.webp)

Quando as ferramentas da faixa de opções são mais largas que a janela, a roda do mouse sobre elas as rola para o lado.

As guias ficam numa só linha. Quando a janela é estreita demais para todas, as últimas passam para **Mais**, no fim da linha, e a guia em que você está fica sempre à vista.

As guias são agrupadas pelo trabalho a que servem. **Interiores** reúne os acabamentos e o mobiliário. **Implantação** reúne o terreno, o paisagismo e o canteiro de obras (andaimes e fôrmas). **Execução** reúne o cronograma da obra e o orçamento (4D e 5D).

Mostre, oculte ou mova uma guia em **Guias** na mesma caixa de diálogo, e a escolha passa a ser **Minha área de trabalho**, guardado neste computador para a próxima vez; os outros espaços de trabalho continuam lá para você trocar. **Vista** sempre fica, pois Interface… está nela.

## Três modos

**Vista → Interface…** (<kbd>Ctrl</kbd> <kbd>.</kbd>) escolhe quanta coisa fica em volta da vista. Escolha um dos três modos no alto da caixa de diálogo; a mudança aparece na hora.

![A caixa de diálogo Interface: os três modos e cada parte separadamente](shots/interface-dialog.webp)

- **Confortável** é a janela como as outras páginas a mostram: a faixa de opções com ferramentas grandes e seus nomes, uma linha de comando sob cada desenho, e o HUD, a dica, os campos de comprimento e ângulo, a planta-chave e as ferramentas rápidas sobre a vista.
- **Compacta** dá à vista a maior parte da janela:

![Compacta: mais espaço para o desenho](shots/interface-compact.webp)

1. **A faixa de opções** é uma única faixa de ícones. Linhas finas separam os grupos. Passe o mouse sobre uma ferramenta para ver o nome, as teclas e o grupo dela.
2. **O navegador de projeto** se recolhe em uma barra. Os ícones dela abrem o projeto, os níveis ou a árvore IFC sobre a vista por um momento.
3. **As ferramentas rápidas** <kbd>1</kbd>–<kbd>9</kbd> ficam na borda esquerda da vista.
4. **A planta-chave** é um botão. Clique nele para abrir o mapa, e no × dele para recolhê-lo de novo.
5. **Uma linha de status** reúne o nome da ferramenta e o que ela pede, o que você digita, os campos de comprimento e ângulo enquanto você desenha, SNAP, ORTHO e POLAR, o nível e a vista, e depois o projeto.

- **Foco** mantém só as guias e uma linha de comando fina. Clique em uma guia e as ferramentas dela descem sobre a vista; a faixa se recolhe de novo assim que você escolhe uma ferramenta, ou com <kbd>Esc</kbd> ou um clique em outro lugar. Os painéis ficam ocultos. Quando algo está selecionado, um pequeno cartão mostra o que é e as primeiras propriedades; **Todas as propriedades…** abre o painel Propriedades sobre a vista. <kbd>Tab</kbd> traz os dois painéis de volta e os oculta de novo.

![Foco: a seleção em um cartão](shots/interface-focus.webp)

Em todos os modos, a barra superior mantém **Desfazer** e **Refazer** na ponta direita: com os nomes no modo Confortável, como duas setas no modo Compacta e no Foco (<kbd>Ctrl</kbd> <kbd>Z</kbd> e <kbd>Ctrl</kbd> <kbd>Y</kbd>; a dica deles diz o que desfazem).

Abaixo dos modos, cada parte pode ser ajustada separadamente: a faixa de opções, as ferramentas rápidas (ou nenhuma: as teclas numéricas continuam funcionando), a linha de comando, o HUD, a solicitação e os campos da ferramenta, a planta-chave, os painéis e a altura das linhas de propriedades. Mudar uma parte torna a escolha **Personalizado**.

Abaixo das partes, **Guias**: desmarque as guias da faixa de opções que você não usa (um arquiteto pode ocultar Estrutura, Armadura simples e Instalações, um engenheiro Arquitetura e Implantação). Vista sempre fica, pois Interface… está nela. As ferramentas de uma guia oculta continuam em Comandos… e nos atalhos de teclado.

O Lapin guarda a sua escolha neste computador, para você. O aplicativo de desktop e o navegador guardam as suas separadamente. No navegador, o Lapin começa no modo Compacta quando a janela tem menos de 800 pixels de altura, até você escolher.

## Ferramentas rápidas

As ferramentas rápidas são os espaços numerados na borda esquerda da vista, junto ao navegador do projeto, de modo que se movem com ele; pequenas e semitransparentes até o ponteiro chegar a elas (View → Interface… pode, em vez disso, deixá-las flutuando no pé da vista); as teclas <kbd>1</kbd>–<kbd>9</kbd> as iniciam. Cada espaço de trabalho tem as suas: começam como sua edição as dá, e você as escolhe:

- arraste uma ferramenta da faixa de opções para um espaço. Uma ferramenta fica em um só espaço, então deixa aquele onde estava;
- arraste um espaço sobre outro para trocá-los;
- clique com o botão direito em um espaço: **Escolha uma ferramenta…**, **Esvaziar este espaço** ou **Restaurar as ferramentas rápidas**;
- **Vista → Ferramentas rápidas…** define os nove espaços em listas, e **Como na edição** traz de volta as da sua edição;
- uma ferramenta que coloca elementos de um tipo (parede, porta, laje, escada…) pode guardar um dos tipos do projeto: escolha-o no menu do espaço ou ao lado da ferramenta em **Vista → Ferramentas rápidas…**. O espaço mostra então o nome do tipo e inicia a ferramenta com ele. O tipo é guardado pelo nome: o espaço funciona em qualquer projeto com um tipo desse nome, e num projeto sem ele a ferramenta usa seu tipo atual.

No pé da vista, cada espaço mostra o nome da sua ferramenta, e um espaço vazio depois do último espera uma ferramenta. O Lapin guarda suas ferramentas rápidas neste computador, para você.

A seta no fim da barra oculta as ferramentas rápidas; fica no lugar delas uma pequena pega **Ferramentas rápidas** que as traz de volta. Ocultam-se à parte nas vistas do modelo (plantas, 3D, cortes) e nas folhas e mapas, por isso pode tê-las escondidas nos documentos e visíveis nos desenhos.

![As ferramentas rápidas: um tipo de parede no espaço 3, a escada no 7, o espaço 2 vazio](shots/quick-tools.webp)

![Vista → Ferramentas rápidas…](shots/quick-tools-dialog.webp)

## O catálogo

A guia **Catálogo** do navegador do projeto mostra os tipos do projeto para cada ferramenta que coloca elementos com tipo: paredes, portas, janelas, lajes, fachadas-cortina, escadas, rampas, forros e rodapés. Cada tipo é desenhado em pequeno (as camadas de uma parede como seu corte em planta, as de uma laje em corte, a elevação de uma porta ou janela, o perfil de um rodapé), com o que ele é em poucas palavras.

- um clique num cartão coloca elementos desse tipo: a ferramenta começa com ele;
- arraste um cartão para um espaço rápido para guardar ali a ferramenta com esse tipo;
- **Editar tipos…** abre o diálogo onde esses tipos são criados e alterados.

![O catálogo: os tipos de parede](shots/catalog.webp)

![Os tipos de janela, desenhados pela sua elevação](shots/catalog-windows.webp)

## Recolher os painéis laterais

Em todos os modos, o navegador de projeto e o painel Propriedades se recolhem em uma barra fina do seu lado:

- o botão no canto superior direito do painel o recolhe, e a seta na barra dele o expande;
- dê um clique duplo na borda entre o painel e a vista;
- <kbd>Ctrl</kbd> <kbd>B</kbd> recolhe ou expande o navegador, <kbd>Ctrl</kbd> <kbd>⇧</kbd> <kbd>B</kbd> as propriedades, e <kbd>Tab</kbd> os dois.

Clique em um ícone na barra de um painel recolhido para abrir o painel sobre a vista sem expandi-lo. Ele some com <kbd>Esc</kbd> ou um clique em outro lugar; o alfinete o mantém aberto. Arraste a borda de um painel para deixá-lo mais largo; o Lapin lembra a largura.

![Um navegador recolhido aberto sobre a vista a partir da barra](shots/browser-overlay.webp)

## Claro e escuro

A lua na ponta direita da faixa de opções torna a interface escura (grafite); o sol torna-a clara de novo (papel). No tema escuro, plantas, cortes e alçados ficam sobre o fundo escuro, com as penas pretas em tinta clara; as folhas continuam papel. Os pequenos desenhos dos configuradores (a planta, o corte e o alçado de uma porta ou janela, de uma cobertura, uma varanda, um elevador, um pavilhão, uma escada…) e as pré-visualizações de etiquetas, carimbos e materiais são desenhados num cartão de papel claro nos dois temas, para que as suas linhas, textos e cotas se leiam como num desenho impresso.

## Atalhos de teclado

**Vista → Atalhos de teclado…** (<kbd>KS</kbd>) lista, por guia, todos os comandos que o teclado alcança. Cada linha mostra os atalhos do comando.

![Os atalhos de teclado, com a busca "parede"](shots/shortcuts-dialog.webp)

- **Pesquisar** pelo nome de um comando ou por uma tecla.
- **+** adiciona um atalho: pressione uma combinação (<kbd>Ctrl</kbd> <kbd>⇧</kbd> <kbd>W</kbd>, <kbd>F4</kbd>) ou digite até três letras e pressione <kbd>Enter</kbd>. <kbd>Esc</kbd> interrompe.
- Se outro comando já tem o atalho, o Lapin diz qual. **Reatribuir** o tira daquele comando, **Manter ambos** o deixa para os dois (vence o primeiro da lista), **Cancelar** não muda nada.
- **×** remove um atalho. A seta no fim de uma linha devolve aquele comando ao que a predefinição tem, e **Redefinir tudo** faz isso para todos os comandos.
- **Predefinição** escolhe o conjunto de partida: **Estilo Revit** (o padrão: as letras usuais do Revit, onde o Lapin tem o comando) ou **Lapin clássico** (as letras do próprio Lapin de antes).
- **Exportar…** grava os seus atalhos em um arquivo, e **Importar…** lê um, para outro computador ou um colega.

As dicas, os menus e a paleta de comandos (<kbd>Ctrl</kbd> <kbd>K</kbd>) sempre mostram os atalhos como estão agora.

### Letras e a linha de comando

Letras como <kbd>WA</kbd> funcionam quando nenhum campo tem o foco e a linha de comando está vazia: digite-as sobre a vista. O comando é executado na hora. Quando um comando da linha de comando começa com as mesmas letras (<kbd>DI</kbd> e <kbd>DIMADD</kbd>), o Lapin espera um momento: continue digitando para escrever o comando mais longo, ou pressione <kbd>Enter</kbd> ou <kbd>Space</kbd> para executar o mais curto agora. Enquanto um comando pede um ponto ou um valor, letras e dígitos vão para ele. Comandos da linha de comando como <kbd>L</kbd>, <kbd>M</kbd> ou <kbd>TRIM</kbd> seguidos de <kbd>Enter</kbd> funcionam em todas as predefinições. <kbd>Esc</kbd> sempre cancela.

### Os atalhos padrão

Estes são os atalhos no estilo Revit, como o Lapin os tem agora:

<!-- shortcuts -->

As teclas numéricas <kbd>1</kbd>–<kbd>9</kbd> escolhem as ferramentas rápidas.
