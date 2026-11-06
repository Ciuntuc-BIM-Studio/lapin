---
title: Grupos e limpeza
order: 112
summary: Mantenha elementos juntos como um grupo, abra um grupo para editá-lo, selecione por uma região ou um laço em 3D e oculte o que atrapalha.
---

## Grupos

Um grupo mantém elementos juntos. Selecione-os e clique em **Modificar → Criar grupo** (<kbd>GP</kbd>). Um clique em qualquer membro passa a selecionar o grupo inteiro, nas plantas e em 3D. O grupo é movido, copiado, girado, espelhado e excluído como um todo, e uma cópia é um grupo próprio. No IFC um grupo é um IfcGroup, então outros programas também o veem.

O menu do clique direito numa planta, num corte ou numa elevação tem os mesmos comandos: **Criar grupo** para a seleção e, num grupo, **Desagrupar** e **Editar grupo**. Linhas, textos e hachuras desenhados numa vista também se agrupam, entre eles as linhas de uma planta DXF importada, de modo que um desenho inteiro se move como um todo.

![Linhas desenhadas na planta, agrupadas pelo menu do clique direito](shots/group-drafting.webp)

As propriedades de um grupo selecionado dão o número de elementos que ele contém, com três botões: **Editar grupo**, **Desagrupar** e **Selecionar membros**.

![Um grupo selecionado: os seus membros destacados, as suas propriedades](shots/group-selected.webp)

## Editar um grupo

Clique duas vezes num membro, ou clique em **Editar grupo** (<kbd>EG</kbd>). O resto do modelo é desenhado esmaecido e não pode ser selecionado, e um clique seleciona um membro de cada vez. Exclua, mova ou altere membros como de costume. **Remover do grupo** tira os membros selecionados. **Concluído**, ou <kbd>Esc</kbd>, fecha o grupo. Um grupo desaparece quando o seu último membro é excluído.

![Um grupo aberto para edição: o muro do jardim fora dele esmaecido](shots/group-edit.webp)

## Selecionar por níveis de conjunto

Alguns elementos são partes de um todo: os pilares, as vigas e as terças de um galpão de aço, as partes de uma montagem (uma caixa do Studio, uma montagem lida de um arquivo IFC) e os membros de um grupo. **Vista → Selecionar por níveis de conjunto**, desligado de início, escolhe-os do jeito que o todo é construído. O primeiro clique numa parte seleciona o todo do topo: o grupo ou o galpão. Enquanto ele está selecionado, um clique dentro dele seleciona a parte um nível abaixo, no caminho até o que você clicou, e cada clique seguinte desce mais um nível, até o próprio elemento. Um clique em outra parte do mesmo todo a seleciona no mesmo nível. <kbd>Esc</kbd> sobe um nível e, no topo, larga a seleção; um clique no vazio também a larga. <kbd>Ctrl</kbd>+clique (<kbd>Cmd</kbd>+clique no Mac) seleciona o próprio elemento de imediato, e <kbd>Shift</kbd> acrescenta à seleção o todo do topo. Uma trilha sobre a vista nomeia os níveis, do pavimento até a seleção: clique num deles para selecioná-lo. Enquanto uma parte dentro de um todo está selecionada, o resto do modelo é desenhado levemente esmaecido. Funciona igual em 3D, plantas, cortes e elevações. Uma caixa ou um laço de seleção pega os todos do topo. Um clique duplo continua a abrir um grupo para edição; dentro dele, a descida começa abaixo do grupo. A escolha fica guardada neste computador e também está em **Vista → Configurações da vista**.

![Dentro de um galpão de aço: a viga selecionada, a trilha sobre o 3D](shots/drill-select.webp)

## Arquivos importados

Todo arquivo IFC, Fragments ou de malha que você importa entra como um grupo com o nome do arquivo. Desmarque **Como um grupo** na importação de IFC para trazer os elementos soltos. Um arquivo vinculado não é agrupado: ele já é um vínculo, somente leitura (veja [Importação e exportação](import-export.md)). Elementos lidos de um arquivo IFC como referências ficam onde o arquivo os colocou: um grupo deles pode ser limpo e excluído, não movido.

## Selecionar uma região em 3D

**Vista → Selecionar região** (<kbd>SR</kbd>) transforma um arrasto com o botão esquerdo na vista 3D numa seleção, como nas plantas. Arrastada para a direita, uma janela pega o que fica totalmente dentro dela. Arrastada para a esquerda, uma seleção cruzada pega o que ela toca. Pressione <kbd>Space</kbd> enquanto arrasta para desenhar um laço, e <kbd>Shift</kbd> para acrescentar à seleção. Enquanto ela está ativa, o botão direito gira a vista e o do meio a desloca. O **×** no seu chip, ou <kbd>SR</kbd> de novo, a desativa.

![Uma seleção cruzada arrastada sobre a casa em 3D](shots/select-region-3d.webp)

## Ocultar por enquanto

**Vista → Ocultar** (<kbd>HH</kbd>) oculta a seleção na vista 3D. **Isolar** (<kbd>HI</kbd>) oculta todo o resto. **Mostrar tudo** (<kbd>HR</kbd>), ou o **×** no chip, traz tudo de volta. Nada é salvo nem excluído: é um jeito de chegar ao que fica dentro de um modelo importado enquanto você o limpa.
