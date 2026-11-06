---
title: Documentos
order: 105
summary: As peças escritas do projeto — o memorial descritivo, o caderno de encargos, a lista de pranchas — feitas a partir de modelos, preenchidas a partir do modelo BIM e exportadas para DOCX, ODT e PDF sem nenhum outro programa.
---

## O que é um documento

Um projeto é mais do que os seus desenhos: tem também as suas peças escritas, um memorial descritivo, um caderno de encargos, uma lista de pranchas. No Lapin são **documentos**: cada um parte de um modelo e é preenchido a partir do projeto — os seus dados, as áreas, os pavimentos e os ambientes, os tipos de parede, as tabelas e as pranchas. O que só você sabe (a estrutura, a segurança contra incêndio, o canteiro de obras) você escreve nas entradas do documento. Um documento acompanha o modelo BIM: mude um tipo de parede ou acrescente um ambiente, e o documento dirá isso da próxima vez que o abrir.

O Lapin grava os arquivos ele mesmo, em DOCX, ODT e PDF, sem Word nem LibreOffice no computador, no aplicativo para desktop e no navegador.

## Informações do projeto

**Gerenciar → Informações do projeto…** guarda o que os documentos pedem ao projeto: nome, número e fase, o cliente e o seu endereço, o endereço da obra, o número cadastral e a matrícula do imóvel, o projetista e o seu endereço, o arquiteto-chefe, quem desenhou e quem verificou. Escreva-os uma vez: todos os documentos os leem, e os carimbos das pranchas tomam daqui quem desenhou e quem verificou quando uma prancha não o diz. Cada campo é salvo quando você sai dele.

![Informações do projeto: o número, a fase, o cliente, a obra e o projetista](shots/project-info.webp)

Os mesmos campos estão na aba **Informações do projeto** da caixa de diálogo Documentos.

Como um carimbo pede mais que um documento, os campos estão agrupados: o projeto, o cliente, o terreno e seu alvará de construção, o projetista (escritório, endereço, número de registro, telefone, e-mail) e as pessoas que assinam (gerente do projeto, arquiteto-chefe, engenheiros de estruturas e de instalações, desenhado, verificado e aprovado por). O que ainda faltar vai em **Parâmetros próprios do projeto**: uma chave única, um rótulo, um tipo (texto, número ou data), um grupo e um valor. Um carimbo a lê como `{project.chave}`, um documento como `{{project.chave}}`, e o IFC exporta tudo com o projeto no conjunto de propriedades `Lapin_ProjectInformation` (e o lê de volta).

![Parâmetros próprios do projeto: chave, rótulo, tipo, grupo e valor](shots/project-info-parameters.webp)

## Criar um documento

**Gerenciar → Documentos…** (ou o **+** ao lado de **Documentos** no navegador do projeto) abre os documentos do projeto. À esquerda estão os documentos já criados e, em **Novo a partir de modelo**, os modelos; clique num e o novo documento é criado e preenchido na hora.

![Documentos: os documentos do projeto e os modelos, o documento nas suas páginas, as suas entradas e o que falta completar](shots/documents.webp)

No meio está o documento como será impresso, página após página, com o sumário, as tabelas e os números. Um campo que o projeto ainda não tem fica marcado onde está. O navegador do projeto lista os documentos em **Documentos**; um clique abre a caixa de diálogo num deles.

## Entradas

A aba **Entradas** tem os textos que só o projetista pode escrever, agrupados como os capítulos do documento. Nos modelos romenos, cada um começa com uma indicação marcada **[De completat]** (a completar) que diz o que vai ali. Escreva por cima: a pré-visualização acompanha um instante depois de você parar de digitar. **Restaurar padrão** traz de volta o texto do modelo.

![Entradas: a estrutura escrita por cima da indicação do modelo](shots/documents-inputs.webp)

## A completar

**A completar** lista o que ainda falta ao documento: um campo do projeto vazio, um valor que o modelo BIM não tem. Clique num para ver onde está no documento e ir ao campo que o preenche (um campo do projeto abre a aba **Informações do projeto**). A lista esvazia à medida que você a preenche.

![A completar: os campos que ainda faltam ao documento](shots/documents-missing.webp)

## Atualizar, renomear, copiar

- **Atualizar a partir do modelo** retoma o texto e a diagramação do modelo, para um documento criado antes de o modelo mudar. As suas entradas são mantidas.
- **Renomear**, **Duplicar** e **Excluir** agem sobre o documento mostrado. Uma exclusão é um só passo de desfazer.

## Editar um documento

**Editar**, embaixo no diálogo Documentos, transforma a página do meio num editor do texto próprio do documento; **Pré-visualização** mostra-o de novo nas suas páginas. O que você muda pertence só a este documento: o modelo fica como estava (**Guardar como modelo…**, mais abaixo, guarda as suas mudanças como modelo).

![Editar um documento: os campos como etiquetas com os seus valores, uma tabela do projeto selecionada e as suas configurações à direita](shots/documents-editor.webp)

- **A barra de ferramentas** tem os estilos do texto: **Parágrafo**, **Título 1**–**3**, negrito, itálico, listas com marcadores e numeradas, desfazer e refazer. As teclas habituais também funcionam (Ctrl+B, Ctrl+I, Ctrl+Z dentro do editor).
- **Os campos** aparecem como etiquetas com o seu nome e o seu valor atual, por exemplo o cliente, *Popescu Ion și Maria*. Um campo que o projeto ainda não preenche fica laranja.
- **Os blocos do projeto** — uma tabela, uma imagem, o sumário — são desenhados com o seu conteúdo real. Um clique seleciona um: as suas configurações aparecem à direita, no lugar das abas.

O salvamento é automático: um instante depois de você parar de digitar e quando volta à **Pré-visualização**; cada mudança é um passo de desfazer do projeto.

### Inserir

**Inserir** coloca no cursor:

- **Campo…**: um campo do projeto, buscado pelo nome; a lista mostra o valor de cada um. Dentro de uma repetição, os campos dos seus itens também são oferecidos (o nome de um pavimento, a área de um ambiente).

  ![Inserir → Campo…: “client” buscado, os campos com os seus valores](shots/documents-insert-field.webp)

- **Tabela do projeto…**: uma tabela ou lista do projeto (a de esquadrias, os ambientes, os materiais…). Nas suas configurações: a origem, o **Título**, as colunas mostradas e a linha de total.
- **Imagem de uma vista…**: uma planta, corte, elevação ou vista 3D. Nas suas configurações: **Escala 1:** ou **Largura (mm)** (nunca mais larga que o texto) e uma **Legenda**.
- **Campos chave–valor**: uma tabela de duas colunas com rótulos e valores; um valor pode conter campos, como `{{project.name}}`.
- **Repetir para…**: o texto de dentro é escrito uma vez para cada item de uma coleção: os pavimentos, os ambientes, as coberturas, os materiais, as pranchas, os documentos… Uma faixa por cima diz **Para cada** e quantos itens há; as suas setas **‹ ›** trocam o item que o editor mostra enquanto você escreve. Nas configurações, **Somente os itens onde** mantém alguns deles, e **Texto quando não houver nenhum** acrescenta o que se escreve quando não há nenhum.
- **Condição…**: o texto de dentro é escrito só quando um campo tem valor (ou, marcado, só quando está vazio). **Senão: texto quando não se cumprir** acrescenta uma segunda parte, escrita no lugar dele.
- **Texto a completar…**: uma entrada nova, com rótulo, grupo e texto padrão; o seu texto você escreve na aba **Entradas**, na pré-visualização.
- **Sumário** e **Quebra de página**.

![Uma repetição sobre os pavimentos, uma condição dentro dela e os ambientes do pavimento mostrado, trocado com ‹ ›](shots/documents-repeat.webp)

Nas configurações de um bloco, **Remover** o tira; numa repetição ou condição, **Manter só o conteúdo** tira a faixa e mantém o que está dentro.

## Os seus modelos

**Guardar como modelo…** guarda o texto, o layout, as entradas e o estilo do documento como modelo. Dê-lhe um título e uma descrição e escolha onde guardá-lo:

- **Este projeto**: vai com o arquivo do projeto, para quem o abrir.
- **A biblioteca deste computador**: qualquer projeto deste computador o oferece.
- **Ambos**.

**Guardar como** pode substituir um modelo guardado antes; os documentos já feitos a partir dele mantêm o seu texto até **Atualizar a partir do modelo**. O modelo novo aparece logo em **Novo a partir de modelo**.

![Guardar como modelo: o título, a descrição e onde guardá-lo](shots/documents-save-template.webp)

**Gerenciar → Modelos de documentos…** mostra os modelos em três grupos: **Integrados** (os do Lapin), **Este projeto** e **Este computador**. Para o escolhido:

- **Renomear** e **Excluir** (excluir um modelo do projeto é um passo de desfazer; os documentos feitos a partir dele mantêm o seu texto).
- **Duplicar no projeto** ou **Duplicar na biblioteca**: um modelo integrado só se muda através de uma cópia.
- **Copiar para a biblioteca** ou **Copiar para o projeto**, para passá-lo de um lugar para o outro.
- **Exportar…** escreve o modelo como arquivo `.json`; **Importar…** lê um para este projeto ou para a biblioteca deste computador. É assim que colegas compartilham os modelos de uma empresa.

![Modelos de documentos: os integrados, os do projeto e os do computador, o escolhido com o seu estilo](shots/document-templates.webp)

## O estilo da empresa

Um documento, e um modelo do projeto ou da biblioteca, tem o seu próprio estilo: a aba **Estilo** do diálogo Documentos para o documento mostrado, a parte **Estilo** de Gerenciar → Modelos de documentos… para um modelo. Os modelos integrados mantêm o aspeto do Lapin: duplique um para lhe dar o seu.

- **Tipo de letra (DOCX, ODT)**: Arial (o do Lapin), Calibri, Times New Roman, Georgia, Verdana ou Liberation Sans.
- **Tipo de letra do PDF**: Helvetica ou Times, as fontes próprias do PDF; por padrão a mais próxima do tipo de letra acima.
- **Tamanho do texto (pt)**: o tamanho do texto corrido, de 6 a 24 pt.
- **Cor dos títulos**.
- **Logótipo no cabeçalho**: **Escolher o logótipo…** pega um PNG ou JPEG (menos de 1 MB); defina a sua **Largura (mm)** e se fica **À esquerda** ou **À direita**. É escrito da mesma forma em DOCX, ODT e PDF.

Uma amostra sob os campos mostra o topo da página como os arquivos o mostrarão. **O aspeto próprio do Lapin** tira o estilo. Cada mudança é um passo de desfazer.

![A aba Estilo: a cor dos títulos e um logótipo no cabeçalho, com a amostra](shots/documents-style.webp)

## Exportar

- **Exportar DOCX** e **Exportar ODT** gravam um arquivo que você abre e edita no Word ou no LibreOffice, com os títulos como estilos do próprio programa, o sumário e a numeração das páginas como campos, e as imagens do projeto.
- **Exportar PDF** grava o documento diagramado pelo Lapin, pronto para imprimir ou enviar.

Um campo ainda vazio fica em branco nos arquivos, por isso verifique **A completar** antes de exportar.

## Emitir documentos

**Gerenciar → Emitir…** leva os documentos com as pranchas e o modelo (veja [Pasta do projeto e emissões](cde.md)). Em **Documentos, cada um como PDF:** todos os documentos vêm marcados; marque **Também como DOCX** para levar também uma cópia editável. Cada arquivo recebe o nome pela convenção ISO 19650 (`RP` para um memorial, `SP` para um caderno de encargos), tem o seu hash calculado e entra na guia de remessa, e o documento recebe a revisão e o estado da emissão.

![Emitir: os documentos com as pranchas e o modelo](shots/issue-documents.webp)

## Os modelos

O Lapin traz três modelos, por enquanto em romeno:

- **Memoriu tehnic de arhitectură** (o memorial técnico de arquitetura, segundo a lei romena 50/1991): os dados gerais das Informações do projeto; o número de pavimentos (por exemplo `P+1E`) e a altura máxima; as áreas — construída, total, útil e do subsolo — no total e por pavimento; os ambientes de cada pavimento com as suas áreas; as paredes por tipo, com as suas camadas; as lajes e as coberturas; a tabela de esquadrias; os acabamentos dos ambientes; entradas para a situação existente, o uso, a estrutura, as fachadas, as instalações, a segurança contra incêndio, a acessibilidade, a eficiência energética e o canteiro de obras.
- **Caiet de sarcini: tâmplărie și finisaje** (o caderno de encargos de esquadrias e acabamentos): os materiais do projeto por categoria, a tabela de esquadrias com as quantidades, os acabamentos ambiente por ambiente, e entradas para as generalidades, os requisitos, a execução e o recebimento, com textos padrão para adaptar.
- **Borderou de piese scrise și desenate** (a lista das peças escritas e desenhadas): os documentos e as pranchas do projeto, com número, título, escala e revisão.

Parta de um deles, mude-o no editor e depois use **Guardar como modelo…** para ter o seu.

## Para assistentes de IA

Um assistente ligado pela ponte MCP do Lapin pode criar um documento, escrever as suas entradas e exportá-lo com `lapin_document_export`: veja [Lapin and AI agents](https://github.com/Ciuntuc-BIM-Studio/lapin-pro/blob/main/docs/mcp.md#documents) (em inglês).
