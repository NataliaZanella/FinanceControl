# FinanceControl 💜 — C# / Blazor / SQLite

**Primeira versão do controle financeiro**, preparada para reutilizar a lógica e a interface no futuro aplicativo Windows. Projeto local, educacional e funcional como ponto de partida. Não contém os dados pessoais da planilha Excel.

## O que já funciona nesta versão

- Cadastro de receitas e despesas por **mês de competência**.
- Categorias, valores previstos, vencimentos e observações.
- Registro de **vários pagamentos** (despesas) ou recebimentos (receitas) no mesmo lançamento.
- Situações: `Pendente`, `Parcial` e `Quitado`.
- Histórico das baixas, com data, valor e observações.
- Painel mensal: receitas previstas/recebidas, despesas previstas/pagas, pendências e saldo realizado.
- API HTTP local para futuramente conectar a aplicação Windows.
- Banco SQLite criado automaticamente na primeira execução.
- **Importação XLSX:** seleção da aba, prévia com checkboxes, ajustes de datas, detecção de duplicidade e confirmação com transação. Abra `/importar`.

**Ainda não implementado:** login, cartão/fatura, parcelamentos automáticos, despesas recorrentes, backup pela interface, relatórios avançados, autenticação, aplicativo Windows e operações em modo offline. Esses itens ficam para as próximas etapas.

## 1. Requisitos (Windows 11)

1. Instale o **.NET 10 SDK**: https://dotnet.microsoft.com/download/dotnet/10.0
2. Utilize **VS Code** com a extensão C# Dev Kit ou uma versão do Visual Studio que suporte .NET 10.
3. Abra o terminal **PowerShell** na pasta extraída `FinanceControl`.

Confirme o SDK:

```powershell
dotnet --version
```

Deve mostrar uma versão `10.0.xxx` ou posterior compatível com `net10.0`.

## 2. Executar o projeto

Abra a pasta `FinanceControl` no VS Code e execute no PowerShell:

```powershell
dotnet restore .\FinanceControl.slnx
dotnet build .\FinanceControl.slnx
dotnet run --project .\FinanceControl.Web --launch-profile http
```

Abra **http://127.0.0.1:5178**. O navegador também pode abrir automaticamente.

Para encerrar o servidor, pressione **Ctrl + C** no terminal.

Se a porta estiver ocupada, altere a porta em `FinanceControl.Web/Properties/launchSettings.json`.

## 3. Testar com três lançamentos

1. Selecione o mês atual e cadastre uma despesa "Aluguel" de R$ 1.000,00. Ela começa **Pendente**.
2. Clique em **Registrar** e pague R$ 400,00. A despesa passa a **Parcial** e restam R$ 600,00.
3. Registre os R$ 600,00 restantes. A despesa passa a **Quitado**; abra **Histórico** para conferir as duas baixas.
4. Cadastre uma receita de R$ 500,00. Ela não será contabilizada como recebida até que você registre o recebimento.
5. Troque de mês e confirme que os dados são filtrados pela competência.

## 4. Onde ficam os dados?

A conexão SQLite usa `Data Source=financecontrol.db`. O arquivo será criado no **diretório de execução atual** (normalmente a pasta onde você abriu o PowerShell para executar `dotnet run`).

O arquivo é ignorado pelo Git para não publicar informações financeiras. Faça cópias de segurança do `.db` **com o aplicativo parado**.

A planilha Excel original **não é alterada** nem copiada para o projeto. A importação com prévia está disponível em `/importar`. Veja `docs/importacao-excel.md` para instruções detalhadas.

**Atenção ao banco existente:** ao atualizar o sistema, mantenha seu `financecontrol.db`. A nova tabela de origens importadas será criada automaticamente, sem apagar as outras.

## 5. Organização da solução

```text
FinanceControl/
├── FinanceControl.Domain/         # Entidades, enumerações, regras básicas
├── FinanceControl.Application/    # Contratos, modelos de entrada/saída
├── FinanceControl.Infrastructure/ # EF Core, SQLite, serviços de dados
├── FinanceControl.SharedUI/       # Página e CSS Blazor reutilizáveis
├── FinanceControl.Web/            # Site Blazor + endpoints de API
├── docs/                          # Planejamento baseado na planilha
└── FinanceControl.slnx
```

**Fluxo:** Web/SharedUI → ILancamentoService → Infrastructure/EF → SQLite.

Para o futuro **Desktop com .NET MAUI Blazor Hybrid**, registraremos uma implementação de `ILancamentoService` que usa `HttpClient` para chamar a API, mantendo os mesmos componentes da `SharedUI`.

## 6. API HTTP (local)

| Método | Endereço | Uso |
| --- | --- | --- |
| GET | `/api/lancamentos?mes=2026-10` | Lista lançamentos do mês |
| GET | `/api/lancamentos/resumo?mes=2026-10` | Resumo financeiro |
| POST | `/api/lancamentos` | Cria lançamento |
| POST | `/api/lancamentos/{id}/baixas` | Registra pagamento/recebimento |
| GET | `/api/lancamentos/{id}/baixas` | Histórico do lançamento |
| DELETE | `/api/lancamentos/{id}` | Exclui lançamento e suas baixas (**irreversível**) |

Exemplo de cadastro no PowerShell (o campo `tipo` vale `1` para despesa ou `2` para receita):

```powershell
$corpo = @{
  descricao = 'Exemplo de conta'
  categoria = 'Moradia'
  tipo = 1
  valorPrevisto = 100
  competencia = '2026-10-01'
  dataVencimento = '2026-10-25'
  observacoes = 'Cadastro de teste'
} | ConvertTo-Json

Invoke-RestMethod -Method Post -Uri 'http://127.0.0.1:5178/api/lancamentos' `
  -ContentType 'application/json' -Body $corpo
```

## 7. Limitações e cuidados

- **Somente para uso local.** Não publique a API na internet: ainda **não existe login/autorização**. O perfil de execução limita o servidor a `127.0.0.1`.
- Os valores do painel são somados **por competência do lançamento**. Isso não é um extrato de fluxo de caixa por data real da baixa, especialmente quando um pagamento ocorre em outro mês.
- Neste MVP, o valor realizado não pode ultrapassar o previsto; ajustes de juros, multas e estornos serão tratados numa etapa posterior.
- Para facilitar a execução inicial, o schema é criado por `Database.EnsureCreated()`. Antes de adicionar tabelas ou colocar em produção, evoluir para **migrations** do EF Core (não misturar migrations automaticamente com este banco inicial sem plano de migração).
- Antes de importar a planilha, será necessário revisar fórmulas, datas seriais do Excel e valores/células ambíguos. **Não considerar célula vazia como conta paga.**
- Em caso de erros de restauração, confira conexão à internet, versão do SDK e acesso ao NuGet.

## 8. Próximos incrementos sugeridos

1. Evoluir a importação com correção de valores, categorias e importação específica da aba de dívidas.
2. Cadastro de contas e categorias + edição e exclusão com confirmação.
3. Dívidas, cartões, parcelas e lançamentos recorrentes.
4. Banco, saldo inicial, fluxo de caixa real e relatórios por período.
5. Login e controle de acesso antes de permitir conexão por outros dispositivos.
6. `FinanceControl.Desktop` usando MAUI Blazor Hybrid e a mesma interface.
