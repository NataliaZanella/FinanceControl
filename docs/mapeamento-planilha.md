# Mapeamento inicial da planilha — outubro/2026

Origem analisada: `PLANILHA DE CONTASs.xlsx` (arquivo fornecido pela usuária, **não incluído** neste repositório).

## Estrutura constatada

- **61 abas**: 60 com registros mensais e 1 intitulada `VALOR TOTAL DE DÍVIDAS`.
- As abas mensais variam de formato ao longo do histórico. Existem abas iniciais apenas com o nome do mês, sem ano no título. Há abas nomeadas explicitamente para 2023–2026.
- Exemplo de layout de outubro/2026: contas em **A (data), B (descrição), C (valor)**; trabalhos em **G (data), H (descrição), I (valor)**; notas/observações aparecem também na região da coluna D.
- Alguns campos de data aparecem como **número serial do Excel**, exigindo conversão segura de calendário.
- Há descrições com referências a parcelas e atrasos (por exemplo `10/12` e observações textuais).
- Existem células vazias em valores e lançamentos futuros/previsões. **Ausência de valor não prova pagamento**.
- Em `VALOR TOTAL DE DÍVIDAS` há números, textos de pagamento, indicadores como `OK`, valores negativos e anotações misturados. Não há estrutura suficiente para inferir com segurança saldo exato e parcelas automaticamente.

## Transformação proposta (futura)

| Origem | Destino | Regra |
| --- | --- | --- |
| Nome da aba mensal | Competência | Validar ano nos nomes abreviados e séries de datas |
| Conta, descrição | Lançamento de despesa | Texto limpo; preservar original em campo de auditoria |
| Valor da conta | Valor previsto | Ler somente números/valores monetários válidos; vazios → revisar |
| Data da conta | Data de vencimento | Interpretar serial Excel ou data real; detectar inconsistências |
| Trabalho, descrição | Lançamento de receita | Marcado como **previsto** até confirmação de recebimento |
| Valor do trabalho | Receita prevista | Ler número; vazios → revisar, não assumir zero |
| Observações | Observações | Manter informações originais, sem inferir status automaticamente |
| Valores da aba dívidas | Revisão individual | Nunca somar automaticamente até mapear o significado das colunas |

## Estratégia de importação segura

1. Criar um **preview somente leitura** por aba, com seletores de colunas e competência.
2. Detectar títulos, cabeçalhos e linhas de totais para não importar linhas resumidas como despesas individuais.
3. Marcar como **revisar** as linhas com data inválida, valor textual, competência indefinida, valor negativo ou observações sugerindo atraso/pagamento.
4. Confirmar duplicatas potenciais entre contas mensais e aba de dívidas.
5. Importar primeiro para uma área de pré-visualização, só depois confirmar o salvamento definitivo.
6. Preservar aba+linha de origem para rastreabilidade, e gerar relatório de importação com erros sem revelar informação sensível.

**Estado atual:** importador mensal implementado em `/importar`, com prévia, ajuste de datas, confirmação transacional e prevenção de duplicidades. O mapeamento manual de colunas e a importação da aba de dívidas continuam pendentes. Consulte `importacao-excel.md` para o comportamento atual.
