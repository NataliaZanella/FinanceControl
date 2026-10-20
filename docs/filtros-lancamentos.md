# Filtros do painel mensal

A tabela do dashboard permite combinar busca por descrição/observação, tipo, situação e categoria. A busca é parcial, ignora diferenças de maiúsculas e acentos e desconsidera espaços nas extremidades. Categoria corresponde ao nome completo, sem diferenciar maiúsculas, e as opções vêm dos lançamentos do mês; não se trata de um cadastro novo de categorias.

Os filtros alteram somente a tabela: os indicadores continuam representando todo o mês de competência. O contador mostra quantos registros foram encontrados em relação ao total mensal. A paginação mantém seis resultados por página e é aplicada depois da filtragem.

- Alterar ou limpar filtros volta à primeira página e fecha os painéis de baixa/histórico. Um formulário de edição aberto é preservado.
- Trocar o mês limpa os filtros. Mover um lançamento para outra competência durante a edição também os limpa.
- Cadastro, edição, baixa e exclusão reaplicam os filtros vigentes e ajustam a página. Um item pode deixar de aparecer porque mudou de categoria/situação, por exemplo; Limpar filtros volta a exibir os demais registros do mês.
- A categoria selecionada permanece disponível no seletor mesmo se seu último registro for editado/excluído, para representar corretamente um resultado vazio até o usuário limpar/trocar o filtro.
- Sem registros no mês, a página explica que é possível cadastrar. Sem correspondências aos filtros, orienta a alterar a busca ou limpar os filtros.
- Nada é gravado no banco e não há alteração de esquema. A API mensal mantém seu comportamento anterior. A consulta em memória fica em `Application/Models/FiltroLancamentos.cs`, compartilhada pela UI Web e pelo futuro cliente Windows.

## Conferência manual com dados fictícios

Execute `powershell -File scripts/Testar-ImportacaoLocal.ps1` e abra `http://127.0.0.1:5189/`.

1. Cadastre no mês escolhido pelo menos sete lançamentos fictícios, variando receita/despesa, categoria e observação. Registre algumas baixas parciais e completas.
2. Pesquise uma descrição e uma observação; procure também sem acentos. Confira o contador de resultados.
3. Combine tipo, situação e categoria. Navegue em Próxima/Anterior quando houver mais de seis resultados.
4. Compare os indicadores antes/depois dos filtros: devem continuar iguais. Use Limpar filtros para retornar à lista mensal inteira.
5. Na segunda página, aplique uma busca que encontre somente um registro. Deve voltar à primeira página sem ficar vazia.
6. Busque um termo inexistente e confira a mensagem de nenhum resultado. Limpe os filtros e troque de mês para conferir o estado de mês vazio.
7. Filtre por Pendente e registre uma baixa completa em um item. Ele deve sair dos resultados; o indicador mensal deve refletir o pagamento. Filtre novamente por Quitado para encontrá-lo com seu histórico.

Validação executada: testes do filtro com DTOs sintéticos, regressões de serviços/importação e build. Conferência HTTP do HTML: quatro controles, categoria do mês, contador, seis linhas por página e nota sobre os indicadores. A interação visual com os controles ainda depende da conferência manual.

Arquivos deste incremento: `FinanceControl.Application/Models/FiltroLancamentos.cs`, `FinanceControl.SharedUI/Components/Pages/Dashboard.razor`, `FinanceControl.SharedUI/wwwroot/finance.css`, `FinanceControl.Tests/FiltroLancamentosTests.cs`, `README.md`, `docs/CONTEXTO_CODEX.md`, `docs/ROADMAP_CODEX.md` e este roteiro.
