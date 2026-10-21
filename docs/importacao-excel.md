# Importação Excel — FinanceControl v2

## Como usar

1. Pare o FinanceControl no PowerShell (`Ctrl+C`). Faça backup do `financecontrol.db` ANTES de atualizar o código, especialmente se já tiver dados reais.
2. Substitua os arquivos do pacote de correção no projeto, mantendo o arquivo SQLite.
3. `dotnet restore .\FinanceControl.slnx`
4. `dotnet build .\FinanceControl.slnx`
5. `dotnet run --project .\FinanceControl.Web --launch-profile http`
6. Abra `http://127.0.0.1:5178/importar` e envie `PLANILHA DE CONTASs.xlsx`.
7. Selecione uma ou várias abas mensais. Para abas sem ano, informe o ano individualmente. Clique em **Gerar prévia das abas selecionadas**.
8. Use **Revisar aba** para conferir cada prévia: descrições, valores, competência, data e observações. A tabela mostra seis linhas por página; seleções e datas ficam preservadas ao alternar entre abas.
9. Desmarque itens errados e confirme os selecionados de todas as abas em uma única operação. No painel principal, selecione o mesmo mês para visualizar os resultados.
10. Importe a MESMA aba novamente para testar a prevenção de duplicatas — a prévia deverá sinalizar os itens já existentes.

## Como funciona

- O arquivo é lido pela biblioteca ClosedXML. Não há envio do arquivo para serviço externo.
- As abas mensais são identificadas pelo nome (por exemplo `OUTUBRO 2026`, `DEZEMBRO 2025.`). Abas sem ano exigem preenchimento manual, para não inventar a competência.
- Procura os cabeçalhos `CONTAS` e `TRABALHOS`. Cada uma possui data, descrição e valor em três colunas consecutivas. Nos meses recentes, isso significa A/B/C e G/H/I. Em abas antigas, o cabeçalho de trabalhos pode estar em E ou F.
- Linhas sem descrição e linhas cujo nome inicia em TOTAL, SOMA, SUBTOTAL, SALDO, etc. são ignoradas.
- Linhas com valor ausente, não positivo ou com mais de duas casas decimais não podem ser importadas nesta versão; exigem análise e correção manual.
- Linhas com data que não combina com a competência são desmarcadas por padrão; a data poderá ser ajustada na prévia antes de selecionar.
- Observações como `PAGUEI`, `RECEBIDO`, `ATRASADO` não criam baixas automaticamente: os registros são sempre criados como previstos, para evitar informações financeiras incorretas.
- A aba `VALOR TOTAL DE DÍVIDAS` é propositalmente bloqueada. Ela tem um layout diferente e precisa de tratamento específico.
- A proteção contra duplicação combina (a) chave única SHA-256 de nome do arquivo + aba + linha + tipo na tabela `RegistrosImportados` e (b) checagem de similaridade para registros já cadastrados, inclusive manualmente (tipo, descrição, valor, competência e vencimento). Itens semelhantes são bloqueados por segurança, mesmo que possam ser registros legítimos distintos. Futuramente adicionaremos ação de revisão dessas coincidências.
- Cada confirmação roda numa única transação de banco; todos os itens selecionados de todas as abas são revertidos em caso de erro de armazenamento.
- A planilha original não é alterada; os dados não são incluídos no projeto ZIP.

## Banco SQLite existente

Como a primeira versão criava o banco via `EnsureCreated()` e não usa migrations, adicionamos apenas a tabela `RegistrosImportados` e índices por `CREATE TABLE/INDEX IF NOT EXISTS` ao iniciar. **Não há comando de exclusão ou recriação de banco.**

## Limitações

- Valores textuais aceitam inteiros e decimais explícitos como `650,00`, `650.00`, `1.234,56` e `1,234.56`. Separadores ambíguos como `1.234` ou `1,234` são bloqueados para revisão; use uma célula numérica ou um decimal explícito. Células numéricas mantêm a regra de até duas casas decimais.
- Abas com conteúdo além da linha 5.000 são recusadas integralmente; divida a aba antes de importar. O sistema não importa apenas uma parte silenciosamente.

- Versão local sem autenticação: não publique a porta para internet; os endpoints existentes não têm autorização.
- Suporta `.xlsx` até 12 MB, até 100 abas por prévia, 20.000 linhas candidatas no conjunto e 2.000 registros selecionados por confirmação (até 5.000 linhas analisadas por aba).
- Não identifica pagamentos efetivos automaticamente. Uma informação em observações sobre valores já pagos deve ser lançada em Baixas manualmente após conferir.
- Importações com nome de arquivo alterado podem ser detectadas por igualdade dos dados, mas a chave de origem depende do nome do arquivo. Alterar linhas existentes pode exigir edição dos lançamentos já cadastrados, não reimportação.
- Itens com o mesmo tipo, competência, descrição, valor e vencimento de um lançamento existente são tratados como duplicados (pode existir coincidência legítima).

## Seleção de várias abas

- Alterar a seleção de abas ou um ano manual invalida a prévia anterior; gere uma nova antes de confirmar.
- Se uma aba selecionada for inválida, a prévia inteira é recusada com indicação da aba; corrija ou desmarque essa aba.
- Linhas equivalentes de abas diferentes da mesma competência são bloqueadas no lote, inclusive na confirmação após correções de data. Linhas iguais em posições distintas da mesma aba são preservadas quando ainda não existem no banco.
- Após salvar, a prévia é recarregada e todas as seleções são limpas. Os itens importados passam a ser sinalizados como duplicados.
- Esta evolução não altera o esquema do banco.
