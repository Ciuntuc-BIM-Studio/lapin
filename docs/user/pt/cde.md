---
title: Pasta do projeto e emissões
order: 125
summary: Emita as pranchas e o modelo como pede a ISO 19650, guardados no arquivo do projeto e, quando você tiver uma, numa pasta do projeto, nomeados pela convenção, com uma guia de remessa, depois das verificações que um escritório de projetos faz.
---

## A pasta do projeto

**Gerenciar → Pasta do projeto** define a pasta em que o projeto também é emitido: uma pasta no seu computador, um compartilhamento de rede ou uma pasta do SharePoint ou do OneDrive sincronizada com o disco. A pasta é **opcional**: cada emissão e os seus arquivos ficam guardados no próprio arquivo do projeto, então um projeto sem pasta emite do mesmo jeito, no aplicativo desktop e no navegador. O Lapin não pede conta nem fala com nenhum servidor: ele grava arquivos, e o que sincroniza a pasta é do seu escritório. O caminho fica guardado no projeto, então viaja com o arquivo; uma pasta que não está neste computador é um aviso, não um erro. Definir uma pasta exige o aplicativo desktop.

![Pasta do projeto: a pasta, os contêineres, a nomenclatura](shots/project-folder.webp)

Dentro da pasta, o Lapin cria os quatro contêineres da ISO 19650 na primeira emissão: **WIP**, **Shared**, **Published** e **Archived**. Um código de status sugere o contêiner: S0 vai para WIP, S1–S7 para Shared, A, B e CR para Published.

## Nomenclatura

Os arquivos são nomeados conforme a ISO 19650-2: **Projeto-Autor-Volume-Nível-Tipo-Função-Número**, depois o status e a revisão, por exemplo `PRJ-ORG-ZZ-00-DR-A-0101_S2_P01.pdf`. Na caixa de diálogo Pasta do projeto você define o código do projeto, o autor (o seu escritório), o volume e o nível e a função padrão. O resto vem do projeto:

- o **nível** de uma prancha é o pavimento que as suas plantas desenham (`00` em ±0.00, `01` acima, `B1` abaixo, `ZZ` quando ela desenha vários ou nenhum);
- a sua **função** é a da sua disciplina (arquitetura A, estrutura S, instalações M, civil C);
- o seu **número** são os dígitos do número da prancha (`A-101` vira `0101`);
- o modelo é `M3`, a guia de remessa `IE`.

## Verificações

**Gerenciar → Verificar** aplica ao projeto inteiro as regras que um escritório de projetos confere a olho antes de uma emissão. Uma ocorrência **bloqueante** impede a emissão; um **aviso** pode ser ignorado. Clique numa ocorrência para ir à prancha, à vista ou ao elemento.

![Verificar: as ocorrências por regra](shots/cde-check.webp)

Bloqueantes: um carimbo sem número, sem título ou sem quem desenhou; uma vista sem escala; uma vista fora do quadro ou sobre o carimbo; uma prancha que mudou desde a última emissão mas continua naquela revisão; um número de prancha que não dá número de arquivo. Avisos: quem verificou, a data e a fase deixados em branco; uma planta colorida por um esquema de ambientes, ou com muitas hachuras, sem legenda; uma planta, corte ou elevação em nenhuma prancha; uma planta de situação sem indicação do norte; elementos sem pavimento, sem tipo ou sem material; portas e janelas sem etiqueta; ambientes sem número; as propriedades IFC exigidas (IsExternal, LoadBearing, FireRating nas portas) deixadas em branco.

## Emitir

**Gerenciar → Emitir…** faz uma emissão, guardada no arquivo do projeto e, quando o projeto tem uma pasta neste computador, gravada também no seu contêiner:

![Emitir: as pranchas, a revisão, o status, as verificações](shots/cde-issue.webp)

1. Dê à emissão um nome, a sua **revisão** (`P01`, `P02`… enquanto preliminar, `C01`… depois de contratual; a próxima é proposta a partir da última emissão), o seu **status** (`S0`–`S7`, `A1`–`A9`, `B1`–`B9`, `CR`) e o **contêiner**.
2. Marque as pranchas que vão, o modelo em IFC, cada prancha também em DXF, ou todas as pranchas também num só PDF. O nome de cada arquivo aparece como será gravado. O IFC só fica guardado no arquivo do projeto quando você marca **Guardar o IFC no arquivo do projeto** (um modelo grande ocupa dezenas de MB por emissão); desmarcado, ele vai para a pasta do projeto e o arquivo do projeto guarda o seu nome e o seu hash. **Também para a pasta do projeto** grava os arquivos também no contêiner (ativado por padrão quando o projeto tem uma pasta).
3. As verificações rodam enquanto você escolhe. Com uma ocorrência bloqueante a emissão não pode sair; só com avisos, marque **Emitir mesmo assim**.
4. **Emissão** gera cada prancha em PDF, o IFC e o resto, nomeados pela convenção, calcula o hash de cada arquivo (SHA-256), gera a **guia de remessa** (um PDF e um CSV: projeto, emissão, data, revisão, status, os arquivos) e um **manifesto** (JSON, com todos os hashes), guarda tudo no arquivo do projeto e registra a emissão no projeto. Cada prancha impressa traz no carimbo a revisão e o status da emissão e, na tabela de revisões, a linha da emissão, como foi emitida.
5. No mesmo passo o projeto é salvo como uma **versão** com o nome da emissão (veja [Arquivos, versões e recuperação](files.md)), então uma emissão pode ser reaberta, restaurada ou comparada com o projeto como está agora, e o projeto é salvo, sem ficar com alterações pendentes. A caixa de diálogo mostra então a emissão como saiu — o nome, a revisão e os arquivos, cada um para abrir — até você fechá-la ou começar uma **Nova emissão**.

![A caixa de diálogo Emitir depois de uma emissão: a emissão como saiu, os seus arquivos](shots/cde-issued.webp)

## As emissões no arquivo do projeto

As emissões aparecem listadas no fim da caixa de diálogo Pasta do projeto, com o espaço que ocupam no arquivo do projeto (arquivos idênticos são guardados uma só vez, não importa quantas emissões os compartilhem). Clique numa emissão para ver os seus arquivos:

![As emissões no arquivo do projeto: o espaço que ocupam, uma emissão aberta com os seus arquivos](shots/cde-issues.webp)

- clique num arquivo para **abri-lo** a partir do arquivo do projeto (o desktop o abre com o programa dele; o navegador faz o download), ou **Salvar uma cópia…**;
- **Baixar como zip** entrega todos os arquivos guardados de uma emissão num só zip, com o nome da sua guia de remessa: é o que você envia quando não há pasta compartilhada;
- **Publicar na pasta…** (o aplicativo desktop) grava uma emissão feita antes no contêiner da pasta do projeto, ou em qualquer pasta que você escolher: emita primeiro, escolha a pasta depois;
- **Excluir os arquivos guardados** tira do arquivo do projeto, no próximo salvamento, os arquivos de uma emissão antiga, para manter o arquivo pequeno; o registro da emissão, os nomes dos arquivos e os seus hashes ficam;
- **Abrir pasta** mostra o contêiner em que a emissão foi gravada.

## No navegador

O CDE também funciona no navegador: Verificar, Emitir, os arquivos das emissões (abrir, Baixar como zip) e os dois tipos de observações. O arquivo do projeto guarda tudo e volta para você como o arquivo que você salva. Uma pasta do projeto, publicar nela e varrê-la exigem o aplicativo desktop.

## Observações (BCF)

**Gerenciar → Observações do modelo…** guarda as observações feitas sobre o modelo — as suas e as de um revisor — como tópicos BCF (o BIM Collaboration Format que BIMcollab, Solibri, Navisworks e Revit trocam entre si), no projeto, nunca no IFC. Uma observação tem um título, uma descrição, um tipo, um status (Aberta, Em andamento, Resolvida, Fechada), uma prioridade, a pessoa a quem foi atribuída, um prazo e rótulos; os seus comentários; e os seus pontos de vista — a câmera 3D, os elementos selecionados, a caixa de corte como planos de recorte, e uma captura.

![Observações: a lista com os seus filtros, uma observação com a sua captura e os seus comentários](shots/remarks.webp)

- **Nova observação** cria uma a partir da vista 3D à frente: a câmera como está, os elementos selecionados, a caixa de corte e uma imagem tirada da vista. Preencha os campos e clique em **Criar**.
- A lista é filtrada por um texto, um status, uma prioridade, a pessoa atribuída e **Só abertas**. Escolha uma observação para ver e mudar os seus campos, ler os seus comentários e responder na caixa (**Comentar**; marque **Com a vista 3D** para anexar à resposta a vista como está).
- **Mostrar em 3D** abre o ponto de vista da observação: a vista 3D vai para a sua câmera, os seus elementos são selecionados (os que o projeto tem, pelo GlobalId do IFC) e os seus planos de recorte viram a caixa de corte. Um comentário com uma vista própria tem **Mostrar** ao lado.
- **Recapturar da vista 3D** substitui o ponto de vista da observação pela vista como está agora.
- **Importar BCF…** lê um arquivo `.bcfzip` (BCF 2.1) ou `.bcf` (BCF 3.0): um tópico que o projeto já tem (o mesmo GUID) é atualizado — os campos do arquivo prevalecem, e os comentários feitos aqui que faltam no arquivo ficam — e os outros são adicionados. **Exportar todas**, **Exportar abertas** ou **Exportar selecionada** gravam um arquivo BCF, 2.1 a menos que você escolha 3.0, para entregar ao revisor ou colocar no contêiner Shared da pasta do projeto. Os tópicos mantêm os seus GUIDs, então uma ida e volta por outro programa retorna às mesmas observações.

![Uma observação aberta no seu ponto de vista na vista 3D](shots/remark-viewpoint.webp)

O seu nome no que você escreve é definido na caixa **Seu nome** da caixa de diálogo (fica guardado neste computador).
## Observações nas pranchas

Um revisor abre um PDF emitido no Acrobat, no Bluebeam, no Pré-Visualização ou no Drawboard e faz marcações: notas adesivas, texto, realces e tachados, traços de caneta, retângulos, círculos, setas, nuvens, carimbos, medições, respostas. **Gerenciar → Observações das pranchas…** traz essas marcações de volta ao projeto como observações na prancha:

![Observações: a lista por prancha, um PDF lido](shots/cde-remarks.webp)

- **Ler observações de PDF…** escolhe o arquivo. A prancha a que ele pertence vem da emissão (o manifesto ao lado do arquivo, ou o registro das emissões do próprio projeto), senão da convenção do nome do arquivo; quando nada corresponde, você escolhe a prancha. As páginas de um PDF de conjunto vão para as suas pranchas na ordem. O que foi encontrado é mostrado — páginas, anotações, como a prancha foi associada, algum aviso — e **Importar** traz as observações. Ler o mesmo arquivo de novo atualiza as suas observações em vez de duplicá-las; uma observação que você resolveu continua resolvida. Um arquivo renomeado no caminho ainda é associado: os bytes emitidos são onde o arquivo do revisor começa.
- **Examinar a pasta do projeto** (o aplicativo desktop) lista os PDFs emitidos em Shared e Published, quais deles mudaram depois da emissão e se as suas observações foram lidas desde então.
- A lista é agrupada por prancha e filtrada por status: **abertas**, **resolvidas**, **fechadas**. Um clique abre a prancha com a observação à frente. Cada observação pode ser resolvida, fechada, reaberta ou excluída; o estado "Completed" ou "Accepted" de um revisor chega como resolvida.

![Uma prancha com as suas observações por cima](shots/cde-remarks-sheet.webp)

Na prancha, as observações são desenhadas sobre o papel no seu lugar: o contorno na cor de revisão, um marcador com as iniciais do autor, o texto numa dica; a que está à frente tem o seu cartão com o texto, quem e quando, as respostas e o status. O botão na barra de ferramentas da prancha as oculta ou mostra, e o navegador do projeto conta as abertas ao lado da prancha. Elas são uma sobreposição só para a tela: uma prancha é impressa e emitida sem as suas observações.

O Lapin lê o PDF por conta própria, sem nenhum outro programa: arquivos clássicos e os salvos com fluxos de referência cruzada, salvamentos incrementais (o que o programa do revisor acrescenta), texto em qualquer codificação. Um arquivo que ele não consegue ler inteiro rende o que der, com um aviso. O Lapin nunca grava no PDF.
