# FinanceControl — roadmap para trabalhar com Codex

Este roteiro é uma proposta de prioridades, não uma ordem para o Codex implementar tudo de uma vez. **A usuária aprova uma etapa por vez.**

## Fase 0 — Auditoria segura do workspace [primeira tarefa recomendada]
**Objetivo:** verificar exatamente quais versões estão presentes no PC e se a atualização de importação foi aplicada.

**Escopo:** somente leitura, mais comandos não destrutivos (`dotnet --version`, `dotnet restore`, `dotnet build`; `git status` se houver Git). Conferir se existe `FinanceControl.SharedUI/Components/Pages/Importar.razor`, se `/importar` está roteada, se o importador está registrado em DI e quais pacotes realmente estão resolvidos. Conferir documentação e `.gitignore`. Não abrir/editar o Excel privado nem o banco durante a auditoria.

**Aceite:** relatório curto do que já funciona, problemas observados, riscos, arquivos relevantes e sugestão de próximo incremento. Não alterar código nem banco sem pedir autorização.

## Fase 1 — Importação XLSX e prevenção de duplicidade
**Objetivo:** instalar/corrigir/testar o importador, caso ainda necessário.

**Aceite:** build sem erros; seleção de aba, revisão, importação confirmada e reimportação sinalizando duplicata; nenhum pagamento presumido; aba dívidas bloqueada. Usar planilha sintética nos testes automáticos. Para teste manual com dados reais, pedir backup e revisão humana antes de confirmar gravação.

## Fase 2 — Operações de lançamentos e robustez
**Objetivo:** edição, exclusão com confirmação, filtros/categorias, mensagens claras de validação, testes de regras financeiras.

**Aceite:** não perder baixas existentes nem criar valores negativos/inconsistentes; explicar como reverter mudanças.

## Fase 3 — Dívidas, parcelamento, cartão e recorrência
**Objetivo:** definir entidades e jornadas de dívida, parcelas, faturas/cartões e contas recorrentes. **Não tentar deduzir estrutura da aba de dívidas sem revisão.**

**Aceite:** regras claras para vencimento, parcela paga, saldo, juros e estorno; reconciliação sem contagem dupla.

## Fase 4 — Relatórios e gestão do banco
**Objetivo:** separar painel por competência de fluxo de caixa por data da baixa; gráficos por período, orçamento/metas, backups e estratégia de migrations.

**Aceite:** indicadores reconciliados com lançamentos, testes de restauração em cópia do banco, preservação de dados.

## Fase 5 — Segurança + versão Windows
**Objetivo:** autenticação e autorização antes de uso remoto; criar projeto Windows reutilizando `SharedUI` + contratos, via API.

**Aceite:** não expor endpoint financeiro sem controle de acesso, compatibilidade Web/Windows, sincronização/offline somente se definido explicitamente.

## Padrão de entrega por fase
- Resumo do problema e da solução proposta.
- Lista exata de arquivos modificados.
- Testes e comandos executados (com resultado real).
- Riscos e medidas de backup/retorno.
- Próximo passo sugerido, sem iniciar novas fases automaticamente.
