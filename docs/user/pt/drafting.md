---
title: Desenho e a linha de comando
order: 80
summary: Digite comandos como no CAD, faça snap em pontos, mova e copie, desenhe linhas e anote com cotas, textos e identificadores.
---

## A linha de comando

Digite as letras de uma ferramenta em qualquer lugar da janela e pressione <kbd>Enter</kbd>. Por exemplo, digite <kbd>WA</kbd> para uma parede, <kbd>CO</kbd> para copiar, <kbd>M</kbd> para mover ou <kbd>L</kbd> para uma linha. A linha de comando, embaixo, mostra o que a ferramenta precisa em seguida. Você pode clicar num ponto ou digitar um comprimento, um ângulo ou coordenadas.

As coordenadas digitadas estão na unidade do projeto, assim como os comprimentos ao lado do cursor: num projeto em milímetros, `@4000,4000` depois do primeiro canto de um retângulo o faz com 4 m por 4 m, e `4000<90` são 4 m direto para cima.

![Copiando uma parede: depois do ponto base, a linha de comando e a entrada dinâmica mostram o comprimento e o ângulo até o mouse](shots/command-line.webp)

Cada ferramenta mostra as suas letras na dica; você pode mudá-las em **Vista → Atalhos de teclado…** (veja [A interface e os atalhos](interface.md)). Estas são as mais usadas:

| Modelo | | Desenho | |
|---|---|---|---|
| Parede | <kbd>WA</kbd> | Linha | <kbd>L</kbd> |
| Porta, janela | <kbd>DR</kbd>, <kbd>WN</kbd> | Polilinha | <kbd>PL</kbd> |
| Piso | <kbd>SB</kbd> | Retângulo, círculo, arco | <kbd>REC</kbd>, <kbd>C</kbd>, <kbd>A</kbd> |
| Telhado | <kbd>RF</kbd> | Mover, copiar | <kbd>M</kbd>, <kbd>CO</kbd> |
| Escada, ambiente | <kbd>ST</kbd>, <kbd>RM</kbd> | Girar, espelhar, escalar | <kbd>RO</kbd>, <kbd>MI</kbd>, <kbd>SC</kbd> |
| Pilar, viga | <kbd>CL</kbd>, <kbd>BM</kbd> | Deslocar, aparar, estender | <kbd>O</kbd>, <kbd>TR</kbd>, <kbd>EX</kbd> |
| Corte | <kbd>SE</kbd> | Concordância, chanfro | <kbd>F</kbd>, <kbd>CHA</kbd> |
| Medir | <kbd>ME</kbd> | Hachura, texto | <kbd>H</kbd>, <kbd>T</kbd> |
| Enquadrar tudo | <kbd>ZE</kbd> | Apagar | <kbd>E</kbd> |

## Snaps e auxiliares

O Lapin faz snap nas pontas, nos pontos médios e nas interseções do que está desenhado, e na grade. <kbd>F3</kbd> liga ou desliga o snap, <kbd>F8</kbd> o modo orto e <kbd>F10</kbd> o rastreamento polar. O estado deles aparece no canto inferior direito da vista.

## Selecionando

Clique num elemento para selecioná-lo; <kbd>Shift</kbd>-clique adiciona à seleção. Para selecionar vários de uma vez, arraste com o botão esquerdo onde não há nada selecionado. Arrastada para a direita, a caixa é azul e pega o que está inteiramente dentro dela (janela); arrastada para a esquerda, ela é verde e tracejada e pega tudo o que toca (cruzamento), como no AutoCAD. Pressione <kbd>Space</kbd> enquanto arrasta e a caixa vira um laço que segue o mouse, mantendo o modo que a caixa tinha; <kbd>Space</kbd> de novo volta para a caixa. Na linha de comando, quando um comando pede para selecionar objetos, digite **WP** ou **CP** e clique nos cantos de um polígono, depois <kbd>Enter</kbd>: um polígono de janela ou um polígono de cruzamento.

![Um laço na planta: a caixa arrastada para a direita, depois Space, e o laço em volta das duas paredes que ele pega](shots/select-lasso.webp)

## Copiando e movendo

Para copiar, selecione o elemento e digite <kbd>CO</kbd>. Clique num ponto base e depois num segundo ponto. O Lapin continua copiando até você pressionar <kbd>Enter</kbd> ou <kbd>Esc</kbd>. **Mover** (<kbd>M</kbd>) funciona do mesmo jeito.

Enquanto você move ou copia, um marcador fica no ponto base e uma linha tracejada vai dele até o mouse, com um contorno esmaecido do que você move deixado onde estava até você clicar ou pressionar <kbd>Esc</kbd>. A linha e o marcador mudam de cor com o snap: verde num ponto com snap, azul quando o orto ou o rastreamento polar mantém a direção, a cor de destaque numa linha de rastreamento, cinza quando livre. Ao lado do mouse, o Lapin mostra quanto você moveu: Δx, Δy, a distância e o ângulo, nas unidades do projeto. Você pode digitar a distância e o ângulo ali, como quando desenha. Na interface Compacta, esses valores ficam na linha de status. Arrastar uma alça mostra o mesmo, e arrastar o próprio elemento selecionado também, a partir do ponto onde você pressionou; pressione <kbd>Esc</kbd> antes de soltar e o elemento fica onde estava.

![Movendo uma parede: o marcador no ponto base, a linha tracejada até o mouse, o fantasma da parede onde ela estava e o deslocamento no mouse](shots/move-trace.webp)

Você também pode arrastar. Arraste a cruz no meio de um elemento selecionado para movê-lo. Segure <kbd>Ctrl</kbd> (<kbd>⌘</kbd> num Mac) enquanto arrasta para deixar uma cópia.

Um eixo da malha move, copia, gira e espelha sozinho ou junto com outros elementos. Uma cópia é um novo eixo da mesma malha, nomeado a partir do último eixo que corre no mesmo sentido: com os eixos 1, 2 e 3, uma cópia de qualquer um deles é o 4.

## Desenho

A guia **Desenho** desenha linhas, polilinhas, retângulos, círculos e arcos. Ela os edita com deslocar, aparar, estender, concordância, chanfro e unir, preenche áreas com hachuras e os organiza em **Layers**. **Importar DXF** traz um desenho CAD como linhas em que você pode fazer snap e que pode editar.

![A guia Desenho](shots/ribbon-draft.webp)

## Anotando

A guia **Anotar** tem as cotas, os textos, os eixos da malha, os níveis e os identificadores. Uma cota lê o modelo, então ela se atualiza quando uma parede se move. A aparência de textos, cotas, eixos e identificadores é definida em **Gerenciar → Padrões de desenho**.

Uma cota alinhada ou linear entra como uma cadeia: depois dos dois primeiros pontos e da sua linha, cada clique adiciona o próximo ponto na mesma linha, e **Enter** (ou **Esc**) faz da cadeia inteira uma só cota (**U** desfaz o último ponto; digite **S** antes para uma cota simples). Uma cadeia se comporta como uma polilinha: um clique a seleciona inteira, e ela se move, é copiada e é excluída como uma só. Uma cadeia que mede paredes, bordas ou eixos da malha fica presa neles: arrastada ou movida sozinha, só a sua linha de cota desliza, perpendicular à cadeia, para que um deslize do mouse nunca a deixe medindo o vazio; movida junto com o que mede, ela vai junto. As suas alças ficam em cada ponto medido (arraste para movê-lo, Ctrl/⌘-clique para retirá-lo), no meio de cada segmento (arraste para adicionar um ponto, Ctrl/⌘-clique para adicionar um na metade) e nas pontas da linha de cota (arraste para mover a linha). Para pôr mais pontos numa cadeia, como no ArchiCAD, selecione-a e clique em **Anotar → Adicionar pontos** (ou digite **DIMADD**, ou **Adicionar pontos** nas propriedades): cada ponto que você clica entra na cadeia onde cai ao longo da sua linha, entre os pontos vizinhos ou além de uma ponta como a nova ponta, e fica preso na parede, na borda ou no eixo em que cai; **Enter** ou **Esc** termina. Para retirar pontos, Ctrl/⌘-clique nas suas alças, ou clique em **Anotar → Remover pontos** (**DIMDEL**, ou **Remover pontos** nas propriedades) e clique perto de cada ponto; os segmentos de cada lado viram um só. O valor de cada segmento também tem uma alça: arraste-a para tirar o valor do seu lugar onde os segmentos são curtos e os valores se sobreporiam; quando ele fica fora do seu segmento, uma linha de chamada fina o liga de volta. Ctrl/⌘-clique na alça, ou **Textos de volta à linha** nas propriedades, o põe de volta. Um valor movido fica com o seu segmento quando os pontos se movem e gira com a cadeia. Dê um clique duplo num segmento para digitar o seu valor: o elemento que ele mede se move, e tudo o que vem depois dele na cadeia também. As propriedades mostram o valor e o texto de cada segmento, dividem uma cadeia em cotas simples e unem cotas simples que continuam uma à outra numa só linha.

![A guia Anotar](shots/ribbon-annotate.webp)
