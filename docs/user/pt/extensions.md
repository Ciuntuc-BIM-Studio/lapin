---
title: Extensões
order: 126
summary: Adicione mobiliário, modelos de projeto, perfis, padrões de hachura, carimbos e tabelas de preços feitos por outras pessoas a partir do catálogo Lapin Extensions, e remova-os de novo.
---

## O catálogo

O **Lapin Extensions** é um catálogo gratuito de coisas feitas para o Lapin pelos seus usuários e por nós: pacotes de mobiliário, modelos de projeto, bibliotecas de perfis, padrões de hachura, carimbos e tabelas de preços. Cada uma vai para a biblioteca que o Lapin já tem para ela, e você a usa como se a tivesse feito.

Abra **Arquivo → Extensões…**. **Explorar** lista o catálogo: pesquise por nome, etiqueta ou pelo que uma extensão contém, ou escolha uma categoria.

![Extensões: Explorar, os detalhes de um pacote de mobiliário](shots/extensions-browse.webp)

Clique em uma extensão para ver seus detalhes antes de instalá-la:

- **Autor** e **Licença**. Toda extensão tem uma licença, escolhida pelo autor. Uma *permissiva* (MIT, CC-BY, CC0…) deixa você usar o que ela contém em qualquer projeto; em outra, *leia os termos* antes.
- **Funciona com**: as versões do Lapin para as quais foi feita. Se não servir para o seu Lapin, ela diz isso em vez de oferecer **Instalar**.
- **Tamanho**, o arquivo e o seu **SHA-256**: a impressão digital do arquivo que o catálogo revisou.
- **Conteúdo**: seus itens, quando o autor os lista.
- **Página** e **Tópico no fórum**: onde perguntar ao autor.

## Instalar, atualizar, desinstalar

Clique em **Instalar**. O Lapin baixa o arquivo, confere se o tamanho e o SHA-256 são exatamente os que o catálogo indica (um arquivo alterado no caminho é recusado e nada é instalado) e então o coloca na sua biblioteca:

| Tipo | Para onde vai |
|---|---|
| Mobiliário | **Interiores → O meu mobiliário…** |
| Modelos de projeto | **Arquivo → Novo a partir de modelo…** |
| Perfis | as bibliotecas de perfis deste computador |
| Padrões de hachura | Meus padrões, oferecidos por **Gerenciar → Materiais** |
| Carimbos | os carimbos deste computador |
| Tabelas de preços | **Execução → Tabelas de preços…**, marcadas como indicativas: os preços são do autor, compare-os com os seus |

Se a sua biblioteca já tiver algo com o mesmo nome, o Lapin pergunta antes de substituir. Nada do que uma extensão traz é executado: extensões são conteúdo.

**Instaladas** lista o que este computador tem. Cada linha abre o lugar para onde foi (**O meu mobiliário…**, **Modelos de projeto…**), oferece **Atualizar** quando o catálogo tem uma versão mais nova, e **Desinstalar**.

![Instaladas: o pacote de mobiliário, com o botão que abre O meu mobiliário](shots/extensions-installed.webp)

![Os tipos de mobiliário do pacote em O meu mobiliário](shots/extensions-my-furniture.webp)

**Desinstalar** tira das suas bibliotecas o que a extensão adicionou, e só isso. O que você já colocou nos seus projetos continua neles: um tipo de mobiliário inserido em um projeto, um projeto iniciado a partir de um modelo.

## Scripts

O catálogo também lista **scripts Python**. No aplicativo de desktop você já pode instalar um; ele roda quando o Lapin tiver o seu console Python, e só quando você o executar. Antes de instalá-lo, seus detalhes dizem o que ele pede (ler ou alterar o projeto aberto, acessar arquivos ou a internet). Um script pode fazer no seu computador o que você pode fazer, então instale scripts de autores em quem você confia. O navegador aceita apenas pacotes de conteúdo.

![Os detalhes de um script: o que ele pede, e que ainda não pode ser executado](shots/extensions-script.webp)

## Outro catálogo

**Catálogo…**, na parte de baixo da janela, define o endereço do catálogo: um endereço https, ou o caminho de um `index.json` neste computador ou em uma pasta compartilhada (o catálogo próprio de um escritório). Deixe vazio para usar o do Lapin. A seta ao lado lê o catálogo de novo.

## Publicar as suas

Qualquer pessoa pode publicar uma extensão: exporte-a do Lapin (O meu mobiliário → Exportar, Guardar como modelo…, uma biblioteca de perfis, um carimbo), anexe o arquivo a uma release de um repositório seu no GitHub e adicione o manifesto dela ao catálogo por um pull request em [Ciuntuc-BIM-Studio/lapin-extensions](https://github.com/Ciuntuc-BIM-Studio/lapin-extensions). O README dele explica como, e o que as verificações automáticas examinam.
