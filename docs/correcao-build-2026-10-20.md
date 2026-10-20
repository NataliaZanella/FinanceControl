# FinanceControl — correção da compilação (20/10/2026)

## O que foi corrigido

1. `FinanceControl.SharedUI/Components/Pages/Dashboard.razor`
   - Campo mês passou a usar `DateTime` com formato `yyyy-MM`.
   - Vencimento passou a usar `DateTime?` e ser convertido para `DateOnly?` ao salvar.
   - Data da baixa passou a usar `DateTime`, sendo convertida para `DateOnly` ao salvar.
   - Removidas conversões de texto que provocavam CS1503/CS0029 nos bindings Razor.
2. `FinanceControl.Infrastructure/FinanceControl.Infrastructure.csproj`
   - Adicionada dependência direta de `SQLitePCLRaw.bundle_e_sqlite3` na versão `2.1.14`, que referencia a biblioteca nativa `SQLitePCLRaw.lib.e_sqlite3` >= 2.1.14. Evita resolução transitiva de 2.1.11, sinalizada como vulnerável no NU1903.

## Como aplicar a correção sem apagar os dados

Se o arquivo `FinanceControl_Correcao_Compilacao.zip` estiver na pasta Downloads, **feche o projeto em execução** e execute no PowerShell:

```powershell
Expand-Archive -LiteralPath "$HOME\Downloads\FinanceControl_Correcao_Compilacao.zip" -DestinationPath "D:\Projetos\FinanceControl" -Force
cd D:\Projetos\FinanceControl
dotnet restore .\FinanceControl.slnx
dotnet build .\FinanceControl.slnx
dotnet run --project .\FinanceControl.Web --launch-profile http
```

Os dois arquivos do patch têm os **caminhos relativos corretos**, sem criar uma pasta `FinanceControl` adicional. O patch não inclui nem apaga `financecontrol.db`.

Abra `http://127.0.0.1:5178` após o terminal indicar que a aplicação iniciou.

> A compilação deve ser validada no computador com SDK .NET 10 e acesso NuGet. Este pacote foi inspecionado estaticamente, mas não foi compilado neste ambiente, onde o SDK não está disponível.

## Referências

- https://learn.microsoft.com/pt-br/aspnet/core/blazor/components/data-binding?view=aspnetcore-10.0
- https://github.com/advisories/GHSA-2m69-gcr7-jv3q
- https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3/2.1.14
