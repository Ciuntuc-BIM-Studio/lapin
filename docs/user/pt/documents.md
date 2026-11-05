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

Um editor visual para os seus próprios modelos virá mais tarde.
