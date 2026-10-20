# FinanceControl — instruções para o Codex

## Objetivo e contexto
Este repositório é um sistema pessoal de controle financeiro em C#/.NET 10, inicialmente Web (Blazor), com arquitetura preparada para um futuro cliente Windows (.NET MAUI Blazor Hybrid). A interface e as regras financeiras devem ser reutilizáveis; **o cliente Windows ainda não foi implementado**.

Antes de modificar código, leia `README.md`, `docs/CONTEXTO_CODEX.md`, `docs/ROADMAP_CODEX.md` e a documentação pertinente à funcionalidade (`docs/importacao-excel.md`, `docs/mapeamento-planilha.md`). Inspecione o código real: a documentação e a cópia local podem estar em versões diferentes.

## Convenções de trabalho
- Responda e documente em português brasileiro. Use nomenclatura clara em C# e preserve a identidade visual atual (roxo, sidebar, cards e textos em pt-BR), salvo pedido específico.
- Trabalhe em mudanças pequenas, com explicação do objetivo, arquivos modificados, como testar e possíveis riscos. Para mudanças amplas, apresente um plano antes de alterar arquivos.
- Não invente funcionalidades nem declare testes bem-sucedidos sem executá-los. Se o build falhar, mostre a causa concreta e corrija no escopo da tarefa.
- Evite duplicar regras entre `Web`, `SharedUI`, `Application` e `Infrastructure`. Preserve o desacoplamento para o futuro Desktop.
- Priorize precisão monetária (`decimal`), datas de competência (`DateOnly`) separadas das datas efetivas de baixa, validações e proteção contra duplicatas.
- Sempre diferencie previsto, efetivamente pago/recebido e pendente; não infira quitação de texto livre, célula vazia ou valor zero.
- Não recrie, apague, zere ou substitua o banco SQLite. Antes de modificar estrutura de dados, explique e planeje backup/migração (o MVP começou com `EnsureCreated()` e tem ajuste manual de esquema para a importação).
- Não use planilhas financeiras nem bancos com dados reais como fixtures de testes; use arquivos sintéticos. Não inclua `.db`, `.xlsx` pessoais, credenciais, extratos ou dados financeiros reais em commits, logs ou mensagens.
- A API atual é somente local e não tem autenticação. Não a exponha na rede/internet sem implementar segurança adequada e aprovação explícita.
- Não atualize pacotes cegamente. Investigue avisos de vulnerabilidade NuGet e proponha atualizações compatíveis e verificáveis.

## Comandos de validação (PowerShell, na raiz)
```powershell
dotnet --version
dotnet restore .\FinanceControl.slnx
dotnet build .\FinanceControl.slnx
dotnet run --project .\FinanceControl.Web --launch-profile http
```
Aplicação local: `http://127.0.0.1:5178` (dashboard) e `/importar` (quando a versão de importação estiver instalada). Para testes automatizados, verifique se já há projeto de testes; não presuma que ele existe.

## Ao começar uma nova tarefa
1. Leia o contexto e confirme quais funcionalidades **existem no workspace atual**.
2. Liste em poucas linhas o que pretende alterar e riscos a dados existentes.
3. Implemente somente o escopo solicitado; preserve os dados locais.
4. Execute build/testes possíveis e descreva o resultado de forma verificável.
5. Atualize `docs/CONTEXTO_CODEX.md` ou `docs/ROADMAP_CODEX.md` apenas quando uma decisão, limitação ou progresso relevante mudar.
