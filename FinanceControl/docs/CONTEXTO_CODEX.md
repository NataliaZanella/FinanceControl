# FinanceControl — contexto consolidado do projeto

Atualização do contexto: 20/10/2026. Documento produzido a partir das decisões e testes conversados com a desenvolvedora. **Não substitui a inspeção do código da cópia local.**

## 1. Visão do produto
- Controle financeiro pessoal evoluído de uma planilha Excel já utilizada há anos.
- Construir em **C#** para uso próprio e aprendizado/portfólio de desenvolvimento.
- Objetivo de duas interfaces: **Web primeiro** e, posteriormente, **Windows**, compartilhando regras de negócio e, na medida do possível, componentes Blazor.
- Interface moderna e responsiva, em pt-BR, com tema roxo, sidebar, cartões de indicadores e tabelas; identidade visual do dashboard atual deve ser preservada.
- Sem dependência de serviços financeiros externos no MVP. Os dados devem permanecer sob controle da usuária.

## 2. Tecnologias e componentes existentes no pacote de referência
- SDK .NET **10.0.402** confirmado instalado no Windows 11 da usuária (outras versões 8 e 9 coexistem).
- Solução `FinanceControl.slnx` com `Domain`, `Application`, `Infrastructure`, `SharedUI` e `Web`.
- ASP.NET Core + Blazor para UI Web; endpoints HTTP locais; EF Core + SQLite como persistência.
- Dependências previstas na versão de importação: EF Core SQLite 10.0.0, ClosedXML 0.105.1, SQLitePCLRaw bundle 2.1.14 (versão corrigida após aviso NU1903 na 2.1.11).
- Banco `financecontrol.db` criado no **diretório de execução** (não presumir caminho absoluto). `EnsureCreated()` no MVP original; cuidado com migrações.
- API sem autenticação e perfil local `http://127.0.0.1:5178`.
- Windows Desktop/MAUI ainda **não** implementado. A API e SharedUI são a base para futura evolução.

## 3. Testes relatados pela desenvolvedora
- Após instalar .NET 10 e corrigir binding de datas do `Dashboard.razor`, a aplicação Web compilou e iniciou.
- Cadastro de despesa, baixa parcial e persistência após recarregar a página **foram testados com sucesso**.
- A captura de tela compartilhada exibia o layout roxo (sidebar à esquerda, quatro indicadores, formulário de lançamento e tabela mensal).
- Uma atualização para **importação Excel com prévia e prevenção de duplicatas** foi entregue posteriormente, mas **não há confirmação de que foi instalada, compilada ou testada no PC da desenvolvedora**. No início da próxima tarefa, confirmar o estado do projeto local e verificar se `/importar` funciona.
- Registros demonstrados no painel eram de testes; **não interpretar como valores financeiros reais**.

## 4. Regras financeiras já definidas
- Lançamento: descrição, categoria, tipo (despesa/receita), valor previsto (`decimal`), competência (mês/ano), vencimento opcional e observações.
- Baixa: múltiplos pagamentos/recebimentos por lançamento, com valor, data efetiva e observação.
- Estados de negócio: `Pendente`, `Parcial`, `Quitado` conforme somatório de baixas em relação ao previsto.
- Receitas previstas **não são** recebidas automaticamente; contas previstas **não são** despesas pagas automaticamente.
- Dashboard do MVP agrupa **por competência do lançamento**, não por data das movimentações; fluxo de caixa real por data é evolução futura.
- Não ultrapassar o valor previsto com baixas na implementação atual, sem regra específica para juros/multas/estornos.

## 5. Planilha Excel que motivou o sistema
Arquivo original fornecido: `PLANILHA DE CONTASs.xlsx` (arquivo **privado**, não incluir em repositórios, exemplos públicos nem pacotes de contexto).
- 61 abas: 60 abas mensais de contas/trabalhos e uma aba `VALOR TOTAL DE DÍVIDAS`.
- Abas mais novas: **contas** geralmente nas colunas A=data, B=descrição, C=valor; **trabalhos/receitas** em G=data, H=descrição, I=valor.
- Abas históricas têm layouts diferentes; algumas não informam ano na aba; algumas colunas de trabalhos podem variar.
- Datas podem vir como números seriais Excel; existem linhas vazias, totais, fórmulas, observações de atraso/parcelas e previsões futuras.
- Não assumir valor em branco = pago; não interpretar `OK`, `PAGUEI`, `RECEBIDO` ou observações livres como baixa automaticamente.
- A aba de dívidas precisa de parser e confirmação próprios, pois possui valores positivos/negativos, textos e status misturados.

## 6. Importação projetada no pacote atualizado
- Tela `/importar`: upload `.xlsx` local; escolha **uma aba por vez**, prévia com checkboxes e correções de data; confirmar só após revisão.
- Processamento com ClosedXML, sem alterar o Excel original.
- Ano explícito ou digitado manualmente para abas sem ano; impedir importação automática da aba de dívidas.
- Ignorar totais e linhas sem descrição; bloquear valores inválidos/vazios e marcar datas suspeitas como não selecionadas.
- Gravar inicialmente como `previsto` e **sem baixa** automática.
- Proteção contra duplicação: chave de origem (arquivo + aba + linha + tipo) e verificação de lançamento semelhante. Coincidências legítimas podem requerer revisão manual.
- Importação atômica por transação SQLite; criação da tabela de origem de importações prevista sem exclusão do banco.
- Limites declarados na implementação: XLSX até 12 MB, 5.000 linhas analisadas por aba, 2.000 registros por confirmação.
- Documentação técnica existente: `docs/importacao-excel.md` e `docs/mapeamento-planilha.md`. Uma frase ao final do mapeamento inicial pode estar desatualizada em relação ao pacote v2; dê preferência ao código real.

## 7. Próximos requisitos de produto (ainda não realizados)
- Conferência da importação com base na aba `OUTUBRO 2026` e teste de reimportação sem duplicatas.
- Edição e exclusão segura dos lançamentos com confirmação e eventual auditoria.
- Categorias e contas, dívida consolidada, parcelamentos, cartões de crédito/faturas e recorrências.
- Metas/orçamentos e relatórios/gráficos, incluindo fluxo de caixa por data real de pagamento.
- Autenticação antes de exposição fora de localhost; backups/restauração e integridade de dados.
- Cliente `FinanceControl.Desktop` com .NET MAUI Blazor Hybrid consumindo API e compartilhando componentes; offline/sincronização só após definição explícita das regras.

## 8. Histórico dos problemas resolvidos
- Primeiro erro `MSB1009`: PowerShell iniciado em `C:\Windows\system32` em vez da pasta da solução.
- Segundo erro `NETSDK1045`: SDK 9 tentando compilar projeto `net10.0`; resolveu-se com SDK .NET 10.0.402.
- Erros `CS1503`/`CS0029` no dashboard: tipos de date binding (`string` vs `DateTime` e `TimeOnly?`) corrigidos em patch fornecido; app testado e funcionando após correção.
- Alerta `NU1903` em SQLitePCLRaw.lib.e_sqlite3 2.1.11: patch de atualização transitiva para 2.1.14 fornecido; confirmar versão instalada e resultado atual com `dotnet list package --vulnerable --include-transitive` quando necessário.

## 9. Cuidados de segurança e privacidade
- Nunca publicar a API sem autenticação na internet.
- Não expor dados pessoais/financeiros da planilha em logs, fixtures, issues públicos nem Git.
- Não apagar ou sobrescrever `.db` existente; fazer backup com o app parado antes de alterações de persistência.
- Usar dados falsos nos testes; não concluir que os dados reais foram importados apenas porque a tela existe.
