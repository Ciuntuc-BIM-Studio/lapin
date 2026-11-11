---
title: Níveis e plantas
order: 60
summary: Adicione níveis, defina as alturas deles e veja outro nível sob a planta em que você desenha.
---

<!-- video levels -->

## Níveis

O navegador do projeto lista os níveis no topo, o mais alto primeiro. O **+** ao lado de **Níveis** adiciona um nível acima do mais alto.

Selecione um nível para ver o seu **nível da base** e o seu **nível do topo**. Qualquer um deles pode estar acima ou abaixo de ±0,00. A altura é a distância entre eles. Quando você move a base, o topo fica onde está. Um projeto novo começa com Fundações, Térreo e Primeiro pavimento. Um nível tem no máximo 100 m de altura e fica a ±10 km de ±0,00: um valor escrito na unidade errada é recusado. Uma escada que subiria mais de 50 m até ao nível acima para aí e diz porquê.

![As propriedades de um nível: nível da base, nível do topo e altura](shots/level-properties.webp)

Cada nível tem a sua própria planta em **Plantas**. Clique duas vezes em uma planta para desenhar nesse nível. O nível em que você desenha aparece na caixa no canto superior esquerdo da vista.

Nos cortes e nas elevações, cada nível é marcado à direita com o seu nome e a sua cota. **Gerenciar → Estilos de objeto → Linhas ocultas** também define o **marcador de nível**: um triângulo vazado, um triângulo com a metade esquerda preta (como a norma romena o desenha), um triângulo preto ou um traço, e o seu tamanho no papel.

As cotas de nível são escritas nas unidades do projeto: em metros (+3.00) num projeto métrico, como os desenhos as escrevem, e em pés e polegadas (+9'-10 1/8") num imperial. Seguem-nas o navegador, a caixa no canto superior esquerdo da vista, as marcas de nível em cortes e alçados e as cotas de nível.

## Ver o nível de baixo

Para ver outro nível enquanto desenha, escolha-o em **Subjacente** na barra da planta. A planta desse nível é desenhada por baixo, esmaecida e cinza. Você pode usar o snap nela, mas não pode selecioná-la.

![O primeiro pavimento desenhado sobre o térreo, mostrado como subjacente](shots/underlay.webp)

O subjacente só ajuda você a desenhar; ele não é impresso. Para imprimir elementos de outros níveis em uma planta, abra as propriedades da vista de planta e escolha-os em **Pavimentos exibidos**. Você pode mostrar alguns níveis ou todos, em meio-tom se quiser.

## Copiar para outros pavimentos

Para repetir paredes, lajes ou uma planta inteira em outros níveis, selecione-as (paredes com suas portas e janelas, lajes, ambientes, um grupo…) e escolha **Modificar → Copiar para pavimentos**. Você também pode digitar `COPYSTOREY` na linha de comando ou clicar com o botão direito na seleção numa planta ou na vista 3D. Marque os níveis de destino. **Todos acima** e **Todos abaixo** marcam todos os níveis acima ou abaixo do da seleção, e um Shift-clique marca todos os níveis entre o clicado e o último clicado. O nível em que a seleção está não pode ser marcado.

![Copiar para pavimentos: dois níveis marcados e as opções](shots/copy-to-storeys.webp)

- **Relativa** (padrão): cada cópia fica à mesma altura sobre o seu novo nível que o original sobre o seu. Uma parede desenhada no térreo fica no primeiro pavimento.
- **Absoluta**: cada cópia fica onde o original está no espaço, no novo nível. O seu deslocamento de base compensa a diferença.
- **Também os elementos hospedados**: portas e janelas vão com as suas paredes, e as aberturas com o que cortam. As etiquetas e cotas dos elementos copiados passam da planta do seu nível para a planta do novo nível. Um ambiente leva os seus acabamentos e uma escada o espaço sob ela. Desmarque a opção para copiar as paredes sem portas e janelas.
- **Refazer as relações entre as cópias**: paredes unidas num canto ou em T são unidas da mesma forma em cada novo nível. Uma parede vinculada a uma laje ou a um telhado copiado com ela é vinculada à cópia. Um grupo torna-se um grupo das cópias, e um conjunto é copiado inteiro. Uma escada sobe até o nível acima do novo e abre ali a sua própria abertura na laje.
- **Ignorar duplicados**: um nível que já tem o mesmo elemento no mesmo lugar não recebe outro. A mensagem embaixo diz quantos foram ignorados.

As cópias usam os mesmos tipos que os originais e recebem novos GlobalIds IFC. Todas as cópias são um único passo de desfazer e continuam selecionadas depois, para movê-las ou alterá-las juntas. Numa sessão compartilhada, só o convidado desfaz as suas próprias cópias.

![As paredes e a laje do térreo copiadas para os dois níveis acima, em 3D](shots/copy-to-storeys-3d.webp)

Para mover a seleção para outro nível, escolha **Modificar → Mover para o pavimento**. Os elementos mantêm as suas alturas sobre o novo nível, ou ficam onde estão no espaço.

## A cota de um elemento

Um elemento de um nível fica à cota do nível e a um deslocamento dela. **Propriedades → Posicionamento** mostra o seu **Pavimento**, o seu **Deslocamento do nível** e a sua **Cota** acima de ±0,00: a base de uma parede ou pilar, o topo de um piso ou viga (o deslocamento de uma viga é a partir do topo do nível), a face inferior de uma sapata (o nível inferior do seu tipo abaixo do nível, mais o deslocamento). Escreva um dos números e o outro segue-o: o elemento move-se em todas as vistas, num só passo de anular. Para pôr um elemento noutro nível, mude o seu **Pavimento**. O que desenhar numa planta fica no nível dessa planta.
