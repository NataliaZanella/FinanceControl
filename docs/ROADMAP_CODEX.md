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
**Progresso:** edição e exclusão com confirmação implementadas em `developer`; exclusão com baixas e alterações de tipo/competência com baixas são bloqueadas. Filtros mensais por busca, tipo, situação e categoria implementados, com seis resultados por página e indicadores do mês inteiro. Cadastro de categorias implementado com renomeação transacional dos usos e exclusão bloqueada em uso; atualização aditiva com backup validada em banco sintético. Testes e endpoints/HTML HTTP verificados. Roteiros em `docs/edicao-exclusao.md`, `docs/filtros-lancamentos.md` e `docs/categorias.md`. Cadastro de contas permanece para incremento posterior.
**Objetivo:** edição, exclusão com confirmação, filtros/categorias, mensagens claras de validação, testes de regras financeiras.

**Aceite:** não perder baixas existentes nem criar valores negativos/inconsistentes; explicar como reverter mudanças.

## Fase 3 — Dívidas, parcelamento, cartão e recorrência
**Progresso parcial:** grupos de valores a receber por pessoa implementados, com itens individuais, saldo, recebimentos parciais e edição/exclusão integrados ao painel. Desmembramento revisado de receitas sem baixas implementado, preservando total mensal e origem de importação; operação em dados reais depende de conferência humana. Atualização aditiva com backup validada somente em bancos sintéticos. Juros, estornos, parcelas, cartões e recorrências continuam pendentes. Consulte `docs/valores-a-receber.md`.
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
